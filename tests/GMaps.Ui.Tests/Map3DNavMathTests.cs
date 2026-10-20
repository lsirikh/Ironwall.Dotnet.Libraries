using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 방향 링 · 이동 단추(2026-10-19) 순수 함수 — 링 각도 · 360° 감기 · 링 끌기(데드존 · 캡처 잃음) · 구 손잡이(기울기) 끌기 ·
/// 누르고 있는 동안 계속 이동(가짜 시계) · 키보드 폴백 · 3D 로 보내는 메시지 형식.
/// </summary>
public class Map3DNavMathTests
{
    private const double DeadZone = 8.0;   // 호출부가 넘기는 DragMath.DeadZone 과 같은 값
    private static readonly DateTime T0 = new(2026, 10, 19, 22, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, -10, 0)]     // 위
    [InlineData(10, 0, 90)]     // 오른쪽
    [InlineData(0, 10, 180)]    // 아래
    [InlineData(-10, 0, 270)]   // 왼쪽
    [InlineData(10, -10, 45)]
    public void should_measure_ring_angle_clockwise_from_top_when_point_given(double dx, double dy, double expected)
        => Assert.Equal(expected, Map3DNavMath.AngleDeg(50, 50, 50 + dx, 50 + dy), 9);

    [Theory]
    [InlineData(350, 10, 20)]
    [InlineData(10, 350, -20)]
    [InlineData(0, 180, 180)]
    [InlineData(90, 90, 0)]
    [InlineData(720, 30, 30)]
    public void should_take_short_way_when_wrapping_angles(double from, double to, double expected)
        => Assert.Equal(expected, Map3DNavMath.WrapDelta(from, to), 9);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, -90)]
    [InlineData(-30, -330)]
    [InlineData(370, -10)]
    public void should_turn_ring_so_north_points_true_north_when_bearing_given(double bearing, double expected)
        => Assert.Equal(expected, Map3DNavMath.RingAngle(bearing), 9);

    [Fact]
    public void should_send_nothing_inside_dead_zone_and_treat_release_as_click()
    {
        var d = new Map3DNavDrag(ring: true, DeadZone);
        d.Press(50, 50, 50, 10);
        Assert.Null(d.Move(55, 15));   // 7.07 DIU
        Assert.Null(d.Move(58, 10));   // 정확히 8 은 아직 클릭
        Assert.True(d.IsPressed);
        Assert.False(d.IsDragging);
        Assert.False(d.Finish(commit: true));
        Assert.False(d.IsPressed);
    }

    [Fact]
    public void should_rotate_bearing_opposite_to_clockwise_ring_drag_including_dead_zone_part()
    {
        var d = new Map3DNavDrag(ring: true, DeadZone);
        d.Press(50, 50, 50, 10);                      // 위(0°)
        var n = d.Move(90, 50);                       // 오른쪽(90°) — 데드존을 넘는 첫 이동도 누른 자리부터 센다
        Assert.NotNull(n);
        Assert.Equal(-90, n!.Value.BearingDelta, 9);  // 링(N)이 손을 따라 시계로 → 방위는 반시계
        Assert.Equal(0, n.Value.PitchDelta);
        Assert.True(d.IsDragging);
        Assert.True(d.Finish(commit: true));
        Assert.Equal(-90, d.Total, 9);
    }

    [Fact]
    public void should_keep_accumulating_past_360_when_ring_dragged_full_turns()
    {
        var d = new Map3DNavDrag(ring: true, DeadZone);
        d.Press(0, 0, 0, -40);
        double sum = 0;
        for (int i = 1; i <= 16; i++)   // 45° 씩 두 바퀴
        {
            double a = i * 45 * Math.PI / 180;
            if (d.Move(40 * Math.Sin(a), -40 * Math.Cos(a)) is { } n) sum += n.BearingDelta;
        }
        Assert.Equal(-720, sum, 6);
        Assert.Equal(-720, d.Total, 6);
    }

    [Fact]
    public void should_ignore_points_too_close_to_ring_center()
    {
        var d = new Map3DNavDrag(ring: true, DeadZone);
        d.Press(50, 50, 50, 10);
        Assert.NotNull(d.Move(80, 20));   // 데드존을 넘어 시계로
        Assert.Null(d.Move(51, 51));      // 가운데 1.4 DIU — 각도가 흔들린다
    }

    [Fact]
    public void should_stop_sending_and_not_commit_when_capture_lost()
    {
        var d = new Map3DNavDrag(ring: true, DeadZone);
        d.Press(50, 50, 50, 10);
        Assert.NotNull(d.Move(90, 50));
        Assert.False(d.Finish(commit: false));   // 캡처 잃음 = 취소(끝 하나의 경로)
        Assert.Null(d.Move(50, 90));             // 이후 움직임은 보내지 않는다
        Assert.False(d.IsPressed);
    }

    [Fact]
    public void should_tilt_toward_horizon_when_handle_dragged_up_and_toward_top_when_down()
    {
        var d = new Map3DNavDrag(ring: false, DeadZone);
        d.Press(0, 0, 20, 100);
        Assert.Null(d.Move(20, 95));   // 데드존 안
        var up = d.Move(20, 80);       // 위로 20 DIU
        Assert.Equal(20 * Map3DNavMath.TiltDegPerDiu, up!.Value.PitchDelta, 9);
        Assert.Equal(0, up.Value.BearingDelta);
        var down = d.Move(20, 90);     // 아래로 10
        Assert.Equal(-10 * Map3DNavMath.TiltDegPerDiu, down!.Value.PitchDelta, 9);
        Assert.Null(d.Move(25, 90));   // 가로 움직임은 기울기와 무관
        Assert.Equal(5, d.Total, 9);
    }

    [Fact]
    public void should_step_once_on_press_then_move_by_elapsed_time_until_released()
    {
        var m = new Map3DNavMover();
        var first = m.Start(Map3DNavMove.Forward, T0);
        Assert.Equal(Map3DNavMath.MoveSpeedPerSecond * Map3DNavMath.FirstStepSeconds, first!.Value.Forward, 9);
        Assert.True(m.IsActive);
        var t = m.Tick(T0.AddMilliseconds(100));
        Assert.Equal(Map3DNavMath.MoveSpeedPerSecond * 0.1, t!.Value.Forward, 9);
        Assert.Equal(0, t.Value.Right);
        m.Stop(Map3DNavMove.Forward);
        Assert.False(m.IsActive);
        Assert.Null(m.Tick(T0.AddMilliseconds(200)));
    }

    [Fact]
    public void should_move_same_distance_per_second_regardless_of_tick_rate()
    {
        double Run(int ticks)
        {
            var m = new Map3DNavMover();
            m.Start(Map3DNavMove.Right, T0);
            double sum = 0;
            for (int i = 1; i <= ticks; i++) sum += m.Tick(T0.AddSeconds((double)i / ticks))?.Right ?? 0;
            return sum;
        }
        Assert.Equal(Map3DNavMath.MoveSpeedPerSecond, Run(10), 9);
        Assert.Equal(Map3DNavMath.MoveSpeedPerSecond, Run(60), 9);
    }

    [Fact]
    public void should_cap_long_gap_so_stalled_timer_does_not_jump_far()
    {
        var m = new Map3DNavMover();
        m.Start(Map3DNavMove.ZoomIn, T0);
        var n = m.Tick(T0.AddSeconds(5));
        Assert.Equal(Map3DNavMath.ZoomSpeedPerSecond * Map3DNavMath.MaxStepSeconds, n!.Value.Zoom, 9);
    }

    [Fact]
    public void should_restart_clock_when_pressed_again_after_release_and_ignore_clock_going_back()
    {
        var m = new Map3DNavMover();
        m.Start(Map3DNavMove.Forward, T0);
        m.StopAll();
        m.Stop(Map3DNavMove.Back);                                   // 누르지 않은 것을 떼도 아무 일 없음
        Assert.False(m.IsActive);
        Assert.NotNull(m.Start(Map3DNavMove.Forward, T0.AddSeconds(10)));   // 10 초 뒤 다시 누름 — 쉬던 시간을 셈하지 않는다
        var t = m.Tick(T0.AddMilliseconds(10040));
        Assert.Equal(Map3DNavMath.MoveSpeedPerSecond * 0.04, t!.Value.Forward, 9);
        Assert.Null(m.Tick(T0.AddSeconds(9)));                      // 시계가 뒤로 가면(시각 맞춤) 움직이지 않는다
        Assert.True(m.IsActive);
        Assert.Null(m.Start(Map3DNavMove.Forward, T0.AddSeconds(11)));      // 이미 누름(키 반복) — 한 걸음을 또 보내지 않는다
    }

    [Fact]
    public void should_combine_held_moves_and_cancel_opposites()
    {
        var m = new Map3DNavMover();
        m.Start(Map3DNavMove.Forward, T0);
        Assert.Null(m.Start(Map3DNavMove.Forward, T0));   // 이미 누름(키 반복)
        Assert.NotNull(m.Start(Map3DNavMove.Left, T0));
        var n = m.Tick(T0.AddMilliseconds(100))!.Value;
        Assert.Equal(0.05, n.Forward, 9);
        Assert.Equal(-0.05, n.Right, 9);
        m.Start(Map3DNavMove.Back, T0.AddMilliseconds(100));
        var n2 = m.Tick(T0.AddMilliseconds(200))!.Value;
        Assert.Equal(0, n2.Forward, 9);
        m.StopAll();
        Assert.False(m.IsActive);
        Assert.Null(m.Tick(T0.AddSeconds(1)));
    }

    [Theory]
    [InlineData(Map3DNavMove.Forward, 1, 0, 0)]
    [InlineData(Map3DNavMove.Back, -1, 0, 0)]
    [InlineData(Map3DNavMove.Right, 0, 1, 0)]
    [InlineData(Map3DNavMove.Left, 0, -1, 0)]
    [InlineData(Map3DNavMove.ZoomIn, 0, 0, 1)]
    [InlineData(Map3DNavMove.ZoomOut, 0, 0, -1)]
    public void should_map_each_move_to_its_axis_and_sign(Map3DNavMove move, int f, int r, int z)
    {
        var n = Map3DNavMath.MoveFor(new[] { move }, 1.0);
        Assert.Equal(Math.Sign(n.Forward), f);
        Assert.Equal(Math.Sign(n.Right), r);
        Assert.Equal(Math.Sign(n.Zoom), z);
        Assert.Equal(0, n.BearingDelta);
        Assert.Equal(0, n.PitchDelta);
    }

    [Theory]
    [InlineData("Left", -15, 0)]
    [InlineData("Right", 15, 0)]
    [InlineData("Up", 0, 5)]
    [InlineData("Down", 0, -5)]
    public void should_turn_and_tilt_by_ring_arrow_keys(string key, double bearing, double pitch)
    {
        var n = Map3DNavMath.KeyNudge(key)!.Value;
        Assert.Equal(bearing, n.BearingDelta);
        Assert.Equal(pitch, n.PitchDelta);
    }

    [Theory]
    [InlineData("W", Map3DNavMove.Forward)]
    [InlineData("S", Map3DNavMove.Back)]
    [InlineData("A", Map3DNavMove.Left)]
    [InlineData("D", Map3DNavMove.Right)]
    [InlineData("Add", Map3DNavMove.ZoomIn)]
    [InlineData("OemPlus", Map3DNavMove.ZoomIn)]
    [InlineData("Subtract", Map3DNavMove.ZoomOut)]
    [InlineData("OemMinus", Map3DNavMove.ZoomOut)]
    public void should_map_keyboard_fallback_to_moves(string key, Map3DNavMove expected)
        => Assert.Equal(expected, Map3DNavMath.KeyMove(key));

    [Fact]
    public void should_ignore_unrelated_keys_and_map_n_to_north()
    {
        foreach (var k in new[] { "Escape", "Enter", "Q", "Tab", "" })
        {
            Assert.Null(Map3DNavMath.KeyNudge(k));
            Assert.Null(Map3DNavMath.KeyMove(k));
            Assert.Null(Map3DNavMath.KeyPreset(k));
        }
        Assert.Equal("north", Map3DNavMath.KeyPreset("N"));
    }

    [Fact]
    public void should_build_nudge_message_with_all_fields()
    {
        var o = JObject.Parse(Map3DNavMath.NudgeJson(new Map3DNudge(-12.5, 3, 0.25, -0.5, 0.3)));
        Assert.Equal("camera.nudge", (string?)o["type"]);
        Assert.Equal(-12.5, (double)o["bearingDelta"]!);
        Assert.Equal(3, (double)o["pitchDelta"]!);
        Assert.Equal(0.25, (double)o["forward"]!);
        Assert.Equal(-0.5, (double)o["right"]!);
        Assert.Equal(0.3, (double)o["zoom"]!);
        Assert.Equal(6, o.Count);
    }

    [Theory]
    [InlineData(Map3DNavMath.PresetNorth, "north")]
    [InlineData(Map3DNavMath.PresetTop, "top")]
    [InlineData(Map3DNavMath.PresetDefault, "default")]
    public void should_build_preset_message_with_viewer_names(string name, string expected)
    {
        var o = JObject.Parse(Map3DNavMath.PresetJson(name));
        Assert.Equal("camera.preset", (string?)o["type"]);
        Assert.Equal(expected, (string?)o["name"]);
    }

    [Fact]
    public void should_not_move_panel_when_inside_dead_zone()
    {
        var m = new Map3DNavPanelMove(8);
        m.Press(10, 20, 100, 100);
        Assert.False(m.Move(105, 104));
        Assert.False(m.IsDragging);
        Assert.Equal((10d, 20d), (m.X, m.Y));
    }

    [Fact]
    public void should_move_panel_by_full_distance_when_dead_zone_passed()
    {
        var m = new Map3DNavPanelMove(8);
        m.Press(10, 20, 100, 100);
        Assert.True(m.Move(112, 95));
        Assert.Equal((22d, 15d), (m.X, m.Y));
        Assert.True(m.Move(130, 90));
        Assert.Equal((40d, 10d), (m.X, m.Y));
        Assert.True(m.Finish(commit: true));
        Assert.Equal((40d, 10d), (m.X, m.Y));
    }

    [Fact]
    public void should_return_to_start_when_drag_cancelled()
    {
        var m = new Map3DNavPanelMove(8);
        m.Press(10, 20, 100, 100);
        m.Move(160, 40);
        Assert.False(m.Finish(commit: false));
        Assert.Equal((10d, 20d), (m.X, m.Y));
        Assert.False(m.Move(200, 200));   // 끝난 뒤 움직임은 무시
    }

    [Fact]
    public void should_report_click_not_drag_when_released_inside_dead_zone()
    {
        var m = new Map3DNavPanelMove(8);
        m.Press(0, 0, 50, 50);
        m.Move(53, 52);
        Assert.False(m.Finish(commit: true));
    }

    [Theory]
    // 칸 0..1000 × 0..800, 판 150 × 300 이 (50, 450) 에 있다
    [InlineData(0, 0, 0, 0)]
    [InlineData(-200, 0, -50, 0)]        // 왼쪽 끝
    [InlineData(2000, 0, 800, 0)]        // 오른쪽 끝 = 1000 - 150 - 50
    [InlineData(0, 500, 0, 50)]          // 아래 끝 = 800 - 300 - 450
    [InlineData(0, -900, 0, -450)]       // 위 끝
    public void should_keep_panel_inside_map_when_clamped(double offX, double offY, double ex, double ey)
    {
        var (x, y) = Map3DNavPanelMove.Clamp(offX, offY, 50, 450, 150, 300, 0, 0, 1000, 800);
        Assert.Equal((ex, ey), (x, y));
    }

    [Fact]
    public void should_pin_to_left_top_when_panel_larger_than_map()
    {
        var (x, y) = Map3DNavPanelMove.Clamp(30, 30, 50, 450, 1200, 900, 0, 0, 1000, 800);
        Assert.Equal((-50d, -450d), (x, y));
    }
}
