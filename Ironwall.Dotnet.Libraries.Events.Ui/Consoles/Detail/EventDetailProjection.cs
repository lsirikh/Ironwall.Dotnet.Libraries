using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;

/// <summary>상세 칸이 다루는 이벤트 종류.</summary>
public enum EventDetailKind
{
    Detection,
    Malfunction,
    Connection,
    Action,
    /// <summary>개요 레일 — 고를 행이 없다(정본 SB L2691: 머리 "개요" + 빈 본문).</summary>
    Overview,
}

/// <summary>
/// 편집 경계 — <b>사람이 고칠 수 있는 것은 판정뿐</b>이고, 발생 기록 · 측정값 · 작성자는 잠긴다.
/// </summary>
/// <remarks>
/// 정본 window-layout-system-storyboard.html L1084-1095 의 표를 그대로 옮긴 것이다.
/// 특히 탐지 <c>상태</c> 는 지금 콤보로 고칠 수 있으나 <b>저장 요청에 실리지 않아 서버에 반영조차 되지 않는다</b>(L1087)
/// — 그래서 "자동 표시" 로 잠근다.
/// </remarks>
public static class EventDetailProjection
{
    /// <summary>고칠 수 있는 칸 이름(그 종류에 하나씩뿐이다).</summary>
    public const string FieldResult = "result";       // 탐지 — 결과(판정)
    public const string FieldReason = "reason";       // 장애 — 사유(판정)
    public const string FieldContent = "content";     // 조치 — 내용(오탈자 정정)

    private static readonly IReadOnlyDictionary<EventDetailKind, string?> EditableField =
        new Dictionary<EventDetailKind, string?>
        {
            [EventDetailKind.Detection] = FieldResult,
            [EventDetailKind.Malfunction] = FieldReason,
            [EventDetailKind.Connection] = null,      // 전부 발생 기록
            [EventDetailKind.Action] = FieldContent,
        };

    /// <summary>이 종류에서 고칠 수 있는 칸. 없으면 null(전부 읽기).</summary>
    public static string? EditableFieldOf(EventDetailKind kind)
        => EditableField.TryGetValue(kind, out var field) ? field : null;

    /// <summary>이 칸을 고칠 수 있는가. 권한이 없으면 무엇도 고칠 수 없다.</summary>
    public static bool CanEdit(EventDetailKind kind, string field, bool hasEditPermission)
        => hasEditPermission && string.Equals(EditableFieldOf(kind), field, StringComparison.Ordinal);

    /// <summary>잠긴 칸에 붙이는 까닭 한 줄. 잠기지 않았으면 null.</summary>
    public static string? LockReason(EventDetailKind kind, string field, bool hasEditPermission)
    {
        if (!hasEditPermission) return "이벤트 수정 권한이 없습니다.";
        if (CanEdit(kind, field, true)) return null;

        return field switch
        {
            "status" => "조치보고를 하면 자동으로 바뀝니다.",
            "signal" or "ai_model" or "inference" or "frame" => "센서 · AI 가 잰 값이라 수정할 수 없습니다. 틀렸다면 조치보고 메모로 남기세요.",
            "fault_section" => "제어기 선 위의 지점이라 수정할 수 없습니다.",
            "user" => "작성자는 자동으로 붙습니다.",
            _ => "발생 기록은 수정할 수 없습니다.",
        };
    }

    /// <summary>탐지 · 장애의 '상태' 칸에 찍는 글자 — 콤보가 아니라 자동 표시다.</summary>
    public static string StatusText(int actionCount)
        => actionCount > 0 ? $"조치 {actionCount}건" : "미조치";

    /// <summary>주 버튼 문구 — 중복 조치보고가 허용이라 조치가 있어도 꺼지지 않는다.</summary>
    /// <remarks>여러 건이면 한 건씩 창을 띄우지 않고 조치 트레이에 담는다 — 글자도 그 일을 말한다.</remarks>
    public static string ReportButtonText(int selectedCount, int actionCountOfFirst)
        => selectedCount > 1 ? $"{selectedCount}건 트레이에 담기"
         : actionCountOfFirst > 0 ? "조치보고 추가"
         : "조치보고";

    /// <summary>
    /// 장애 고장 구간 글자 — 제어기 루프 위의 <b>지점(정수)</b> 이지 시각이 아니다. 0 은 해당 없음.
    /// </summary>
    public static string FaultSectionText(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        var parts = new List<string>();
        if (firstStart != 0 || firstEnd != 0) parts.Add($"1차 {firstStart}–{firstEnd}");
        if (secondStart != 0 || secondEnd != 0) parts.Add($"2차 {secondStart}–{secondEnd}");
        return parts.Count == 0 ? "—" : string.Join(" · ", parts);
    }

    /// <summary>
    /// 조치 행의 원본 찾기 — <b>Id 와 타입이 둘 다</b> 맞아야 한다. Id 만으로 맞추면 탐지 3번과 장애 3번이 섞인다.
    /// </summary>
    public static T? FindOrigin<T>(IEnumerable<T> candidates, int originId, EventDetailKind originKind,
                                   Func<T, int> idOf, Func<T, EventDetailKind> kindOf) where T : class
        => candidates.FirstOrDefault(c => idOf(c) == originId && kindOf(c) == originKind);

    /// <summary>선택 없음 상태의 한 줄 — 무엇이 열리는가만. 여러 건 고르기 · 트레이 담기는 상세 "?"(<c>Events.Detail</c>).</summary>
    public static string EmptyHint(EventDetailKind kind) => kind switch
    {
        EventDetailKind.Detection => "탐지 행을 고르면 스냅샷 · 판정 · 조치 내역이 열립니다.",
        EventDetailKind.Malfunction => "장애 행을 고르면 사유 · 고장 구간 · 조치 내역이 열립니다.",
        EventDetailKind.Connection => "연결 행을 고르면 장비 정보가 열립니다. 연결 이벤트에는 조치보고가 없습니다.",
        EventDetailKind.Overview => "개요에서는 고를 행이 없습니다. 왼쪽에서 내역을 고르세요.",
        _ => "조치 행을 고르면 원본 이벤트가 함께 열립니다.",
    };

    /// <summary>상세 머리의 윗줄.</summary>
    public static string KindLabel(EventDetailKind kind) => kind switch
    {
        EventDetailKind.Detection => "탐지",
        EventDetailKind.Malfunction => "장애",
        EventDetailKind.Connection => "연결",
        EventDetailKind.Overview => "개요",
        _ => "조치",
    };
}
