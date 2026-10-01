using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>두 줄 개념도 배치 · 그림(fence-wiring-editor v0.3 FR-20 · 참고 그림 4장) — 순수 함수.</summary>
public class ConceptLanesLayoutTests
{
    /// <summary>아래 줄 n 대(6m 간격 · 기둥 위) + 위 줄 m 대(같은 기둥).</summary>
    private static (List<ConceptLaneItem> Items, List<double> Posts, double Length) Fence(int lower, int upper, double span = 6)
    {
        var columns = System.Math.Max(lower, upper);
        var items = Enumerable.Range(0, lower).Select(i => new ConceptLaneItem(1 + i, FenceLane.Lower, i * span))
            .Concat(Enumerable.Range(0, upper).Select(i => new ConceptLaneItem(101 + i, FenceLane.Upper, i * span)))
            .ToList();
        return (items, Enumerable.Range(0, columns).Select(i => i * span).ToList(), (columns - 1) * span);
    }

    private static List<ConceptNodeInfo> Infos(IEnumerable<ConceptLaneItem> items)
        => items.Select((it, i) => new ConceptNodeInfo(it.Key, it.Key, i + 1, it.Key, $"#{it.Key}", false, string.Empty, SignalLevel.Unknown, false, false, false)).ToList();

    [Fact]
    public void should_draw_reference_one_with_two_lanes_full_chips_and_every_tick_when_six_plus_six_sensors_sit_left_of_the_controller()
    {
        // Arrange — 그림 ①: 아래 1…6 · 위 101…106 · C 왼쪽
        var (items, posts, length) = Fence(6, 6);

        // Act
        var g = ConceptLayout.Build(items, posts, length, FenceControllerEnd.Left, new Size(600, 150));

        // Assert
        Assert.Equal(ConceptChipMode.Full, g.Mode);
        Assert.All(g.Nodes.Where(n => n.Lane == FenceLane.Upper), n => Assert.Equal(g.UpperY, n.Center.Y));
        Assert.All(g.Nodes.Where(n => n.Lane == FenceLane.Lower), n => Assert.Equal(g.LowerY, n.Center.Y));
        Assert.True(g.UpperY < g.LowerY);
        Assert.True(g.Controller.Right < g.FenceLeft);                                          // C 는 펜스 왼쪽 바깥
        Assert.Equal(g.FenceRight, g.FarX);                                                      // 꺾임선은 오른쪽 끝
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, g.Ticks.Select(t => t.Position));
        Assert.All(g.Nodes, n => Assert.True(n.ShowLabel));
        Assert.All(g.Nodes, n => Assert.InRange(n.Center.X, 0, 600));
    }

    [Fact]
    public void should_anchor_the_controller_at_the_right_and_still_count_ticks_from_the_left_when_the_controller_is_right()
    {
        var (items, posts, length) = Fence(22, 0);

        var g = ConceptLayout.Build(items, posts, length, FenceControllerEnd.Right, new Size(1100, 150));

        Assert.True(g.Controller.Left > g.FenceRight);
        Assert.Equal(g.FenceLeft, g.FarX);
        Assert.Equal(1, g.Ticks[0].Position);
        Assert.True(g.Ticks[0].X < g.Ticks[^1].X);                                               // 왼쪽부터 1(그림 ④)
        Assert.Equal(new[] { 1, 6, 11, 16, 21 }, g.Ticks.Select(t => t.Position));              // 22대 — 5칸마다(그림 ③ · ④)
    }

    [Fact]
    public void should_keep_chips_and_thin_labels_and_ticks_when_sixty_five_sensors_share_one_lane()
    {
        var (items, posts, length) = Fence(65, 0);

        var g = ConceptLayout.Build(items, posts, length, FenceControllerEnd.Left, new Size(1320, 150));

        Assert.Equal(65, g.Nodes.Count);
        Assert.NotEqual(ConceptChipMode.Full, g.Mode);
        Assert.Contains(g.Nodes, n => !n.ShowLabel);
        Assert.Equal(new[] { 1, 11, 21, 31, 41, 51, 61 }, g.Ticks.Select(t => t.Position));      // 그림 ②
        Assert.All(g.Nodes, n => Assert.InRange(n.Center.X, 0, 1320));                           // 가로로 잘리지 않는다
    }

    [Fact]
    public void should_draw_dot_chips_without_labels_when_two_hundred_fifty_sensors_are_squeezed()
    {
        var (items, posts, length) = Fence(250, 0, span: 3);

        var g = ConceptLayout.Build(items, posts, length, FenceControllerEnd.Left, new Size(1200, 150));
        var shapes = ConceptScene.Background(g, Infos(items));

        Assert.Equal(ConceptChipMode.Dot, g.Mode);
        Assert.DoesNotContain(shapes, s => s.Ink == FenceInk.ConceptLabelLower);
        Assert.All(g.Nodes, n => Assert.InRange(n.Center.X, 0, 1200));
    }

    [Fact]
    public void should_draw_ch1_solid_to_the_far_end_and_ch2_solid_on_the_upper_sensors_then_dashed_to_the_controller_when_both_lanes_have_sensors()
    {
        var (items, posts, length) = Fence(6, 6);
        var g = ConceptLayout.Build(items, posts, length, FenceControllerEnd.Left, new Size(600, 150));

        var shapes = ConceptScene.Background(g, Infos(items));

        var ch1 = Assert.Single(shapes, s => s.Ink == FenceInk.ConceptCh1);
        Assert.Equal(g.FarX, ch1.Points[^1].X);
        var turn = shapes.Single(s => s.Ink == FenceInk.ConceptCh2 && s.Points[0].X == g.FarX && s.Points[1].X == g.FarX);
        Assert.Equal(g.UpperY, turn.Points[1].Y);
        var dashed = Assert.Single(shapes, s => s.Ink == FenceInk.ConceptCh2Dash);
        Assert.Equal(g.Port2, dashed.Points[^1]);                                                // 점선은 C 위 포트로 들어간다
        Assert.Equal(g.Nodes.Where(n => n.Lane == FenceLane.Upper).Min(n => n.Center.X), dashed.Points[0].X);
    }

    [Fact]
    public void should_put_the_vbus_between_the_middle_sensors_and_snap_a_drag_to_the_nearest_gap()
    {
        var (items, posts, length) = Fence(6, 0);
        var g = ConceptLayout.Build(items, posts, length, FenceControllerEnd.Left, new Size(600, 150));
        var chain = items.Select(i => i.Key).ToList();

        var at = ConceptLayout.VbusPoint(g, chain, 3);
        var snapped = ConceptLayout.VbusGapAt(g, chain, new Point(g.Nodes.Single(n => n.Key == 5).Center.X + 5, g.LowerY));

        Assert.Equal((g.Nodes.Single(n => n.Key == 3).Center.X + g.Nodes.Single(n => n.Key == 4).Center.X) / 2, at.X, 3);
        Assert.Equal(5, snapped);
    }

    [Fact]
    public void should_pick_the_lane_and_gap_under_the_pointer_when_dragging_between_lanes()
    {
        var (items, posts, length) = Fence(4, 4);
        var g = ConceptLayout.Build(items, posts, length, FenceControllerEnd.Left, new Size(600, 150));
        var x = (g.Nodes.Single(n => n.Key == 102).Center.X + g.Nodes.Single(n => n.Key == 103).Center.X) / 2;

        var lane = ConceptLayout.LaneAt(g, new Point(x, g.UpperY + 3));
        var index = ConceptLayout.IndexAt(g, lane, new Point(x, g.UpperY + 3), new[] { 2 });

        Assert.Equal(FenceLane.Upper, lane);
        Assert.Equal(2, index);                                                                   // 101 · 102 다음
    }
}
