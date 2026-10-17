using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;

/****************************************************************************
   Purpose      : 부대 편제 트리 조립 — 화면 없이 도는 순수 함수 (N-11 FR-02)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>편제 트리의 노드 한 개 — 화면이 그대로 그릴 수 있게 <b>깊이까지 계산된</b> 사실.</summary>
/// <remarks>
/// <para>형제 순서는 <b>코드 오름차순</b>이다. 서버 계약에 <c>order</c>·<c>sort_order</c> 가 없어
/// 순서를 저장할 곳이 없기 때문이다(와이어프레임 L446 · 스토리보드 L359-361).
/// 그래서 이 화면은 형제 순서 드래그를 <b>제공하지 않는다</b> — 저장되지 않는 조작은 거짓 UI 다.</para>
/// </remarks>
public sealed class UnitTreeNode
{
    public UnitTreeNode(UnitListDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Id = source.Id;
        Code = source.Code ?? string.Empty;
        Name = source.Name ?? string.Empty;
        EchelonRaw = source.EchelonRaw ?? string.Empty;
        IsEnable = source.IsEnable;
        Echelon = UnitRules.ParseEchelon(source.EchelonRaw);
    }

    public int Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string EchelonRaw { get; }
    public bool IsEnable { get; }

    /// <summary>해석된 제대. 서버가 나중에 제대를 늘리면 <c>null</c> 이다 — 그때는 이동·인접을 막는다.</summary>
    public EnumUnitEchelon? Echelon { get; }

    /// <summary>이 응답 안에서 실제로 부모로 쓰인 id. 트리 밖 부모를 가리키면 <c>null</c>(화면상 최상단).</summary>
    public int? ParentId { get; internal set; }

    /// <summary>서버가 준 원래 <c>parent_id</c>. <see cref="ParentId"/> 와 다르면 부분 그래프의 고아다.</summary>
    public int? RawParentId { get; internal set; }

    /// <summary>부모가 이 응답에 없다 — 인접으로 끌려온 서브트리 밖 노드(스토리보드 R6).</summary>
    public bool IsOrphan => RawParentId.HasValue && !ParentId.HasValue;

    /// <summary>화면 들여쓰기 단계(루트 0). 들여쓰기는 단계당 16 DIU(와이어프레임 L117).</summary>
    public int Depth { get; internal set; }

    public List<int> ChildIds { get; } = new();
    public List<int> AdjacentIds { get; } = new();

    public bool HasChildren => ChildIds.Count > 0;

    public override string ToString() => $"[{Id}] {Code} {Name} d{Depth}";
}

/// <summary>조립이 끝난 편제 트리. 화면·드롭 판정·자동화가 모두 이 한 덩어리만 본다.</summary>
public sealed class UnitTreeModel
{
    internal UnitTreeModel(IReadOnlyList<UnitTreeNode> ordered, IReadOnlyDictionary<int, UnitTreeNode> byId)
    {
        Ordered = ordered;
        ById = byId;
    }

    /// <summary>깊이 우선 · 형제는 코드 오름차순으로 펼친 평면 목록 — 화면이 그리는 순서 그대로.</summary>
    public IReadOnlyList<UnitTreeNode> Ordered { get; }

    public IReadOnlyDictionary<int, UnitTreeNode> ById { get; }

    public int Count => Ordered.Count;

    public static UnitTreeModel Empty { get; } =
        new(Array.Empty<UnitTreeNode>(), new Dictionary<int, UnitTreeNode>());

    public UnitTreeNode? Find(int id) => ById.TryGetValue(id, out var node) ? node : null;

    /// <summary><paramref name="candidateId"/> 가 <paramref name="ancestorId"/> 의 자손인가(자기 자신은 아니다).</summary>
    public bool IsDescendantOf(int candidateId, int ancestorId)
    {
        if (candidateId == ancestorId) return false;

        var guard = 0;
        var cursor = Find(candidateId);
        while (cursor?.ParentId is int parentId)
        {
            if (parentId == ancestorId) return true;
            if (++guard > Ordered.Count) return false;      // 망가진 간선에서도 돌지 않는다
            cursor = Find(parentId);
        }
        return false;
    }

    /// <summary>그 부대에 매달린 모든 자손 id(깊이 우선).</summary>
    public IReadOnlyList<int> DescendantIds(int id)
    {
        var result = new List<int>();
        var root = Find(id);
        if (root == null) return result;

        var stack = new Stack<int>(Enumerable.Reverse(root.ChildIds));
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (result.Count > Ordered.Count) break;
            result.Add(current);
            var node = Find(current);
            if (node == null) continue;
            foreach (var child in Enumerable.Reverse(node.ChildIds)) stack.Push(child);
        }
        return result;
    }
}

/// <summary>
/// <c>GET /api/units/graph</c> 응답을 화면이 그릴 수 있는 평면 트리로 조립한다.
/// </summary>
/// <remarks>
/// <para><b>계층의 정본은 <c>edges.hierarchy</c></b> 다(와이어프레임 L473 R6). 간선이 비어 있을 때만
/// 노드의 <c>parent_id</c> 로 폴백한다 — 옛 응답·가짜 데이터에서도 화면이 비지 않게.</para>
/// <para>부모를 이 응답 안에서 찾지 못하는 노드는 <b>최상단</b>으로 올린다(<see cref="UnitTreeNode.IsOrphan"/>).
/// 부분 그래프(<c>root_id</c>)에서는 인접 상대가 트리 밖 부모를 가리킨 채 끌려 들어온다.</para>
/// </remarks>
public static class UnitTreeBuilder
{
    public static UnitTreeModel Build(UnitGraphDto? graph)
    {
        if (graph?.Nodes is not { Count: > 0 }) return UnitTreeModel.Empty;

        var byId = new Dictionary<int, UnitTreeNode>();
        foreach (var dto in graph.Nodes)
        {
            if (dto == null || dto.Id <= 0 || byId.ContainsKey(dto.Id)) continue;
            byId[dto.Id] = new UnitTreeNode(dto) { RawParentId = dto.ParentId };
        }

        // ① 부모 배선 — 간선이 정본, 없으면 parent_id.
        var edges = graph.Edges?.HierarchyPairs?.ToList() ?? new List<(int Parent, int Child)>();
        if (edges.Count > 0)
        {
            foreach (var (parent, child) in edges)
            {
                if (!byId.TryGetValue(child, out var node) || !byId.ContainsKey(parent) || parent == child) continue;
                node.ParentId = parent;
            }
        }
        else
        {
            foreach (var node in byId.Values)
                if (node.RawParentId is int parentId && byId.ContainsKey(parentId) && parentId != node.Id)
                    node.ParentId = parentId;
        }

        // ② 순환 방어 — 서버가 구조적으로 막지만(제대 규칙), 망가진 응답에서 화면이 멈추면 안 된다.
        foreach (var node in byId.Values)
            if (CreatesCycle(byId, node)) node.ParentId = null;

        // ③ 자식 목록 — 코드 오름차순.
        foreach (var node in byId.Values.OrderBy(n => n.Code, StringComparer.Ordinal).ThenBy(n => n.Id))
            if (node.ParentId is int parentId && byId.TryGetValue(parentId, out var parent))
                parent.ChildIds.Add(node.Id);

        // ④ 인접 — 무방향이라 양쪽에 싣는다.
        foreach (var (low, high) in graph.Edges?.AdjacencyPairs ?? Enumerable.Empty<(int, int)>())
        {
            if (low == high) continue;
            if (byId.TryGetValue(low, out var a) && !a.AdjacentIds.Contains(high)) a.AdjacentIds.Add(high);
            if (byId.TryGetValue(high, out var b) && !b.AdjacentIds.Contains(low)) b.AdjacentIds.Add(low);
        }
        foreach (var node in byId.Values) node.AdjacentIds.Sort();

        // ⑤ 평면화 — 최상단(제대 순위 → 코드) 부터 깊이 우선.
        var ordered = new List<UnitTreeNode>(byId.Count);
        var roots = byId.Values
                        .Where(n => !n.ParentId.HasValue)
                        .OrderBy(n => n.Echelon.HasValue ? UnitRules.Rank(n.Echelon.Value) : int.MaxValue)
                        .ThenBy(n => n.Code, StringComparer.Ordinal)
                        .ThenBy(n => n.Id)
                        .ToList();

        foreach (var root in roots) Walk(byId, root, 0, ordered);

        // 어디에도 실리지 못한 노드(망가진 간선)는 뒤에 붙여 잃지 않는다.
        foreach (var node in byId.Values.OrderBy(n => n.Id))
            if (!ordered.Contains(node)) { node.Depth = 0; ordered.Add(node); }

        return new UnitTreeModel(ordered, byId);
    }

    private static void Walk(Dictionary<int, UnitTreeNode> byId, UnitTreeNode node, int depth, List<UnitTreeNode> sink)
    {
        if (sink.Contains(node)) return;
        node.Depth = depth;
        sink.Add(node);
        foreach (var childId in node.ChildIds)
            if (byId.TryGetValue(childId, out var child)) Walk(byId, child, depth + 1, sink);
    }

    private static bool CreatesCycle(Dictionary<int, UnitTreeNode> byId, UnitTreeNode node)
    {
        var guard = 0;
        var cursor = node;
        while (cursor?.ParentId is int parentId)
        {
            if (parentId == node.Id) return true;
            if (++guard > byId.Count) return true;
            byId.TryGetValue(parentId, out cursor);
        }
        return false;
    }
}
