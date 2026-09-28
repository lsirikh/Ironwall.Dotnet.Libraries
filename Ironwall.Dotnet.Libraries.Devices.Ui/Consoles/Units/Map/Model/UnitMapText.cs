using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 관계도 문구 — 짧은 이름 · 제대 표지 · peer 문자열 · 배치 문구 · 칩 · 막대 (FR-11 · 20 · 30 · 31 · 41 · 45 · 52)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>확인 오버레이(FR-32) 문구 — 제목 · 본문 줄 · 확정 단추.</summary>
public sealed record UnitMapConfirmText(string Title, IReadOnlyList<string> Lines, string OkText);

/// <summary>
/// 관계도가 화면 · UIA 에 내는 문구를 한 곳에서 만든다 — 화면 없이 도는 순수 함수.
/// </summary>
/// <remarks>
/// <para>한글 조사 병기("을(를)" 등)는 커널 <see cref="KoreanParticles"/> 로 여기서 고른다 — 이름이 실행 중에 정해지는 문구다.
/// 단 판정 사유(<see cref="UnitDropRules"/> 의 <c>Reason</c>)는 규칙과 글자 그대로 같아야 하므로(드롭 판정 시험이 대조한다)
/// 판정기는 원문을 싣고, 칩으로 그릴 때(<see cref="DropChip"/>) 고른다.</para>
/// <para>문구 원본은 PRD FR-11 · FR-31 · FR-45 · SB S5~S9 · S12 이다. 시나리오 ISSUE-7 이 짚은 빈 상태(읽는 중 · 판 불일치 · 빈 문서)를 더했다.</para>
/// </remarks>
public static class UnitMapText
{
    #region - 막힘 사유(드롭 판정기가 싣는다) -
    /// <summary>권한 없음(FR-05) — 숨기지 않고 막힘으로 보인다.</summary>
    public const string NoPermission = "권한이 없습니다";

    /// <summary>모르는 제대의 부대는 끌 수 없다(FR-22 · 부모 FR-19).</summary>
    public const string UnknownEchelonDrag = "제대를 알 수 없는 부대는 끌 수 없습니다";

    /// <summary>배치를 아직 읽는 중 — 위치 쓰기는 응답 뒤(SIM-P041).</summary>
    public const string LayoutLoadingBlocked = "배치를 불러오는 중입니다";

    /// <summary>배치 읽기 실패 — 쓰기 금지(FR-50).</summary>
    public const string LayoutReadFailedBlocked = "배치를 불러오지 못해 위치를 옮길 수 없습니다";

    /// <summary>배치 판 불일치 — 쓰기 금지(FR-07).</summary>
    public const string LayoutVersionBlocked = "배치 판이 달라 위치를 옮길 수 없습니다";
    #endregion

    #region - 이름 · 표지 · 단계 -
    /// <summary>짧은 이름 — 이름의 <b>마지막 낱말</b>(L1 틀 아래 — FR-20). 비었으면 빈 문자열.</summary>
    public static string ShortName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? string.Empty : words[^1];
    }

    /// <summary>APP-6(D) 제대 표지(FR-22) — 사단 XX · 연대 ||| · 대대 || · 중대 | · 소초 ●●●. 모르는 제대는 "?".</summary>
    public static string EchelonMark(EnumUnitEchelon? echelon) => echelon switch
    {
        EnumUnitEchelon.Division => "XX",
        EnumUnitEchelon.Regiment => "|||",
        EnumUnitEchelon.Battalion => "||",
        EnumUnitEchelon.Company => "|",
        EnumUnitEchelon.Outpost => "●●●",
        _ => "?",
    };

    /// <summary>단계 이름 — HUD(개요 · 부대 · 상세).</summary>
    public static string LevelName(UnitMapLevel level) => level switch
    {
        UnitMapLevel.L1 => "부대",
        UnitMapLevel.L2 => "상세",
        _ => "개요",
    };

    /// <summary>HUD "단계 · 배율"(예 "부대 · 50%") — <c>Units.Map.ZoomLevel</c>(FR-14).</summary>
    public static string ZoomLabel(UnitMapLevel level, double scale)
        => $"{LevelName(level)} · {Math.Round(scale * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";
    #endregion

    #region - peer · 툴팁 (FR-41 · FR-20 · FR-23) -
    /// <summary>노드 peer <c>Name</c> — "7중대 (중대, c0207)". 모르는 제대는 서버 원문.</summary>
    public static string PeerName(UnitTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return $"{node.Name} ({UnitDropRules.EchelonTextOf(node)}, {node.Code})";
    }

    /// <summary>노드 peer <c>ItemStatus</c> — "단계=L2; 옮김=예; 장비=16; 오류=0; 중지=아니오".</summary>
    public static string ItemStatus(UnitMapLevel level, bool isMoved, int deviceCount, int errorCount, bool isSuspended)
        => string.Create(CultureInfo.InvariantCulture,
            $"단계={level}; 옮김={YesNo(isMoved)}; 장비={deviceCount}; 오류={errorCount}; 중지={YesNo(isSuspended)}");

    /// <summary>툴팁 — 전체 이름 · 코드 · 제대. 편제 밖 상위를 가리키면 둘째 줄에 알린다(FR-23).</summary>
    public static string Tooltip(UnitTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var head = $"{node.Name} · {node.Code} · {UnitDropRules.EchelonTextOf(node)}";
        return node.IsOrphan ? head + Environment.NewLine + "상위 편제 밖 — 상위 부대가 이 편제에 없습니다" : head;
    }
    #endregion

    #region - 배치 상태 문구 (FR-11 · Units.Map.LayoutStatus) -
    /// <summary>
    /// 캔버스 위 가운데 배치 상태 문구 — PRD §2.1-A 의 7행 표 그대로(ISSUE-7).
    /// </summary>
    /// <param name="state">배치 상태.</param>
    /// <param name="canEdit"><c>units:edit</c> — 공유 배치에서 없으면 끝에 " · 보기 전용".</param>
    /// <param name="updatedBy">마지막 변경자 이름(<c>updated_by.name</c>). 없으면 아직 아무도 옮기지 않은 문서(빈 문서).</param>
    /// <param name="updatedAtLocal">마지막 변경 시각(<b>이미 로컬 시각</b>으로 바꾼 값).</param>
    /// <param name="updatedByMe">마지막 변경자가 이 운영자 — 이름 대신 "나".</param>
    /// <param name="liveOff">전역 구독이 꺼져 실시간 반영이 없다 — 모든 행에 " · 실시간 반영 꺼짐" 꼬리표(ISSUE-32).</param>
    /// <param name="sessionExpired">읽기 실패가 401 이다 — 7행의 변형 문구.</param>
    public static string LayoutStatus(UnitMapLayoutState state, bool canEdit, string? updatedBy = null, DateTime? updatedAtLocal = null,
                                      bool updatedByMe = false, bool liveOff = false, bool sessionExpired = false)
    {
        var text = state switch
        {
            UnitMapLayoutState.Shared => SharedStatus(canEdit, updatedBy, updatedAtLocal, updatedByMe),
            UnitMapLayoutState.SessionOnly => "이 서버는 배치 저장을 지원하지 않습니다 — 옮긴 위치는 창을 닫으면 자동 배치로 돌아갑니다",
            UnitMapLayoutState.ReadFailed => sessionExpired
                ? "로그인이 만료되어 배치를 불러오지 못했습니다"
                : "배치를 불러오지 못했습니다 — 자동 배치로 보입니다",
            UnitMapLayoutState.VersionMismatch => "배치 판이 달라 자동 배치로 보입니다 — 이 판에서는 위치를 옮길 수 없습니다",
            _ => "배치를 불러오는 중 — 자동 배치로 보입니다",
        };
        return liveOff ? text + LiveOffSuffix : text;
    }

    /// <summary>전역 구독이 꺼졌을 때 모든 배치 문구 끝에 붙는 꼬리표(ISSUE-32).</summary>
    public const string LiveOffSuffix = " · 실시간 반영 꺼짐";

    private static string SharedStatus(bool canEdit, string? updatedBy, DateTime? updatedAtLocal, bool updatedByMe)
    {
        string head;
        if (string.IsNullOrWhiteSpace(updatedBy) && !updatedByMe)
        {
            head = "배치: 모든 운영자 공유 · 아직 아무도 옮기지 않았습니다";
        }
        else
        {
            var who = updatedByMe ? "나" : updatedBy!.Trim();
            var when = updatedAtLocal is DateTime at ? " " + at.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;
            head = $"배치: 모든 운영자 공유 · 마지막 변경 {who}{when}";
        }
        return canEdit ? head : head + " · 보기 전용";
    }
    #endregion

    #region - 끄는 동안 칩 (FR-30) -
    /// <summary>
    /// 머문 노드 위 칩 — "3대대 밑으로 옮기기" · "9중대와 인접 연결 · 양방향" · 막힘 사유. 위치(빈 곳)는 칩이 없다(<c>null</c>).
    /// </summary>
    public static string? DropChip(UnitMapDropDecision decision, UnitTreeModel? tree)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var targetName = decision.TargetId is int id ? tree?.Find(id)?.Name : null;
        return decision.Kind switch
        {
            UnitMapDropKind.Reparent when targetName != null => $"{targetName} 밑으로 옮기기",
            UnitMapDropKind.Adjoin when targetName != null => KoreanParticles.Resolve($"{targetName}과(와) 인접 연결 · 양방향"),
            UnitMapDropKind.Blocked => KoreanParticles.Resolve(decision.Reason ?? string.Empty),
            _ => null,
        };
    }

    /// <summary>끌리는 사본 옆 칩 — "예하 5 함께". 예하가 없으면 <c>null</c>.</summary>
    public static string? SubtreeChip(int descendantCount)
        => descendantCount > 0 ? string.Create(CultureInfo.InvariantCulture, $"예하 {descendantCount} 함께") : null;
    #endregion

    #region - 되돌리기 막대 · 실패 · 충돌 (FR-31 · FR-34 · FR-35 · FR-52) -
    /// <summary>위치 옮김 — 공유면 "모든 운영자에게 보입니다", 세션 전용이면 "창을 닫으면 사라집니다"(FR-31).</summary>
    public static string PositionMovedBar(string unitName, bool sessionOnly)
        => sessionOnly
            ? $"{Quote(unitName)} 위치를 옮겼습니다. 이 서버는 배치를 저장하지 않습니다 — 창을 닫으면 사라집니다."
            : $"{Quote(unitName)} 위치를 옮겼습니다 — 모든 운영자에게 보입니다.";

    /// <summary>상위 바꿈 — "‘8중대’를 ‘3대대’ 밑으로 옮겼습니다."(SB S9).</summary>
    public static string ReparentedBar(string movingName, string targetName)
        => KoreanParticles.Resolve($"{Quote(movingName)}을(를) {Quote(targetName)} 밑으로 옮겼습니다.");

    /// <summary>인접 연결 — "‘6중대’와 ‘9중대’를 인접 부대로 이었습니다."</summary>
    public static string AdjoinedBar(string unitName, string otherName)
        => KoreanParticles.Resolve($"{Quote(unitName)}과(와) {Quote(otherName)}을(를) 인접 부대로 이었습니다.");

    /// <summary>편제 쓰기 실패 → 재조회(FR-34) — "‘8중대’를 옮기지 못했습니다. {사유} 편제를 다시 읽었습니다."</summary>
    public static string WriteFailedBar(string unitName, string reason)
        => KoreanParticles.Resolve($"{Quote(unitName)}을(를) 옮기지 못했습니다. {reason.Trim()} 편제를 다시 읽었습니다.");

    /// <summary>배치 쓰기 실패(충돌 외) → 서버 배치를 다시 읽어 그대로 그림(FR-34).</summary>
    public static string LayoutWriteFailedBar(string unitName, string reason)
        => $"{Quote(unitName)} 위치를 저장하지 못했습니다. {reason.Trim()} 서버 배치를 다시 불러왔습니다.";

    /// <summary>412 충돌 — 적용하지 않고 최신 배치를 그렸다(FR-52). 말없이 덮어쓰지 않는다.</summary>
    public static string LayoutConflictBar(string unitName)
        => $"다른 운영자가 방금 {Quote(unitName)} 배치를 바꿨습니다 — 최신 배치를 불러왔습니다.";

    /// <summary>되돌리기 거절 — 그 사이 다른 운영자가 그 부대 배치를 바꿨다(FR-35 — 남의 변경을 되돌리기로 지우지 않는다).</summary>
    public static string UndoRefusedBar(string unitName)
        => $"다른 운영자가 {Quote(unitName)} 배치를 바꿔 되돌리지 않았습니다.";
    #endregion

    /// <summary>배치 쓰기가 시간 초과 — 반영됐을 수 있다(ISSUE-6): 다시 읽은 서버 상태로 그렸음을 말한다.</summary>
    public static string LayoutWriteUnknownBar(string unitName)
        => $"{Quote(unitName)} 위치 저장 결과를 확인하지 못했습니다 — 서버 배치를 다시 불러와 그대로 보입니다.";

    /// <summary>위치 되돌림.</summary>
    public static string PositionUndoneBar(string unitName) => $"{Quote(unitName)} 위치를 되돌렸습니다.";

    /// <summary>상위 바꿈 되돌림.</summary>
    public static string ReparentUndoneBar(string unitName)
        => KoreanParticles.Resolve($"{Quote(unitName)}의 상위 변경을 되돌렸습니다.");

    /// <summary>상위 되돌리기 거절 — 그 사이 다른 곳에서 상위가 또 바뀌었다(ISSUE-21).</summary>
    public static string ReparentUndoRefusedBar(string unitName)
        => $"다른 곳에서 {Quote(unitName)} 상위를 바꿔 되돌리지 않았습니다.";

    /// <summary>되돌릴 부대가 편제에서 사라졌다(SIM-F119).</summary>
    public static string UndoUnitGoneBar(string unitName)
        => $"{Quote(unitName)} 부대가 편제에 없어 되돌리지 않았습니다.";

    /// <summary>인접 되돌림.</summary>
    public static string AdjacencyUndoneBar(string unitName, string otherName)
        => KoreanParticles.Resolve($"{Quote(unitName)}과(와) {Quote(otherName)}의 인접 연결을 되돌렸습니다.");

    /// <summary>되돌릴 인접 조작이 이미 다른 곳에서 반영돼 있다(SIM-F120).</summary>
    public static string AdjacencyAlreadyBar(string unitName, string otherName, bool wasAdded)
        => KoreanParticles.Resolve(wasAdded
            ? $"{Quote(unitName)}과(와) {Quote(otherName)}은(는) 이미 인접이 끊겨 있습니다 — 되돌릴 것이 없습니다."
            : $"{Quote(unitName)}과(와) {Quote(otherName)}은(는) 이미 인접입니다 — 되돌릴 것이 없습니다.");

    /// <summary>상세가 더럽고 그 부대가 이 조작의 영향 부대다(조정자 필수 항목 5 · ISSUE-20).</summary>
    public const string DirtyDetailBlocked = "상세에 적용하지 않은 변경이 있습니다 — 먼저 [적용]하거나 되돌린 뒤 옮기세요";

    /// <summary>편제 쓰기 실패의 일반 사유(콘솔이 상태 띠에 자세한 사유를 적는다).</summary>
    public const string OrgWriteFailedReason = "서버가 거절했거나 응답하지 않았습니다.";

    /// <summary>[배치 초기화] 확인 오버레이.</summary>
    public static UnitMapConfirmText ConfirmResetLayout(bool sessionOnly, int movedCount = 0)
    {
        var undoLine = movedCount > 1000 ? ResetUndoUnavailableLine : "되돌리기 가능";
        return new UnitMapConfirmText(
            "배치 초기화",
            sessionOnly
                ? new[] { "이 창에서 옮긴 위치를 모두 자동 배치로 돌립니다.", undoLine }
                : new[] { "모든 운영자의 배치가 자동 배치로 돌아갑니다.", movedCount > 1000 ? "서버에 바로 저장됩니다 · " + ResetUndoUnavailableLine : "서버에 바로 저장됩니다 · 되돌리기 가능" },
            "초기화");
    }

    /// <summary>[배치 초기화] 완료.</summary>
    public static string LayoutResetBar(bool sessionOnly)
        => sessionOnly ? "이 창의 배치를 초기화했습니다 — 창을 닫으면 원래대로 사라집니다." : "배치를 초기화했습니다 — 모든 운영자에게 보입니다.";

    /// <summary>[이 부대 배치 초기화] 완료.</summary>
    public static string NodeLayoutResetBar(string unitName) => $"{Quote(unitName)} 배치를 초기화했습니다.";

    /// <summary>초기화 되돌림 — 편제에서 사라진 부대는 건너뛰었다(조정자 필수 항목 4).</summary>
    public static string LayoutResetUndoneBar(int skipped)
        => skipped > 0
            ? string.Create(CultureInfo.InvariantCulture, $"배치 초기화를 되돌렸습니다 — 편제에서 사라진 {skipped}곳은 건너뛰었습니다.")
            : "배치 초기화를 되돌렸습니다.";

    /// <summary>초기화 되돌리기 거절 — 그 사이 누가 배치를 바꿨다(전부 아니면 전무).</summary>
    public const string LayoutResetUndoRefusedBar = "그 사이 다른 운영자가 배치를 바꿔 초기화를 되돌리지 않았습니다.";

    /// <summary>초기화 되돌리기 — 항목이 일괄 쓰기 상한(1000)을 넘는다.</summary>
    public const string LayoutResetTooLargeBar = "되돌릴 부대가 너무 많아(1000 초과) 초기화를 되돌리지 않았습니다.";

    /// <summary>초기화 되돌리기 — 되돌릴 부대가 하나도 편제에 남지 않았다.</summary>
    public const string LayoutResetNothingToRestoreBar = "되돌릴 부대가 편제에 남아 있지 않습니다.";

    /// <summary>[이 부대 배치 초기화] — 옮긴 적이 없다.</summary>
    public static string NodeNotMovedStatus(string unitName) => KoreanParticles.Resolve($"{Quote(unitName)}은(는) 옮긴 적이 없습니다.");

    /// <summary>쓰기 실패 사유(배치).</summary>
    public static string LayoutFailureReason(Ironwall.Dotnet.Libraries.Devices.Api.Services.UnitLayoutFailureKind kind) => kind switch
    {
        Ironwall.Dotnet.Libraries.Devices.Api.Services.UnitLayoutFailureKind.Unauthorized => "로그인이 만료되었습니다(401).",
        Ironwall.Dotnet.Libraries.Devices.Api.Services.UnitLayoutFailureKind.Forbidden => "권한이 없습니다(403).",
        Ironwall.Dotnet.Libraries.Devices.Api.Services.UnitLayoutFailureKind.Unreachable => "서버에 연결하지 못했습니다.",
        Ironwall.Dotnet.Libraries.Devices.Api.Services.UnitLayoutFailureKind.Server => "서버 오류입니다.",
        Ironwall.Dotnet.Libraries.Devices.Api.Services.UnitLayoutFailureKind.Parse => "응답을 읽지 못했습니다.",
        Ironwall.Dotnet.Libraries.Devices.Api.Services.UnitLayoutFailureKind.PreconditionRequired => "버전 확인이 빠진 요청이었습니다(428).",
        _ => "잠시 후 다시 시도하세요.",
    };

    /// <summary>배치 쓰기가 서버 규칙에 맞지 않았다(422 — 재시도 유도 금지).</summary>
    public const string LayoutRejectedReason = "서버 규칙에 맞지 않아 거절됐습니다.";

    /// <summary>검색 — 일치 없음(조정자 필수 항목 2).</summary>
    public static string SearchNoMatch(string query)
        => KoreanParticles.Resolve($"{Quote(query.Trim())}과(와) 일치하는 부대가 없습니다.");

    /// <summary>M 위치 이동 모드 안내(<c>Units.Map.MoveMode</c>).</summary>
    public static string MoveModeStatus(string unitName)
        => $"{Quote(unitName)} 위치 이동 — 화살표 20 · Shift+화살표 5 · Enter 확정 · Esc 취소";

    /// <summary>Alt+↑ — 이미 최상위.</summary>
    public static string AlreadyTopStatus(string unitName) => KoreanParticles.Resolve($"{Quote(unitName)}은(는) 이미 최상위 부대입니다.");

    /// <summary>Alt+↑ — 상위의 상위가 없다(최상위로 올리기는 편제 트리에서).</summary>
    public static string NoGrandParentStatus(string unitName)
        => KoreanParticles.Resolve($"{Quote(unitName)}을(를) 최상위로 올리는 것은 편제 트리에서 합니다.");

    /// <summary>Alt+↓ — 바로 앞에 받을 상위가 없다.</summary>
    public static string NoParentCandidateBelowStatus(string unitName)
        => KoreanParticles.Resolve($"{Quote(unitName)}을(를) 받을 수 있는 상위 부대가 바로 위에 없습니다.");

    #region - 지도 회신 (FR-45) -
    /// <summary>
    /// [지도에서 보기] 회신 — "7중대 장비 16 중 11 을 지도에 표시했습니다 · 5 는 지도에 없음".
    /// </summary>
    public static string MapLocateResult(string title, int shown, int missing, int hidden = 0, int outsideAnchor = 0)
    {
        var total = shown + missing;
        if (shown <= 0) return $"{title} 장비 {total} 중 지도에 보이는 것이 없습니다" + HiddenTail(missing, hidden);

        var text = missing <= 0
            ? $"{title} 장비 {shown} {Particle(shown, "을(를)")} 지도에 표시했습니다"
            : $"{title} 장비 {total} 중 {shown} {Particle(shown, "을(를)")} 지도에 표시했습니다 · {missing} {Particle(missing, "은(는)")} 지도에 없음{HiddenTail(missing, hidden)}";
        if (outsideAnchor > 0) text += $" · {outsideAnchor} {Particle(outsideAnchor, "은(는)")} 사이트 구역 밖";
        return text;
    }

    /// <summary>"지도에 없음" 중 레이어에서 숨긴 수(회신의 <c>Hidden</c> — <c>Missing</c> 에 포함된다).</summary>
    private static string HiddenTail(int missing, int hidden)
        => hidden > 0 && missing > 0 ? string.Create(CultureInfo.InvariantCulture, $"(레이어에서 숨김 {Math.Min(hidden, missing)})") : string.Empty;
    #endregion

    #region - 확인 오버레이 (FR-32 · SB S6 · S7) -
    /// <summary>상위 바꾸기 확인 — 옮길 부대 · 새 상위 · 함께 옮겨지는 예하 수.</summary>
    public static UnitMapConfirmText ConfirmReparent(UnitTreeModel tree, int movingId, int targetId, bool showParentPath = false)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var moving = tree.Find(movingId);
        var target = tree.Find(targetId);
        var lines = new List<string>
        {
            KoreanParticles.Resolve(
                $"{Quote(moving?.Name)}({EchelonOf(moving)})을(를) {Quote(target?.Name)}({EchelonOf(target)}{SuspendedTag(target)}) 밑으로 옮깁니다."),
        };
        if (showParentPath && target is not null) lines.Add($"새 상위 경로: {ParentPath(tree, targetId)}");

        var descendants = tree.DescendantIds(movingId).Select(tree.Find).Where(n => n != null).ToList();
        if (descendants.Count > 0)
        {
            var echelons = descendants.Select(n => UnitDropRules.EchelonTextOf(n!)).Distinct().ToList();
            lines.Add(echelons.Count == 1
                ? $"예하 {echelons[0]} {descendants.Count}곳이 함께 옮겨집니다."
                : $"예하 {descendants.Count}곳이 함께 옮겨집니다.");
        }
        lines.Add("서버에 바로 저장됩니다 · 되돌리기 1회 가능");
        return new UnitMapConfirmText("상위 부대 바꾸기", lines, "옮기기");
    }

    /// <summary>인접 연결 확인 — 무방향 · 보내기 직전 재조회를 알린다.</summary>
    public static UnitMapConfirmText ConfirmAdjoin(UnitTreeModel tree, int unitId, int otherId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var lines = new[]
        {
            KoreanParticles.Resolve($"{Quote(tree.Find(unitId)?.Name)}과(와) {Quote(tree.Find(otherId)?.Name)}{SuspendedTag(tree.Find(otherId))}을(를) 인접 부대로 잇습니다."),
            "양쪽 상세에 함께 표시됩니다(무방향).",
            "보내기 직전 최신 인접 목록을 다시 읽습니다",
        };
        return new UnitMapConfirmText("인접 부대 연결", lines, "연결");
    }
    #endregion

    #region - v1.3 추가 문구 -
    /// <summary>대상이 운용 중지면 확인 문구에 붙는 표지(ISSUE-16).</summary>
    public const string SuspendedMark = "(운용 중지 부대)";

    private static string SuspendedTag(UnitTreeNode? node) => node is { IsEnable: false } ? " " + SuspendedMark : string.Empty;

    /// <summary>부대 → 조상 경로("3대대 › 1연대 › 제○○사단").</summary>
    public static string ParentPath(UnitTreeModel tree, int unitId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var names = new List<string>();
        var cursor = tree.Find(unitId);
        var guard = 0;
        while (cursor is not null && guard++ <= tree.Count)
        {
            names.Add(cursor.Name);
            cursor = cursor.ParentId is int p ? tree.Find(p) : null;
        }
        return string.Join(" › ", names);
    }

    /// <summary>캔버스 밖 · 오버레이 위에서 놓아 끌기를 취소했다(조정자 결정 · ISSUE-13 — 캔버스가 부른다).</summary>
    public const string DropOutsideCancelled = "관계도 밖에 놓아 취소했습니다";

    /// <summary>콘솔이 앞선 저장 중 — 확인 오버레이의 [확정]을 끄는 까닭(ISSUE-23).</summary>
    public const string ConsoleBusyStatus = "앞선 저장이 끝나면 확정할 수 있습니다";

    /// <summary>인접선을 끈 채 인접을 이었다 — 새 선이 보이지 않는다(ISSUE-55). 막대 단추는 <see cref="ShowAdjacencyAction"/>.</summary>
    public const string AdjacencyHiddenNote = " 인접선이 꺼져 있어 보이지 않습니다.";

    /// <summary>인접선 켜기 단추 글(막대 오른쪽).</summary>
    public const string ShowAdjacencyAction = "인접선 켜기";

    /// <summary>지도 · 다른 창에서 온 이동 요청을 상세 가드가 막았다(ISSUE-43).</summary>
    public static string RevealBlockedBand(string unitName)
        => KoreanParticles.Resolve($"적용하지 않은 변경이 있어 {Quote(unitName)}(으)로 이동하지 않았습니다 — [적용] 또는 [되돌리기] 후 다시 하세요.");

    /// <summary>상위 되돌리기 성공 — 배치(Δ)는 되살리지 않는다(결정 D-2026-09-27-215b6d).</summary>
    public static string ReparentUndoneWithLayoutBar(string unitName)
        => KoreanParticles.Resolve($"{Quote(unitName)}의 상위 변경을 되돌렸습니다 — 배치는 자동 배치로 돌아갑니다.");

    /// <summary>
    /// 편제 쓰기 실패의 상태별 사유(FR-34 · ISSUE-25 · 6). 서버 원문은 붙이지 않는다(로그로만).
    /// </summary>
    /// <param name="errorCode">서버 오류 코드(<c>ApiErrorCodes</c>) — 없으면 일반 사유.</param>
    public static string OrgFailureReason(string? errorCode) => errorCode switch
    {
        "UNAUTHORIZED" or "SESSION_REVOKED" => "로그인이 만료되었습니다(401) — 다시 로그인하세요.",
        "FORBIDDEN" => "권한이 없습니다(403).",
        "NOT_FOUND" => "다른 곳에서 삭제된 부대입니다(404).",
        "VALIDATION_ERROR" or "CONFLICT" or "BAD_REQUEST" => "서버 규칙에 맞지 않아 거절됐습니다.",
        "PRECONDITION_REQUIRED" => "버전 확인이 빠진 요청이었습니다(428).",
        "GATEWAY_TIMEOUT" => "결과를 확인하지 못했습니다 — 편제를 다시 읽어 실제 상태로 보입니다.",
        "SERVICE_UNAVAILABLE" or "BAD_GATEWAY" => "서버 연결을 확인하세요.",
        "INTERNAL_ERROR" => "서버 오류입니다 — 잠시 후 다시 시도하세요.",
        _ => "잠시 후 다시 시도하세요.",
    };

    /// <summary>M 모드 — 선택 없이 M 을 눌렀다(FR-37 경계 표).</summary>
    public const string MoveModeNeedsSelection = "옮길 부대를 먼저 고르세요";

    /// <summary>M 모드 중 그 부대가 다른 곳에서 지워졌다(FR-37 경계 표).</summary>
    public const string MoveModeUnitDeleted = "다른 곳에서 삭제된 부대입니다";

    /// <summary>배치 알림이 오래(30초) 미뤄지고 있다 — 한 번만 알린다(ISSUE-9).</summary>
    public const string LayoutNoticeDeferredStatus = "다른 운영자가 배치를 바꿨습니다 — 지금 하던 일이 끝나면 반영합니다";

    /// <summary>전체 보기가 최소 배율에서도 넘친다(FR-15 · ISSUE-39).</summary>
    public const string FitOverflowStatus = "전체가 한 화면에 들지 않습니다 — 검색 · 미니맵으로 이동하세요";

    /// <summary>부대 0 — 관계도 빈 상태(FR-15).</summary>
    public const string EmptyMapStatus = "표시할 부대가 없습니다";

    /// <summary>되돌리기 가능한 초기화의 상한(S-1 ③) — 넘으면 확인 문구가 미리 알린다(ISSUE-50).</summary>
    public const string ResetUndoUnavailableLine = "배치가 1000곳을 넘어 이번 초기화는 되돌릴 수 없습니다.";
    #endregion

    #region - 도움 -
    private static string Quote(string? name) => $"‘{name ?? string.Empty}’";

    private static string EchelonOf(UnitTreeNode? node) => node == null ? "?" : UnitDropRules.EchelonTextOf(node);

    private static string YesNo(bool value) => value ? "예" : "아니오";

    /// <summary>숫자 뒤 조사 하나 — "11" + "을(를)" → "을". 숫자와 조사 사이를 띄워 쓰는 문구(FR-45)용.</summary>
    private static string Particle(int number, string dual)
    {
        var digits = number.ToString(CultureInfo.InvariantCulture);
        return KoreanParticles.Resolve(digits + dual)[digits.Length..];
    }
    #endregion
}
