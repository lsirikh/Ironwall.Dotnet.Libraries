using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>이벤트 창 상태 헤드리스 시험(FR-10 · 13~16). 창 · 디스패처 없이 VM 만.</summary>
public class EventWindowViewModelTests
{
    private readonly FakeHostClock _clock = new();
    private readonly RecordingControlFactory _controls = new();
    private readonly List<IIpcMessage> _sent = new();

    private static EventWindowCamera Ptz(string id, string? target = null, int delay = 0, bool allowed = true, string? home = null) => new()
    {
        CameraId = id,
        Name = "PTZ-" + id,
        IsPtz = true,
        PtzAllowed = allowed,
        TargetPresetToken = target,
        TargetPresetName = target,
        HomePresetToken = home,
        DelaySeconds = delay,
        Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern },
    };

    private static EventWindowCamera Fixed(string id) => new()
    {
        CameraId = id,
        Name = "고정-" + id,
        Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern },
    };

    private EventWindowViewModel Create(Action<List<EventWindowCamera>> cameras, int columns = 0, int rows = 0, int timer = 0,
        bool pinned = false, int extra = 0, EventWindowKind kind = EventWindowKind.Detection)
    {
        var list = new List<EventWindowCamera>();
        cameras(list);
        var msg = new OpenEventWindow
        {
            Kind = kind,
            EventId = "42",
            Header = new EventWindowHeader { ZoneName = "구역-07", DeviceName = "펜스 센서 #104", EventTypeText = "침입", OccurredAt = new DateTimeOffset(2026, 9, 30, 9, 41, 7, TimeSpan.FromHours(9)) },
            GridColumns = columns,
            GridRows = rows,
            TimerCloseSeconds = timer,
            Pinned = pinned,
            ExtraCameraCount = extra,
        };
        msg.Cameras.AddRange(list);
        return new EventWindowViewModel(msg, _controls, _clock, _sent.Add, null);
    }

    // ───────── 격자(FR-10) ─────────

    [Fact]
    public void should_fill_one_empty_cell_when_five_cameras_on_3x2()
    {
        var vm = Create(c => c.AddRange(Enumerable.Range(1, 5).Select(i => Fixed("c" + i))), 3, 2);

        Assert.Equal((3, 2), (vm.Columns, vm.Rows));
        Assert.Equal(6, vm.Tiles.Count);
        Assert.Equal(5, vm.CameraTiles.Count());
        Assert.True(vm.Tiles[5].IsEmpty);
        Assert.Equal("CameraPopup.EventWindow.detection-42.Tile.Empty.0", vm.Tiles[5].AutomationId);
        Assert.Equal("CameraPopup.EventWindow.detection-42.Tile.c1", vm.Tiles[0].AutomationId);
    }

    [Fact]
    public void should_use_default_grid_when_requested_grid_cannot_hold_cameras()
    {
        var vm = Create(c => c.AddRange(Enumerable.Range(1, 4).Select(i => Fixed("c" + i))), 2, 1);

        Assert.Equal((2, 2), (vm.Columns, vm.Rows));
        Assert.Equal(4, vm.Tiles.Count);
    }

    [Fact]
    public void should_show_only_six_and_plus_n_when_more_cameras_mapped()
    {
        var vm = Create(c => c.AddRange(Enumerable.Range(1, 6).Select(i => Fixed("c" + i))), 3, 2, extra: 2);

        Assert.Equal("매핑 카메라 8대 중 6대 표시 · +2", vm.ExtraText);
        Assert.Equal("카메라 6대", vm.CameraCountText);
    }

    [Fact]
    public void should_build_header_and_badge_text_when_detection_or_malfunction()
    {
        var det = Create(c => c.Add(Fixed("a")));
        var mal = Create(c => c.Add(Fixed("a")), kind: EventWindowKind.Malfunction);

        Assert.True(det.IsDetection);
        Assert.Equal("탐지", det.BadgeText);
        Assert.Equal("장애", mal.BadgeText);
        Assert.Equal("구역-07 · 펜스 센서 #104 · 침입", det.HeaderText);
        Assert.Equal("CameraPopup.EventWindow.detection-42", det.AutomationId);
        Assert.Equal("CameraPopup.EventWindow.malfunction-42", mal.AutomationId);
    }

    // ───────── 프리셋 자동 이동 + delay(FR-13) ─────────

    [Fact]
    public void should_goto_target_and_count_down_delay_when_window_starts()
    {
        var vm = Create(c => c.Add(Ptz("p1", target: "P2", delay: 3)));
        var tile = vm.CameraTiles.Single();

        vm.Start();

        Assert.Contains("goto:P2", _controls.Simulated("p1").Calls);
        Assert.Equal("P2 로 이동 중 · 3초", tile.DelayText);
        _clock.Advance(1.2);
        vm.Tick();
        Assert.Equal("P2 로 이동 중 · 2초", tile.DelayText);
        _clock.Advance(2.0);
        vm.Tick();
        Assert.Null(tile.DelayText);
        Assert.False(tile.IsDelayVisible);
        Assert.True(tile.AutoMoveIssued);
    }

    [Fact]
    public async Task should_cancel_delay_and_follow_user_when_ptz_touched_during_delay()
    {
        var vm = Create(c => c.Add(Ptz("p1", target: "P2", delay: 5)));
        var tile = vm.CameraTiles.Single();
        vm.Start();
        Assert.NotNull(tile.DelayText);

        await tile.PtzMoveAsync(0.5, 0, 0);

        Assert.Null(tile.DelayText);
        vm.Tick();
        Assert.Null(tile.DelayText); // 다시 살아나지 않는다
        Assert.Equal(new[] { "goto:P2", "move:0.5,0,0" }, _controls.Simulated("p1").Calls);
    }

    [Fact]
    public void should_auto_move_even_without_user_permission_when_mapping_has_target()
    {
        var vm = Create(c => c.Add(Ptz("p1", target: "P2", delay: 2, allowed: false)));

        vm.Start();

        Assert.Contains("goto:P2", _controls.Simulated("p1").Calls);
        Assert.False(vm.CameraTiles.Single().IsPtzEnabled);
    }

    [Fact]
    public void should_not_auto_move_when_camera_is_fixed_or_provider_cannot_ptz()
    {
        var rtsp = Ptz("r1", target: "P1", delay: 3);
        var vm = Create(c =>
        {
            c.Add(Fixed("f1"));
            c.Add(new EventWindowCamera { CameraId = rtsp.CameraId, IsPtz = true, PtzAllowed = true, TargetPresetToken = "P1", DelaySeconds = 3, Provider = new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "rtsp://x/1" } });
        });

        vm.Start();

        Assert.All(vm.CameraTiles, t => Assert.Null(t.DelayText));
        Assert.All(vm.CameraTiles, t => Assert.False(t.AutoMoveIssued));
        Assert.Empty(_controls.Simulated("f1").Calls);
    }

    // ───────── PTZ 비활성 이유(FR-14) ─────────

    [Theory]
    [InlineData(false, true, null, PtzAvailability.FixedCameraReason)]
    [InlineData(false, false, "x", PtzAvailability.FixedCameraReason)]
    [InlineData(true, false, null, PtzAvailability.NoPermissionReason)]
    [InlineData(true, false, "제공자", PtzAvailability.NoPermissionReason)]
    [InlineData(true, true, "제공자 불가", "제공자 불가")]
    [InlineData(true, true, null, null)]
    [InlineData(true, true, "  ", null)]
    public void should_give_disabled_reason_in_priority_order_when_ptz_unavailable(bool isPtz, bool allowed, string? provider, string? expected)
    {
        Assert.Equal(expected, PtzAvailability.DisabledReason(isPtz, allowed, provider));
    }

    [Fact]
    public async Task should_disable_ptz_with_provider_reason_when_provider_is_rtsp()
    {
        var vm = Create(c => c.Add(new EventWindowCamera { CameraId = "r1", IsPtz = true, PtzAllowed = true, Provider = new VideoProviderInfo { Kind = VideoProviderKind.Rtsp } }));
        var tile = vm.CameraTiles.Single();

        Assert.False(tile.IsPtzEnabled);
        Assert.Equal(TileCameraControlFactory.RtspNoPtzReason, tile.PtzDisabledReason);
        tile.TogglePad();
        Assert.False(tile.IsPadVisible);
        await tile.GotoHomeAsync();
        Assert.Null(tile.Notice); // 부르지도 않는다
    }

    [Fact]
    public async Task should_show_notice_on_that_tile_only_when_ptz_call_fails()
    {
        _controls.FailingIds.Add("bad");
        var vm = Create(c => { c.Add(Ptz("bad")); c.Add(Ptz("good")); });
        var bad = vm.CameraTiles.First();
        var good = vm.CameraTiles.Last();

        await bad.GotoPresetAsync("P1");
        await bad.LoadPresetsAsync();
        await good.GotoPresetAsync("P1");

        Assert.Equal("프리셋 이동 실패", bad.Notice);
        Assert.Equal("불러오기 실패", bad.PresetsStatus);
        Assert.Null(good.Notice);
    }

    [Fact]
    public async Task should_list_presets_when_loaded_from_provider()
    {
        var vm = Create(c => c.Add(Ptz("p1")));
        var tile = vm.CameraTiles.Single();

        await tile.LoadPresetsAsync();

        Assert.Null(tile.PresetsStatus);
        Assert.Equal(new[] { "P1", "P2", "P3", "P4" }, tile.Presets.Select(p => p.Token));
    }

    // ───────── 타이머 닫기 · 📌 · 닫기 사유(FR-15) ─────────

    [Fact]
    public void should_restart_countdown_when_user_interacts()
    {
        var vm = Create(c => c.Add(Fixed("a")), timer: 30);
        var reasons = new List<EventWindowCloseReason>();
        vm.CloseRequested += reasons.Add;
        vm.Start();

        _clock.Advance(20);
        vm.Tick();
        Assert.Equal(10, vm.RemainingSeconds);
        Assert.Equal("타이머 10초", vm.TimerText);

        vm.NoteInteraction();
        Assert.Equal(30, vm.RemainingSeconds);
        _clock.Advance(29);
        vm.Tick();
        Assert.Empty(reasons);

        _clock.Advance(1.5);
        vm.Tick();
        vm.Tick();
        Assert.Equal(new[] { EventWindowCloseReason.Timer }, reasons);
    }

    [Fact]
    public void should_reset_timer_when_tile_ptz_used()
    {
        var vm = Create(c => c.Add(Ptz("p1")), timer: 10);
        vm.Start();
        _clock.Advance(8);
        vm.Tick();
        Assert.Equal(2, vm.RemainingSeconds);

        _ = vm.CameraTiles.Single().PtzStopAsync();

        Assert.Equal(10, vm.RemainingSeconds);
    }

    [Fact]
    public void should_not_close_by_timer_and_notify_gis_when_pinned()
    {
        var vm = Create(c => c.Add(Fixed("a")), timer: 5);
        var reasons = new List<EventWindowCloseReason>();
        vm.CloseRequested += reasons.Add;
        vm.Start();

        vm.IsPinned = true; // 📌 양방향 바인딩 경로(UIA Toggle 포함)
        _clock.Advance(100);
        vm.Tick();

        Assert.Empty(reasons);
        Assert.Null(vm.TimerText);
        Assert.Contains("고정", vm.CloseConditionsText);
        var pin = Assert.IsType<PinChanged>(Assert.Single(_sent));
        Assert.Equal(("detection-42", true), (pin.EventKey, pin.Pinned));

        vm.TogglePin();
        Assert.Equal(5, vm.RemainingSeconds); // 풀면 다시 센다
    }

    [Fact]
    public void should_restart_full_countdown_when_unpinned_after_opening_pinned()
    {
        var vm = Create(c => c.Add(Fixed("a")), timer: 10, pinned: true); // 재시작 복원처럼 고정 상태로 열림
        var reasons = new List<EventWindowCloseReason>();
        vm.CloseRequested += reasons.Add;
        vm.Start();

        _clock.Advance(600);
        vm.Tick();
        Assert.Empty(reasons);
        Assert.Null(vm.RemainingSeconds);

        vm.SetPinned(false);
        Assert.Equal(10, vm.RemainingSeconds); // 처음부터 다시 센다
        _clock.Advance(9.5);
        vm.Tick();
        Assert.Empty(reasons);
        _clock.Advance(0.6);
        vm.Tick();
        Assert.Equal(new[] { EventWindowCloseReason.Timer }, reasons);
    }

    [Theory]
    [InlineData(true, 0, "조치보고 오면 닫힘")]
    [InlineData(false, 30, "시간이 되면 닫힘")]
    [InlineData(false, 0, "직접 닫을 때까지")]
    public void should_describe_close_conditions_when_policy_given(bool onActionReport, int timer, string expected)
    {
        var msg = new OpenEventWindow { EventId = "1", CloseOnActionReport = onActionReport, TimerCloseSeconds = timer, Cameras = { Fixed("a") } };
        var vm = new EventWindowViewModel(msg, _controls, _clock, _sent.Add, null);

        Assert.Equal(expected, vm.CloseConditionsText);
        Assert.Equal(timer > 0, vm.IsTimerActive);
    }

    [Fact]
    public void should_raise_close_once_when_user_closes_repeatedly()
    {
        var vm = Create(c => c.Add(Fixed("a")), timer: 1);
        var reasons = new List<EventWindowCloseReason>();
        vm.CloseRequested += reasons.Add;
        vm.Start();

        vm.RequestClose(EventWindowCloseReason.User);
        vm.RequestClose(EventWindowCloseReason.User);
        _clock.Advance(5);
        vm.Tick();

        Assert.Equal(new[] { EventWindowCloseReason.User }, reasons);
    }

    [Fact]
    public async Task should_return_home_only_auto_moved_tiles_when_shutdown_with_return_home()
    {
        var vm = Create(c => { c.Add(Ptz("moved", target: "P2", home: "HOME")); c.Add(Ptz("manual")); });
        vm.Start();

        await vm.ShutdownAsync(returnHome: true);

        Assert.Equal(new[] { "goto:P2", "home:HOME" }, _controls.Simulated("moved").Calls);
        Assert.Empty(_controls.Simulated("manual").Calls);
    }

    [Fact]
    public async Task should_not_return_home_when_shutdown_without_return_home()
    {
        var vm = Create(c => c.Add(Ptz("moved", target: "P2")));
        vm.Start();

        await vm.ShutdownAsync(returnHome: false);

        Assert.Equal(new[] { "goto:P2" }, _controls.Simulated("moved").Calls);
    }

    // ───────── 타일 순서 · 크게 · 닫기 ─────────

    [Fact]
    public void should_move_selected_tile_with_keyboard_fallback_when_alt_arrow()
    {
        var vm = Create(c => c.AddRange(new[] { Fixed("a"), Fixed("b"), Fixed("c") }), 2, 2);
        var a = vm.Tiles[0];

        Assert.True(vm.MoveTileBy(a, +1));
        Assert.Equal(new[] { "b", "a", "c" }, vm.CameraOrder);
        Assert.True(vm.MoveTileBy(a, +1));
        Assert.Equal(new[] { "b", "c", "a" }, vm.CameraOrder);
        Assert.False(vm.MoveTileBy(a, +1)); // 끝 — 빈 칸 너머로 가지 않는다
        Assert.True(vm.Tiles[3].IsEmpty);
        Assert.True(vm.MoveTileBy(a, -1));
        Assert.Equal(new[] { "b", "a", "c" }, vm.CameraOrder);
        Assert.False(vm.MoveTileBy(vm.Tiles[3], -1)); // 빈 칸은 움직이지 않는다
    }

    [Fact]
    public void should_reorder_tiles_when_drag_commits_insert_index()
    {
        var vm = Create(c => c.AddRange(new[] { Fixed("a"), Fixed("b"), Fixed("c"), Fixed("d") }), 2, 2);

        Assert.True(vm.MoveTile(vm.Tiles[0], 3));       // a 를 d 앞으로
        Assert.Equal(new[] { "b", "c", "a", "d" }, vm.CameraOrder);
        Assert.False(vm.MoveTile(vm.Tiles[2], 2));      // 제자리(a 앞)
        Assert.False(vm.MoveTile(vm.Tiles[2], 3));      // 제자리(a 뒤)
        Assert.True(vm.MoveTile(vm.Tiles[3], 0));       // d 를 맨 앞으로
        Assert.Equal(new[] { "d", "b", "c", "a" }, vm.CameraOrder);
    }

    [Fact]
    public void should_enlarge_one_tile_to_1x1_and_restore_when_toggled_twice()
    {
        var vm = Create(c => c.AddRange(new[] { Fixed("a"), Fixed("b"), Fixed("c") }), 3, 1);
        var b = vm.Tiles[1];

        vm.ToggleEnlarge(b);
        Assert.Equal((1, 1), (vm.Columns, vm.Rows));
        Assert.True(b.IsEnlarged);
        Assert.All(vm.Tiles.Where(t => t != b), t => Assert.True(t.IsHidden));

        vm.ToggleEnlarge(b);
        Assert.Equal((3, 1), (vm.Columns, vm.Rows));
        Assert.All(vm.Tiles, t => Assert.False(t.IsHidden));
    }

    [Fact]
    public void should_notify_gis_and_keep_grid_shape_when_tile_closed()
    {
        var vm = Create(c => c.AddRange(new[] { Fixed("a"), Fixed("b") }), 2, 1);
        TileViewModel? removed = null;
        vm.TileRemoved += t => removed = t;
        var a = vm.Tiles[0];
        vm.ToggleEnlarge(a);

        vm.CloseTile(a);

        Assert.Same(a, removed);
        Assert.Equal(2, vm.Tiles.Count);
        Assert.True(vm.Tiles[1].IsEmpty);
        Assert.Equal((2, 1), (vm.Columns, vm.Rows)); // 크게 보던 타일을 닫으면 원래 격자로
        Assert.Equal("카메라 1대", vm.CameraCountText);
        var closed = Assert.IsType<TileClosed>(Assert.Single(_sent));
        Assert.Equal(("detection-42", "a"), (closed.EventKey, closed.CameraId));
    }

    [Fact]
    public void should_send_window_moved_when_user_drags_header()
    {
        var vm = Create(c => c.Add(Fixed("a")));

        vm.NotifyUserMoved(-1500, 300);

        var moved = Assert.IsType<WindowMoved>(Assert.Single(_sent));
        Assert.Equal(("detection-42", -1500, 300), (moved.EventKey, moved.X, moved.Y));
    }

    [Fact]
    public void should_select_single_tile_and_ignore_empty_cells_when_selected()
    {
        var vm = Create(c => c.AddRange(new[] { Fixed("a"), Fixed("b"), Fixed("c") }), 2, 2);

        vm.Select(vm.Tiles[0]);
        vm.Select(vm.Tiles[1]);
        Assert.False(vm.Tiles[0].IsSelected);
        Assert.True(vm.Tiles[1].IsSelected);

        vm.Select(vm.Tiles[3]); // 빈 칸
        Assert.Null(vm.SelectedTile);
        Assert.All(vm.Tiles, t => Assert.False(t.IsSelected));
    }

    // ───────── 스트림 상태(FR-26) ─────────

    [Theory]
    [InlineData(StreamState.Opening, "연결 중", false)]
    [InlineData(StreamState.Playing, "재생", false)]
    [InlineData(StreamState.Stalled, "멈춤", true)]
    [InlineData(StreamState.Failed, "연결 안 됨", true)]
    public void should_show_state_text_and_retry_when_stream_state_changes(StreamState state, string text, bool retry)
    {
        var vm = Create(c => c.Add(Fixed("a")));
        var tile = vm.CameraTiles.Single();

        tile.SetStreamState(state, "detail");

        Assert.Equal(text, tile.StateText);
        Assert.Equal(retry, tile.IsRetryVisible);
    }

    [Fact]
    public void should_request_retry_for_that_tile_only_when_retry_clicked()
    {
        var vm = Create(c => { c.Add(Fixed("a")); c.Add(Fixed("b")); });
        var a = vm.Tiles[0];
        a.SetStreamState(StreamState.Failed, "open-timeout");
        vm.Tiles[1].SetStreamState(StreamState.Playing, null);
        var retried = new List<TileViewModel>();
        foreach (var t in vm.CameraTiles) t.RetryRequested += retried.Add;

        a.RequestRetry();

        Assert.Equal(new[] { a }, retried);
        Assert.Equal(StreamState.Opening, a.StreamState);
        Assert.Equal(StreamState.Playing, vm.Tiles[1].StreamState);
    }
}
