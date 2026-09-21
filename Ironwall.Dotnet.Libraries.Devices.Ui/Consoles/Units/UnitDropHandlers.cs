using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔 드롭 처리기 — 판정은 순수 함수, 전송은 콘솔이 (N-11 FR-06 ~ FR-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>드롭 한 번이 콘솔에 넘기는 요청.</summary>
public sealed record UnitDropRequest(
    string ZoneKey,
    int TargetUnitId,
    IReadOnlyList<UnitNodeRowViewModel> Units,
    IReadOnlyList<UnitDeviceRowViewModel> Devices);

/// <summary>
/// 부대 콘솔의 드롭존 네 곳을 한 처리기가 맡는다 — 상위 바꾸기 · 루트로 · 인접 · 장비 배치.
/// </summary>
/// <remarks>
/// <para><see cref="CanDrop"/> 는 끄는 동안 마우스가 움직일 때마다 불린다 — <b>서버를 부르지 않고</b>
/// <see cref="UnitDropRules"/> 로만 판정한다(커널 계약).</para>
/// <para><see cref="Drop"/> 은 보내지 않는다. 무엇을 어디에 놓았는지만 콘솔에 넘기고,
/// 호출 1회짜리(상위 바꾸기 · 인접)는 콘솔이 곧바로 보내고 N회짜리(장비 배치)는 Draft 트레이에 쌓는다
/// — 드래그 와이어프레임 L432 의 판정 규칙 그대로.</para>
/// </remarks>
public sealed class UnitDropHandler : IDragDropHandler
{
    private readonly Func<UnitTreeModel?> _tree;
    private readonly Func<int> _selectedUnitId;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _isBusy;
    private readonly Action<UnitDropRequest> _onDrop;
    private readonly Action<string>? _onBlocked;

    public UnitDropHandler(
        Func<UnitTreeModel?> tree,
        Func<int> selectedUnitId,
        Func<bool> canEdit,
        Func<bool> isBusy,
        Action<UnitDropRequest> onDrop,
        Action<string>? onBlocked = null)
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _selectedUnitId = selectedUnitId ?? throw new ArgumentNullException(nameof(selectedUnitId));
        _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        _isBusy = isBusy ?? throw new ArgumentNullException(nameof(isBusy));
        _onDrop = onDrop ?? throw new ArgumentNullException(nameof(onDrop));
        _onBlocked = onBlocked;
    }

    public bool CanDrop(DragPayload payload, DropTarget target)
        => Verdict(payload?.Items, target).IsAllowed;

    public void Drop(DragPayload payload, DropTarget target) => Drop(payload?.Items, target);

    /// <summary>
    /// 같은 처리를 <b>끌린 항목만</b> 으로 한다 — <see cref="DragPayload"/> 는 <c>ItemsControl</c> 을 요구해
    /// STA 스레드가 없으면 만들 수 없다. 헤드리스 테스트는 이쪽을 쓴다.
    /// </summary>
    public void Drop(IReadOnlyList<object>? items, DropTarget? target)
    {
        // 막혔으면 조용히 끝내지 않는다 — 규칙이 열다섯 가지라 "왜 안 놓아지는지" 를 말해 주지 않으면
        // 운영자는 같은 자리에 계속 놓아 본다.
        var verdict = Verdict(items, target);
        if (!verdict.IsAllowed)
        {
            if (verdict.Reason is { Length: > 0 } reason) _onBlocked?.Invoke(reason);
            return;
        }

        _onDrop(new UnitDropRequest(target!.ZoneKey, TargetIdOf(target), UnitsOf(items!), DevicesOf(items!)));
    }

    /// <summary>판정 + 막힌 까닭. 화면이 "왜 못 놓는지" 를 한 줄로 보여줄 때도 쓴다.</summary>
    public UnitDropVerdict Verdict(DragPayload? payload, DropTarget? target)
        => Verdict(payload?.Items, target);

    /// <summary>
    /// 같은 판정을 <b>끌린 항목만</b> 으로 한다 — <see cref="DragPayload"/> 는 <c>ItemsControl</c> 을 요구해
    /// STA 스레드가 없으면 만들 수 없다. 헤드리스 테스트는 이쪽을 쓴다.
    /// </summary>
    public UnitDropVerdict Verdict(IReadOnlyList<object>? items, DropTarget? target)
    {
        if (items == null || target == null) return UnitDropVerdict.Block("끌어 온 것이 없습니다.");
        if (_isBusy()) return UnitDropVerdict.Block("앞선 작업이 아직 끝나지 않았습니다.");
        if (!_canEdit()) return UnitDropVerdict.Block("부대를 바꿀 권한이 없습니다(units:edit).");

        var tree = _tree();
        var units = UnitsOf(items);
        var devices = DevicesOf(items);

        switch (target.ZoneKey)
        {
            case UnitDropRules.ZONE_ROOT:
                if (units.Count != 1) return UnitDropVerdict.Block("부대 한 개만 옮길 수 있습니다.");
                return UnitDropRules.CanMove(tree, units[0].Id, null);

            case UnitDropRules.ZONE_PARENT:
            {
                var targetId = TargetIdOf(target);
                if (targetId <= 0) return UnitDropVerdict.Block("놓을 부대를 찾지 못했습니다.");

                if (devices.Count > 0)
                    return UnitDropRules.CanAssignDevices(tree, targetId, devices.Select(d => d.Item.UnitId).ToList());

                if (units.Count != 1) return UnitDropVerdict.Block("부대 한 개만 옮길 수 있습니다.");
                return UnitDropRules.CanMove(tree, units[0].Id, targetId);
            }

            case UnitDropRules.ZONE_ADJACENCY:
            {
                var selfId = _selectedUnitId();
                if (selfId <= 0) return UnitDropVerdict.Block("먼저 부대를 고르십시오.");
                if (units.Count != 1) return UnitDropVerdict.Block("부대 한 개만 인접으로 이을 수 있습니다.");
                return UnitDropRules.CanAdjoin(tree, units[0].Id, selfId);
            }

            default:
                return UnitDropVerdict.Block("여기에는 놓을 수 없습니다.");
        }
    }

    private static int TargetIdOf(DropTarget target) => target.ZoneData switch
    {
        UnitNodeRowViewModel row => row.Id,
        int id => id,
        _ => 0,
    };

    private static IReadOnlyList<UnitNodeRowViewModel> UnitsOf(IEnumerable<object> items)
        => items.OfType<UnitNodeRowViewModel>().ToList();

    private static IReadOnlyList<UnitDeviceRowViewModel> DevicesOf(IEnumerable<object> items)
        => items.OfType<UnitDeviceRowViewModel>().ToList();
}
