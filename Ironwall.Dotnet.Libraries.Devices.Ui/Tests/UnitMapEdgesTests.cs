using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-07 — 관계도 선 기하(FR-24 · FR-25 · FR-27). 좌표는 캔버스 좌표(월드 × 배율).
/// 계층 꺾은선(부모 아래 → 가운데 높이 → 자식 위) · 세로 한 줄 묶음의 척추(칸 왼쪽 + 4) + 가지 ·
/// 같은 줄 인접 = 위로 휜 호(휨 12~38) · 다른 줄 = 수직 이등분선 쪽 곡선 · 인접 쌍 1회 · 선택 강조 · Δ 반영.
/// </summary>
public class UnitMapEdgesTests
{
    private const double Tolerance = 1e-9;

    #region - Fixtures -
    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null)
        => new() { Id = id, Code = code, Name = code.ToUpperInvariant(), EchelonRaw = echelon, ParentId = parentId };

    /// <summary>
    /// 사단 1 → 연대 2 · 연대 3(끝) / 연대 2 → 대대 4 · 대대 5(끝) / 대대 4 → 중대 6 · 중대 7(끝 — 세로 한 줄 묶음).
    /// 인접: 6–7(서버가 두 방향으로 줘도 한 번) · 4–5 · 2–3.
    /// </summary>
    private static UnitTreeModel Tree() => UnitTreeBuilder.Build(new UnitGraphDto
    {
        Nodes = new List<UnitListDto>
        {
            Node(1, "d01", "Division"),
            Node(2, "r01", "Regiment", 1),
            Node(3, "r02", "Regiment", 1),
            Node(4, "b01", "Battalion", 2),
            Node(5, "b02", "Battalion", 2),
            Node(6, "c01", "Company", 4),
            Node(7, "c02", "Company", 4),
        },
        Edges = new UnitGraphEdgesDto
        {
            Hierarchy = new() { new() { 1, 2 }, new() { 1, 3 }, new() { 2, 4 }, new() { 2, 5 }, new() { 4, 6 }, new() { 4, 7 } },
            Adjacency = new() { new() { 6, 7 }, new() { 7, 6 }, new() { 4, 5 }, new() { 2, 3 } },
        },
    });

    /// <summary>자동 배치와 같은 모양의 월드 위치(칸 200 · 층 160 · 줄 130).</summary>
    private static Dictionary<int, Point> World() => new()
    {
        [1] = new Point(300, 0),
        [2] = new Point(200, 160),
        [3] = new Point(500, 160),
        [4] = new Point(100, 320),
        [5] = new Point(300, 320),
        [6] = new Point(100, 480),
        [7] = new Point(100, 610),
    };

    private static UnitMapEdge Hierarchy(IEnumerable<UnitMapEdge> edges, int parent, int child)
        => edges.Single(e => e.Kind == UnitMapEdgeKind.Hierarchy && e.FromId == parent && e.ToId == child);

    private static void AssertPoints(IReadOnlyList<Point> actual, params (double X, double Y)[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs(actual[i].X - expected[i].X) < 1e-6 && Math.Abs(actual[i].Y - expected[i].Y) < 1e-6,
                $"점 {i}: 기대 ({expected[i].X}, {expected[i].Y}) · 실제 ({actual[i].X}, {actual[i].Y})");
        }
    }
    #endregion

    #region - 닻 -
    [Fact]
    public void should_use_fixed_screen_anchors_when_level_is_L1_or_L2()
    {
        Assert.Equal(new UnitMapAnchor(28, 28, 66), UnitMapEdges.AnchorOf(UnitMapLevel.L2, EnumUnitEchelon.Company));
        Assert.Equal(new UnitMapAnchor(21, 28, 15), UnitMapEdges.AnchorOf(UnitMapLevel.L1, EnumUnitEchelon.Company));
    }

    [Theory]
    [InlineData(EnumUnitEchelon.Division, 7.5, 7.5, 10)]
    [InlineData(EnumUnitEchelon.Outpost, 4, 4, 4)]
    public void should_size_L0_anchor_by_the_echelon_frame_when_level_is_L0(EnumUnitEchelon echelon, double top, double bottom, double left)
    {
        Assert.Equal(new UnitMapAnchor(top, bottom, left), UnitMapEdges.AnchorOf(UnitMapLevel.L0, echelon));
    }
    #endregion

    #region - 계층선 -
    [Fact]
    public void should_draw_elbow_from_parent_bottom_to_child_top_when_child_is_not_in_a_column()
    {
        var edges = UnitMapEdges.Build(Tree(), World(), 1.0, UnitMapLevel.L2);

        // 연대 2 (200,160) → 대대 5 (300,320). L2 닻 위 28 · 아래 28 → 188 ~ 292, 가운데 240.
        AssertPoints(Hierarchy(edges, 2, 5).Points, (200, 188), (200, 240), (300, 240), (300, 292));
        Assert.False(Hierarchy(edges, 2, 5).IsCurve);
    }

    [Fact]
    public void should_run_the_spine_at_cell_left_plus_4_when_children_are_all_leaves()
    {
        var edges = UnitMapEdges.Build(Tree(), World(), 0.5, UnitMapLevel.L1);

        // 대대 4 (100,320)×0.5 = (50,160). 칸 왼쪽 = 50 − 200/2×0.5 = 0 → 척추 x = 4.
        // 첫 자식 위 = 160 + 160×0.5 − 21 = 219, 부모 아래 = 160 + 28 = 188, 가운데 203.5.
        AssertPoints(Hierarchy(edges, 4, 6).Points, (50, 188), (50, 203.5), (4, 203.5), (4, 240), (35, 240));
        AssertPoints(Hierarchy(edges, 4, 7).Points, (50, 188), (50, 203.5), (4, 203.5), (4, 305), (35, 305));
    }

    [Fact]
    public void should_not_use_a_spine_when_a_parent_has_a_child_with_children()
    {
        var edges = UnitMapEdges.Build(Tree(), World(), 1.0, UnitMapLevel.L2);

        // 연대 2 의 자식 중 대대 4 가 자식을 가진다 → 세로 한 줄 묶음이 아니다(꺾은선 4점).
        Assert.Equal(4, Hierarchy(edges, 2, 4).Points.Count);
        Assert.Equal(6, edges.Count(e => e.Kind == UnitMapEdgeKind.Hierarchy));
    }
    #endregion

    #region - 인접선 -
    [Theory]
    [InlineData(20, 12)]      // 20 × 0.18 = 3.6 → 아래 끝 12
    [InlineData(100, 18)]     // 100 × 0.18 = 18
    [InlineData(1000, 38)]    // 180 → 위 끝 38
    public void should_clamp_the_bow_between_12_and_38_when_distance_varies(double distance, double bow)
    {
        Assert.Equal(bow, UnitMapEdges.AdjacencyBow(distance), 6);
    }

    [Fact]
    public void should_bow_the_arc_upward_when_both_units_sit_on_the_same_row()
    {
        var edges = UnitMapEdges.Build(Tree(), World(), 1.0, UnitMapLevel.L2);
        var arc = edges.Single(e => e.Kind == UnitMapEdgeKind.Adjacency && e.FromId == 4 && e.ToId == 5);

        // 대대 4 (100,320) · 대대 5 (300,320). 닻 위 28 + 2 → y0 = 290, 거리 200 → 휨 36.
        Assert.True(arc.IsCurve);
        AssertPoints(arc.Points, (100, 290), (200, 254), (300, 290));
    }

    [Fact]
    public void should_bend_toward_the_perpendicular_bisector_when_rows_differ()
    {
        var world = World();
        world[7] = new Point(260, 700);                      // 옮긴 부대 — 같은 줄이 아니다
        var edges = UnitMapEdges.Build(Tree(), world, 1.0, UnitMapLevel.L2);
        var arc = edges.Single(e => e.Kind == UnitMapEdgeKind.Adjacency && e.FromId == 6 && e.ToId == 7);

        var a = arc.Points[0];
        var control = arc.Points[1];
        var b = arc.Points[2];
        Assert.True(arc.IsCurve);
        Assert.Equal((control - a).Length, (control - b).Length, 6);            // 이등분선 위
        var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        Assert.InRange((control - mid).Length, 12 - 1e-6, 38 + 1e-6);          // 같은 휨 규칙
    }

    [Fact]
    public void should_emit_each_adjacency_pair_once_as_low_then_high_when_server_sends_both_directions()
    {
        var edges = UnitMapEdges.Build(Tree(), World(), 1.0, UnitMapLevel.L2);
        var adjacency = edges.Where(e => e.Kind == UnitMapEdgeKind.Adjacency).Select(e => (e.FromId, e.ToId)).ToList();

        Assert.Equal(new[] { (2, 3), (4, 5), (6, 7) }, adjacency.OrderBy(p => p.FromId));
    }
    #endregion

    #region - 선택 강조 · Δ · 빠진 위치 -
    [Fact]
    public void should_emphasize_only_edges_touching_the_selected_unit_when_a_unit_is_selected()
    {
        var edges = UnitMapEdges.Build(Tree(), World(), 1.0, UnitMapLevel.L2, selectedId: 4);

        var emphasized = edges.Where(e => e.IsEmphasized).Select(e => (e.Kind, e.FromId, e.ToId)).OrderBy(x => x.Kind).ThenBy(x => x.FromId).ThenBy(x => x.ToId).ToList();
        Assert.Equal(new[]
        {
            (UnitMapEdgeKind.Hierarchy, 2, 4),
            (UnitMapEdgeKind.Hierarchy, 4, 6),
            (UnitMapEdgeKind.Hierarchy, 4, 7),
            (UnitMapEdgeKind.Adjacency, 4, 5),
        }, emphasized);
    }

    [Fact]
    public void should_emphasize_nothing_when_no_unit_is_selected()
    {
        Assert.DoesNotContain(UnitMapEdges.Build(Tree(), World(), 1.0, UnitMapLevel.L2), e => e.IsEmphasized);
    }

    [Fact]
    public void should_follow_the_delta_when_positions_already_carry_it()
    {
        const double scale = 0.8;
        var before = UnitMapEdges.Build(Tree(), World(), scale, UnitMapLevel.L2);

        // 대대 4 에 Δ(−50, 30) — 예하 6 · 7 도 같은 만큼(레인 A 의 배치가 입힌 결과를 흉내)
        var moved = World();
        foreach (var id in new[] { 4, 6, 7 }) moved[id] = moved[id] + new Vector(-50, 30);
        var after = UnitMapEdges.Build(Tree(), moved, scale, UnitMapLevel.L2);

        var shift = new Vector(-50 * scale, 30 * scale);
        var inside = Hierarchy(after, 4, 6).Points.Zip(Hierarchy(before, 4, 6).Points, (a, b) => a - b).ToList();
        Assert.All(inside, d => Assert.True((d - shift).Length < 1e-6, $"예하 선이 Δ 를 따라가지 않았다: {d}"));

        var into = Hierarchy(after, 2, 4).Points;          // 부모 2 는 제자리 → 시작점은 그대로, 끝점만 Δ
        Assert.Equal(Hierarchy(before, 2, 4).Points[0], into[0]);
        Assert.True((into[^1] - Hierarchy(before, 2, 4).Points[^1] - shift).Length < 1e-6);
    }

    [Fact]
    public void should_skip_edges_when_an_endpoint_has_no_position()
    {
        var world = World();
        world.Remove(7);

        var edges = UnitMapEdges.Build(Tree(), world, 1.0, UnitMapLevel.L2);

        Assert.DoesNotContain(edges, e => e.FromId == 7 || e.ToId == 7);
        Assert.Contains(edges, e => e.Kind == UnitMapEdgeKind.Hierarchy && e.FromId == 4 && e.ToId == 6);
    }

    [Fact]
    public void should_return_no_edges_when_tree_is_empty()
    {
        Assert.Empty(UnitMapEdges.Build(UnitTreeModel.Empty, new Dictionary<int, Point>(), 1.0, UnitMapLevel.L1));
    }
    #endregion
}
