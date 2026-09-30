using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>
/// PRD §5-A 생존 시험 K1 · K2 · K3 · K8 (+ 비차단 · 메모리 한도). 이 시험 프로세스가 GIS 대역이다.
/// 호스트는 헤드리스(창 없음) + 디버그 명령 허용으로 띄우고, 영상은 시험 무늬 생산자(LibVLC 없음)를 쓴다.
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class SupervisorSurvivalTests
{
    private const int NonBlockingBudgetMs = 50;
    private readonly ITestOutputHelper _output;

    public SupervisorSurvivalTests(ITestOutputHelper output) => _output = output;

    private (CameraPopupHostSupervisor Host, StateRecorder Recorder, TestLog Log) NewSupervisor(Action<CameraPopupHostOptions>? configure = null)
    {
        var options = new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = true,
            EnableDebugCommands = true,
            HostLogDirectory = HostPaths.NewLogDirectory(),
        };
        configure?.Invoke(options);
        var log = new TestLog();
        var host = new CameraPopupHostSupervisor(options, log);
        return (host, new StateRecorder(host), log);
    }

    private static OverlayStreamRequest TestPattern(string id) => new()
    {
        StreamId = id,
        Camera = new CameraRef { CameraId = "cam-" + id, Name = "시험 " + id },
        Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern },
        Width = 320,
        Height = 180,
    };

    private static OpenEventWindow EventWindow(string key) => new()
    {
        EventKey = key,
        Title = "탐지 " + key,
        Layout = "2x1",
        Cameras =
        {
            new EventWindowCamera { Camera = new CameraRef { CameraId = "c1" }, Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
            new EventWindowCamera { Camera = new CameraRef { CameraId = "c2" }, Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
        },
    };

    private void Metric(string text)
    {
        _output.WriteLine(text);
        try
        {
            File.AppendAllText(Path.Combine(HostPaths.NewLogDirectory(), "survival-metrics.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {text}{Environment.NewLine}");
        }
        catch (IOException) { }
    }

    private void DumpLog(TestLog log)
    {
        foreach (var line in log.Lines.TakeLast(40)) _output.WriteLine(line);
    }

    private static async Task<int> StartRunningAsync(CameraPopupHostSupervisor host, StateRecorder recorder)
    {
        host.Start();
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(15));
        Assert.True(running.HasValue, "host did not reach Running");
        return running!.Value.Args.HostProcessId!.Value;
    }

    private static void AssertGisAlive()
    {
        Assert.Equal(0, CrashWitness.Unhandled);
        Assert.False(Process.GetCurrentProcess().HasExited);
    }

    // ── K1 ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("external-kill")]
    [InlineData("native-access-violation")]
    [InlineData("fail-fast")]
    [InlineData("unhandled-exception")]
    [InlineData("dispatcher-exception")]
    public async Task should_restart_within_3s_and_replay_when_host_dies_during_stream(string crash)
    {
        var (host, recorder, log) = NewSupervisor();
        using var _ = host;
        int oldPid = await StartRunningAsync(host, recorder);

        using var source = host.OpenOverlay(TestPattern("k1"));
        Assert.NotNull(source);
        host.OpenEventWindow(EventWindow("evt-k1"));
        Assert.True(await StateRecorder.WaitUntilAsync(() => source!.PublishedSequence > 5, TimeSpan.FromSeconds(5)), "no frames before crash");
        Assert.NotNull(await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == "evt-k1", 0, TimeSpan.FromSeconds(5)));

        long t0 = recorder.NowMs;
        switch (crash)
        {
            case "external-kill": Process.GetProcessById(oldPid).Kill(); break;
            case "native-access-violation": Assert.True(host.SendDebugCommand(DebugCommandKind.NativeAccessViolation)); break;
            case "fail-fast": Assert.True(host.SendDebugCommand(DebugCommandKind.FailFast)); break;
            case "unhandled-exception": Assert.True(host.SendDebugCommand(DebugCommandKind.UnhandledException)); break;
            case "dispatcher-exception": Assert.True(host.SendDebugCommand(DebugCommandKind.DispatcherException)); break;
        }

        var detected = await recorder.WaitForStateAsync(CameraPopupHostState.Restarting, t0, TimeSpan.FromSeconds(5));
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, t0, TimeSpan.FromSeconds(8));
        if (running is null) DumpLog(log);
        Assert.NotNull(detected);
        Assert.NotNull(running);

        long seqAtRestart = source!.PublishedSequence;
        bool resumed = await StateRecorder.WaitUntilAsync(() => source.PublishedSequence > seqAtRestart + 3, TimeSpan.FromSeconds(3));
        long framesMs = recorder.NowMs - t0;
        var replayedWindow = await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == "evt-k1", running!.Value.AtMs, TimeSpan.FromSeconds(3));

        Metric($"K1[{crash}] detect={detected!.Value.AtMs - t0}ms running={running.Value.AtMs - t0}ms framesResumed={framesMs}ms exit={detected.Value.Args.ExitCode} oldPid={oldPid} newPid={running.Value.Args.HostProcessId}");
        Assert.True(running.Value.AtMs - t0 <= 3000, $"restart took {running.Value.AtMs - t0} ms");
        Assert.True(resumed, "overlay frames did not resume after restart");
        Assert.NotNull(replayedWindow);
        Assert.NotEqual(oldPid, running.Value.Args.HostProcessId);
        Assert.True(Process.GetProcessesByName("Ironwall.CameraPopupHost").All(p => p.Id != oldPid), "old host still alive");
        AssertGisAlive();
    }

    // ── K2 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_kill_and_restart_when_host_hangs()
    {
        var (host, recorder, log) = NewSupervisor();
        using var _ = host;
        int oldPid = await StartRunningAsync(host, recorder);
        using var source = host.OpenOverlay(TestPattern("k2"));
        Assert.True(await StateRecorder.WaitUntilAsync(() => source!.PublishedSequence > 3, TimeSpan.FromSeconds(5)));

        long t0 = recorder.NowMs;
        Assert.True(host.SendDebugCommand(DebugCommandKind.Hang));

        // 멈춘 호스트에 명령을 쏟아부어도 호출은 기다리지 않는다(FR-28).
        long worstCallMs = 0;
        var sw = new Stopwatch();
        for (int i = 0; i < 2000; i++)
        {
            sw.Restart();
            host.SendPtz(new PtzCommand { CameraId = "cam" + (i % 50), Operation = PtzOperation.ContinuousMove, Pan = 0.1 });
            if (i % 100 == 0) host.OpenEventWindow(EventWindow("flood-" + i));
            worstCallMs = Math.Max(worstCallMs, sw.ElapsedMilliseconds);
        }

        var detected = await recorder.WaitForStateAsync(CameraPopupHostState.Restarting, t0, TimeSpan.FromSeconds(8));
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, t0, TimeSpan.FromSeconds(12));
        if (running is null) DumpLog(log);
        Assert.NotNull(detected);
        Assert.NotNull(running);
        long seqAtRestart = source!.PublishedSequence;
        Assert.True(await StateRecorder.WaitUntilAsync(() => source.PublishedSequence > seqAtRestart + 3, TimeSpan.FromSeconds(3)));

        long detectMs = detected!.Value.AtMs - t0;
        Metric($"K2 hang detect={detectMs}ms running={running!.Value.AtMs - t0}ms restartAfterDetect={running.Value.AtMs - detected.Value.AtMs}ms worstCall={worstCallMs}ms reason='{detected.Value.Args.Reason}'");
        Assert.InRange(detectMs, 2500, 5000);
        Assert.Contains("heartbeat timeout", detected.Value.Args.Reason);
        Assert.True(running.Value.AtMs - detected.Value.AtMs <= 3000);
        Assert.True(worstCallMs < NonBlockingBudgetMs, $"worst call {worstCallMs} ms");
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(oldPid));
        AssertGisAlive();
    }

    // ── K3 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_suspend_after_four_crashes_in_a_minute_and_resume_when_restart_called()
    {
        var (host, recorder, log) = NewSupervisor();
        using var _ = host;
        await StartRunningAsync(host, recorder);
        using var source = host.OpenOverlay(TestPattern("k3"));
        host.OpenEventWindow(EventWindow("evt-k3"));

        var crashes = new[] { DebugCommandKind.NativeAccessViolation, DebugCommandKind.UnhandledException, DebugCommandKind.DispatcherException };
        long t0 = recorder.NowMs;
        for (int i = 0; i < 4; i++)
        {
            long since = recorder.NowMs;
            if (i < crashes.Length)
            {
                Assert.True(host.SendDebugCommand(crashes[i]), $"crash #{i + 1} not sent (state {host.State})");
            }
            else
            {
                Process.GetProcessById(host.HostProcessId!.Value).Kill();
            }
            var next = i < 3
                ? await recorder.WaitForStateAsync(CameraPopupHostState.Running, since + 1, TimeSpan.FromSeconds(8))
                : await recorder.WaitForStateAsync(CameraPopupHostState.Suspended, since, TimeSpan.FromSeconds(8));
            if (next is null) DumpLog(log);
            Assert.NotNull(next);
        }
        long suspendedMs = recorder.NowMs - t0;
        Assert.Equal(CameraPopupHostState.Suspended, host.State);
        Assert.Null(host.HostProcessId);

        // 호스트가 없는 동안 모든 호출은 기다리지 않고 던지지 않는다.
        long worst = 0;
        var sw = new Stopwatch();
        for (int i = 0; i < 200; i++)
        {
            sw.Restart();
            host.SendPtz(new PtzCommand { CameraId = "c" + i, Operation = PtzOperation.Stop });
            host.OpenEventWindow(EventWindow("dead-" + i));
            host.CloseEventWindow("dead-" + i, "action-report");
            var tmp = host.OpenOverlay(TestPattern("dead-" + i));
            tmp?.Dispose();
            host.Start();
            worst = Math.Max(worst, sw.ElapsedMilliseconds);
        }
        Assert.True(worst < NonBlockingBudgetMs, $"worst call while suspended {worst} ms");
        await Task.Delay(1500);
        Assert.Equal(CameraPopupHostState.Suspended, host.State); // Start() 는 Suspended 를 풀지 않는다

        long r0 = recorder.NowMs;
        host.Restart();
        var resumed = await recorder.WaitForStateAsync(CameraPopupHostState.Running, r0, TimeSpan.FromSeconds(8));
        Assert.NotNull(resumed);
        long seq = source!.PublishedSequence;
        Assert.True(await StateRecorder.WaitUntilAsync(() => source.PublishedSequence > seq + 3, TimeSpan.FromSeconds(3)), "overlay not replayed after Restart()");
        Assert.NotNull(await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == "evt-k3", resumed!.Value.AtMs, TimeSpan.FromSeconds(3)));

        var exitCodes = recorder.States.Where(s => s.Args.NewState is CameraPopupHostState.Restarting or CameraPopupHostState.Suspended)
            .Select(s => s.Args.ExitCode?.ToString() ?? "-");
        Metric($"K3 suspendedAfter={suspendedMs}ms exitCodes=[{string.Join(",", exitCodes)}] worstCallWhileSuspended={worst}ms restartToRunning={resumed.Value.AtMs - r0}ms");
        AssertGisAlive();
    }

    // ── K8 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_be_unavailable_without_exceptions_when_host_exe_missing()
    {
        var (host, recorder, _) = NewSupervisor(o => o.HostExecutablePath = Path.Combine(Path.GetTempPath(), "no-such-dir", "Ironwall.CameraPopupHost.exe"));
        using var _h = host;

        var sw = Stopwatch.StartNew();
        host.Start();
        long startCallMs = sw.ElapsedMilliseconds;
        var unavailable = await recorder.WaitForStateAsync(CameraPopupHostState.Unavailable, 0, TimeSpan.FromSeconds(3));
        Assert.NotNull(unavailable);
        Assert.Contains("not found", unavailable!.Value.Args.Reason);

        long worst = 0;
        for (int i = 0; i < 100; i++)
        {
            sw.Restart();
            var src = host.OpenOverlay(TestPattern("k8-" + i));
            host.OpenEventWindow(EventWindow("k8-" + i));
            host.SendPtz(new PtzCommand { CameraId = "c", Operation = PtzOperation.GotoPreset, PresetToken = "1" });
            host.CloseEventWindow("k8-" + i);
            host.CloseOverlay("k8-" + i);
            src?.Dispose();
            host.OpenOverlay(new OverlayStreamRequest { Width = 0, Height = 0 }); // 잘못된 크기 → null, 예외 없음
            host.CloseOverlay(null!);
            host.OpenEventWindow(null!);
            host.SendPtz(null!);
            worst = Math.Max(worst, sw.ElapsedMilliseconds);
        }
        host.Restart();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Unavailable, unavailable.Value.AtMs + 1, TimeSpan.FromSeconds(3)));

        Metric($"K8 startCall={startCallMs}ms toUnavailable={unavailable.Value.AtMs}ms worstCall={worst}ms");
        Assert.True(startCallMs < NonBlockingBudgetMs);
        Assert.True(worst < NonBlockingBudgetMs, $"worst call {worst} ms");
        AssertGisAlive();
    }

    // ── 메모리 한도(FR-25) ───────────────────────────────────────────────

    [Fact]
    public async Task should_restart_without_spending_crash_budget_when_host_exceeds_memory_limit()
    {
        var (host, recorder, log) = NewSupervisor(o =>
        {
            o.HostMemoryLimitMb = 1; // 시작하자마자 넘는다 → 종료 코드 20
            o.MaxPlannedRestartsInWindow = 6;
        });
        using var _ = host;
        host.Start();

        var suspended = await recorder.WaitForStateAsync(CameraPopupHostState.Suspended, 0, TimeSpan.FromSeconds(30));
        if (suspended is null) DumpLog(log);
        Assert.NotNull(suspended);

        var planned = recorder.States.Count(s => s.Args.ExitCode == HostExitCodes.MemoryLimit);
        Metric($"MEM planned restarts before suspend={planned} (crash budget 3) suspendedAt={suspended!.Value.AtMs}ms");
        Assert.True(planned > 3, $"planned restarts {planned} should exceed the crash budget");
        AssertGisAlive();
    }

    // ── 대기열 합치기(FR-28) ────────────────────────────────────────────

    [Fact]
    public async Task should_coalesce_ptz_per_camera_when_commands_flood()
    {
        var (host, recorder, _) = NewSupervisor();
        using var _h = host;
        await StartRunningAsync(host, recorder);

        long worst = 0;
        var sw = new Stopwatch();
        for (int i = 0; i < 20_000; i++)
        {
            sw.Restart();
            host.SendPtz(new PtzCommand { CameraId = "cam" + (i % 4), Operation = PtzOperation.ContinuousMove, Pan = i % 2 });
            worst = Math.Max(worst, sw.ElapsedMilliseconds);
        }
        await Task.Delay(1500);
        var client = host.LiveClient!;
        var errors = recorder.Messages.Count(m => m.Message is HostError { Code: "ptz-not-implemented" });

        Metric($"FLOOD 20000 ptz worstCall={worst}ms coalesced={client.CoalescedCount} dropped={client.DroppedCount} reachedHost={errors}");
        Assert.True(worst < NonBlockingBudgetMs);
        Assert.True(client.CoalescedCount + client.DroppedCount > 0);
        Assert.Equal(CameraPopupHostState.Running, host.State);
        AssertGisAlive();
    }
}
