using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-11 FR-02 — <c>GET /api/units/graph</c> 를 화면이 그리는 평면 트리로 조립한다.
/// </summary>
/// <remarks>
/// 계층의 정본은 <c>edges.hierarchy</c> 다(와이어프레임 L473 R6). 부분 그래프에서는 인접 상대가
/// <b>트리 밖 부모</b>를 가리킨 채 끌려 들어오므로, 그 노드를 최상단으로 올려 잃지 않아야 한다.
/// </remarks>
public class UnitTreeBuilderTests
{
    #region - Fixtures -
    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null, bool isEnable = true)
        => new() { Id = id, Code = code, Name = code.ToUpperInvariant(), EchelonRaw = echelon, ParentId = parentId, IsEnable = isEnable };

    private static UnitGraphDto Graph(IEnumerable<UnitListDto> nodes, IEnumerable<(int, int)>? hierarchy = null, IEnumerable<(int, int)>? adjacency = null)
        => new()
        {
            Nodes = nodes.ToList(),
            Edges = new UnitGraphEdgesDto
            {
                Hierarchy = (hierarchy ?? Enumerable.Empty<(int, int)>()).Select(e => new List<int> { e.Item1, e.Item2 }).ToList(),
                Adjacency = (adjacency ?? Enumerable.Empty<(int, int)>()).Select(e => new List<int> { e.Item1, e.Item2 }).ToList(),
            },
        };

    /// <summary>사단 1 → 연대 2 → 대대 3 → (중대 5 · 중대 6 · 중대 4) → 소초 7 (6 밑).</summary>
    private static UnitGraphDto Sample() => Graph(
        new[]
        {
            Node(1, "d01", "Division"),
            Node(2, "r01", "Regiment", 1),
            Node(3, "b02", "Battalion", 2),
            Node(4, "c07", "Company", 3),
            Node(5, "c05", "Company", 3),
            Node(6, "c06", "Company", 3),
            Node(7, "o03", "Outpost", 6),
        },
        hierarchy: new[] { (1, 2), (2, 3), (3, 4), (3, 5), (3, 6), (6, 7) },
        adjacency: new[] { (5, 6), (6, 7) });
    #endregion

    [Fact]
    public void should_return_empty_tree_when_graph_is_null()
    {
        var tree = UnitTreeBuilder.Build(null);

        Assert.Equal(0, tree.Count);
        Assert.Empty(tree.Ordered);
    }

    [Fact]
    public void should_lay_out_depth_first_with_siblings_by_code_when_hierarchy_edges_are_given()
    {
        var tree = UnitTreeBuilder.Build(Sample());

        // 형제(4·5·6)는 코드 오름차순 c05 · c06 · c07 이다 — 서버에 순서 필드가 없어 코드가 유일한 기준이다.
        Assert.Equal(new[] { "d01", "r01", "b02", "c05", "c06", "o03", "c07" }, tree.Ordered.Select(n => n.Code));
        Assert.Equal(new[] { 0, 1, 2, 3, 3, 4, 3 }, tree.Ordered.Select(n => n.Depth));
    }

    [Fact]
    public void should_fall_back_to_parent_id_when_hierarchy_edges_are_empty()
    {
        var graph = Graph(new[] { Node(1, "d01", "Division"), Node(2, "r01", "Regiment", 1) });

        var tree = UnitTreeBuilder.Build(graph);

        Assert.Equal(1, tree.Find(2)!.ParentId);
        Assert.Equal(1, tree.Find(2)!.Depth);
    }

    [Fact]
    public void should_show_node_at_top_when_its_parent_is_outside_the_response()
    {
        // 부분 그래프에서 인접으로 끌려온 외부 노드 — parent_id 가 nodes 에 없다.
        var graph = Graph(new[] { Node(3, "b02", "Battalion"), Node(9, "c09", "Company", parentId: 99) },
                          hierarchy: new[] { (99, 9) });

        var tree = UnitTreeBuilder.Build(graph);

        var orphan = tree.Find(9)!;
        Assert.Null(orphan.ParentId);
        Assert.Equal(99, orphan.RawParentId);
        Assert.True(orphan.IsOrphan);
        Assert.Equal(0, orphan.Depth);
        Assert.Equal(2, tree.Count);            // 잃지 않는다
    }

    [Fact]
    public void should_not_hang_when_hierarchy_edges_form_a_cycle()
    {
        var graph = Graph(new[] { Node(1, "a", "Division"), Node(2, "b", "Regiment") },
                          hierarchy: new[] { (1, 2), (2, 1) });

        var tree = UnitTreeBuilder.Build(graph);

        Assert.Equal(2, tree.Count);            // 둘 다 살아 있고
        Assert.All(tree.Ordered, n => Assert.True(n.Depth >= 0));
    }

    [Fact]
    public void should_link_adjacency_on_both_sides_when_edges_are_undirected()
    {
        var tree = UnitTreeBuilder.Build(Sample());

        Assert.Equal(new[] { 6 }, tree.Find(5)!.AdjacentIds);
        Assert.Equal(new[] { 5, 7 }, tree.Find(6)!.AdjacentIds);
        Assert.Equal(new[] { 6 }, tree.Find(7)!.AdjacentIds);
    }

    [Fact]
    public void should_report_descendants_when_asked()
    {
        var tree = UnitTreeBuilder.Build(Sample());

        Assert.Equal(new[] { 5, 6, 7, 4 }, tree.DescendantIds(3));
        Assert.Empty(tree.DescendantIds(5));
        Assert.True(tree.IsDescendantOf(7, 3));
        Assert.False(tree.IsDescendantOf(3, 7));
        Assert.False(tree.IsDescendantOf(3, 3));    // 자기 자신은 자손이 아니다
    }

    [Fact]
    public void should_keep_unknown_echelon_as_raw_text_when_server_adds_a_new_one()
    {
        var graph = Graph(new[] { Node(1, "x", "Platoon") });

        var node = UnitTreeBuilder.Build(graph).Find(1)!;

        Assert.Null(node.Echelon);                      // 역직렬화로 죽지 않는다
        Assert.Equal("Platoon", UnitDropRules.EchelonTextOf(node));
    }

    [Fact]
    public void should_order_multiple_roots_by_echelon_rank_then_code()
    {
        var graph = Graph(new[]
        {
            Node(1, "z", "Outpost"),
            Node(2, "b", "Division"),
            Node(3, "a", "Division"),
        });

        var tree = UnitTreeBuilder.Build(graph);

        Assert.Equal(new[] { "a", "b", "z" }, tree.Ordered.Select(n => n.Code));
    }
}
