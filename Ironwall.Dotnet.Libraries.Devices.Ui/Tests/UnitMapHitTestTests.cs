using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-06 (FR-13 · FR-29 · NFR-11) — 월드 점 → 노드 id. 균등 격자 색인(256 월드).
/// 도형은 단계마다 <b>화면</b>에서 고정 크기라 월드 판정은 배율로 나눠 잰다. 시나리오: SIM-D013(ISSUE-14) · SIM-G 계열 누른 곳 판정.
/// </summary>
public class UnitMapHitTestTests
{
    private static UnitMapHitItem Item(int id, double x, double y, EnumUnitEchelon? echelon = EnumUnitEchelon.Company)
        => new(id, new Point(x, y), echelon);

    /// <summary>화면 오프셋(DIU)만큼 떨어진 월드 점.</summary>
    private static Point At(Point center, double scale, double screenDx, double screenDy)
        => new(center.X + screenDx / scale, center.Y + screenDy / scale);

    #region - 도형 안 · 밖 -
    [Theory]
    [InlineData(UnitMapLevel.L0, 0.2)]
    [InlineData(UnitMapLevel.L1, 0.5)]
    [InlineData(UnitMapLevel.L2, 1.0)]
    public void should_hit_node_when_point_inside_frame(UnitMapLevel level, double scale)
    {
        var hit = UnitMapHitTest.Build(new[] { Item(7, 1000, 480) }, level, scale);

        Assert.Equal(7, hit.HitTest(new Point(1000, 480)));
        Assert.Equal(7, hit.HitTest(At(new Point(1000, 480), scale, 3, -2)));
    }

    [Fact]
    public void should_return_null_when_point_on_empty_canvas()
    {
        var hit = UnitMapHitTest.Build(new[] { Item(1, 100, 0), Item(2, 300, 0) }, UnitMapLevel.L2, 1.0);

        Assert.Null(hit.HitTest(new Point(200, 0)));        // 두 카드 사이(칸 경계)
        Assert.Null(hit.HitTest(new Point(100, 200)));      // 아래
        Assert.Null(hit.HitTest(new Point(-5000, -5000)));  // 멀리 · 음수 격자
    }

    [Theory]
    [InlineData(65.5, 0, true)]
    [InlineData(66.0, 0, true)]      // 가장자리 포함
    [InlineData(66.5, 0, false)]
    [InlineData(-65.5, 27.5, true)]
    [InlineData(-66.5, 0, false)]
    [InlineData(0, 28.5, false)]
    [InlineData(0, -28.5, false)]
    public void should_respect_l2_card_edge_within_half_diu(double screenDx, double screenDy, bool expected)
    {
        var center = new Point(700, 320);
        var scale = 0.9;
        var hit = UnitMapHitTest.Build(new[] { Item(5, center.X, center.Y) }, UnitMapLevel.L2, scale);

        var result = hit.HitTest(At(center, scale, screenDx, screenDy));

        Assert.Equal(expected, result == 5);
    }

    [Theory]
    [InlineData(0, -19, true)]       // 표지 윗선
    [InlineData(0, -19.5, false)]
    [InlineData(0, 26, true)]        // 이름 줄 아래
    [InlineData(0, 26.5, false)]
    [InlineData(22, 0, true)]        // 틀 옆(★ · ▲ 자리)
    [InlineData(22.5, 0, false)]
    [InlineData(40, 20, true)]       // 이름 줄은 칸 폭 − 8 까지 넓다: 0.5 × 200 − 8 = 92 → ±46
    [InlineData(46.5, 20, false)]
    [InlineData(40, 0, false)]       // 이름 줄 폭은 틀 높이에는 없다
    public void should_hit_l1_frame_and_name_line_when_within_vertical_range_minus_19_to_plus_26(double screenDx, double screenDy, bool expected)
    {
        var center = new Point(500, 480);
        var hit = UnitMapHitTest.Build(new[] { Item(9, center.X, center.Y) }, UnitMapLevel.L1, 0.5);

        Assert.Equal(expected, hit.HitTest(At(center, 0.5, screenDx, screenDy)) == 9);
    }

    [Theory]
    [InlineData(EnumUnitEchelon.Division, 20, 13)]
    [InlineData(EnumUnitEchelon.Regiment, 17, 11)]
    [InlineData(EnumUnitEchelon.Battalion, 14, 9)]
    [InlineData(EnumUnitEchelon.Company, 12, 8)]
    [InlineData(EnumUnitEchelon.Outpost, 8, 6)]
    public void should_use_echelon_frame_size_plus_padding_when_l0(EnumUnitEchelon echelon, double width, double height)
    {
        // L0 틀은 크기가 제대를 말한다(SB S2) — 8×6 은 겨누기 어려워 사방 3 DIU 를 더한다(SB box()).
        var center = new Point(3000, 640);
        var scale = 0.2;
        var hit = UnitMapHitTest.Build(new[] { Item(3, center.X, center.Y, echelon) }, UnitMapLevel.L0, scale);
        var halfW = width / 2 + UnitMapHitTest.L0HitPadding;
        var halfH = height / 2 + UnitMapHitTest.L0HitPadding;

        Assert.Equal(new Size(width, height), UnitMapHitTest.L0FrameSize(echelon));
        Assert.Equal(3, hit.HitTest(At(center, scale, halfW - 0.25, 0)));
        Assert.Null(hit.HitTest(At(center, scale, halfW + 0.5, 0)));
        Assert.Equal(3, hit.HitTest(At(center, scale, 0, -(halfH - 0.25))));
        Assert.Null(hit.HitTest(At(center, scale, 0, -(halfH + 0.5))));
    }

    [Fact]
    public void should_use_company_frame_when_l0_echelon_unknown()
    {
        Assert.Equal(new Size(12, 8), UnitMapHitTest.L0FrameSize(null));
    }
    #endregion

    #region - 겹침 · 격자 -
    [Fact]
    public void should_pick_node_drawn_on_top_when_nodes_overlap()
    {
        // 사용자가 옮겨 겹친 두 노드(FR-21 "겹친 노드는 그대로 둔다") — 나중에 그린 것이 위
        var items = new[] { Item(1, 400, 400), Item(2, 420, 405) };
        var hit = UnitMapHitTest.Build(items, UnitMapLevel.L2, 1.0);

        Assert.Equal(2, hit.HitTest(new Point(410, 402)));
        Assert.Equal(1, hit.HitTest(new Point(350, 400)));   // 1 만 덮는 곳
        var reversed = UnitMapHitTest.Build(items.Reverse(), UnitMapLevel.L2, 1.0);
        Assert.Equal(1, reversed.HitTest(new Point(410, 402)));
    }

    [Fact]
    public void should_find_node_whose_box_crosses_grid_cell_boundary()
    {
        // 중심 x 250 은 칸 0(0~256), 카드 오른쪽 끝 316 은 칸 1 — 칸 1 의 점도 이 노드를 찾아야 한다
        var hit = UnitMapHitTest.Build(new[] { Item(4, 250, 250) }, UnitMapLevel.L2, 1.0);

        Assert.Equal(4, hit.HitTest(new Point(300, 270)));   // x 칸 1 · y 칸 1
        Assert.Equal(4, hit.HitTest(new Point(190, 230)));   // x 칸 0 · y 칸 0
    }

    [Fact]
    public void should_span_many_cells_when_scale_is_small()
    {
        // 배율 0.1 에서 사단 틀(20+6 DIU)은 260 월드 — 격자 칸보다 크다
        var center = new Point(3600, 0);
        var hit = UnitMapHitTest.Build(new[] { Item(1, center.X, center.Y, EnumUnitEchelon.Division) }, UnitMapLevel.L0, 0.1);

        Assert.Equal(1, hit.HitTest(At(center, 0.1, 12.5, 9)));
        Assert.Equal(1, hit.HitTest(At(center, 0.1, -12.5, -9)));
    }

    [Fact]
    public void should_index_all_nodes_when_built()
    {
        var fixture = UnitMapTestData.Standard200();
        var layout = UnitMapLayout.Compute(fixture.Tree);
        var items = fixture.Tree.Ordered.Select(n => new UnitMapHitItem(n.Id, layout.Positions[n.Id], n.Echelon)).ToList();

        var hit = UnitMapHitTest.Build(items, UnitMapLevel.L2, 0.8);

        Assert.Equal(200, hit.Count);
        Assert.Equal(UnitMapLevel.L2, hit.Level);
        Assert.Equal(0.8, hit.Scale);
        foreach (var item in items)
            Assert.Equal(item.UnitId, hit.HitTest(item.World));   // 자동 배치는 겹치지 않으므로 중심은 자기 자신
    }
    #endregion

    #region - 끌기 제외 -
    [Fact]
    public void should_exclude_dragged_unit_and_descendants_when_dragging()
    {
        // Arrange — 6중대(소초 4)를 끈다
        var fixture = UnitMapTestData.Standard200();
        var layout = UnitMapLayout.Compute(fixture.Tree);
        var items = fixture.Tree.Ordered.Select(n => new UnitMapHitItem(n.Id, layout.Positions[n.Id], n.Echelon));
        var hit = UnitMapHitTest.Build(items, UnitMapLevel.L2, 1.0);
        var dragged = fixture.IdOf("6중대");
        var outpost = fixture.IdOf("61소초");

        // Act
        var exclude = UnitMapHitTest.DragExclusion(fixture.Tree, dragged);

        // Assert — 자기 · 예하는 판정에서 빠진다(끌리는 사본 · 원래 자리 잔상 위 = 빈 곳)
        Assert.Equal(new HashSet<int>(fixture.Tree.DescendantIds(dragged)) { dragged }, exclude.ToHashSet());
        Assert.Null(hit.HitTest(layout.Positions[dragged], exclude));
        Assert.Null(hit.HitTest(layout.Positions[outpost], exclude));
        Assert.Equal(fixture.IdOf("7중대"), hit.HitTest(layout.Positions[fixture.IdOf("7중대")], exclude));
    }

    [Fact]
    public void should_fall_through_to_node_below_when_top_node_excluded()
    {
        var hit = UnitMapHitTest.Build(new[] { Item(1, 400, 400), Item(2, 410, 400) }, UnitMapLevel.L2, 1.0);

        Assert.Equal(1, hit.HitTest(new Point(405, 400), new HashSet<int> { 2 }));
    }

    [Fact]
    public void should_return_only_dragged_when_unit_not_in_tree()
    {
        var fixture = UnitMapTestData.Mixed();

        Assert.Equal(new[] { 999 }, UnitMapHitTest.DragExclusion(fixture.Tree, 999));
    }
    #endregion

    #region - 경계 상자 -
    [Fact]
    public void should_report_screen_bounds_per_level()
    {
        Assert.Equal(new Rect(-66, -28, 132, 56), UnitMapHitTest.NodeBounds(UnitMapLevel.L2, EnumUnitEchelon.Company, 1.0));
        Assert.Equal(new Rect(-46, -19, 92, 45), UnitMapHitTest.NodeBounds(UnitMapLevel.L1, EnumUnitEchelon.Company, 0.5));
        Assert.Equal(new Rect(-22, -19, 44, 45), UnitMapHitTest.NodeBounds(UnitMapLevel.L1, EnumUnitEchelon.Company, 0.2));
        Assert.Equal(new Rect(-13, -9.5, 26, 19), UnitMapHitTest.NodeBounds(UnitMapLevel.L0, EnumUnitEchelon.Division, 0.1));
    }
    #endregion
}
