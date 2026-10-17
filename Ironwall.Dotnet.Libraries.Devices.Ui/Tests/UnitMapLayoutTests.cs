using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-04 (FR-06 · FR-07 · FR-21 · NFR-11) — 자동 배치 · Δ 는 화면 없이 도는 순수 함수다.
/// 시나리오: SIM-L001~L056.
/// </summary>
public class UnitMapLayoutTests
{
    private const double W = UnitMapLayout.SlotWidth;
    private const double H = UnitMapLayout.LayerHeight;
    private const double P = UnitMapLayout.ColumnPitch;

    #region - 노드 도형(화면 DIU, 노드 중심 기준) — PRD §3.3 · SB S2 -
    private static Rect L0Frame(EnumUnitEchelon? echelon) => echelon switch
    {
        EnumUnitEchelon.Division => Centered(20, 13),
        EnumUnitEchelon.Regiment => Centered(17, 11),
        EnumUnitEchelon.Battalion => Centered(14, 9),
        EnumUnitEchelon.Company => Centered(12, 8),
        _ => Centered(8, 6),
    };

    /// <summary>L1 — 틀 30×20 + 표지 + 이름 줄: 세로 −19 ~ +26, 가로는 이름 폭(칸×배율 − 8)과 틀(±22) 중 큰 쪽.</summary>
    private static Rect L1Box(double scale)
    {
        var half = Math.Max(22, (W * scale - 8) / 2);
        return new Rect(-half, -19, half * 2, 45);
    }

    private static Rect L2Card => Centered(132, 56);

    private static Rect Centered(double width, double height) => new(-width / 2, -height / 2, width, height);

    private static Rect ScreenBox(Point world, double scale, Rect box)
        => new(world.X * scale + box.X, world.Y * scale + box.Y, box.Width, box.Height);

    private static bool Overlaps(Rect a, Rect b)
        => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
    #endregion

    #region - 자동 배치 -
    [Fact]
    public void should_place_leaf_children_in_one_column_when_all_children_are_leaves()
    {
        // Arrange — 중대 1 밑에 소초 3(코드 p013 · p011 · p012 가 id 순서와 다르다)
        var fixture = UnitMapTestData.LeafChildren();

        // Act
        var layout = UnitMapLayout.Compute(fixture.Tree);

        // Assert — 부모 아래 세로 한 줄, 간격 P, 첫 줄은 한 층 아래. 칸은 1개.
        var company = layout.Positions[fixture.IdOf("1중대")];
        Assert.Equal(new Point(W / 2, 0), company);
        Assert.Equal(new Point(W / 2, H), layout.Positions[fixture.IdOf("11소초")]);
        Assert.Equal(new Point(W / 2, H + P), layout.Positions[fixture.IdOf("12소초")]);
        Assert.Equal(new Point(W / 2, H + 2 * P), layout.Positions[fixture.IdOf("13소초")]);
        Assert.Equal(1, layout.SlotCount);
        Assert.True(layout.Slots[fixture.IdOf("11소초")].IsColumnChild);
        Assert.False(layout.Slots[fixture.IdOf("1중대")].IsColumnChild);
    }

    [Fact]
    public void should_center_parent_over_child_slots_when_mixed()
    {
        // 대대 밑에 c01(소초 2) · c02(끝 부대) · c03(소초 1) — 대대는 세로 줄이 아니라 칸 3개의 가운데
        var fixture = UnitMapTestData.Mixed();

        var layout = UnitMapLayout.Compute(fixture.Tree);

        Assert.Equal(3, layout.SlotCount);
        Assert.Equal(new Point(1.5 * W, 0), layout.Positions[fixture.IdOf("1대대")]);
        Assert.Equal(new Point(0.5 * W, H), layout.Positions[fixture.IdOf("1중대")]);
        Assert.Equal(new Point(1.5 * W, H), layout.Positions[fixture.IdOf("2중대")]);
        Assert.Equal(new Point(2.5 * W, H), layout.Positions[fixture.IdOf("3중대")]);
        Assert.Equal(new Point(0.5 * W, 2 * H), layout.Positions[fixture.IdOf("11소초")]);
        Assert.Equal(new Point(0.5 * W, 2 * H + P), layout.Positions[fixture.IdOf("12소초")]);
        Assert.Equal(new Point(2.5 * W, 2 * H), layout.Positions[fixture.IdOf("31소초")]);
        Assert.Equal(new UnitMapSlot(0, 3, false), layout.Slots[fixture.IdOf("1대대")]);
        Assert.Equal(new UnitMapSlot(W, 1, false), layout.Slots[fixture.IdOf("2중대")]);
    }

    [Fact]
    public void should_order_siblings_by_code_ascending()
    {
        var fixture = UnitMapTestData.Standard200();

        var layout = UnitMapLayout.Compute(fixture.Tree);

        // 1대대 밑의 중대 c0101 · c0102 · c0103 · c0104 는 왼→오
        var xs = new[] { "1중대", "2중대", "3중대", "4중대" }.Select(n => layout.Positions[fixture.IdOf(n)].X).ToList();
        Assert.Equal(xs.OrderBy(x => x), xs);
        Assert.Equal(new[] { 0.5 * W, 1.5 * W, 2.5 * W, 3.5 * W }, xs);
    }

    [Fact]
    public void should_lay_out_standard_200_units_in_36_slots()
    {
        var fixture = UnitMapTestData.Standard200();

        var layout = UnitMapLayout.Compute(fixture.Tree);

        Assert.Equal(200, fixture.Tree.Count);
        Assert.Equal(60, fixture.AdjacencyPairCount);
        Assert.Equal(200, layout.Positions.Count);
        Assert.Equal(36, layout.SlotCount);
        Assert.Equal(new Point(18 * W, 0), layout.Positions[fixture.IdOf("제○○사단")]);
        Assert.Equal(new Point(6 * W, H), layout.Positions[fixture.IdOf("1연대")]);
        Assert.Equal(new Point(2 * W, 2 * H), layout.Positions[fixture.IdOf("1대대")]);
        Assert.Equal(new Point(0.5 * W, 4 * H + 4 * P), layout.Positions[fixture.IdOf("15소초")]);   // 1중대의 다섯째 소초
        Assert.Equal(new Rect(0.5 * W, 0, 35 * W, 4 * H + 4 * P), layout.Bounds);
    }

    [Theory]
    [InlineData(UnitMapLevel.L0, 0.10)]    // L0 의 최소(가장 좁은) 배율
    [InlineData(UnitMapLevel.L1, 0.36)]    // L1 의 나가는 경계 — 줄 46.8 ≥ 노드 46
    [InlineData(UnitMapLevel.L2, 0.72)]    // L2 의 나가는 경계 — 칸 144 ≥ 카드 132
    [InlineData(UnitMapLevel.L2, 1.60)]
    public void should_not_overlap_nodes_at_exit_threshold_of_each_level_when_200_units(UnitMapLevel level, double scale)
    {
        // Arrange
        var fixture = UnitMapTestData.Standard200();
        var layout = UnitMapLayout.Compute(fixture.Tree);

        // Act — 단계마다 화면에서 고정된 도형을 배율로 벌어진 자리에 놓는다
        var boxes = layout.Positions
            .Select(kv =>
            {
                var box = level switch
                {
                    UnitMapLevel.L0 => L0Frame(fixture.Tree.Find(kv.Key)!.Echelon),
                    UnitMapLevel.L1 => L1Box(scale),
                    _ => L2Card,
                };
                return (Id: kv.Key, Box: ScreenBox(kv.Value, scale, box));
            })
            .ToList();

        // Assert — 겹침 0쌍(FR-21 · SIM-L001~L007)
        var overlaps = new List<(int, int)>();
        for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
                if (Overlaps(boxes[i].Box, boxes[j].Box)) overlaps.Add((boxes[i].Id, boxes[j].Id));
        Assert.Empty(overlaps);
    }

    [Fact]
    public void should_leave_spine_room_beside_l2_cards_when_exit_threshold()
    {
        // L2 @0.72: 칸 144 − 카드 132 = 12 — 척추(칸 왼쪽 + 4) 와 선 굵기가 들어갈 자리
        Assert.True(W * 0.72 - 132 >= 8);
        Assert.True(P * 0.36 >= 46);
    }

    [Fact]
    public void should_place_orphan_as_root_when_parent_outside_graph()
    {
        // c09 의 상위 999 는 응답에 없다 — 트리 빌더가 최상단으로 올린 그대로 뿌리 줄(y 0)에
        var fixture = UnitMapTestData.OrphanParent();

        var layout = UnitMapLayout.Compute(fixture.Tree);

        Assert.Equal(new Point(0.5 * W, 0), layout.Positions[fixture.IdOf("1대대")]);
        Assert.Equal(new Point(0.5 * W, H), layout.Positions[fixture.IdOf("1중대")]);
        Assert.Equal(new Point(1.5 * W, 0), layout.Positions[fixture.IdOf("9중대")]);
        Assert.Equal(2, layout.SlotCount);
    }

    [Fact]
    public void should_order_roots_like_unit_tree_when_several_roots()
    {
        // 뿌리 순서는 편제 트리(UnitTreeBuilder: 제대 순위 → 코드)와 같다 — 시나리오 ISSUE-48(SIM-L050).
        var fixture = UnitMapTestData.MultiRoot();

        var layout = UnitMapLayout.Compute(fixture.Tree);

        Assert.Equal(new Point(0.5 * W, 0), layout.Positions[fixture.IdOf("제1사단")]);     // d01
        Assert.Equal(new Point(1.5 * W, 0), layout.Positions[fixture.IdOf("제2사단")]);     // d02
        Assert.Equal(new Point(0.5 * W, H + P), layout.Positions[fixture.IdOf("12연대")]);
        var rootsInTreeOrder = fixture.Tree.Ordered.Where(n => n.ParentId is null).Select(n => n.Id).ToList();
        var rootsLeftToRight = rootsInTreeOrder.OrderBy(id => layout.Positions[id].X).ToList();
        Assert.Equal(rootsInTreeOrder, rootsLeftToRight);
    }

    [Fact]
    public void should_put_higher_echelon_root_first_when_orphan_code_sorts_earlier()
    {
        // SIM-L050 모양 — 고아 중대 c09 가 사단 d01 보다 코드가 앞서도 트리처럼 사단이 왼쪽
        var graph = UnitMapTestData.Graph(new[]
        {
            UnitMapTestData.Node(1, "d01", "제1사단", UnitMapTestData.ECHELON_DIVISION),
            UnitMapTestData.Node(2, "c09", "9중대", UnitMapTestData.ECHELON_COMPANY, 999),
            UnitMapTestData.Node(3, "unit001", "기본중대", UnitMapTestData.ECHELON_COMPANY),
        });

        var layout = UnitMapLayout.Compute(UnitMapTestData.Tree(graph));

        Assert.Equal(new[] { 1, 2, 3 }, layout.Positions.OrderBy(kv => kv.Value.X).Select(kv => kv.Key));
    }

    [Fact]
    public void should_be_deterministic_when_same_input()
    {
        var first = UnitMapLayout.Compute(UnitMapTestData.Standard200().Tree);
        var second = UnitMapLayout.Compute(UnitMapTestData.Standard200().Tree);

        // 노드 목록을 거꾸로 준 응답도 같은 그림
        var reversedGraph = UnitMapTestData.Standard200().Graph;
        var reversed = new UnitGraphDto
        {
            Nodes = Enumerable.Reverse(reversedGraph.Nodes).ToList(),
            Edges = new UnitGraphEdgesDto
            {
                Hierarchy = Enumerable.Reverse(reversedGraph.Edges.Hierarchy).ToList(),
                Adjacency = reversedGraph.Edges.Adjacency,
            },
        };
        var third = UnitMapLayout.Compute(UnitMapTestData.Tree(reversed));

        Assert.Equal(first.Positions.OrderBy(kv => kv.Key), second.Positions.OrderBy(kv => kv.Key));
        Assert.Equal(first.Positions.OrderBy(kv => kv.Key), third.Positions.OrderBy(kv => kv.Key));
    }

    [Fact]
    public void should_fit_200_units_within_min_scale_when_canvas_756()
    {
        // PRD §3.3 — 콘솔 캔버스 폭 1280 − 184 − 340 = 756. 전체 보기가 최소 배율 안에 들고 도형까지 여백 16 안.
        var fixture = UnitMapTestData.Standard200();
        var layout = UnitMapLayout.Compute(fixture.Tree);
        var widestL0 = L0Frame(EnumUnitEchelon.Division);

        var fitted = GraphViewport.Fit(layout.Bounds, new Size(756, 560), nodeBox: widestL0);

        Assert.True(fitted.Scale >= GraphViewport.MinScale);
        foreach (var (id, world) in layout.Positions)
        {
            var screen = fitted.WorldToScreen(world);
            var frame = L0Frame(fixture.Tree.Find(id)!.Echelon);
            Assert.InRange(screen.X + frame.Left, 16 - 1e-6, 756 - 16 + 1e-6);
            Assert.InRange(screen.X + frame.Right, 16 - 1e-6, 756 - 16 + 1e-6);
        }
    }

    [Fact]
    public void should_return_empty_layout_when_tree_is_empty()
    {
        var layout = UnitMapLayout.Compute(Consoles.Units.Model.UnitTreeModel.Empty);

        Assert.Empty(layout.Positions);
        Assert.Equal(0, layout.SlotCount);
        Assert.True(layout.Bounds.IsEmpty);
    }
    #endregion

    #region - Δ -
    [Fact]
    public void should_move_subtree_when_parent_has_delta()
    {
        // Arrange — 2대대 Δ(40, 0) · 6중대 Δ(0, 10)
        var fixture = UnitMapTestData.Standard200();
        var battalion = fixture.IdOf("2대대");
        var company = fixture.IdOf("6중대");
        var deltas = new Dictionary<int, Vector> { [battalion] = new(40, 0), [company] = new(0, 10) };

        // Act
        var auto = UnitMapLayout.Compute(fixture.Tree);
        var moved = UnitMapLayout.Compute(fixture.Tree, deltas);

        // Assert — 조상 Δ 합 + 자기 Δ(SIM-L051). 2대대 밖은 그대로.
        var subtree = new HashSet<int>(fixture.Tree.DescendantIds(battalion)) { battalion };
        var companySubtree = new HashSet<int>(fixture.Tree.DescendantIds(company)) { company };
        foreach (var (id, point) in moved.Positions)
        {
            var expected = auto.Positions[id];
            if (subtree.Contains(id)) expected += new Vector(40, 0);
            if (companySubtree.Contains(id)) expected += new Vector(0, 10);
            Assert.Equal(expected, point);
        }
        Assert.Equal(auto.Positions, moved.AutoPositions);
        Assert.False(moved.IsVersionMismatch);
    }

    [Fact]
    public void should_ignore_delta_when_unit_not_in_graph()
    {
        var fixture = UnitMapTestData.Mixed();
        var deltas = new Dictionary<int, Vector> { [999] = new(500, 500) };

        var layout = UnitMapLayout.Compute(fixture.Tree, deltas);

        Assert.Equal(layout.AutoPositions, layout.Positions);   // SIM-L052
        Assert.False(layout.IsVersionMismatch);
    }

    [Fact]
    public void should_not_apply_deltas_when_layout_version_differs()
    {
        var fixture = UnitMapTestData.Mixed();
        var deltas = new Dictionary<int, Vector> { [fixture.IdOf("1중대")] = new(30, 0) };

        var layout = UnitMapLayout.Compute(fixture.Tree, deltas, deltaLayoutVersion: 2);

        Assert.Equal(layout.AutoPositions, layout.Positions);   // SIM-L053 — 판이 다르면 Δ 의 기준이 다르다
        Assert.True(layout.IsVersionMismatch);
    }

    [Fact]
    public void should_flag_version_mismatch_when_document_version_differs_without_deltas()
    {
        var layout = UnitMapLayout.Compute(UnitMapTestData.Mixed().Tree, null, deltaLayoutVersion: 2);

        Assert.True(layout.IsVersionMismatch);                  // 문서의 판이 다르다는 사실은 Δ 가 없어도 알린다
        Assert.False(UnitMapLayout.Compute(UnitMapTestData.Mixed().Tree).IsVersionMismatch);
    }

    [Fact]
    public void should_include_moved_positions_in_bounds_when_delta_applied()
    {
        var fixture = UnitMapTestData.LeafChildren();
        var deltas = new Dictionary<int, Vector> { [fixture.IdOf("13소초")] = new(-400, 50) };

        var layout = UnitMapLayout.Compute(fixture.Tree, deltas);

        Assert.Equal(W / 2 - 400, layout.Bounds.Left);
        Assert.Equal(H + 2 * P + 50, layout.Bounds.Bottom);
    }

    // 골든(모든 좌표 · layout_version 1)은 UnitMapLayoutGoldenTests 가 잠근다(TEST-67).


    [Fact]
    public void should_report_layout_version_1()
    {
        Assert.Equal(1, UnitMapLayout.LayoutVersion);
        Assert.Equal((200.0, 160.0, 130.0), (UnitMapLayout.SlotWidth, UnitMapLayout.LayerHeight, UnitMapLayout.ColumnPitch));
    }
    #endregion
}
