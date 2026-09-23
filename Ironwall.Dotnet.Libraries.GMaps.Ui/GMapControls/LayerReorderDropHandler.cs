using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 레이어 패널 오버레이 목록의 드롭 담당(D-36). 공용 커널(<see cref="CaptureDragBehavior"/> ·
/// <see cref="ReorderKeyboardBehavior"/>)이 끌기와 Alt+↑↓ 양쪽에서 <b>같은</b> 이 담당을 부른다.
/// </summary>
/// <remarks>
/// 판정은 <see cref="LayerReorderRules"/>(순수 함수)에 맡기고, 여기서는 출발 목록이 같은 종류의 드롭존인지만 더 본다 —
/// 같은 창에 떠 있는 다른 콘솔의 끌기가 레이어 목록에 떨어지지 않게.
/// 서버 · DB 를 모른다: 놓으면 <c>onReorder</c> 로 "이 섹션을 이 순서로" 만 알린다.
/// </remarks>
public sealed class LayerReorderDropHandler : IDragDropHandler
{
    private readonly Action<LayerTreeNode, IReadOnlyList<LayerTreeNode>> _onReorder;

    public LayerReorderDropHandler(Action<LayerTreeNode, IReadOnlyList<LayerTreeNode>> onReorder)
        => _onReorder = onReorder ?? throw new ArgumentNullException(nameof(onReorder));

    public bool CanDrop(DragPayload payload, DropTarget target)
        => payload != null && IsOwnList(payload) && LayerReorderRules.CanReorder(payload.Items, target);

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (payload == null || !IsOwnList(payload)) return;
        if (!LayerReorderRules.TryPlan(payload.Items, target, out var section, out var newOrder) || section == null) return;
        _onReorder(section, newOrder);
    }

    private static bool IsOwnList(DragPayload payload)
        => payload.Source != null && DropZone.GetKey(payload.Source) == LayerReorderRules.ZoneKey;
}
