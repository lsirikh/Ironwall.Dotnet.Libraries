using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;

/****************************************************************************
   Purpose      : Alt+↑/↓ 상위 바꾸기의 후보 판정 — 편제 트리 순서(Tree.Ordered) 기준 순수 함수 (FR-38 · ISSUE-34)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>상위 바꾸기 계획 — 새 상위(최상위면 <see cref="ToRoot"/>) 또는 막힘 사유.</summary>
/// <param name="NewParentId">새 상위 id. 최상위로 올리면 <c>null</c>(<see cref="ToRoot"/>).</param>
/// <param name="ToRoot">최상위로 올리는 계획이다.</param>
/// <param name="BlockedReason">막혔으면 사유(<see cref="UnitDropRules"/> 문구와 같은 말투).</param>
public sealed record UnitMovePlan(int? NewParentId, bool ToRoot, string? BlockedReason)
{
    public bool IsAllowed => BlockedReason is null;

    public static UnitMovePlan Blocked(string reason) => new(null, false, reason);
}

/// <summary>
/// 키보드 폴백 <c>Alt+↑</c> · <c>Alt+↓</c> 의 판정만 — 서버를 부르지 않는다. 트리 레일과 관계도가 같은 함수를 쓴다.
/// </summary>
/// <remarks>
/// <para>후보는 <b>편제 전체의 트리 순서</b>(<see cref="UnitTreeModel.Ordered"/>)에서 찾는다 — 트리 레일의 보이는 행(접힘 · 제대 칩 · 검색이
/// 걸린 투영)에서 찾으면 관계도에서는 엉뚱한 후보가 나오고, 같은 키가 레일마다 다른 뜻이 된다(ISSUE-34).</para>
/// <para>규칙은 <see cref="UnitDropRules.CanMove"/> 를 부르기만 한다(사본 금지).</para>
/// </remarks>
public static class UnitMovePlanner
{
    /// <summary><c>Alt+↑</c> — 상위의 상위 밑으로(상위의 상위가 없으면 최상위로).</summary>
    public static UnitMovePlan PlanMoveUp(UnitTreeModel? tree, int unitId)
    {
        var node = tree?.Find(unitId);
        if (tree is null || node is null) return UnitMovePlan.Blocked(UnitDropRules.CanMove(tree, unitId, null).Reason ?? "옮길 부대를 편제에서 찾지 못했습니다.");
        if (node.ParentId is not int parentId) return UnitMovePlan.Blocked($"'{node.Name}'은(는) 이미 최상위 부대입니다.");

        var grandParentId = tree.Find(parentId)?.ParentId;
        var verdict = UnitDropRules.CanMove(tree, unitId, grandParentId);
        return verdict.IsAllowed
            ? new UnitMovePlan(grandParentId, grandParentId is null, null)
            : UnitMovePlan.Blocked(verdict.Reason ?? string.Empty);
    }

    /// <summary><c>Alt+↓</c> — 트리 순서에서 바로 앞쪽의, 받을 수 있는 첫 상위 밑으로.</summary>
    public static UnitMovePlan PlanMoveDown(UnitTreeModel? tree, int unitId)
    {
        var node = tree?.Find(unitId);
        if (tree is null || node is null) return UnitMovePlan.Blocked(UnitDropRules.CanMove(tree, unitId, null).Reason ?? "옮길 부대를 편제에서 찾지 못했습니다.");

        var index = -1;
        for (var i = 0; i < tree.Ordered.Count; i++)
            if (tree.Ordered[i].Id == unitId) { index = i; break; }

        for (var i = index - 1; i >= 0; i--)
        {
            var candidate = tree.Ordered[i].Id;
            if (UnitDropRules.CanMove(tree, unitId, candidate).IsAllowed) return new UnitMovePlan(candidate, false, null);
        }
        return UnitMovePlan.Blocked($"'{node.Name}'을(를) 받을 수 있는 상위 부대가 바로 위에 없습니다.");
    }
}
