using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using CameraPopupGridLayout = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupGridLayout;
using CameraPopupGridLayouts = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupGridLayouts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// 영상 위 드래그 PTZ 의 순수 부분 — 제스처 판정(데드존 · 단일 종료) · 누름 상태(한 번 이동 · 한 번 정지) ·
/// 드래그 → 이동량 환산(방식 고르기 · 줌 반영 · 한계 자르기) · 계약(판 3 · 직렬화) · GIS 창구.
/// </summary>
public class PtzDragGestureTests
{
    [Fact]
    public void should_stay_a_click_when_move_is_inside_dead_zone()
    {
        var g = new PtzDragGesture();
        Assert.True(g.Press(100, 100, 400, 300));

        Assert.False(g.Move(105, 106));   // √(25+36) ≈ 7.8 < 8

        Assert.False(g.IsDragging);
        Assert.Null(g.Finish(commit: true));
        Assert.False(g.IsPressed);
    }

    [Fact]
    public void should_return_view_fraction_when_released_beyond_dead_zone()
    {
        var g = new PtzDragGesture();
        g.Press(100, 100, 400, 300);

        Assert.True(g.Move(200, 70));
        var v = g.Finish(commit: true);

        Assert.NotNull(v);
        Assert.Equal(0.25, v!.Value.ViewX, 6);     // 100 / 400
        Assert.Equal(-0.1, v.Value.ViewY, 6);      // -30 / 300
        Assert.Equal(400d / 300d, v.Value.ViewAspect, 6);
    }

    [Fact]
    public void should_send_nothing_when_cancelled_by_escape_or_lost_capture()
    {
        var g = new PtzDragGesture();
        g.Press(10, 10, 400, 300);
        g.Move(300, 200);

        Assert.Null(g.Finish(commit: false));
        Assert.False(g.IsPressed);
        Assert.False(g.IsDragging);
    }

    [Fact]
    public void should_return_null_when_finished_twice()
    {
        var g = new PtzDragGesture();
        g.Press(10, 10, 400, 300);
        g.Move(300, 200);

        Assert.NotNull(g.Finish(commit: true));
        Assert.Null(g.Finish(commit: true));   // 뗌 뒤에 오는 LostMouseCapture 재진입
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, 301)]
    [InlineData(401, 10)]
    public void should_not_start_when_press_is_outside_video(double x, double y)
        => Assert.False(new PtzDragGesture().Press(x, y, 400, 300));

    [Fact]
    public void should_not_start_when_video_has_no_size()
        => Assert.False(new PtzDragGesture().Press(0, 0, 0, 0));

    [Fact]
    public void should_keep_dragging_when_pointer_returns_inside_dead_zone()
    {
        var g = new PtzDragGesture();
        g.Press(100, 100, 400, 300);
        g.Move(150, 100);

        Assert.True(g.Move(101, 100));   // 되돌리려면 Esc — 데드존으로 다시 클릭이 되지 않는다
        Assert.Equal(1d / 400d, g.Finish(true)!.Value.ViewX, 6);
    }

    [Theory]
    [InlineData(40, -30, 240, 120)]      // 중심(200,150) + 벡터
    [InlineData(900, 0, 400, 150)]       // 오른쪽 밖 → 가장자리
    [InlineData(-900, -900, 0, 0)]
    public void should_place_target_at_center_plus_drag_when_clamped_to_view(double dx, double dy, double x, double y)
        => Assert.Equal((x, y), PtzDragGesture.TargetPoint(400, 300, dx, dy));
}

public class PtzHoldPressTests
{
    [Fact]
    public void should_move_once_and_stop_once_when_pressed_and_released()
    {
        var hold = new PtzHoldPress();

        Assert.True(hold.Press("up"));
        Assert.Equal("up", hold.Finish());
        Assert.Null(hold.Finish());   // LostCapture · Deactivated · 닫힘이 뒤따라 와도 정지는 한 번
    }

    [Fact]
    public void should_ignore_key_auto_repeat_when_key_is_held()
    {
        var hold = new PtzHoldPress();
        Assert.True(hold.Press("left"));

        Assert.False(hold.Press("left", isRepeat: true));
        Assert.False(hold.Press("left", isRepeat: true));
        Assert.False(hold.Press("left"));   // 같은 단추 재누름

        Assert.Equal("left", hold.Finish());
    }

    [Fact]
    public void should_not_stop_when_nothing_was_pressed()
        => Assert.Null(new PtzHoldPress().Finish());

    [Fact]
    public void should_not_start_when_first_key_down_is_a_repeat()
    {
        var hold = new PtzHoldPress();
        Assert.False(hold.Press("up", isRepeat: true));
        Assert.False(hold.IsActive);
    }

    [Fact]
    public void should_switch_to_new_button_when_another_is_pressed_while_holding()
    {
        var hold = new PtzHoldPress();
        hold.Press("left");

        Assert.True(hold.Press("right"));   // 새 이동이 이전 이동을 대체한다
        Assert.Equal("right", hold.Finish());
        Assert.Null(hold.Finish());
    }
}

public class PtzDragMathTests
{
    private static readonly PtzLensModel Lens = PtzLensModel.Default;   // 광각 60° · 30배 · 팬 360° · 틸트 180°

    private static PtzDragSpaces Spaces(bool fov = false, bool rel = true, bool abs = true, bool cont = true) => new(
        fov, -1, 1, -1, 1,
        rel, -1, 1, -1, 1,
        abs, -1, 1, -1, 1,
        0, 1,
        cont);

    private static PtzKnownPosition At(double pan = 0, double tilt = 0, double zoom = 0) => new(pan, tilt, zoom, true, true);

    [Fact]
    public void should_prefer_fov_space_when_camera_supports_it()
        => Assert.Equal(PtzDragMoveKind.RelativeFov, PtzDragMath.SelectKind(true, true, true, true, true));

    [Fact]
    public void should_use_generic_relative_when_no_fov_space_and_zoom_is_known()
        => Assert.Equal(PtzDragMoveKind.RelativeGeneric, PtzDragMath.SelectKind(false, true, true, true, true));

    [Fact]
    public void should_use_absolute_when_relative_is_missing_and_position_is_known()
        => Assert.Equal(PtzDragMoveKind.Absolute, PtzDragMath.SelectKind(false, false, true, true, true));

    [Fact]
    public void should_fall_back_to_timed_continuous_when_zoom_is_unknown()
        => Assert.Equal(PtzDragMoveKind.ContinuousPulse, PtzDragMath.SelectKind(false, true, false, false, true));

    [Fact]
    public void should_use_generic_relative_at_wide_when_nothing_else_is_possible()
        => Assert.Equal(PtzDragMoveKind.RelativeGeneric, PtzDragMath.SelectKind(false, true, false, false, false));

    [Fact]
    public void should_report_none_when_camera_has_no_usable_space()
        => Assert.Equal(PtzDragMoveKind.None, PtzDragMath.SelectKind(false, false, false, false, false));

    [Fact]
    public void should_map_half_width_drag_to_view_edge_when_fov_space()
    {
        var (pan, tilt) = PtzDragMath.FovTranslation(0.5, -0.25, -1, 1, -1, 1);

        Assert.Equal(1.0, pan, 6);     // 화면 반 너비 = 가장자리가 중심으로
        Assert.Equal(0.5, tilt, 6);    // 위로 끌면 틸트 올림
    }

    [Theory]
    [InlineData(0.0, 60.0)]
    [InlineData(1.0, 2.2050)]    // 30배 망원
    [InlineData(0.5, 4.2664)]
    public void should_narrow_field_of_view_when_zoomed_in(double zoom, double expectedDeg)
        => Assert.Equal(expectedDeg, PtzDragMath.HorizontalFovDeg(zoom, Lens), 3);

    [Fact]
    public void should_move_by_dragged_share_of_field_of_view_when_generic_space_at_wide()
    {
        // 화면 너비의 절반을 오른쪽으로 → 화각 60° 의 절반 = 30° → 360° 가 폭 2 이므로 30/360×2
        var (pan, tilt) = PtzDragMath.GenericTranslation(0.5, 0, 16d / 9d, 0, Lens, 2, 2, -1, 1, -1, 1);

        Assert.Equal(30d / 360d * 2d, pan, 6);
        Assert.Equal(0, tilt, 9);
    }

    [Fact]
    public void should_move_less_for_same_drag_when_zoomed_in()
    {
        var wide = PtzDragMath.GenericTranslation(0.5, 0.5, 16d / 9d, 0, Lens, 2, 2, -1, 1, -1, 1);
        var tele = PtzDragMath.GenericTranslation(0.5, 0.5, 16d / 9d, 1, Lens, 2, 2, -1, 1, -1, 1);

        Assert.True(Math.Abs(tele.Pan) < Math.Abs(wide.Pan) / 20, $"wide={wide.Pan} tele={tele.Pan}");
        Assert.True(Math.Abs(tele.Tilt) < Math.Abs(wide.Tilt) / 20, $"wide={wide.Tilt} tele={tele.Tilt}");
        Assert.True(wide.Pan > 0 && wide.Tilt < 0);   // 오른쪽 · 아래로 끌면 오른쪽 · 아래를 본다
    }

    [Fact]
    public void should_clamp_to_relative_range_when_camera_range_is_small()
    {
        var (pan, tilt) = PtzDragMath.GenericTranslation(1, -1, 16d / 9d, 0, Lens, 2, 2, -0.05, 0.05, -0.02, 0.02);

        Assert.Equal(0.05, pan, 9);
        Assert.Equal(0.02, tilt, 9);
    }

    [Fact]
    public void should_not_push_tilt_beyond_limit_when_already_at_the_edge()
    {
        var (pan, tilt) = PtzDragMath.ClampToTravel(0.2, 0.98, 0.1, 0.1, -1, 1, -1, 1, panWraps: true);

        Assert.Equal(0.1, pan, 9);      // 팬은 한 바퀴 도는 카메라 — 자르지 않는다
        Assert.Equal(0.02, tilt, 9);    // 남은 0.02 만
    }

    [Fact]
    public void should_wrap_pan_and_clamp_tilt_when_absolute_target_crosses_range()
    {
        var wrapped = PtzDragMath.AbsoluteTarget(0.95, -0.9, 0.1, -0.3, -1, 1, -1, 1, panWraps: true);
        var clamped = PtzDragMath.AbsoluteTarget(0.95, -0.9, 0.1, -0.3, -1, 1, -1, 1, panWraps: false);

        Assert.Equal(-0.95, wrapped.Pan, 9);
        Assert.Equal(-1, wrapped.Tilt, 9);
        Assert.Equal(1, clamped.Pan, 9);
    }

    [Fact]
    public void should_plan_nothing_when_drag_vector_is_zero()
        => Assert.Equal(PtzDragMoveKind.None, PtzDragMath.Plan(Spaces(), At(), 0, 0, 16d / 9d).Kind);

    [Fact]
    public void should_plan_absolute_target_from_tracked_position_when_only_absolute_space()
    {
        var plan = PtzDragMath.Plan(Spaces(rel: false), At(pan: 0.1, tilt: 0.2), 0.5, 0, 16d / 9d);

        Assert.Equal(PtzDragMoveKind.Absolute, plan.Kind);
        Assert.Equal(0.1 + (30d / 360d * 2d), plan.Pan, 6);
        Assert.Equal(0.2, plan.Tilt, 6);
    }

    [Fact]
    public void should_plan_timed_continuous_when_position_was_never_read()
    {
        var plan = PtzDragMath.Plan(Spaces(), PtzKnownPosition.Unknown, 0.5, 0, 16d / 9d);

        Assert.Equal(PtzDragMoveKind.ContinuousPulse, plan.Kind);
        Assert.True(plan.Pan > 0 && plan.Tilt == 0);
        Assert.InRange(plan.PulseMs, PtzDragMath.PulseMinMs, PtzDragMath.PulseMaxMs);
    }

    [Fact]
    public void should_keep_minimum_pulse_when_drag_is_tiny()
    {
        var plan = PtzDragMath.ContinuousPulse(0.01, 0, 16d / 9d);

        Assert.Equal(PtzDragMath.PulseMinMs, plan.PulseMs);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]   // 유한하지 않으면 안 움직이는 쪽
    [InlineData(-7, -1)]
    [InlineData(0.3, 0.3)]
    public void should_sanitize_fraction_when_value_is_wild(double input, double expected)
        => Assert.Equal(expected, PtzDragMath.SanitizeFraction(input));
}

public class PtzDragContractTests
{
    [Fact]
    public void should_round_trip_drag_move_when_serialized()
    {
        var original = new PtzCommand
        {
            CameraId = "c1", Operation = PtzOperation.DragMove, ViewX = 0.25, ViewY = -0.125, ViewAspect = 1.5,
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 },
        };

        var bytes = IpcSerializer.Serialize(original, 5);
        var status = IpcSerializer.TryDeserialize(bytes, out var decoded, out _, out var type);

        Assert.Equal(DecodeStatus.Ok, status);
        Assert.Equal("Ptz", type);
        var back = Assert.IsType<PtzCommand>(decoded);
        Assert.Equal((PtzOperation.DragMove, 0.25, -0.125, 1.5), (back.Operation, back.ViewX, back.ViewY, back.ViewAspect));
        Assert.Contains("\"DragMove\"", System.Text.Encoding.UTF8.GetString(bytes));   // 열거 이름이 계약이다
    }

    [Fact]
    public void should_be_protocol_three_and_reject_older_peers_when_drag_move_was_added()
    {
        Assert.Equal(3, ProtocolVersion.Current);
        Assert.True(ProtocolVersion.IsCompatible(3));
        Assert.False(ProtocolVersion.IsCompatible(2));   // 판 2 호스트는 DragMove 를 읽지 못한다 — 조용히 버리지 않고 판 불일치로 드러낸다
        Assert.False(ProtocolVersion.IsCompatible(4));
    }

    [Fact]
    public void should_send_one_coalescable_drag_message_when_gis_control_drags()
    {
        var (host, recorder) = RecordingHost.Create();
        using var control = new CameraPopupControl(host);
        var provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5" };

        Assert.False(control.DragMove("7", provider, double.NaN, 0, 1.5));   // 유한하지 않은 값은 보내지 않는다
        control.DragMove("7", provider, 0.3, -2.5, 1.5);

        var call = Assert.Single(recorder.Calls, c => c.Member == nameof(ICameraPopupHost.Send));
        var command = Assert.IsType<PtzCommand>(call.Args[0]);
        Assert.Equal("ptz:7", call.Args[1]);   // 이동 · 정지 · 드래그가 같은 합치기 키 → 밀리면 최신 하나만
        Assert.Equal((PtzOperation.DragMove, 0.3, -1.0, 1.5), (command.Operation, command.ViewX, command.ViewY, command.ViewAspect));
    }
}

/// <summary>이벤트 창 격자 정하기(한 곳) — 실제 카메라 수가 설정과 같으면 설정 격자, 적으면 그 창에서 16:9 타일이 가장 큰 격자.</summary>
public class CameraPopupGridForWindowTests
{
    [Theory]
    // 960×600(기본 창)
    [InlineData(960, 600, 1, "1x1")]
    [InlineData(960, 600, 2, "1x2")]
    [InlineData(960, 600, 3, "2x2")]   // 3×1 은 320×180 타일 + 위아래 큰 띠 → 2×2 한 칸 비움(480×270)
    [InlineData(960, 600, 4, "2x2")]
    [InlineData(960, 600, 5, "2x3")]
    [InlineData(960, 600, 6, "2x3")]
    // 1280×720(16:9 창)
    [InlineData(1280, 720, 1, "1x1")]
    [InlineData(1280, 720, 2, "2x1")]
    [InlineData(1280, 720, 3, "2x2")]
    [InlineData(1280, 720, 4, "2x2")]
    [InlineData(1280, 720, 5, "3x2")]
    [InlineData(1280, 720, 6, "3x2")]
    // 600×900(세로 창)
    [InlineData(600, 900, 1, "1x1")]
    [InlineData(600, 900, 2, "1x2")]
    [InlineData(600, 900, 3, "1x3")]
    [InlineData(600, 900, 4, "1x4")]
    [InlineData(600, 900, 5, "2x3")]
    [InlineData(600, 900, 6, "2x3")]
    // 1600×500(가로로 긴 창)
    [InlineData(1600, 500, 1, "1x1")]
    [InlineData(1600, 500, 2, "2x1")]
    [InlineData(1600, 500, 3, "3x1")]
    [InlineData(1600, 500, 4, "2x2")]
    [InlineData(1600, 500, 5, "3x2")]
    [InlineData(1600, 500, 6, "3x2")]
    public void should_pick_grid_with_largest_tile_when_window_size_given(double width, double height, int cameras, string expected)
        => Assert.Equal(expected, CameraPopupGridLayouts.BestFit(cameras, width, height).Key);

    [Theory]
    [InlineData(4, "2x2")]
    [InlineData(4, "4x1")]
    [InlineData(6, "6x1")]
    [InlineData(3, "1x3")]
    public void should_keep_configured_grid_when_actual_count_equals_configured(int cameras, string configured)
    {
        Assert.True(CameraPopupGridLayout.TryParse(configured, out var layout));

        Assert.Equal(layout, CameraPopupGridLayouts.ForWindow(cameras, cameras, layout, 960, 600));
    }

    [Fact]
    public void should_leave_one_cell_empty_when_three_cameras_open_in_four_camera_window()
    {
        // 실카메라 헤디드 결과: 설정 4대 · 2×2 인데 매핑 3대 → 3×1 한 줄(위아래 검은 띠). 이제 2×2 한 칸 비움.
        var layout = CameraPopupGridLayouts.ForWindow(3, 4, new CameraPopupGridLayout(2, 2), 960, 600);

        Assert.Equal(new CameraPopupGridLayout(2, 2), layout);
        Assert.True(CameraPopupGridLayouts.TileArea(layout, 960, 600) > 2 * CameraPopupGridLayouts.TileArea(new CameraPopupGridLayout(3, 1), 960, 600));
    }

    [Fact]
    public void should_always_hold_every_camera_when_best_fit_is_chosen()
    {
        foreach (var (w, h) in new[] { (960d, 600d), (320d, 1200d), (3000d, 400d), (800d, 800d) })
            for (int n = 1; n <= 6; n++)
                Assert.True(CameraPopupGridLayouts.BestFit(n, w, h).Capacity >= n, $"{n} cameras in {w}x{h}");
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(960, -1)]
    [InlineData(double.NaN, 600)]
    public void should_use_default_grid_when_window_size_is_unknown(double width, double height)
        => Assert.Equal(CameraPopupGridLayouts.Default(3), CameraPopupGridLayouts.BestFit(3, width, height));

    [Fact]
    public void should_prefer_fewer_empty_cells_when_tile_size_ties()
    {
        // 960×600 · 3대: 3×1 과 3×2 는 타일 크기가 같다(320×180) — 빈 칸 적은 3×1 이 앞선다(둘 다 2×2 보다는 작다).
        Assert.Equal(CameraPopupGridLayouts.TileArea(new CameraPopupGridLayout(3, 1), 960, 600),
                     CameraPopupGridLayouts.TileArea(new CameraPopupGridLayout(3, 2), 960, 600), 6);
        Assert.Equal("2x1", CameraPopupGridLayouts.BestFit(2, 1280, 720).Key);   // 2×1 과 1×2 가 같은 크기 → 표 순서(가로 우선)
    }
}
