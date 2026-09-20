using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;

/****************************************************************************
   Purpose      : 부대 콘솔 드롭 판정 — 화면 없이 도는 순수 함수 (N-11 FR-06 · FR-09 · FR-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>드롭 한 번의 판정. <see cref="Reason"/> 는 <b>막힐 때만</b> 채워지고 그대로 화면에 뜬다.</summary>
public sealed record UnitDropVerdict(bool IsAllowed, string? Reason)
{
    public static UnitDropVerdict Allow() => new(true, null);
    public static UnitDropVerdict Block(string reason) => new(false, reason);
}

/// <summary>
/// 무엇을 어디에 놓을 수 있는가 — 서버에 나가기 전에 전부 지역에서 판정한다.
/// </summary>
/// <remarks>
/// <para>서버가 422 로 거절할 것을 미리 막는 것이 목적이다(와이어프레임 L356-364 §5-2).
/// 드래그가 시작되면 <b>놓을 수 있는 노드만 윤곽</b>이 서고 나머지는 사선 해치로 흐려진다 —
/// 그 판정이 여기서 나온다. 끄는 동안 자주 불리므로 서버를 부르지 않는다.</para>
/// <para>제대 규칙(<c>상위 제대만</c>)이 순환을 <b>구조적으로</b> 막지만(스토리보드 L353),
/// 제대를 해석하지 못하는 노드가 섞이면 그 방어가 사라진다 — 그래서 자손 검사를 따로 둔다.</para>
/// </remarks>
public static class UnitDropRules
{
    /// <summary>부대 노드 위 — 상위 부대 바꾸기.</summary>
    public const string ZONE_PARENT = "unit-parent";

    /// <summary>트리 맨 위 — 루트로 올리기(<c>parent_id: null</c>).</summary>
    public const string ZONE_ROOT = "unit-root";

    /// <summary>상세 칸의 인접 드롭존.</summary>
    public const string ZONE_ADJACENCY = "unit-adjacency";

    /// <summary>부대 노드 위 — 미배치 장비 놓기.</summary>
    public const string ZONE_DEVICE = "unit-device";

    #region - 상위 부대 바꾸기 -
    /// <summary>
    /// <paramref name="movingId"/> 를 <paramref name="targetParentId"/> 아래로 옮길 수 있는가.
    /// <paramref name="targetParentId"/> 가 <c>null</c> 이면 "루트로 올리기"다.
    /// </summary>
    public static UnitDropVerdict CanMove(UnitTreeModel? tree, int movingId, int? targetParentId)
    {
        if (tree == null) return UnitDropVerdict.Block("편제를 아직 읽지 못했습니다.");

        var moving = tree.Find(movingId);
        if (moving == null) return UnitDropVerdict.Block("옮길 부대를 편제에서 찾지 못했습니다.");

        if (targetParentId is null)
        {
            return moving.ParentId is null
                ? UnitDropVerdict.Block($"'{moving.Name}' 은 이미 최상위 부대입니다.")
                : UnitDropVerdict.Allow();
        }

        var target = targetParentId.Value;
        if (target == movingId) return UnitDropVerdict.Block("자기 자신을 상위 부대로 삼을 수 없습니다.");

        var parent = tree.Find(target);
        if (parent == null) return UnitDropVerdict.Block("상위로 삼을 부대를 편제에서 찾지 못했습니다.");

        if (moving.ParentId == target)
            return UnitDropVerdict.Block($"'{moving.Name}' 은 이미 '{parent.Name}' 소속입니다.");

        if (tree.IsDescendantOf(target, movingId))
            return UnitDropVerdict.Block($"'{parent.Name}' 은 '{moving.Name}' 의 하위 부대입니다 — 자기 밑으로는 옮길 수 없습니다.");

        if (moving.Echelon is not EnumUnitEchelon movingEchelon)
            return UnitDropVerdict.Block($"'{moving.Name}' 의 제대('{moving.EchelonRaw}')를 이 판본이 알지 못합니다 — 옮기지 않습니다.");

        if (parent.Echelon is not EnumUnitEchelon parentEchelon)
            return UnitDropVerdict.Block($"'{parent.Name}' 의 제대('{parent.EchelonRaw}')를 이 판본이 알지 못합니다 — 옮기지 않습니다.");

        if (!UnitRules.IsAllowedParent(parentEchelon, movingEchelon))
            return UnitDropVerdict.Block(
                $"{EchelonText(movingEchelon)}는 {EchelonText(parentEchelon)}에 붙일 수 없습니다 — 엄격히 상위 제대에만 놓을 수 있습니다.");

        return UnitDropVerdict.Allow();
    }
    #endregion

    #region - 인접 연결 -
    /// <summary><paramref name="sourceId"/> 와 <paramref name="targetId"/> 를 인접으로 이을 수 있는가.</summary>
    /// <remarks>인접은 <b>무방향</b>이다 — 한쪽에서 이으면 반대쪽 상세에도 나타난다(스토리보드 L374).</remarks>
    public static UnitDropVerdict CanAdjoin(UnitTreeModel? tree, int sourceId, int targetId)
    {
        if (tree == null) return UnitDropVerdict.Block("편제를 아직 읽지 못했습니다.");
        if (sourceId == targetId) return UnitDropVerdict.Block("자기 자신과는 인접이 될 수 없습니다.");

        var source = tree.Find(sourceId);
        var target = tree.Find(targetId);
        if (source == null || target == null) return UnitDropVerdict.Block("인접으로 이을 부대를 편제에서 찾지 못했습니다.");

        if (source.Echelon is not EnumUnitEchelon a || target.Echelon is not EnumUnitEchelon b)
            return UnitDropVerdict.Block("제대를 알 수 없는 부대는 인접으로 잇지 않습니다.");

        if (!UnitRules.IsAllowedAdjacency(a, b))
            return UnitDropVerdict.Block($"인접은 같은 제대끼리만 됩니다 — {EchelonText(a)} ↔ {EchelonText(b)}.");

        if (target.AdjacentIds.Contains(sourceId))
            return UnitDropVerdict.Block($"'{source.Name}' 과 '{target.Name}' 은 이미 인접입니다.");

        return UnitDropVerdict.Allow();
    }

    /// <summary>
    /// 인접 <b>전체 집합</b>을 만든다 — 서버 <c>PATCH adjacent_unit_ids</c> 는 그 부대의 인접을
    /// <b>전삭제 후 재생성</b>하므로 "추가" 도 현재 목록 전부를 함께 보내야 한다(스토리보드 L366-368).
    /// </summary>
    public static List<int> MergeAdjacency(IEnumerable<int>? current, int selfId, int? add = null, int? remove = null)
    {
        var set = new HashSet<int>(current ?? Enumerable.Empty<int>());
        if (add is int a) set.Add(a);
        if (remove is int r) set.Remove(r);
        return UnitRules.NormalizeAdjacency(set, selfId) ?? new List<int>();
    }
    #endregion

    #region - 미배치 장비 놓기 -
    /// <summary>끌어 온 장비들을 <paramref name="targetUnitId"/> 에 붙일 수 있는가.</summary>
    /// <param name="deviceUnitIds">장비마다 현재 소속 부대 id(없으면 <c>null</c>).</param>
    public static UnitDropVerdict CanAssignDevices(UnitTreeModel? tree, int targetUnitId, IReadOnlyList<int?> deviceUnitIds)
    {
        if (tree == null) return UnitDropVerdict.Block("편제를 아직 읽지 못했습니다.");

        var target = tree.Find(targetUnitId);
        if (target == null) return UnitDropVerdict.Block("놓을 부대를 편제에서 찾지 못했습니다.");
        if (deviceUnitIds is not { Count: > 0 }) return UnitDropVerdict.Block("끌어 온 장비가 없습니다.");

        if (deviceUnitIds.All(unitId => unitId == targetUnitId))
            return UnitDropVerdict.Block($"이미 '{target.Name}' 소속입니다.");

        return UnitDropVerdict.Allow();
    }
    #endregion

    #region - 표기 -
    public static string EchelonText(EnumUnitEchelon echelon) => echelon switch
    {
        EnumUnitEchelon.Division => "사단",
        EnumUnitEchelon.Regiment => "연대",
        EnumUnitEchelon.Battalion => "대대",
        EnumUnitEchelon.Company => "중대",
        EnumUnitEchelon.Outpost => "소초",
        _ => echelon.ToString(),
    };

    /// <summary>해석하지 못한 제대는 서버가 준 원문을 그대로 보인다 — 빈 칸으로 감추지 않는다.</summary>
    public static string EchelonTextOf(UnitTreeNode node)
        => node.Echelon is EnumUnitEchelon e ? EchelonText(e)
         : string.IsNullOrWhiteSpace(node.EchelonRaw) ? "제대 없음" : node.EchelonRaw;
    #endregion
}
