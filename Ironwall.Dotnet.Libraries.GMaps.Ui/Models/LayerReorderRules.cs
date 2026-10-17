using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// 레이어 패널 순서 바꾸기(D-36)의 순수 판정 — 끌기 · Alt+↑↓ · 우클릭 '위로/아래로' 가 전부 이 한 곳을 지난다.
/// 시각 트리 · DB 에 기대지 않는다(헤드리스 단위 테스트 대상).
/// </summary>
/// <remarks>
/// <para><b>목록 순서 = ZOrder 오름차순</b>(<c>FetchMapLayersAsync</c> 가 <c>ORDER BY LayerType, ZOrder, Id</c>).
/// 끌기는 화면에 보이는 목록 순서를 그대로 바꾸고, 그 순서를 ZOrder 로 옮겨 적는다 — 예전 '위로/아래로' 와 같은 의미다.</para>
/// <para>지도에서는 ZOrder 가 <b>낮을수록 먼저(아래에)</b> 그려진다 — 오버레이 맵은 <c>GMapCustomControl.RenderOverlayMapTiles</c> 의
/// <c>OrderBy(ZIndex)</c>, 오버레이 이미지는 마커 컨테이너 <c>Panel.ZIndex</c>. 즉 <b>목록 맨 위 행이 지도에서는 맨 아래</b>에 깔린다.
/// 이 의미는 이번 변경에서 바꾸지 않았다.</para>
/// </remarks>
public static class LayerReorderRules
{
    /// <summary>레이어 패널 오버레이 목록의 드롭존 종류. 다른 창의 끌기가 이 목록에 떨어지지 않게 한다.</summary>
    public const string ZoneKey = "gmaps-layer-overlay";

    /// <summary>
    /// <paramref name="items"/> 를 <paramref name="target"/> 에 놓을 수 있는가 —
    /// 같은 오버레이 섹션의 리프끼리, 편집 권한이 있고, 순서가 실제로 바뀔 때만.
    /// </summary>
    public static bool CanReorder(IReadOnlyList<object> items, DropTarget target)
        => TryPlan(items, target, out _, out _);

    /// <summary>판정 + 결과 순서. 놓을 수 없으면 false.</summary>
    public static bool TryPlan(IReadOnlyList<object> items, DropTarget target,
        out LayerTreeNode? section, out IReadOnlyList<LayerTreeNode> newOrder)
    {
        section = null;
        newOrder = Array.Empty<LayerTreeNode>();

        if (target == null || target.ZoneKey != ZoneKey || !target.IsReorder) return false;
        if (target.ZoneData is not LayerTreeNode s || !s.IsReorderSection) return false;
        if (items == null || items.Count == 0) return false;

        var moving = new List<LayerTreeNode>(items.Count);
        foreach (var item in items)
        {
            if (item is not LayerTreeNode node || !IsMovableLeafOf(node, s)) return false;
            moving.Add(node);
        }

        if (!TryPlan(s, moving, target.InsertionIndex, out newOrder)) return false;
        section = s;
        return true;
    }

    /// <summary>
    /// 섹션 <paramref name="section"/> 안에서 <paramref name="moving"/> 을 삽입 인덱스(옮기기 <b>전</b> 목록 기준 0..Count)로 옮긴 결과 순서.
    /// 순서가 그대로면(제자리 · 바로 아래) false.
    /// </summary>
    public static bool TryPlan(LayerTreeNode section, IReadOnlyList<LayerTreeNode> moving, int insertionIndex,
        out IReadOnlyList<LayerTreeNode> newOrder)
    {
        newOrder = Array.Empty<LayerTreeNode>();
        if (section == null || !section.IsReorderSection || moving == null || moving.Count == 0) return false;

        var children = section.Children;
        if (insertionIndex < 0 || insertionIndex > children.Count) return false;
        if (moving.Any(n => !IsMovableLeafOf(n, section))) return false;

        var indexes = moving.Select(children.IndexOf).ToList();
        if (indexes.Any(i => i < 0)) return false;

        var order = children.ToList();
        DragMath.MoveMany(order, indexes, insertionIndex);
        if (order.SequenceEqual(children)) return false;         // 제자리 — 쓰지 않는다

        newOrder = order;
        return true;
    }

    /// <summary>
    /// Alt+↑↓ · 우클릭 '위로/아래로' 의 삽입 인덱스 — <c>ReorderKeyboardBehavior</c> 와 같은 식.
    /// 위로: 한 칸 앞 / 아래로: 다음 행의 뒤. 끝이면 -1.
    /// </summary>
    public static int StepInsertion(int index, int direction, int count)
    {
        if (index < 0 || index >= count || direction == 0) return -1;
        var insertion = direction < 0 ? index - 1 : index + 2;
        return insertion < 0 || insertion > count ? -1 : insertion;
    }

    /// <summary>
    /// 새 목록 순서에 ZOrder 를 매긴다. 이 섹션이 이미 쓰던 값들을 오름차순으로 늘어놓은 <b>자리값</b>을
    /// 새 순서대로 나눠 준다 — 다른 섹션 · 심볼과의 상대 높이를 흔들지 않는다.
    /// 값이 겹치면(예전 데이터는 전부 0) 앞 자리보다 1 크게 벌린다: [0,0,0] → [0,1,2], [0,0,4] → [0,1,4].
    /// </summary>
    public static IReadOnlyList<ZOrderAssignment> AssignZOrders(IReadOnlyList<IMapLayerModel> newOrder)
    {
        if (newOrder == null || newOrder.Count == 0) return Array.Empty<ZOrderAssignment>();

        var slots = newOrder.Select(l => l.ZOrder).OrderBy(z => z).ToArray();
        for (var i = 1; i < slots.Length; i++)
            if (slots[i] <= slots[i - 1]) slots[i] = slots[i - 1] + 1;

        var result = new ZOrderAssignment[newOrder.Count];
        for (var i = 0; i < newOrder.Count; i++)
            result[i] = new ZOrderAssignment(newOrder[i], newOrder[i].ZOrder, slots[i]);
        return result;
    }

    /// <summary>
    /// <paramref name="children"/> 를 <paramref name="target"/> 순서로 <b>제자리에서</b> 옮긴다(Clear 하지 않는다).
    /// 노드 객체가 그대로 남아 목록의 선택 · 초점이 유지된다 — 연달아 Alt+↑ 를 눌러도 같은 행을 쥐고 있다.
    /// </summary>
    public static void ApplyOrderInPlace(ObservableCollection<LayerTreeNode> children, IReadOnlyList<LayerTreeNode> target)
    {
        if (children == null || target == null) return;
        if (children.Count != target.Count || target.Any(n => !children.Contains(n)))
            throw new ArgumentException("목표 순서는 같은 섹션의 같은 노드들이어야 합니다.", nameof(target));

        for (var i = 0; i < target.Count; i++)
        {
            var from = children.IndexOf(target[i]);
            if (from != i) children.Move(from, i);
        }
    }

    private static bool IsMovableLeafOf(LayerTreeNode node, LayerTreeNode section)
        => node != null
        && ReferenceEquals(node.Parent, section)
        && node.NodeType == LayerNodeType.Leaf
        && !node.IsSymbolLeaf
        && node.Model != null
        && node.Model.LayerType == section.OverlayLayerType
        && node.IsMapEditable;                                // 우클릭 '위로/아래로' 와 같은 편집 권한 관문
}

/// <summary>한 레이어의 ZOrder 변경 계획(이전 값 → 새 값). Undo 기록 · 렌더 동기화 · 일괄 기록이 같은 계획을 쓴다.</summary>
public readonly record struct ZOrderAssignment(IMapLayerModel Layer, int OldZOrder, int NewZOrder)
{
    public bool IsChanged => OldZOrder != NewZOrder;
}
