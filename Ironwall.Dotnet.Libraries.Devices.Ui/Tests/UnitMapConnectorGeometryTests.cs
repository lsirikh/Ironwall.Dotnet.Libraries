using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 부대 관계도 계층선 기하 불변식 — 1:1 곧은 선 · 선이 노드를 지나지 않음 · 닻에 붙음 (2026-09-28 결함 재발 방지)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 자동 배치(<see cref="UnitMapLayout"/>) + 선(<see cref="UnitMapEdges"/>)을 여러 편제 모양 · 단계 · 배율로 돌려 기하 불변식을 잠근다.
/// </summary>
/// <remarks>
/// <para>결함(실앱 · 2026-09-28): 대대 1 → 중대 1 에서 선이 "아래 → 왼쪽 → 아래 → 오른쪽" 갈고리로 중대의 <b>왼쪽 옆</b>에 붙었다 —
/// 끝 부대 자식이 하나뿐이어도 세로 한 줄 묶음(척추)으로 그렸기 때문이다.</para>
/// <para>노드 사각형은 닻(<see cref="UnitMapEdges.AnchorOf"/>)으로 만든다 — 단계마다 화면에서 고정인 크기(FR-21)이고 선이 붙는 자리다.</para>
/// </remarks>
public class UnitMapConnectorGeometryTests
{
    private const double Eps = 1e-6;

    #region - 편제 모양 -
    private sealed class Org
    {
        private readonly List<UnitListDto> _nodes = new();
        private int _next = 1;

        public int Add(string name, string echelon, int? parent = null)
        {
            var id = _next++;
            _nodes.Add(UnitMapTestData.Node(id, $"u{id:000}", name, echelon, parent));
            return id;
        }

        public UnitTreeModel Tree() => UnitMapTestData.Tree(UnitMapTestData.Graph(_nodes));
    }

    /// <summary>시험 서버 실데이터 모양 — 대대(뿌리) 1 → 중대 1.</summary>
    private static UnitTreeModel BattalionWithOneCompany()
    {
        var o = new Org();
        o.Add("기본 부대", "Company", o.Add("대대", "Battalion"));
        return o.Tree();
    }

    private static UnitTreeModel Shape(string name)
    {
        var o = new Org();
        switch (name)
        {
            case "server-1to1":
                return BattalionWithOneCompany();
            case "chain-1to1":
            {
                var d = o.Add("1사단", "Division");
                var r = o.Add("1연대", "Regiment", d);
                var b = o.Add("1대대", "Battalion", r);
                o.Add("1중대", "Company", b);
                break;
            }
            case "mixed-1-and-n":
            {
                var r = o.Add("1연대", "Regiment");
                o.Add("1중대", "Company", o.Add("1대대", "Battalion", r));
                var b2 = o.Add("2대대", "Battalion", r);
                for (var i = 2; i <= 5; i++) o.Add($"{i}중대", "Company", b2);
                var b3 = o.Add("3대대", "Battalion", r);
                for (var i = 6; i <= 8; i++) o.Add($"{i}중대", "Company", b3);
                break;
            }
            case "mixed-depth":
            {
                var r = o.Add("1연대", "Regiment");
                var c1 = o.Add("1중대", "Company", o.Add("1대대", "Battalion", r));
                for (var i = 1; i <= 3; i++) o.Add($"1{i}소초", "Outpost", c1);
                var b2 = o.Add("2대대", "Battalion", r);
                o.Add("2중대", "Company", b2);
                var c3 = o.Add("3중대", "Company", b2);
                o.Add("31소초", "Outpost", c3);
                o.Add("32소초", "Outpost", c3);
                break;
            }
            case "fanout-8-wide":
            {
                var b = o.Add("1대대", "Battalion");
                for (var i = 1; i <= 8; i++) o.Add($"{i}1소초", "Outpost", o.Add($"{i}중대", "Company", b));
                break;
            }
            case "fanout-8-leaves":
            {
                var b = o.Add("1대대", "Battalion");
                for (var i = 1; i <= 8; i++) o.Add($"{i}중대", "Company", b);
                break;
            }
            case "deep-chain":
            {
                var d = o.Add("1사단", "Division");
                var c = o.Add("1중대", "Company", o.Add("1대대", "Battalion", o.Add("1연대", "Regiment", d)));
                o.Add("11소초", "Outpost", c);
                break;
            }
            case "unbalanced":
            {
                var d = o.Add("1사단", "Division");
                var b1 = o.Add("1대대", "Battalion", o.Add("1연대", "Regiment", d));
                var c1 = o.Add("1중대", "Company", b1);
                o.Add("11소초", "Outpost", c1);
                o.Add("12소초", "Outpost", c1);
                o.Add("21소초", "Outpost", o.Add("2중대", "Company", b1));
                o.Add("2연대", "Regiment", d);
                o.Add("3대대", "Battalion", o.Add("3연대", "Regiment", d));
                break;
            }
            case "skipped-echelon":
            {
                var d = o.Add("1사단", "Division");
                o.Add("직1소초", "Outpost", o.Add("직할중대", "Company", d));
                var b = o.Add("1대대", "Battalion", o.Add("1연대", "Regiment", d));
                o.Add("1중대", "Company", b);
                o.Add("2중대", "Company", b);
                break;
            }
            case "forest":
            {
                o.Add("1중대", "Company", o.Add("1대대", "Battalion"));
                var b2 = o.Add("2대대", "Battalion");
                o.Add("2중대", "Company", b2);
                o.Add("3중대", "Company", b2);
                o.Add("독립중대", "Company");
                break;
            }
            case "single":
                o.Add("1대대", "Battalion");
                break;
            case "standard200":
                return UnitMapTestData.Standard200().Tree;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
        return o.Tree();
    }

    public static IEnumerable<object[]> ShapesAndViews()
    {
        var shapes = new[]
        {
            "server-1to1", "chain-1to1", "mixed-1-and-n", "mixed-depth", "fanout-8-wide", "fanout-8-leaves",
            "deep-chain", "unbalanced", "skipped-echelon", "forest", "single", "standard200",
        };
        // 각 단계의 나가는 경계 · 가운데 · 최대 배율(FR-21 과 같은 경계들).
        var views = new (UnitMapLevel Level, double Scale)[]
        {
            (UnitMapLevel.L0, 0.10), (UnitMapLevel.L0, 0.30), (UnitMapLevel.L1, 0.36), (UnitMapLevel.L1, 0.50),
            (UnitMapLevel.L1, 0.79), (UnitMapLevel.L2, 0.72), (UnitMapLevel.L2, 1.00), (UnitMapLevel.L2, 1.60),
        };
        foreach (var shape in shapes)
            foreach (var (level, scale) in views)
                yield return new object[] { shape, level, scale };
    }
    #endregion

    #region - 기하 도움 -
    private static Point Screen(Point world, double scale) => new(world.X * scale, world.Y * scale);

    /// <summary>노드 사각형(캔버스 좌표) — 닻이 곧 선이 붙는 가장자리다.</summary>
    private static Rect NodeRect(UnitTreeModel tree, int id, IReadOnlyDictionary<int, Point> world, double scale, UnitMapLevel level)
    {
        var c = Screen(world[id], scale);
        var a = UnitMapEdges.AnchorOf(level, tree.Find(id)!.Echelon);
        return new Rect(c.X - a.Left, c.Y - a.Top, 2 * a.Left, a.Top + a.Bottom);
    }

    /// <summary>축 정렬 선분이 사각형 <b>안쪽</b>(가장자리 제외)을 지나는가.</summary>
    private static bool CutsThrough(Point p, Point q, Rect r)
    {
        const double inset = 0.5;
        double left = r.Left + inset, right = r.Right - inset, top = r.Top + inset, bottom = r.Bottom - inset;
        if (left >= right || top >= bottom) return false;
        if (Math.Abs(p.Y - q.Y) < Eps)
        {
            var (x1, x2) = (Math.Min(p.X, q.X), Math.Max(p.X, q.X));
            return p.Y > top && p.Y < bottom && x2 > left && x1 < right;
        }
        var (y1, y2) = (Math.Min(p.Y, q.Y), Math.Max(p.Y, q.Y));
        return p.X > left && p.X < right && y2 > top && y1 < bottom;
    }

    private static IReadOnlyList<UnitMapEdge> HierarchyEdges(UnitTreeModel tree, IReadOnlyDictionary<int, Point> world, double scale, UnitMapLevel level)
        => UnitMapEdges.Build(tree, world, scale, level, null, UnitMapLayout.SlotWidth, UnitMapLayout.LayerHeight)
                       .Where(e => e.Kind == UnitMapEdgeKind.Hierarchy).ToList();

    private static bool IsComb(UnitTreeModel tree, int parentId)
    {
        var parent = tree.Find(parentId)!;
        return parent.ChildIds.Count >= 2 && UnitMapEdges.IsColumnParent(tree, parent);
    }
    #endregion

    #region - 1:1 -
    [Theory]
    [InlineData(UnitMapLevel.L0, 0.30)]
    [InlineData(UnitMapLevel.L1, 0.50)]
    [InlineData(UnitMapLevel.L2, 0.93)]
    [InlineData(UnitMapLevel.L2, 1.60)]
    public void should_draw_one_straight_vertical_line_from_parent_bottom_center_to_child_top_center_when_a_battalion_has_one_company(UnitMapLevel level, double scale)
    {
        // Arrange — 실앱 화면과 같은 편제(대대 → 기본 부대)
        var tree = BattalionWithOneCompany();
        var world = UnitMapLayout.Compute(tree).Positions;
        int battalion = tree.Ordered[0].Id, company = tree.Ordered[1].Id;

        // Act
        var edge = HierarchyEdges(tree, world, scale, level).Single();

        // Assert — 배치: 같은 x, 한 층 아래 · 선: 점 2개(곧은 세로선), 부모 아래 가운데 → 자식 위 가운데
        Assert.Equal(world[battalion].X, world[company].X, 9);
        Assert.Equal(world[battalion].Y + UnitMapLayout.LayerHeight, world[company].Y, 9);
        var pa = UnitMapEdges.AnchorOf(level, tree.Find(battalion)!.Echelon);
        var ca = UnitMapEdges.AnchorOf(level, tree.Find(company)!.Echelon);
        var p = Screen(world[battalion], scale);
        var c = Screen(world[company], scale);
        Assert.Equal(2, edge.Points.Count);
        Assert.Equal(new Point(p.X, p.Y + pa.Bottom), edge.Points[0]);
        Assert.Equal(new Point(c.X, c.Y - ca.Top), edge.Points[1]);
    }

    [Fact]
    public void should_keep_every_link_straight_when_the_whole_chain_is_1_to_1()
    {
        var tree = Shape("chain-1to1");
        var world = UnitMapLayout.Compute(tree).Positions;

        var edges = HierarchyEdges(tree, world, 1.0, UnitMapLevel.L2);

        Assert.Equal(3, edges.Count);
        Assert.All(edges, e =>
        {
            Assert.Equal(2, e.Points.Count);
            Assert.Equal(e.Points[0].X, e.Points[1].X, 9);
        });
        Assert.Single(world.Values.Select(v => v.X).Distinct());
    }

    [Fact]
    public void should_not_use_the_spine_when_a_leaf_group_has_only_one_child_but_should_when_it_has_two()
    {
        var tree = Shape("mixed-1-and-n");
        var world = UnitMapLayout.Compute(tree).Positions;
        int Id(string n) => tree.Ordered.First(x => x.Name == n).Id;

        var edges = HierarchyEdges(tree, world, 1.0, UnitMapLevel.L2);
        UnitMapEdge Edge(string from, string to) => edges.Single(e => e.FromId == Id(from) && e.ToId == Id(to));

        Assert.Equal(2, Edge("1대대", "1중대").Points.Count);                  // 1:1 → 곧은 선
        Assert.Equal(5, Edge("2대대", "3중대").Points.Count);                  // 1:4 → 척추 + 가지
        Assert.Equal(5, Edge("3대대", "7중대").Points.Count);
    }
    #endregion

    #region - 불변식(모든 모양 · 단계) -
    [Theory]
    [MemberData(nameof(ShapesAndViews))]
    public void should_route_every_hierarchy_line_orthogonally_on_node_edges_without_crossing_a_node_when_auto_layout(string shape, UnitMapLevel level, double scale)
    {
        // Arrange
        var tree = Shape(shape);
        var world = UnitMapLayout.Compute(tree).Positions;
        var rects = world.Keys.ToDictionary(id => id, id => NodeRect(tree, id, world, scale, level));

        // Act
        var edges = HierarchyEdges(tree, world, scale, level);

        // Assert
        Assert.Equal(tree.Ordered.Count(n => n.ParentId is not null), edges.Count);
        var problems = new List<string>();
        foreach (var e in edges)
        {
            var from = rects[e.FromId];
            var to = rects[e.ToId];
            var pc = Screen(world[e.FromId], scale);
            var cc = Screen(world[e.ToId], scale);

            // ① 부모 아래 가운데에서 나간다.
            if ((e.Points[0] - new Point(pc.X, from.Bottom)).Length > Eps) problems.Add($"{e.FromId}->{e.ToId} 시작 {e.Points[0]}");

            // ② 자식 위 가운데로 들어간다 — 세로 한 줄 묶음(둘 이상)만 틀 왼쪽 가운데.
            var expectedEnd = IsComb(tree, e.FromId) ? new Point(to.Left, cc.Y) : new Point(cc.X, to.Top);
            if ((e.Points[^1] - expectedEnd).Length > Eps) problems.Add($"{e.FromId}->{e.ToId} 끝 {e.Points[^1]} ≠ {expectedEnd}");

            for (var k = 1; k < e.Points.Count; k++)
            {
                var p = e.Points[k - 1];
                var q = e.Points[k];

                // ③ 직교 선분만.
                if (Math.Abs(p.X - q.X) > Eps && Math.Abs(p.Y - q.Y) > Eps) problems.Add($"{e.FromId}->{e.ToId} 대각 {p}→{q}");

                // ④ 어느 노드도 가로지르지 않는다(끝점 노드 포함 — 가장자리에만 닿는다).
                foreach (var (id, rect) in rects)
                    if (CutsThrough(p, q, rect)) problems.Add($"{e.FromId}->{e.ToId} 선분 {k} 이 노드 {id} 를 지남");
            }

            // ⑤ 1:1 은 곧은 세로선 한 줄.
            if (tree.Find(e.FromId)!.ChildIds.Count == 1 && Math.Abs(pc.X - cc.X) < Eps && e.Points.Count != 2)
                problems.Add($"{e.FromId}->{e.ToId} 1:1 인데 점 {e.Points.Count}개");
        }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(20)));
    }

    [Theory]
    [MemberData(nameof(ShapesAndViews))]
    public void should_draw_one_bus_per_family_when_siblings_sit_on_the_same_row(string shape, UnitMapLevel level, double scale)
    {
        // 형제가 여럿인 가족의 가로 버스는 한 높이 — 제대가 섞인(건너뛴) 형제여도 겹줄이 생기지 않는다(L0 닻은 제대마다 다르다).
        var tree = Shape(shape);
        var world = UnitMapLayout.Compute(tree).Positions;

        var byParent = HierarchyEdges(tree, world, scale, level)
            .Where(e => e.Points.Count == 4)
            .GroupBy(e => e.FromId);

        foreach (var family in byParent)
            Assert.Single(family.Select(e => Math.Round(e.Points[1].Y, 6)).Distinct());
    }

    [Theory]
    [InlineData(UnitMapLevel.L2, 0.72)]
    [InlineData(UnitMapLevel.L2, 1.0)]
    [InlineData(UnitMapLevel.L2, 1.6)]
    [InlineData(UnitMapLevel.L1, 0.5)]
    [InlineData(UnitMapLevel.L0, 0.1)]
    public void should_keep_the_spine_inside_the_slot_and_the_branch_at_most_24_when_a_leaf_column_is_drawn(UnitMapLevel level, double scale)
    {
        var tree = Shape("fanout-8-leaves");
        var layout = UnitMapLayout.Compute(tree);
        var parent = tree.Ordered[0].Id;

        var edges = HierarchyEdges(tree, layout.Positions, scale, level);

        var slotLeft = layout.Slots[parent].Left * scale;
        Assert.All(edges, e =>
        {
            var spine = e.Points[2].X;
            var branch = e.Points[^1].X - spine;
            Assert.InRange(spine, slotLeft + UnitMapEdges.SPINE_INSET - Eps, double.MaxValue);
            Assert.InRange(branch, -Eps, UnitMapEdges.MAX_BRANCH + Eps);
        });
    }
    #endregion

    #region - 끌어 옮긴 위치(Δ) -
    private static (UnitTreeModel Tree, Dictionary<string, int> Ids) DragFixture()
    {
        var o = new Org();
        var ids = new Dictionary<string, int>();
        int Add(string n, string e, string? p = null) => ids[n] = o.Add(n, e, p is null ? null : ids[p]);
        Add("1연대", "Regiment");
        Add("1대대", "Battalion", "1연대");
        Add("1중대", "Company", "1대대");
        Add("2중대", "Company", "1대대");
        Add("3중대", "Company", "1대대");
        Add("2대대", "Battalion", "1연대");
        Add("4중대", "Company", "2대대");
        Add("41소초", "Outpost", "4중대");
        Add("3대대", "Battalion", "1연대");
        Add("5중대", "Company", "3대대");
        return (o.Tree(), ids);
    }

    public static IEnumerable<object[]> Drags() => new[]
    {
        new object[] { "4중대", 120.0, -260.0 },       // 자식을 부모 위로 올림
        new object[] { "5중대", -420.0, 90.0 },        // 1:1 자식을 멀리 왼쪽 아래로
        new object[] { "2중대", -150.0, 0.0 },         // 묶음 자식을 척추 왼쪽으로
        new object[] { "2중대", 160.0, 40.0 },         // 묶음 자식을 오른쪽 아래로
        new object[] { "3대대", 0.0, -160.0 },         // 대대를 연대와 같은 줄로(옆)
        new object[] { "2대대", 60.0, 70.0 },          // 부모를 옮김 — 예하가 따라온다
    };

    [Theory]
    [MemberData(nameof(Drags))]
    public void should_keep_the_moved_units_lines_orthogonal_and_off_both_endpoint_nodes_when_a_unit_is_dragged(string unit, double dx, double dy)
    {
        foreach (var (level, scale) in new[] { (UnitMapLevel.L1, 0.5), (UnitMapLevel.L2, 1.0) })
        {
            // Arrange
            var (tree, ids) = DragFixture();
            var world = UnitMapLayout.Compute(tree, new Dictionary<int, Vector> { [ids[unit]] = new Vector(dx, dy) }).Positions;

            // Act
            var edges = HierarchyEdges(tree, world, scale, level);

            // Assert — 옮긴 부대에 닿는 선만 본다(다른 노드 위로의 겹침은 사용자가 만든 배치라 그대로 둔다 — FR-21).
            foreach (var e in edges.Where(x => x.FromId == ids[unit] || x.ToId == ids[unit]))
            {
                var from = NodeRect(tree, e.FromId, world, scale, level);
                var to = NodeRect(tree, e.ToId, world, scale, level);
                for (var k = 1; k < e.Points.Count; k++)
                {
                    var p = e.Points[k - 1];
                    var q = e.Points[k];
                    Assert.True(Math.Abs(p.X - q.X) < Eps || Math.Abs(p.Y - q.Y) < Eps, $"{level} 대각 선분 {p}→{q}");
                    Assert.False(CutsThrough(p, q, from), $"{level} {e.FromId}->{e.ToId} 선분 {k} 가 부모를 지남");
                    Assert.False(CutsThrough(p, q, to), $"{level} {e.FromId}->{e.ToId} 선분 {k} 가 자식을 지남");
                }
                Assert.Equal(Screen(world[e.FromId], scale).X, e.Points[0].X, 6);
                Assert.Equal(from.Bottom, e.Points[0].Y, 6);
            }
        }
    }
    #endregion

    #region - 경로(드래그 미리보기와 같은 함수) -
    [Fact]
    public void should_return_two_points_when_parent_and_child_share_a_center_x()
    {
        var a = new UnitMapAnchor(28, 28, 66);

        var route = UnitMapEdges.Route(new Point(100, 0), a, new Point(100.2, 200), a);

        Assert.Equal(new[] { new Point(100, 28), new Point(100, 172) }, route);
    }

    [Fact]
    public void should_use_the_family_bus_when_it_lies_inside_the_gap_even_if_the_child_was_moved_far_down()
    {
        var a = new UnitMapAnchor(10, 10, 10);

        var route = UnitMapEdges.Route(new Point(0, 0), a, new Point(100, 400), a, bus: 40);

        Assert.Equal(new[] { new Point(0, 10), new Point(0, 40), new Point(100, 40), new Point(100, 390) }, route);
    }

    [Fact]
    public void should_use_the_middle_of_the_gap_when_the_requested_bus_is_outside_it()
    {
        var a = new UnitMapAnchor(10, 10, 10);

        var route = UnitMapEdges.Route(new Point(0, 0), a, new Point(100, 100), a, bus: 500);

        // 틈 10 ~ 90 → 가운데 50
        Assert.Equal(new[] { new Point(0, 10), new Point(0, 50), new Point(100, 50), new Point(100, 90) }, route);
    }

    [Fact]
    public void should_detour_through_the_lane_between_the_nodes_when_the_child_is_beside_the_parent()
    {
        var a = new UnitMapAnchor(10, 10, 20);

        var route = UnitMapEdges.Route(new Point(0, 0), a, new Point(100, 0), a);

        // 부모 오른쪽 20 · 자식 왼쪽 80 → 통로 50. 아래 10 + 12 = 22 · 위 −10 − 12 = −22.
        Assert.Equal(new[]
        {
            new Point(0, 10), new Point(0, 22), new Point(50, 22), new Point(50, -22), new Point(100, -22), new Point(100, -10),
        }, route);
    }

    [Fact]
    public void should_detour_outside_both_nodes_when_the_child_sits_straight_above_the_parent()
    {
        var a = new UnitMapAnchor(10, 10, 20);

        var route = UnitMapEdges.Route(new Point(0, 0), a, new Point(0, -100), a);

        // 가로로 겹친다 → 바깥 통로(오른쪽 20 + 12 = 32)
        Assert.Equal(32, route[2].X, 9);
        Assert.Equal(6, route.Count);
        Assert.Equal(new Point(0, -110), route[^1]);
    }
    #endregion
}
