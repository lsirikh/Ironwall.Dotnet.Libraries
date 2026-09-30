using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Xunit;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;
using VideoProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/****************************************************************************
   Purpose      : 이벤트 창 관리자 헤드리스 시험 (PRD camera-popup-modes FR-09 · 11 · 12 · 15 · 27 · 28)
   Created By   : Claude (T-04/T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventWindowManagerTests
{
    private static readonly DisplayMonitorInfo Primary = new(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true);
    private static readonly DisplayMonitorInfo Second = new(@"\\.\DISPLAY2", new ScreenRect(1920, 0, 2560, 1440), new ScreenRect(1920, 0, 2560, 1400), false, 144);

    private sealed class FixedMonitors : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> Monitors { get; set; } = new[] { Primary, Second };
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors() => Monitors;
    }

    private sealed class MutableSettings : ICameraPopupSettingsSource
    {
        public CameraPopupSettings Value { get; set; } = new();
        public CameraPopupSettings Current => Value;
    }

    private static (EventWindowManager Manager, RecordingHost Host, MutableSettings Settings, FixedMonitors Monitors, List<string> Notices)
        NewManager(CameraPopupSettings? settings = null)
    {
        var (host, recorder) = RecordingHost.Create();
        var source = new MutableSettings { Value = settings ?? new CameraPopupSettings() };
        var monitors = new FixedMonitors();
        var manager = new EventWindowManager(host, source, monitors, new TestLog());
        var notices = new List<string>();
        manager.NoticeRaised += (_, text) => notices.Add(text);
        return (manager, recorder, source, monitors, notices);
    }

    private static EventWindowRequest Request(int id, int cameras = 2, EventWindowKind kind = EventWindowKind.Detection) => new()
    {
        Kind = kind,
        EventId = id.ToString(),
        Header = new EventWindowHeader { KindLabel = "탐지", DeviceName = "펜스 #" + id },
        Cameras = Enumerable.Range(1, cameras)
            .Select(i => new EventWindowCamera { CameraId = $"c{i}", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } })
            .ToList(),
    };

    private static string Key(int id, EventWindowKind kind = EventWindowKind.Detection) => EventKeys.Build(kind, id.ToString());

    // ───────────────────────── FR-11 동시 창 한도 ─────────────────────────

    [Fact]
    public void should_evict_oldest_unpinned_window_when_eleventh_event_opens_with_max_ten()
    {
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings { MaxOpenWindows = 10 });
        for (var i = 1; i <= 10; i++) Assert.Equal(EventWindowOpenResult.Opened, manager.Open(Request(i)));
        host.Raise(new PinChanged { EventKey = Key(1), Pinned = true });   // 가장 오래된 창은 📌

        var result = manager.Open(Request(11));

        Assert.Equal(EventWindowOpenResult.Opened, result);
        Assert.Equal(10, manager.OpenCount);
        var closed = Assert.Single(host.Closed);
        Assert.Equal(Key(2), closed.Key);                              // 고정 창을 건너뛴 가장 오래된 창
        Assert.Equal(EventWindowCloseReason.Evicted, closed.Reason);
        Assert.True(manager.IsOpen(Key(1)));
        Assert.True(manager.IsOpen(Key(11)));
        Assert.False(manager.IsOpen(Key(2)));
    }

    [Fact]
    public void should_not_open_and_notify_when_every_open_window_is_pinned()
    {
        var (manager, host, _, _, notices) = NewManager(new CameraPopupSettings { MaxOpenWindows = 2 });
        manager.Open(Request(1));
        manager.Open(Request(2));
        host.Raise(new PinChanged { EventKey = Key(1), Pinned = true });
        host.Raise(new PinChanged { EventKey = Key(2), Pinned = true });

        var result = manager.Open(Request(3));

        Assert.Equal(EventWindowOpenResult.AllPinned, result);
        Assert.Equal(2, host.Opened.Count);
        Assert.Empty(host.Closed);
        Assert.Single(notices);
    }

    // ───────────────────────── FR-09 같은 이벤트 ─────────────────────────

    [Fact]
    public void should_bring_existing_window_to_front_when_same_event_arrives_again()
    {
        var (manager, host, _, _, _) = NewManager();
        manager.Open(Request(7));

        var result = manager.Open(Request(7));

        Assert.Equal(EventWindowOpenResult.BroughtToFront, result);
        Assert.Single(host.Opened);
        Assert.Equal(new[] { Key(7) }, host.BroughtToFront);
    }

    [Fact]
    public void should_keep_detection_and_malfunction_windows_apart_when_ids_collide()
    {
        var (manager, host, _, _, _) = NewManager();
        manager.Open(Request(7, kind: EventWindowKind.Detection));

        var result = manager.Open(Request(7, kind: EventWindowKind.Malfunction));

        Assert.Equal(EventWindowOpenResult.Opened, result);
        Assert.Equal(2, host.Opened.Count);
    }

    // ───────────────────────── FR-12 계단 배치 ─────────────────────────

    [Fact]
    public void should_cascade_right_down_by_step_and_wrap_to_first_position_when_reaching_work_area_edge()
    {
        // 작업영역 1920×1040, 창 960×600 at (24,24), 간격 100 → 한 바퀴 = min((1920-984)/100, (1040-624)/100)+1 = 5
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings { FirstWindowX = 24, FirstWindowY = 24, WindowWidth = 960, WindowHeight = 600, CascadeStepPx = 100 });

        for (var i = 1; i <= 6; i++) manager.Open(Request(i));

        var rects = host.Opened.Select(o => (o.Window.X, o.Window.Y)).ToList();
        Assert.Equal(new[] { (24, 24), (124, 124), (224, 224), (324, 324), (424, 424), (24, 24) }, rects);
        Assert.All(host.Opened, o => Assert.Equal((960, 600), (o.Window.Width, o.Window.Height)));
    }

    [Fact]
    public void should_place_on_target_monitor_work_area_with_its_dpi_when_monitor_exists()
    {
        var (manager, host, _, _, notices) = NewManager(new CameraPopupSettings { TargetMonitorId = Second.Id, FirstWindowX = 10, FirstWindowY = 20 });

        manager.Open(Request(1));

        var opened = Assert.Single(host.Opened);
        Assert.Equal((1930, 20), (opened.Window.X, opened.Window.Y));
        Assert.Equal(1.5, opened.DpiScale);
        Assert.Equal(@"\\.\DISPLAY2", opened.MonitorDeviceName);
        Assert.Equal((1920, 0, 2560, 1400), (opened.MonitorWorkArea.X, opened.MonitorWorkArea.Y, opened.MonitorWorkArea.Width, opened.MonitorWorkArea.Height));
        Assert.Empty(notices);
    }

    [Fact]
    public void should_leave_cascade_when_host_reports_window_moved()
    {
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings { CascadeStepPx = 24 });
        manager.Open(Request(1));
        manager.Open(Request(2));

        host.Raise(new WindowMoved { EventKey = Key(1), X = 900, Y = 500 });
        manager.Open(Request(3));

        Assert.Null(manager.CascadeIndexOf(Key(1)));   // 옮긴 창은 줄에서 빠진다
        Assert.Equal(0, manager.CascadeIndexOf(Key(3)));  // 비워진 첫 자리를 새 창이 쓴다
        Assert.Equal(host.Opened[0].Window.X, host.Opened[2].Window.X);
        Assert.True(manager.IsOpen(Key(1)));
    }

    [Fact]
    public void should_free_slot_without_reflowing_others_when_window_closed()
    {
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings { CascadeStepPx = 24 });
        manager.Open(Request(1));
        manager.Open(Request(2));
        manager.Open(Request(3));
        var callsBefore = host.Calls.Count;

        host.Raise(new WindowClosed { EventKey = Key(2), Reason = EventWindowCloseReason.User });

        Assert.Equal(callsBefore, host.Calls.Count);          // 남은 창에 아무 명령도 보내지 않는다(다시 늘어놓지 않음)
        Assert.Equal(2, manager.CascadeIndexOf(Key(3)));
        manager.Open(Request(4));
        Assert.Equal(1, manager.CascadeIndexOf(Key(4)));      // 빈 자리를 채운다
        Assert.Equal(host.Opened[1].Window.X, host.Opened[3].Window.X);
    }

    [Fact]
    public void should_clamp_to_primary_and_notify_once_when_target_monitor_is_missing()
    {
        var (manager, host, _, _, notices) = NewManager(new CameraPopupSettings { TargetMonitorId = @"\\.\DISPLAY9@3840x2160", FirstWindowX = 24, FirstWindowY = 24 });

        manager.Open(Request(1));
        manager.Open(Request(2));

        Assert.Single(notices);
        Assert.All(host.Opened, o => Assert.True(o.Window.X >= 0 && o.Window.X + o.Window.Width <= 1920));
        Assert.All(host.Opened, o => Assert.Equal(@"\\.\DISPLAY1", o.MonitorDeviceName));
    }

    [Fact]
    public void should_pull_window_inside_and_notify_when_first_position_is_off_screen()
    {
        var (manager, host, _, _, notices) = NewManager(new CameraPopupSettings { FirstWindowX = 5000, FirstWindowY = 3000, WindowWidth = 960, WindowHeight = 600 });

        manager.Open(Request(1));

        var w = Assert.Single(host.Opened).Window;
        Assert.Equal((1920 - 960, 1040 - 600), (w.X, w.Y));
        Assert.Single(notices);
    }

    [Fact]
    public void should_use_fallback_area_and_notify_when_no_monitor_can_be_read()
    {
        var (manager, host, _, monitors, notices) = NewManager();
        monitors.Monitors = Array.Empty<DisplayMonitorInfo>();

        Assert.Equal(EventWindowOpenResult.Opened, manager.Open(Request(1)));

        Assert.Single(host.Opened);
        Assert.Single(notices);
    }

    [Fact]
    public void should_fill_window_policy_from_settings_when_opening()
    {
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings
        {
            CamerasPerWindow = 6, GridLayout = new CameraPopupGridLayout(2, 3), AlwaysOnTop = false,
            CloseByTimer = true, CloseTimerSeconds = 45, CloseOnActionReport = true, ReturnHomePresetOnClose = false,
        });
        var request = Request(1, cameras: 2);

        manager.Open(new EventWindowRequest { Kind = request.Kind, EventId = request.EventId, Cameras = request.Cameras, ExtraCameraCount = 3 });

        var o = Assert.Single(host.Opened);
        Assert.Equal((1, 2), (o.GridColumns, o.GridRows));   // 설정(6대)보다 적은 2대 → 960×600 창에서 16:9 타일이 가장 큰 격자(위아래 2칸)
        Assert.Equal(3, o.ExtraCameraCount);
        Assert.Equal(45, o.TimerCloseSeconds);
        Assert.False(o.AlwaysOnTop);
        Assert.False(o.ReturnHomeOnClose);
        Assert.Equal(Key(1), o.EventKey);
    }

    // ───────────────────────── 모드 ─────────────────────────

    [Theory]
    [InlineData(CameraPopupMode.Broker)]
    [InlineData(CameraPopupMode.None)]
    public void should_not_open_window_when_mode_is_not_self(CameraPopupMode mode)
    {
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings { Mode = mode });

        Assert.Equal(EventWindowOpenResult.Disabled, manager.Open(Request(1)));
        Assert.Empty(host.Calls);
    }

    [Fact]
    public void should_not_open_window_when_no_camera_given()
    {
        var (manager, host, _, _, _) = NewManager();
        Assert.Equal(EventWindowOpenResult.NoCameras, manager.Open(Request(1, cameras: 0)));
        Assert.Empty(host.Calls);
    }

    // ───────────────────────── FR-15 조치보고 닫기 ─────────────────────────

    [Fact]
    public void should_close_with_action_reported_and_return_home_when_report_arrives()
    {
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings { CloseOnActionReport = true, ReturnHomePresetOnClose = true });
        manager.Open(Request(5));

        Assert.True(manager.CloseForActionReport(EventWindowKind.Detection, "5"));

        var closed = Assert.Single(host.Closed);
        Assert.Equal((Key(5), EventWindowCloseReason.ActionReported, true), closed);
        Assert.False(manager.IsOpen(Key(5)));
    }

    [Fact]
    public void should_keep_window_when_report_is_for_other_kind_pinned_or_close_on_report_off()
    {
        var (manager, host, settings, _, _) = NewManager();
        manager.Open(Request(5, kind: EventWindowKind.Detection));
        manager.Open(Request(6));
        host.Raise(new PinChanged { EventKey = Key(6), Pinned = true });

        Assert.False(manager.CloseForActionReport(EventWindowKind.Malfunction, "5"));   // 종류가 다르다
        Assert.False(manager.CloseForActionReport(EventWindowKind.Detection, "6"));     // 📌
        settings.Value = new CameraPopupSettings { CloseOnActionReport = false };
        Assert.False(manager.CloseForActionReport(EventWindowKind.Detection, "5"));     // 조치보고 닫기 끔

        Assert.Empty(host.Closed);
    }

    // ───────────────────────── 📌 고정은 GIS 책임(호스트는 CloseEventWindow 를 늘 따른다) ─────────────────────────

    [Fact]
    public void should_never_send_close_for_pinned_window_when_reports_and_evictions_arrive()
    {
        var (manager, host, settings, _, _) = NewManager(new CameraPopupSettings { MaxOpenWindows = 3 });
        for (var i = 1; i <= 3; i++) manager.Open(Request(i));
        host.Raise(new PinChanged { EventKey = Key(1), Pinned = true });
        host.Raise(new PinChanged { EventKey = Key(3), Pinned = true });

        manager.CloseForActionReport(EventWindowKind.Detection, "1");   // 고정 — 보내지 않는다
        manager.Open(Request(4));                                       // 한도 3 → 고정 안 된 2 만 정리
        settings.Value = new CameraPopupSettings { MaxOpenWindows = 1 }; // 한도를 줄여도 고정 창은 건드리지 않는다
        var result = manager.Open(Request(5));

        Assert.Equal(new[] { Key(2), Key(4) }, host.Closed.Select(c => c.Key));
        Assert.DoesNotContain(host.Closed, c => c.Key == Key(1) || c.Key == Key(3));
        Assert.Equal(EventWindowOpenResult.AllPinned, result);          // 전부 고정 → 새 창 거부와 같은 규칙
        Assert.True(manager.IsPinned(Key(1)) && manager.IsPinned(Key(3)));
    }

    [Fact]
    public void should_close_on_report_and_evict_again_when_window_is_unpinned()
    {
        var (manager, host, _, _, _) = NewManager(new CameraPopupSettings { MaxOpenWindows = 2 });
        manager.Open(Request(1));
        manager.Open(Request(2));
        host.Raise(new PinChanged { EventKey = Key(1), Pinned = true });
        host.Raise(new PinChanged { EventKey = Key(2), Pinned = true });
        Assert.Equal(EventWindowOpenResult.AllPinned, manager.Open(Request(3)));

        host.Raise(new PinChanged { EventKey = Key(1), Pinned = false });
        Assert.Equal(EventWindowOpenResult.Opened, manager.Open(Request(3)));   // 푼 창이 정리 대상이 된다
        host.Raise(new PinChanged { EventKey = Key(2), Pinned = false });
        Assert.True(manager.CloseForActionReport(EventWindowKind.Detection, "2"));

        Assert.Equal(new[] { (Key(1), EventWindowCloseReason.Evicted), (Key(2), EventWindowCloseReason.ActionReported) },
                     host.Closed.Select(c => (c.Key, c.Reason)));
    }

    [Fact]
    public void should_match_restore_list_pin_state_when_host_reports_pin()
    {
        // 감시자는 같은 PinChanged 를 기록부에 덧씌워 크래시 뒤 복원 메시지에 Pinned 로 싣는다 — 관리자 판단과 같아야 한다.
        var (manager, host, _, _, _) = NewManager();
        var registry = new HostSessionRegistry();
        manager.Open(Request(1));
        manager.Open(Request(2));
        foreach (var o in host.Opened) registry.SetWindow(o);

        var pin = new PinChanged { EventKey = Key(2), Pinned = true };
        registry.ApplyHostNotice(pin);
        host.Raise(pin);

        var restored = registry.Snapshot().Windows;
        Assert.All(restored, w => Assert.Equal(w.Pinned, manager.IsPinned(w.EventKey)));
        Assert.False(manager.CloseForActionReport(EventWindowKind.Detection, "2"));
        Assert.True(restored.Single(w => w.EventKey == Key(2)).Pinned);
    }

    // ───────────────────────── FR-27/28 ─────────────────────────

    [Fact]
    public void should_not_throw_when_host_throws_on_every_call()
    {
        var (manager, host, _, _, _) = NewManager();
        host.ThrowOnCall = true;

        var ex = Record.Exception(() =>
        {
            manager.Open(Request(1));
            manager.Open(Request(1));
            manager.CloseForActionReport(EventWindowKind.Detection, "1");
            manager.BringToFront(EventWindowKind.Detection, "1");
        });

        Assert.Null(ex);
    }

    [Fact]
    public void should_not_throw_when_settings_source_throws()
    {
        var (host, _) = RecordingHost.Create();
        var manager = new EventWindowManager(host, new DelegateCameraPopupSettingsSource(() => throw new InvalidOperationException("broken")), new FixedMonitors());

        Assert.Equal(EventWindowOpenResult.Disabled, manager.Open(Request(1)));
        Assert.Equal(CameraPopupMode.None, manager.CurrentSettings.Mode);
    }

    [Fact]
    public void should_return_within_budget_and_not_throw_when_supervisor_is_unavailable()
    {
        using var supervisor = new CameraPopupHostSupervisor(
            new CameraPopupHostOptions { HostExecutablePath = Path.Combine(Path.GetTempPath(), "no-such-camhost-" + Guid.NewGuid().ToString("N") + ".exe") },
            new TestLog());
        supervisor.Start();
        var manager = new EventWindowManager(supervisor, new DelegateCameraPopupSettingsSource(() => new CameraPopupSettings()), new FixedMonitors(), new TestLog());
        manager.Open(Request(0));   // 첫 호출(JIT) 은 재지 않는다

        var worst = 0L;
        for (var i = 1; i <= 30; i++)
        {
            var sw = Stopwatch.StartNew();
            manager.Open(Request(i));
            manager.CloseForActionReport(EventWindowKind.Detection, i.ToString());
            worst = Math.Max(worst, sw.ElapsedMilliseconds);
        }

        Assert.True(worst < 50, $"가장 느린 호출 {worst} ms — 50 ms 예산 초과");
        Assert.True(manager.OpenCount <= 10);
    }
}
