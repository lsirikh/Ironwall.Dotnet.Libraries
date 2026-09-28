using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// unit-relationship-map TEST-01 (FR-12 · FR-14 · FR-15 · NFR-03 · NFR-11) — 관계도 뷰포트와 휠 합침은
/// 화면 없이 도는 순수 수학이다. 다음 관계도(서버 · 결선)도 같은 형식을 쓴다(D-4).
/// </summary>
public class GraphViewportTests
{
    private const double Eps = 1e-6;

    private static void AssertPoint(Point expected, Point actual, double eps = Eps)
    {
        Assert.InRange(actual.X, expected.X - eps, expected.X + eps);
        Assert.InRange(actual.Y, expected.Y - eps, expected.Y + eps);
    }

    #region - 커서 기준 줌 -
    [Theory]
    [InlineData(1.0, 0, 0, 300, 200, 1.2)]
    [InlineData(0.5, -120, 40, 17.5, 333.25, 1 / 1.2)]
    [InlineData(0.37, 250, -90, 0, 0, 1.2)]
    [InlineData(1.3, 12.5, 7.25, 755, 599, 1.2)]
    public void should_keep_world_point_under_cursor_when_zooming(double scale, double ox, double oy, double cx, double cy, double factor)
    {
        // Arrange
        var viewport = new GraphViewport(scale, new Vector(ox, oy));
        var cursor = new Point(cx, cy);
        var worldBefore = viewport.ScreenToWorld(cursor);

        // Act
        var zoomed = viewport.ZoomAt(cursor, factor);

        // Assert — o' = c − (c − o)·s'/s 이면 커서 아래 월드 점이 그대로다.
        AssertPoint(worldBefore, zoomed.ScreenToWorld(cursor));
        Assert.Equal(scale * factor, zoomed.Scale, 9);
        Assert.Equal(cx - (cx - ox) * zoomed.Scale / scale, zoomed.Offset.X, 9);
        Assert.Equal(cy - (cy - oy) * zoomed.Scale / scale, zoomed.Offset.Y, 9);
    }

    [Theory]
    [InlineData(1.5, 1.2, 1.60)]
    [InlineData(1.60, 1.2, 1.60)]
    [InlineData(0.11, 1 / 1.2, 0.10)]
    [InlineData(0.10, 0.5, 0.10)]
    [InlineData(1.0, 100, 1.60)]
    public void should_clamp_scale_to_0_10_and_1_60_when_zoom_exceeds(double scale, double factor, double expected)
    {
        var viewport = new GraphViewport(scale, new Vector(10, 20));

        var zoomed = viewport.ZoomAt(new Point(100, 100), factor);

        Assert.Equal(expected, zoomed.Scale, 9);
    }

    [Fact]
    public void should_keep_world_point_under_cursor_when_zoom_is_clamped()
    {
        var viewport = new GraphViewport(1.5, new Vector(-40, 12));
        var cursor = new Point(222, 111);
        var before = viewport.ScreenToWorld(cursor);

        var zoomed = viewport.ZoomAt(cursor, 1.2);   // 1.8 → 1.6 으로 잘린다

        Assert.Equal(GraphViewport.MaxScale, zoomed.Scale, 9);
        AssertPoint(before, zoomed.ScreenToWorld(cursor));
    }

    [Fact]
    public void should_not_move_when_zoom_is_already_at_limit()
    {
        var viewport = new GraphViewport(GraphViewport.MinScale, new Vector(33, 44));

        var zoomed = viewport.ZoomAt(new Point(500, 300), 1 / 1.2);

        Assert.Equal(viewport, zoomed);
    }

    [Fact]
    public void should_zoom_to_target_scale_around_cursor_when_zoom_to()
    {
        var viewport = new GraphViewport(0.3, new Vector(15, -5));
        var cursor = new Point(400, 250);
        var before = viewport.ScreenToWorld(cursor);

        var zoomed = viewport.ZoomTo(cursor, 0.40);

        Assert.Equal(0.40, zoomed.Scale, 9);
        AssertPoint(before, zoomed.ScreenToWorld(cursor));
    }
    #endregion

    #region - 전체 보기 -
    [Fact]
    public void should_center_bounds_with_16_padding_when_fit()
    {
        // Arrange — 콘솔 캔버스 756 × 600, 월드 1000 × 500
        var bounds = new Rect(100, 50, 1000, 500);
        var size = new Size(756, 600);

        // Act
        var fitted = GraphViewport.Fit(bounds, size);

        // Assert — 폭이 결정한다: (756 − 32) / 1000 = 0.724. 가로 양 끝이 정확히 여백 16.
        Assert.Equal(0.724, fitted.Scale, 9);
        AssertPoint(new Point(16, 300 - 250 * 0.724), fitted.WorldToScreen(bounds.TopLeft));
        AssertPoint(new Point(756 - 16, 300 + 250 * 0.724), fitted.WorldToScreen(bounds.BottomRight));
        AssertPoint(new Point(378, 300), fitted.WorldToScreen(new Point(600, 300)));
    }

    [Fact]
    public void should_stop_at_min_scale_when_fit_bounds_too_wide()
    {
        var bounds = new Rect(0, 0, 20000, 800);
        var size = new Size(756, 600);

        var fitted = GraphViewport.Fit(bounds, size);

        Assert.Equal(GraphViewport.MinScale, fitted.Scale, 9);
        AssertPoint(new Point(378, 300), fitted.WorldToScreen(new Point(10000, 400)));   // 가운데는 맞춘다
    }

    [Fact]
    public void should_stop_at_max_scale_when_fit_bounds_are_tiny()
    {
        var fitted = GraphViewport.Fit(new Rect(40, 40, 10, 10), new Size(756, 600));

        Assert.Equal(GraphViewport.MaxScale, fitted.Scale, 9);
        AssertPoint(new Point(378, 300), fitted.WorldToScreen(new Point(45, 45)));
    }

    [Fact]
    public void should_fit_7200_world_within_756_canvas_when_min_scale()
    {
        // PRD §3.3 — 200 부대 = 36칸 × 200 = 7,200 월드 → 0.10 에서 720 DIU + 여백 32 = 752 ≤ 756.
        var fitted = GraphViewport.Fit(new Rect(0, 0, 7200, 1200), new Size(756, 600));

        Assert.True(fitted.Scale >= GraphViewport.MinScale);
        Assert.True(fitted.WorldToScreen(new Point(0, 0)).X >= 16 - Eps);
        Assert.True(fitted.WorldToScreen(new Point(7200, 0)).X <= 756 - 16 + Eps);
    }

    [Fact]
    public void should_keep_viewport_when_fit_has_empty_bounds_or_viewport()
    {
        Assert.Equal(GraphViewport.Identity, GraphViewport.Fit(Rect.Empty, new Size(756, 600)));
        Assert.Equal(GraphViewport.Identity, GraphViewport.Fit(new Rect(0, 0, 100, 100), new Size(0, 0)));
    }

    [Fact]
    public void should_keep_edge_node_shapes_inside_padding_when_fit_with_node_box()
    {
        // 시나리오 ISSUE-39(SIM-V001) — 점만 맞추면 고정 크기 도형(L0 틀 20×13)의 가장자리가 잘린다.
        var bounds = new Rect(100, 0, 7000, 1160);          // 200 부대 자동 배치의 노드 중심 경계
        var nodeBox = new Rect(-10, -6.5, 20, 13);

        var fitted = GraphViewport.Fit(bounds, new Size(756, 560), nodeBox: nodeBox);

        Assert.Equal((756 - 32 - 20) / 7000.0, fitted.Scale, 9);
        Assert.Equal(16, fitted.WorldToScreen(bounds.TopLeft).X + nodeBox.Left, 6);
        Assert.Equal(756 - 16, fitted.WorldToScreen(bounds.BottomRight).X + nodeBox.Right, 6);
    }

    [Fact]
    public void should_center_asymmetric_node_box_when_fit()
    {
        // L1 노드는 세로 −19 ~ +26 으로 위아래가 다르다 — 도형까지 포함한 내용의 가운데를 맞춘다.
        var bounds = new Rect(0, 0, 400, 300);
        var nodeBox = new Rect(-22, -19, 44, 45);

        var fitted = GraphViewport.Fit(bounds, new Size(800, 600), nodeBox: nodeBox);

        var top = fitted.WorldToScreen(bounds.TopLeft).Y + nodeBox.Top;
        var bottom = fitted.WorldToScreen(bounds.BottomRight).Y + nodeBox.Bottom;
        Assert.Equal(600 - bottom, top, 6);
        Assert.True(top >= 16 - Eps);
    }

    [Fact]
    public void should_report_nothing_to_fit_when_no_units()
    {
        // 시나리오 SIM-V019 — 부대 0: 0 나눗셈 없이 거절하고 호출부가 지금 배율을 지킨다.
        var fitted = GraphViewport.TryFit(Rect.Empty, new Size(756, 560), out var viewport);

        Assert.False(fitted);
        Assert.Equal(GraphViewport.Identity, viewport);
    }

    [Fact]
    public void should_fit_single_point_at_max_scale_when_one_unit()
    {
        var fitted = GraphViewport.TryFit(new Rect(new Point(300, 160), new Size(0, 0)), new Size(756, 560), out var viewport,
                                          nodeBox: new Rect(-66, -28, 132, 56));

        Assert.True(fitted);
        Assert.Equal(GraphViewport.MaxScale, viewport.Scale, 9);
        AssertPoint(new Point(378, 280), viewport.WorldToScreen(new Point(300, 160)));
    }

    [Fact]
    public void should_put_world_point_at_viewport_center_when_center_on()
    {
        var viewport = new GraphViewport(0.5, new Vector(-300, 90));

        var centered = viewport.CenterOn(new Point(1234, 567), new Size(800, 600));

        Assert.Equal(0.5, centered.Scale, 9);
        AssertPoint(new Point(400, 300), centered.WorldToScreen(new Point(1234, 567)));
    }
    #endregion

    #region - 팬 -
    [Theory]
    [InlineData(1, 0, -100, 0)]     // Ctrl+→ : 뷰가 오른쪽으로 → 그림은 왼쪽으로 1/8
    [InlineData(-1, 0, 100, 0)]
    [InlineData(0, 1, 0, -75)]      // Ctrl+↓
    [InlineData(0, -1, 0, 75)]
    public void should_pan_by_one_eighth_viewport_when_ctrl_arrow(int dirX, int dirY, double expectedDx, double expectedDy)
    {
        var viewport = new GraphViewport(0.8, new Vector(10, 20));

        var panned = viewport.PanStep(new Size(800, 600), dirX, dirY);

        Assert.Equal(0.8, panned.Scale, 9);
        Assert.Equal(10 + expectedDx, panned.Offset.X, 9);
        Assert.Equal(20 + expectedDy, panned.Offset.Y, 9);
    }

    [Fact]
    public void should_move_offset_by_screen_delta_when_pan()
    {
        var viewport = new GraphViewport(0.37, new Vector(5, 6));

        var panned = viewport.Pan(-40, 12.5);

        Assert.Equal(new Vector(-35, 18.5), panned.Offset);
        Assert.Equal(0.37, panned.Scale, 9);
    }
    #endregion

    #region - 팬 한계 (조정자 결정 D-2026-09-27-6615ba · ISSUE-40 · SIM-V079 · V080) -
    private static readonly Rect Org = new(100, 0, 7000, 1160);     // 200 부대 자동 배치의 노드 중심 경계
    private static readonly Size Canvas = new(756, 560);

    /// <summary>그림(경계 × 배율)이 뷰포트와 겹치는 가로 · 세로 길이.</summary>
    private static (double X, double Y) Overlap(GraphViewport v, Rect world, Size viewport)
    {
        var a = v.WorldToScreen(world.TopLeft);
        var b = v.WorldToScreen(world.BottomRight);
        return (Math.Min(b.X, viewport.Width) - Math.Max(a.X, 0), Math.Min(b.Y, viewport.Height) - Math.Max(a.Y, 0));
    }

    [Fact]
    public void should_leave_viewport_unchanged_when_org_is_well_inside()
    {
        var viewport = GraphViewport.Fit(Org, Canvas);

        Assert.Equal(viewport, viewport.ClampPan(Org, Canvas));
    }

    [Theory]
    [InlineData(5000, 0)]        // 빈 곳 끌기로 +5,000 DIU(SIM-V079)
    [InlineData(-5000, 0)]
    [InlineData(0, 3000)]
    [InlineData(0, -3000)]
    [InlineData(-9000, 9000)]
    public void should_keep_at_least_20_percent_of_org_visible_when_panned_far(double dx, double dy)
    {
        var viewport = new GraphViewport(0.5, new Vector(0, 0)).Pan(dx, dy);

        var clamped = viewport.ClampPan(Org, Canvas);

        var (x, y) = Overlap(clamped, Org, Canvas);
        var width = Math.Min(Org.Width * 0.5, Canvas.Width);
        var height = Math.Min(Org.Height * 0.5, Canvas.Height);
        Assert.True(x >= 0.2 * width - 1e-6, $"가로 {x} < {0.2 * width}");
        Assert.True(y >= 0.2 * height - 1e-6, $"세로 {y} < {0.2 * height}");
        Assert.Equal(0.5, clamped.Scale, 9);
    }

    [Fact]
    public void should_stop_at_limit_when_ctrl_arrow_repeats_100_times()
    {
        // SIM-V080 — Ctrl+→ 100회: 한계에서 멈춘다(더 밀어도 그대로)
        var viewport = new GraphViewport(0.5, new Vector(0, 0));
        for (var i = 0; i < 100; i++) viewport = viewport.PanStep(Canvas, 1, 0).ClampPan(Org, Canvas);
        var once = viewport.PanStep(Canvas, 1, 0).ClampPan(Org, Canvas);

        Assert.Equal(viewport, once);
        Assert.True(Overlap(viewport, Org, Canvas).X > 0);
    }

    [Fact]
    public void should_keep_single_point_inside_viewport_when_org_has_no_extent()
    {
        var point = new Rect(new Point(300, 160), new Size(0, 0));
        var viewport = new GraphViewport(1.0, new Vector(-4000, 2500));

        var clamped = viewport.ClampPan(point, Canvas);
        var screen = clamped.WorldToScreen(point.TopLeft);

        Assert.InRange(screen.X, 0, Canvas.Width);
        Assert.InRange(screen.Y, 0, Canvas.Height);
    }

    [Fact]
    public void should_return_same_viewport_when_bounds_empty()
    {
        var viewport = new GraphViewport(0.3, new Vector(-99999, 99999));

        Assert.Equal(viewport, viewport.ClampPan(Rect.Empty, Canvas));
    }
    #endregion

    #region - 넘침 · 크기 변경 · Δ 절단 (TEST-68 ③④⑤ · ②) -
    [Fact]
    public void should_report_overflow_only_when_min_scale_still_does_not_fit()
    {
        // 200 부대(7,000 월드) @756 은 0.10 에서 든다 · 폭 63 캔버스나 20,000 월드는 넘친다(SIM-V097~104)
        Assert.False(GraphViewport.Overflows(new Rect(100, 0, 7000, 1160), new Size(756, 560), nodeBox: new Rect(-10, -6.5, 20, 13)));
        Assert.True(GraphViewport.Overflows(new Rect(0, 0, 20000, 800), new Size(756, 560)));
        Assert.True(GraphViewport.Overflows(new Rect(0, 0, 400, 300), new Size(63, 560)));
        Assert.False(GraphViewport.Overflows(Rect.Empty, new Size(756, 560)));
        Assert.False(GraphViewport.Overflows(new Rect(0, 0, 400, 300), new Size(0, 0)));
    }

    [Fact]
    public void should_fit_at_min_scale_and_overflow_when_viewport_is_1_wide()
    {
        var bounds = new Rect(0, 0, 400, 300);

        Assert.True(GraphViewport.TryFit(bounds, new Size(1, 560), out var fitted));
        Assert.Equal(GraphViewport.MinScale, fitted.Scale, 9);
        Assert.True(GraphViewport.Overflows(bounds, new Size(1, 560)));
    }

    [Fact]
    public void should_keep_world_point_at_center_when_viewport_resized()
    {
        // ISSUE-54 — 756 → 1096(상세 칸 접힘 · 최대화)에도 화면 가운데의 월드 점이 그대로
        var viewport = new GraphViewport(0.5, new Vector(-120, 40));
        var before = viewport.ScreenToWorld(new Point(378, 280));

        var resized = viewport.Resize(new Size(756, 560), new Size(1096, 700));

        Assert.Equal(0.5, resized.Scale, 9);
        AssertPoint(before, resized.ScreenToWorld(new Point(548, 350)));
    }

    [Fact]
    public void should_do_nothing_when_resized_from_or_to_zero_size()
    {
        var viewport = new GraphViewport(0.5, new Vector(-120, 40));

        Assert.Equal(viewport, viewport.Resize(new Size(0, 0), new Size(756, 560)));
        Assert.Equal(viewport, viewport.Resize(new Size(756, 560), new Size(0, 560)));
    }

    [Theory]
    [InlineData(5_000_000, -3_000_000, 1_000_000, -1_000_000)]
    [InlineData(12.5, -7, 12.5, -7)]
    [InlineData(double.NaN, double.PositiveInfinity, 0, 0)]
    public void should_clamp_delta_to_server_limit(double x, double y, double ex, double ey)
    {
        Assert.Equal(new Vector(ex, ey), GraphViewport.ClampDelta(new Vector(x, y)));
    }
    #endregion

    #region - 좌표 변환 -
    [Theory]
    [InlineData(1.0, 0, 0, 0, 0)]
    [InlineData(0.1, -250, 33, 7200, 1160)]
    [InlineData(1.6, 12.3, -45.6, -77.7, 88.8)]
    [InlineData(0.4321, 1000, 1000, 3.5, -2.25)]
    public void should_roundtrip_screen_world_when_scaled_and_offset(double scale, double ox, double oy, double wx, double wy)
    {
        var viewport = new GraphViewport(scale, new Vector(ox, oy));
        var world = new Point(wx, wy);

        var screen = viewport.WorldToScreen(world);

        AssertPoint(new Point(wx * scale + ox, wy * scale + oy), screen);
        AssertPoint(world, viewport.ScreenToWorld(screen));
    }

    [Theory]
    [InlineData(0.05, 0.10)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(2.0, 1.60)]
    [InlineData(0.5, 0.5)]
    public void should_clamp_scale_when_value_out_of_range(double value, double expected)
    {
        Assert.Equal(expected, GraphViewport.ClampScale(value), 9);
    }
    #endregion
}

/// <summary>
/// unit-relationship-map TEST-01 (FR-12 · NFR-03) — 연속 휠은 누적만 하고 33ms 에 한 번 적용한다(최대 30Hz).
/// 시간은 가짜 시계로만 흘린다 — <c>Task.Delay</c> · <c>sleep</c> 없음.
/// </summary>
public class GraphWheelAccumulatorTests
{
    /// <summary>가짜 시계 — <c>IClock.UtcNow</c> 자리. Utils 는 Base 를 참조하지 않아 시각 공급자를 함수로 받는다.</summary>
    private sealed class FakeClock
    {
        public DateTime Now { get; set; } = new(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc);
        public void Advance(double ms) => Now = Now.AddMilliseconds(ms);
    }

    [Fact]
    public void should_apply_once_with_1_728_when_three_notches_within_33ms()
    {
        // Arrange
        var clock = new FakeClock();
        var wheel = new GraphWheelAccumulator(() => clock.Now);

        // Act — 0 · 10 · 25 ms 에 한 칸씩
        wheel.Add(1, new Point(10, 10));
        clock.Advance(10);
        wheel.Add(1, new Point(11, 10));
        clock.Advance(15);
        wheel.Add(1, new Point(12, 10));
        var early = wheel.TryFlush(out _, out _);     // 25ms — 아직
        clock.Advance(8);                               // 33ms
        var due = wheel.TryFlush(out var factor, out var cursor);
        var again = wheel.TryFlush(out _, out _);

        // Assert
        Assert.False(early);
        Assert.True(due);
        Assert.Equal(1.728, factor, 9);
        Assert.Equal(new Point(12, 10), cursor);        // 마지막 커서 기준
        Assert.False(again);                            // 한 번만
        Assert.False(wheel.IsPending);
    }

    [Fact]
    public void should_cancel_out_when_notches_go_both_ways_within_window()
    {
        var clock = new FakeClock();
        var wheel = new GraphWheelAccumulator(() => clock.Now);

        wheel.Add(2, new Point(0, 0));
        wheel.Add(-2, new Point(0, 0));
        clock.Advance(40);

        Assert.True(wheel.TryFlush(out var factor, out _));
        Assert.Equal(1.0, factor, 9);
    }

    [Fact]
    public void should_divide_by_1_2_per_notch_when_wheel_down()
    {
        var clock = new FakeClock();
        var wheel = new GraphWheelAccumulator(() => clock.Now);

        wheel.Add(-1, new Point(5, 5));
        clock.Advance(33);

        Assert.True(wheel.TryFlush(out var factor, out _));
        Assert.Equal(1 / 1.2, factor, 9);
    }

    [Fact]
    public void should_open_new_window_when_notch_arrives_after_flush()
    {
        var clock = new FakeClock();
        var wheel = new GraphWheelAccumulator(() => clock.Now);

        wheel.Add(1, new Point(0, 0));
        clock.Advance(33);
        Assert.True(wheel.TryFlush(out _, out _));

        clock.Advance(100);
        wheel.Add(1, new Point(0, 0));
        Assert.Equal(GraphWheelAccumulator.Window, wheel.DueIn());
        clock.Advance(20);
        Assert.False(wheel.TryFlush(out _, out _));
        Assert.Equal(TimeSpan.FromMilliseconds(13), wheel.DueIn());
    }

    [Fact]
    public void should_report_nothing_pending_when_no_notch()
    {
        var clock = new FakeClock();
        var wheel = new GraphWheelAccumulator(() => clock.Now);

        Assert.False(wheel.IsPending);
        Assert.False(wheel.TryFlush(out var factor, out _));
        Assert.Equal(1.0, factor, 9);
        Assert.Equal(TimeSpan.Zero, wheel.DueIn());
    }

    [Fact]
    public void should_ignore_zero_notches_when_added()
    {
        var clock = new FakeClock();
        var wheel = new GraphWheelAccumulator(() => clock.Now);

        wheel.Add(0, new Point(1, 1));

        Assert.False(wheel.IsPending);
    }
}
