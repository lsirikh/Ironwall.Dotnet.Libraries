using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 관계도 드롭 판정 — 위치 · 상위 바꾸기 · 인접 연결 · 막힘 (FR-05 · FR-29)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 드롭 판정에 필요한 권한 · 배치 상태.
/// </summary>
/// <param name="CanView"><c>units:view</c>.</param>
/// <param name="CanEdit"><c>units:edit</c> — 상위 · 인접 · <b>공유</b> 배치 쓰기(FR-05).</param>
/// <param name="Layout">배치 상태 — 위치 드롭을 받을 수 있는지가 여기서 갈린다(시나리오 ISSUE-17).</param>
public sealed record UnitMapDropPolicy(bool CanView, bool CanEdit, UnitMapLayoutState Layout);

/// <summary>
/// 끌어 놓은 곳의 뜻을 정한다 — <b>포인터 아래</b> 부대로(끌리는 사본 위치가 아니다 — FR-29).
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>빈 곳 · <c>Ctrl</c> · 자기 · 예하 위</term><description>위치 — 배치 상태 · 권한이 허락할 때만</description></item>
/// <item><term>상위(또는 하위 · 모르는) 제대 위</term><description><see cref="UnitDropRules.CanMove"/> 그대로 — 허락이면 상위 바꾸기</description></item>
/// <item><term>같은 제대 위</term><description><see cref="UnitDropRules.CanAdjoin"/> 그대로 — 허락이면 인접 연결. 같은 제대는 상위가 될 수 없어(서버 422) 모호하지 않다</description></item>
/// </list>
/// <para><b>규칙 사본이 없다</b> — 편제 판정은 트리 레일과 같은 <see cref="UnitDropRules"/> 를 부르기만 하고 사유도 그대로 싣는다
/// (시험이 200 × 200 전수로 두 결과의 일치를 단언한다). 이 형식이 더하는 것은 권한 · 배치 상태 · <c>Ctrl</c> 뿐이다.</para>
/// <para><b>위치 권한</b>(FR-05): 공유 배치는 모든 운영자 화면을 바꾸므로 <c>units:edit</c>, 세션 전용은 누구 화면도 바꾸지 않으므로
/// <c>units:view</c>. 읽는 중 · 읽기 실패 · 판 불일치면 위치는 막힌다 — 끄는 동안 위치 칩이 떴다가 놓는 순간 거절되는 일이 없게.
/// 상위 바꾸기 · 인접 연결은 배치 상태와 무관하다(FR-51).</para>
/// <para>현 상위 위는 규칙대로 <b>막힘</b>("이미 소속")이다(플랜 TEST-11). 위치로 볼지는 시나리오 ISSUE-15 로 조정자 결정 대기.</para>
/// <para>끄는 동안 자주 불린다 — 서버를 부르지 않는다. UI 스레드 전용.</para>
/// </remarks>
public static class UnitMapDropClassifier
{
    /// <summary>
    /// <paramref name="movingId"/> 를 <paramref name="hoverId"/>(빈 곳이면 <c>null</c>) 위에 놓으면 무엇이 되는가.
    /// </summary>
    public static UnitMapDropDecision Classify(UnitTreeModel? tree, int movingId, int? hoverId, bool ctrl, UnitMapDropPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var moving = tree?.Find(movingId);
        if (tree == null || moving == null)
            return UnitMapDropDecision.Blocked(UnitDropRules.CanMove(tree, movingId, null).Reason ?? UnitMapText.UnknownEchelonDrag);

        if (!policy.CanView) return UnitMapDropDecision.Blocked(UnitMapText.NoPermission, hoverId);
        if (moving.Echelon is null) return UnitMapDropDecision.Blocked(UnitMapText.UnknownEchelonDrag, hoverId);

        // 자기 · 예하 위(원래 자리 잔상 위) = 빈 곳 = 위치(조정자 결정 D-2026-09-27-215b6d · ISSUE-14).
        if (hoverId is int self && (self == movingId || tree.IsDescendantOf(self, movingId))) hoverId = null;

        if (hoverId is not int targetId || ctrl) return ClassifyPosition(policy);
        if (!policy.CanEdit) return UnitMapDropDecision.Blocked(UnitMapText.NoPermission, targetId);

        var target = tree.Find(targetId);
        var sameEchelon = target?.Echelon is EnumUnitEchelon targetEchelon && targetEchelon == moving.Echelon;
        if (sameEchelon)
        {
            var adjoin = UnitDropRules.CanAdjoin(tree, movingId, targetId);
            return adjoin.IsAllowed
                ? UnitMapDropDecision.Adjoin(targetId)
                : UnitMapDropDecision.Blocked(adjoin.Reason ?? string.Empty, targetId);
        }

        var move = UnitDropRules.CanMove(tree, movingId, targetId);
        return move.IsAllowed
            ? UnitMapDropDecision.Reparent(targetId)
            : UnitMapDropDecision.Blocked(move.Reason ?? string.Empty, targetId);
    }

    /// <summary>캔버스가 올린 드롭 요청 그대로 판정한다.</summary>
    public static UnitMapDropDecision Classify(UnitTreeModel? tree, UnitMapDropRequest request, UnitMapDropPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Classify(tree, request.UnitId, request.HoverUnitId, request.Ctrl, policy);
    }

    /// <summary>
    /// 그 노드를 끌 수 있는가 — L1 · L2 이고 제대를 안다(L0 는 끌지 않는다 — FR-13, 모르는 제대는 끌 수 없다 — FR-22).
    /// 제스처의 <c>GraphPress.CanDragNode</c> 로 넘긴다. 권한이 없어도 끌 수는 있다 — 판정이 막힘을 <b>보인다</b>(FR-05).
    /// </summary>
    public static bool CanDrag(UnitTreeModel? tree, int unitId, UnitMapLevel level)
        => level != UnitMapLevel.L0 && tree?.Find(unitId)?.Echelon is not null;

    private static UnitMapDropDecision ClassifyPosition(UnitMapDropPolicy policy) => policy.Layout switch
    {
        UnitMapLayoutState.Shared => policy.CanEdit
            ? UnitMapDropDecision.Position()
            : UnitMapDropDecision.Blocked(UnitMapText.NoPermission),
        UnitMapLayoutState.SessionOnly => UnitMapDropDecision.Position(),
        UnitMapLayoutState.ReadFailed => UnitMapDropDecision.Blocked(UnitMapText.LayoutReadFailedBlocked),
        UnitMapLayoutState.VersionMismatch => UnitMapDropDecision.Blocked(UnitMapText.LayoutVersionBlocked),
        _ => UnitMapDropDecision.Blocked(UnitMapText.LayoutLoadingBlocked),
    };
}
