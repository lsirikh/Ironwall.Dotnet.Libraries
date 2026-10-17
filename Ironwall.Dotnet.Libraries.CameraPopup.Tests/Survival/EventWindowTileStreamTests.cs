using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>
/// 이벤트 창 타일 스트림 · 닫기 사유 · 충돌 복원(T-05) — 실제 호스트 프로세스, 이 시험 프로세스가 GIS 대역이다.
/// 호스트는 헤드리스(창 뷰 없음)지만 창 상태 · 타일 스트림(시험 무늬) · 타이머 · 닫기 경로는 운영과 같은 코드를 탄다.
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class EventWindowTileStreamTests
{
    private readonly ITestOutputHelper _output;

    public EventWindowTileStreamTests(ITestOutputHelper output) => _output = output;

    private static OpenEventWindow ThreeTileWindow(string id) => new()
    {
        Kind = EventWindowKind.Detection,
        EventId = id,
        Header = new EventWindowHeader { ZoneName = "구역-07", DeviceName = "펜스 센서 #104", EventTypeText = "침입", OccurredAt = DateTimeOffset.Now },
        GridColumns = 2,
        GridRows = 2,
        Window = new PixelRect { X = 40, Y = 40, Width = 800, Height = 500 },
        TimerCloseSeconds = 0,
        Cameras =
        {
            new EventWindowCamera { CameraId = "t1", Name = "외곽 PTZ-3", IsPtz = true, PtzAllowed = true, TargetPresetToken = "P2", DelaySeconds = 2, Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
            new EventWindowCamera { CameraId = "t2", Name = "정문 고정-1", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
            new EventWindowCamera { CameraId = "t3", Name = "후문 고정-2", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
        },
    };

    private static CameraPopupHostSupervisor NewHost(TestLog log, bool debug) => new(new CameraPopupHostOptions
    {
        HostExecutablePath = HostPaths.RequireHostExe(),
        Headless = true,
        EnableDebugCommands = debug,
        HostLogDirectory = HostPaths.NewLogDirectory(),
    }, log);

    private static async Task<bool> AllTilesPlayingAsync(StateRecorder recorder, string eventKey, long sinceMs)
    {
        foreach (var camera in new[] { "t1", "t2", "t3" })
        {
            var playing = await recorder.WaitForMessageAsync<StreamStateChanged>(
                m => m.EventKey == eventKey && m.StreamId == $"{eventKey}#{camera}" && m.State == StreamState.Playing, sinceMs, TimeSpan.FromSeconds(5));
            if (playing is null) return false;
        }
        return true;
    }

    [Fact]
    public async Task should_play_three_tiles_close_and_restore_after_crash_when_event_window_opened_via_supervisor()
    {
        var log = new TestLog();
        using var host = NewHost(log, debug: true);
        var recorder = new StateRecorder(host);
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(15)));

        // ① 열기 — 3 타일 모두 재생, 조치보고 닫기 → WindowClosed(ActionReported)
        var first = ThreeTileWindow("t05-a");
        long t0 = recorder.NowMs;
        host.OpenEventWindow(first);
        Assert.NotNull(await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == first.EventKey && !m.Reused, t0, TimeSpan.FromSeconds(5)));
        Assert.True(await AllTilesPlayingAsync(recorder, first.EventKey, t0), "tiles did not reach Playing");

        long t1 = recorder.NowMs;
        host.CloseEventWindow(first.EventKey, EventWindowCloseReason.ActionReported, returnHome: true);
        var closed = await recorder.WaitForMessageAsync<WindowClosed>(m => m.EventKey == first.EventKey, t1, TimeSpan.FromSeconds(5));
        Assert.NotNull(closed);
        Assert.Equal(EventWindowCloseReason.ActionReported, closed!.Value.Message.Reason);
        Assert.False(host.Registry.HasWindow(first.EventKey));

        // ② 같은 이벤트 두 번 → 새 창 없이 앞으로(Reused)
        var second = ThreeTileWindow("t05-b");
        long t2 = recorder.NowMs;
        host.OpenEventWindow(second);
        Assert.NotNull(await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == second.EventKey && !m.Reused, t2, TimeSpan.FromSeconds(5)));
        Assert.True(await AllTilesPlayingAsync(recorder, second.EventKey, t2));
        host.OpenEventWindow(second);
        Assert.NotNull(await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == second.EventKey && m.Reused, t2, TimeSpan.FromSeconds(5)));

        // ③ 창이 열린 채 호스트 네이티브 충돌 → 재시작 → 같은 창 복원 + 타일 다시 재생
        long t3 = recorder.NowMs;
        Assert.True(host.SendDebugCommand(DebugCommandKind.NativeAccessViolation));
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, t3, TimeSpan.FromSeconds(8));
        if (running is null)
        {
            foreach (var line in log.Lines.TakeLast(30)) _output.WriteLine(line);
        }
        Assert.NotNull(running);
        Assert.NotNull(await recorder.WaitForMessageAsync<WindowOpened>(m => m.EventKey == second.EventKey && !m.Reused, running!.Value.AtMs, TimeSpan.FromSeconds(5)));
        Assert.True(await AllTilesPlayingAsync(recorder, second.EventKey, running.Value.AtMs), "tiles not replayed after crash");
        _output.WriteLine($"crash→running {running.Value.AtMs - t3} ms");

        long t4 = recorder.NowMs;
        host.CloseEventWindow(second.EventKey, EventWindowCloseReason.Evicted, returnHome: false);
        var evicted = await recorder.WaitForMessageAsync<WindowClosed>(m => m.EventKey == second.EventKey, t4, TimeSpan.FromSeconds(5));
        Assert.Equal(EventWindowCloseReason.Evicted, evicted?.Message.Reason);
        Assert.Equal(0, CrashWitness.Unhandled);
    }

    [Fact]
    public async Task should_close_with_timer_reason_when_timer_elapses_in_host()
    {
        using var host = NewHost(new TestLog(), debug: false);
        var recorder = new StateRecorder(host);
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(15)));

        var msg = new OpenEventWindow
        {
            Kind = EventWindowKind.Malfunction,
            EventId = "t05-timer",
            TimerCloseSeconds = 1,
            Cameras = { new EventWindowCamera { CameraId = "t1", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } } },
        };
        long t0 = recorder.NowMs;
        host.OpenEventWindow(msg);
        var closed = await recorder.WaitForMessageAsync<WindowClosed>(m => m.EventKey == msg.EventKey, t0, TimeSpan.FromSeconds(5));

        Assert.NotNull(closed);
        Assert.Equal(EventWindowCloseReason.Timer, closed!.Value.Message.Reason);
        Assert.InRange(closed.Value.AtMs - t0, 900, 3000);
        // 타이머로 닫힌 창은 재시작 복원 목록에서도 빠진다.
        Assert.True(await StateRecorder.WaitUntilAsync(() => !host.Registry.HasWindow(msg.EventKey), TimeSpan.FromSeconds(2)));
    }
}
