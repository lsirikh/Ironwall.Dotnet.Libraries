using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.CameraPopup.Wpf;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>
/// 적대 검토 후속(survfix) — "팝업은 GIS 를 절대 죽이거나 멈추게 하지 않는다".
/// 이 시험 프로세스가 GIS 대역이고, <see cref="StaDispatcher"/> 가 진짜 WPF UI 스레드 역할을 한다.
/// <list type="bullet">
/// <item>H1: GIS UI 가 5초 바빠도 건강한 호스트를 죽이지 않는다(받기 줄이 UI 를 동기로 부르지 않는다).</item>
/// <item>M2: 종료(Kill) 실패 · 제어 루프 중단에도 스스로 회복한다.</item>
/// <item>L4: 감시자 Dispose 는 1초 안에 끝나고 호스트는 내려간다.</item>
/// <item>L5: 1 MiB 넘는 메시지는 그 하나만 버린다(쓰기 줄 · 재시작 폭주 없음).</item>
/// <item>T7: 디스패처 + <see cref="HostedVideoView"/> 가 도는 중 호스트가 죽어도 미관찰 예외 없음.</item>
/// </list>
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class GisDispatcherSurvivalTests
{
    private readonly ITestOutputHelper _output;

    public GisDispatcherSurvivalTests(ITestOutputHelper output) => _output = output;

    private static CameraPopupHostOptions NewOptions() => new()
    {
        HostExecutablePath = HostPaths.RequireHostExe(),
        Headless = true,
        EnableDebugCommands = true,
        HostLogDirectory = HostPaths.NewLogDirectory(),
    };

    private static (CameraPopupHostSupervisor Host, StateRecorder Recorder, TestLog Log) NewSupervisor(Func<long>? clock = null)
    {
        var log = new TestLog();
        var host = clock is null
            ? new CameraPopupHostSupervisor(NewOptions(), log)
            : new CameraPopupHostSupervisor(NewOptions(), log, clock);
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

    private static OpenEventWindow EventWindow(string id, string? title = null) => new()
    {
        Kind = EventWindowKind.Detection,
        EventId = id,
        Title = title ?? "탐지 " + id,
        GridColumns = 1,
        GridRows = 1,
        Cameras = { new EventWindowCamera { CameraId = "c1", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } } },
    };

    private static string Key(string id) => EventKeys.Build(EventWindowKind.Detection, id);

    private static async Task<int> StartRunningAsync(CameraPopupHostSupervisor host, StateRecorder recorder)
    {
        host.Start();
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(15));
        Assert.True(running.HasValue, "host did not reach Running");
        return running!.Value.Args.HostProcessId!.Value;
    }

    private static bool ProcessGone(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.HasExited;
        }
        catch (ArgumentException) { return true; }
        catch (InvalidOperationException) { return true; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FlushFinalizers()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private void DumpLog(TestLog log)
    {
        foreach (var line in log.Lines.TakeLast(40)) _output.WriteLine(line);
    }

    private static IReadOnlyList<(long AtMs, CameraPopupHostStateChangedEventArgs Args)> FailuresSince(StateRecorder recorder, long t0)
        => recorder.States.Where(s => s.AtMs >= t0 && s.Args.NewState is CameraPopupHostState.Restarting or CameraPopupHostState.Suspended).ToList();

    // ── H1 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_not_restart_healthy_host_when_gis_ui_thread_is_blocked_5s()
    {
        // Arrange — 오버레이 상태 알림을 Caliburn 식(동기 Dispatcher.Invoke)으로 받는 GIS
        var (host, recorder, log) = NewSupervisor();
        using var _ = host;
        using var ui = new StaDispatcher("gis-ui-h1");
        int pid = await StartRunningAsync(host, recorder);
        int uiUpdates = 0;
        void CaliburnLike(object? sender, EventArgs e) => ui.InvokeLikeCaliburn(() => Interlocked.Increment(ref uiUpdates));
        host.StatusReceived += (_, _) => ui.InvokeLikeCaliburn(() => { });

        var first = host.OpenOverlay(TestPattern("h1-a"));
        Assert.NotNull(first);
        first!.StateChanged += CaliburnLike;
        Assert.True(await StateRecorder.WaitUntilAsync(() => first.State == StreamState.Playing, TimeSpan.FromSeconds(5)), "first overlay not playing");

        // Act — UI 스레드를 5초 막고, 그동안 호스트가 상태 메시지를 계속 보내게 한다
        long t0 = recorder.NowMs;
        var blocked = ui.Dispatcher.BeginInvoke(() => Thread.Sleep(5000));
        await Task.Delay(150);
        var second = host.OpenOverlay(TestPattern("h1-b"));
        Assert.NotNull(second);
        second!.StateChanged += CaliburnLike;
        host.OpenEventWindow(EventWindow("evt-h1"));
        first.Dispose();
        await Task.Delay(6500);

        // Assert — 재시작 없음 · 같은 pid · UI 알림은 풀린 뒤 도착
        var failures = FailuresSince(recorder, t0);
        if (failures.Count > 0) DumpLog(log);
        Assert.Equal(DispatcherOperationStatus.Completed, blocked.Status);
        Assert.Empty(failures);
        Assert.Equal(CameraPopupHostState.Running, host.State);
        Assert.Equal(pid, host.HostProcessId);
        Assert.True(await StateRecorder.WaitUntilAsync(() => second.State == StreamState.Playing, TimeSpan.FromSeconds(3)), "second overlay not playing");
        Assert.True(await StateRecorder.WaitUntilAsync(() => Volatile.Read(ref uiUpdates) > 0, TimeSpan.FromSeconds(3)), "UI notifications never delivered");
        Assert.Equal(0, ui.UnhandledCount);
    }

    // ── M2 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_relaunch_when_killing_hung_host_throws_aggregate_exception()
    {
        // Arrange — 프로세스 트리 종료가 (실제로 죽인 뒤) AggregateException 을 던진다(자식 접근 거부 등)
        var (host, recorder, log) = NewSupervisor();
        using var _ = host;
        int killFailures = 0;
        host.KillProcessTree = p =>
        {
            p.Kill(entireProcessTree: true);
            Interlocked.Increment(ref killFailures);
            throw new AggregateException(new Win32Exception(5, "Access is denied"));
        };
        int oldPid = await StartRunningAsync(host, recorder);

        // Act 1 — 멈춤 → 심박 끊김 → 종료 시도 실패
        long t0 = recorder.NowMs;
        Assert.True(host.SendDebugCommand(DebugCommandKind.Hang));
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, t0 + 1, TimeSpan.FromSeconds(12));
        if (running is null) DumpLog(log);

        // Assert 1 — 멈춰 서지 않고 새 호스트로 Running(연결 있음)
        Assert.NotNull(running);
        Assert.NotEqual(oldPid, running!.Value.Args.HostProcessId);
        Assert.True(Volatile.Read(ref killFailures) >= 1, "kill override never ran");
        Assert.NotNull(host.LiveClient);
        Assert.Contains(log.Lines, l => l.Contains("host kill failed"));

        // Act 2 — 건강한 상태에서도 [다시 시작]은 동작한다(종료가 또 던져도)
        long r0 = recorder.NowMs;
        host.Restart();
        var restarted = await recorder.WaitForStateAsync(CameraPopupHostState.Running, r0 + 1, TimeSpan.FromSeconds(8));
        if (restarted is null) DumpLog(log);
        Assert.NotNull(restarted);
        Assert.NotEqual(running.Value.Args.HostProcessId, restarted!.Value.Args.HostProcessId);
        Assert.NotNull(host.LiveClient);
    }

    [Fact]
    public async Task should_self_heal_when_failure_handling_aborts_leaving_running_without_client()
    {
        // Arrange — 실패 처리 도중 한 번 던지게 한다(시계) → "Running + 연결 없음" 으로 남는 상황
        var armed = new StrongBox<int>(0);
        var (host, recorder, log) = NewSupervisor(() =>
            Interlocked.Exchange(ref armed.Value, 0) == 1 ? throw new InvalidOperationException("injected clock fault") : Environment.TickCount64);
        using var _ = host;
        int oldPid = await StartRunningAsync(host, recorder);

        // Act
        long t0 = recorder.NowMs;
        Interlocked.Exchange(ref armed.Value, 1);
        Process.GetProcessById(oldPid).Kill();
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, t0 + 1, TimeSpan.FromSeconds(8));
        if (running is null) DumpLog(log);

        // Assert — 감시 틱이 스스로 알아채고 다시 띄운다
        Assert.NotNull(running);
        Assert.NotEqual(oldPid, running!.Value.Args.HostProcessId);
        Assert.NotNull(host.LiveClient);
        Assert.Contains(log.Lines, l => l.Contains("injected clock fault"));
    }

    // ── L4 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_dispose_within_1s_and_kill_host_when_teardown_is_slow()
    {
        // Arrange — 제어 루프의 종료 처리가 5초 걸린다(느린 Kill · WaitForExit 재현)
        var (host, recorder, _) = NewSupervisor();
        int pid = await StartRunningAsync(host, recorder);
        host.KillProcessTree = p =>
        {
            Thread.Sleep(5000);
            p.Kill(entireProcessTree: true);
        };

        // Act
        var sw = Stopwatch.StartNew();
        host.Dispose();
        long disposeMs = sw.ElapsedMilliseconds;

        // Assert — 1초 안에 돌아오고, 호스트는 비상 종료로 내려간다
        _output.WriteLine($"dispose={disposeMs}ms");
        Assert.True(disposeMs <= 1300, $"dispose took {disposeMs} ms");
        Assert.True(await StateRecorder.WaitUntilAsync(() => ProcessGone(pid), TimeSpan.FromSeconds(3)), "host still alive after dispose");
    }

    // ── L5 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_drop_only_oversized_message_without_restart_when_message_exceeds_1mib()
    {
        // Arrange
        FlushFinalizers();
        int unobserved0 = CrashWitness.Unobserved;
        var (host, recorder, log) = NewSupervisor();
        using var _ = host;
        int pid = await StartRunningAsync(host, recorder);

        // Act — 1 MiB 넘는 창 요청 하나 + 정상 요청 하나
        long t0 = recorder.NowMs;
        host.OpenEventWindow(EventWindow("evt-huge", new string('x', FrameCodec.MaxPayloadBytes + 1024)));
        host.OpenEventWindow(EventWindow("evt-after"));
        var opened = await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == Key("evt-after"), t0, TimeSpan.FromSeconds(5));
        await Task.Delay(4000);
        FlushFinalizers();

        // Assert — 뒤 요청은 호스트에 닿고, 재시작 · 일시 중지 없음, 큰 요청은 복원 목록에도 없음
        var failures = FailuresSince(recorder, t0);
        if (opened is null || failures.Count > 0) DumpLog(log);
        Assert.NotNull(opened);
        Assert.Empty(failures);
        Assert.Equal(pid, host.HostProcessId);
        Assert.False(host.Registry.HasWindow(Key("evt-huge")));
        Assert.Contains(log.Lines, l => l.Contains("too large"));
        Assert.Equal(unobserved0, CrashWitness.Unobserved);
    }

    // ── T7 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task should_keep_gis_dispatcher_rendering_without_unobserved_exceptions_when_host_crashes_repeatedly()
    {
        // Arrange — GIS 대역: 디스패처 + HostedVideoView 그리기(16 ms) + Caliburn 식 동기 알림
        FlushFinalizers();
        int unobserved0 = CrashWitness.Unobserved;
        int unhandled0 = CrashWitness.Unhandled;
        var (host, recorder, log) = NewSupervisor();
        using var ui = new StaDispatcher("gis-ui-t7");
        await StartRunningAsync(host, recorder);
        var source = host.OpenOverlay(TestPattern("t7"));
        Assert.NotNull(source);

        var view = ui.Dispatcher.Invoke(() =>
        {
            var v = new HostedVideoView { FrameSource = source };
            v.Hook();   // 실제 화면 갱신 구독 경로도 함께
            return v;
        });
        source!.StateChanged += (_, _) => ui.InvokeLikeCaliburn(() => _ = view.FrameSource);
        host.StateChanged += (_, _) => ui.Dispatcher.BeginInvoke(() => _ = view.FramesRendered);
        host.StatusReceived += (_, _) => ui.InvokeLikeCaliburn(() => { });
        var timer = ui.Dispatcher.Invoke(() =>
        {
            var t = new DispatcherTimer(DispatcherPriority.Render, ui.Dispatcher) { Interval = TimeSpan.FromMilliseconds(16) };
            t.Tick += (_, _) => view.RenderLatest();
            t.Start();
            return t;
        });

        var worstUiMs = new StrongBox<long>(0);
        using var probeCts = new CancellationTokenSource();
        var probe = Task.Run(async () =>
        {
            var sw = new Stopwatch();
            while (!probeCts.IsCancellationRequested)
            {
                sw.Restart();
                ui.Dispatcher.Invoke(() => { });
                Interlocked.Exchange(ref worstUiMs.Value, Math.Max(Interlocked.Read(ref worstUiMs.Value), sw.ElapsedMilliseconds));
                await Task.Delay(50);
            }
        });

        long FramesOnUi() => ui.Dispatcher.Invoke(() => view.FramesRendered);
        Assert.True(await StateRecorder.WaitUntilAsync(() => FramesOnUi() > 5, TimeSpan.FromSeconds(5)), "no frames rendered before crash");

        // Act — 세 가지로 죽인다(재시작 예산 3 안)
        string[] crashes = { "external-kill", "native-access-violation", "unhandled-exception" };
        foreach (var crash in crashes)
        {
            long since = recorder.NowMs;
            switch (crash)
            {
                case "external-kill": Process.GetProcessById(host.HostProcessId!.Value).Kill(); break;
                case "native-access-violation": Assert.True(host.SendDebugCommand(DebugCommandKind.NativeAccessViolation)); break;
                case "unhandled-exception": Assert.True(host.SendDebugCommand(DebugCommandKind.UnhandledException)); break;
            }
            var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, since + 1, TimeSpan.FromSeconds(8));
            if (running is null) DumpLog(log);
            Assert.NotNull(running);
            long before = FramesOnUi();
            Assert.True(await StateRecorder.WaitUntilAsync(() => FramesOnUi() > before + 3, TimeSpan.FromSeconds(4)), $"frames did not resume after {crash}");
        }

        // 종료도 UI 스레드에서 — 1초 안
        var disposeMs = ui.Dispatcher.Invoke(() =>
        {
            var sw = Stopwatch.StartNew();
            host.Dispose();
            return sw.ElapsedMilliseconds;
        });
        probeCts.Cancel();
        await probe;
        ui.Dispatcher.Invoke(() => { timer.Stop(); view.Unhook(); });
        FlushFinalizers();

        // Assert
        _output.WriteLine($"T7 worstUi={Interlocked.Read(ref worstUiMs.Value)}ms dispose={disposeMs}ms frames={FramesOnUi()}");
        Assert.True(disposeMs <= 1300, $"dispose on UI thread took {disposeMs} ms");
        Assert.True(Interlocked.Read(ref worstUiMs.Value) < 1000, $"GIS UI thread stalled {worstUiMs.Value} ms");
        Assert.Equal(0, ui.UnhandledCount);
        Assert.Equal(unhandled0, CrashWitness.Unhandled);
        Assert.Equal(unobserved0, CrashWitness.Unobserved);
    }
}
