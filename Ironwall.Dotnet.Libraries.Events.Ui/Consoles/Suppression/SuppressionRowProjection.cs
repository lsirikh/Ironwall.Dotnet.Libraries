using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 억제 스케줄 목록 행의 콘솔 투영 — 상태를 '색'이 아니라 '형태 + 글자'로.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 상태 표시의 <b>형태</b>. 라이트 테마에서 Primary · Selection · Focus 가 대비 1.00:1 로 완전히 같아
/// 색만으로는 구분되지 않는다(규칙 <c>drag-first-ux.md</c> §시각 피드백) — 그래서 모양이 다르다.
/// </summary>
public enum SuppressionStatusShape
{
    /// <summary>지금 억제 중 — 꽉 찬 동그라미.</summary>
    Suppressing,
    /// <summary>유효기간 안이지만 지금 회차는 아님 — 가운데가 빈 동그라미.</summary>
    InWindow,
    /// <summary>아직 시작 전 — 점선 동그라미.</summary>
    Scheduled,
    /// <summary>기간이 끝남 — 가로 줄.</summary>
    Ended,
    /// <summary>취소됨 — ✕.</summary>
    Cancelled,
}

/// <summary>상태 두 축(생애주기 <c>status</c> · 지금 억제 중 <c>is_suppressing_now</c>)을 한 형태로 접는다.</summary>
public static class SuppressionStatusView
{
    /// <summary>
    /// 형태를 고른다.
    /// </summary>
    /// <param name="status">서버 파생 상태 — pending / active / expired / cancelled.</param>
    /// <param name="isSuppressingNow">
    /// 지금 이 순간 억제 중인가. <c>status=="active"</c> 와 <b>다른 축</b>이다 —
    /// 주간 반복 창은 유효기간의 대부분을 active 이면서 미억제로 보낸다.
    /// </param>
    public static SuppressionStatusShape Resolve(string? status, bool isSuppressingNow)
    {
        if (string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)) return SuppressionStatusShape.Cancelled;
        if (string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase)) return SuppressionStatusShape.Ended;
        if (isSuppressingNow) return SuppressionStatusShape.Suppressing;
        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) return SuppressionStatusShape.InWindow;
        return SuppressionStatusShape.Scheduled;
    }

    /// <summary>형태에 붙는 글자 — 아이콘만으로 읽게 두지 않는다.</summary>
    public static string Label(SuppressionStatusShape shape) => shape switch
    {
        SuppressionStatusShape.Suppressing => "억제중",
        SuppressionStatusShape.InWindow => "진행중",
        SuppressionStatusShape.Scheduled => "예정",
        SuppressionStatusShape.Ended => "종료",
        _ => "취소",
    };

    /// <summary>MaterialDesign <c>PackIconKind</c> 이름. 모양이 서로 확실히 다른 것만 고른다.</summary>
    public static string IconName(SuppressionStatusShape shape) => shape switch
    {
        SuppressionStatusShape.Suppressing => "Circle",
        SuppressionStatusShape.InWindow => "CircleOutline",
        SuppressionStatusShape.Scheduled => "ClockOutline",
        SuppressionStatusShape.Ended => "Minus",
        _ => "Close",
    };

    /// <summary>필터 칩 키 — 정본 SB L2313 (전체 · 억제중 · 진행중 · 예정).</summary>
    public const string FilterAll = "all";
    public const string FilterSuppressing = "suppressing";
    public const string FilterActive = "active";
    public const string FilterPending = "pending";
    /// <summary>종료 — 정본에는 없지만 <b>정리(일괄 하드삭제)의 대상을 찾는 유일한 길</b>이다.</summary>
    public const string FilterExpired = "expired";
    /// <summary>취소 — 같은 이유.</summary>
    public const string FilterCancelled = "cancelled";
    /// <summary>
    /// 끝난 것(종료 + 취소) — <b>정리(일괄 하드삭제)의 대상 집합 그대로</b>다.
    /// <para>칩을 둘로 나누면 툴바가 넘치고(폭 실측), 운용자가 실제로 하고 싶은 일은
    /// "지울 수 있는 것만 보여 줘" 하나다. 서버 status 는 한 값만 받으므로 전량 받아 클라에서 좁힌다.</para>
    /// </summary>
    public const string FilterTerminal = "terminal";

    /// <summary>필터 칩이 서버 <c>status</c> 파라미터로 번역되는가 — 되면 그 값, 아니면 null(전량 받아 걸러 낸다).</summary>
    public static string? ServerStatusFor(string? filterKey) => filterKey switch
    {
        FilterActive => "active",
        FilterPending => "pending",
        FilterExpired => "expired",
        FilterCancelled => "cancelled",
        // 종료 + 취소 두 값은 한 번의 status 로 못 부른다 — 전량 받아 아래에서 좁힌다.
        FilterTerminal => null,
        // '억제중' 은 서버 status 가 아니라 is_suppressing_now 다 — active 를 받아 클라에서 좁힌다.
        FilterSuppressing => "active",
        _ => null,
    };

    /// <summary>그 행이 필터에 걸리는가(클라 쪽 좁히기).</summary>
    public static bool Matches(string? filterKey, SuppressionStatusShape shape) => filterKey switch
    {
        FilterSuppressing => shape == SuppressionStatusShape.Suppressing,
        FilterActive => shape is SuppressionStatusShape.Suppressing or SuppressionStatusShape.InWindow,
        FilterPending => shape == SuppressionStatusShape.Scheduled,
        FilterExpired => shape == SuppressionStatusShape.Ended,
        FilterCancelled => shape == SuppressionStatusShape.Cancelled,
        FilterTerminal => shape is SuppressionStatusShape.Ended or SuppressionStatusShape.Cancelled,
        _ => true,
    };
}

/// <summary>
/// 콘솔 목록의 억제 스케줄 행.
/// </summary>
/// <remarks>
/// 기존 행 뷰모델(<see cref="EventSuppressionScheduleItemViewModel"/>)을 <b>물려받아</b> 쓴다 —
/// 대상 요약 · 시간창 표기 · 취소 가능 · 하드삭제 가능 판정을 두 벌로 만들지 않는다.
/// 콘솔이 더 필요로 하는 것(원본 DTO · 상태 형태)만 여기서 더한다.
/// </remarks>
public sealed class SuppressionConsoleRow : EventSuppressionScheduleItemViewModel
{
    public SuppressionConsoleRow(EventSuppressionScheduleDto dto,
                                 DeviceProvider? deviceProvider,
                                 DeviceGroupProvider? groupProvider,
                                 Action? onSelectionChanged = null)
        : base(dto, deviceProvider, groupProvider, onSelectionChanged)
    {
        Dto = dto ?? throw new ArgumentNullException(nameof(dto));
        Shape = SuppressionStatusView.Resolve(dto.Status, IsSuppressingNow);
    }

    /// <summary>서버 원본. 수정(PATCH)은 <b>이 값에서 다시 채워</b> 만든다 — 빈 DTO 는 값을 지운다.</summary>
    public EventSuppressionScheduleDto Dto { get; }

    /// <summary>상태 형태.</summary>
    public SuppressionStatusShape Shape { get; }

    /// <summary>상태 글자 — 형태 옆에 반드시 같이 낸다.</summary>
    public string ShapeLabel => SuppressionStatusView.Label(Shape);

    /// <summary>상태 아이콘 이름.</summary>
    public string ShapeIconName => SuppressionStatusView.IconName(Shape);

    /// <summary>반복 요약이 있으면 그것, 없으면 단발 표기.</summary>
    public string RepeatText => string.IsNullOrEmpty(RecurrenceSummary) ? "단발" : RecurrenceSummary;

    /// <summary>
    /// 회차 줄을 낼 것인가 — 비어 있는데 자리를 잡으면 반복 칸이 두 줄 높이가 되어
    /// "단발" 이 옆 칸보다 위로 떠 보인다(실창 캡처).
    /// </summary>
    public bool HasOccurrence => !string.IsNullOrEmpty(OccurrenceText);

    /// <summary>목록 열은 좀다 — 해가 아니라 월·일부터 보인다(전체 값은 툴팁 · 상세 칸에 그대로 있다).</summary>
    public string WindowStartShort => Shorten(WindowStartText);

    /// <summary>종료 짧은 표기. '무제한' 은 그대로 남긴다.</summary>
    public string WindowEndShort => Shorten(WindowEndText);

    private static string Shorten(string text)
        => text.Length >= 16 && text[4] == '-' ? text[5..] : text;

    /// <summary>
    /// 억제 범위 · 감지/감시 한 줄. 범위 문구는 <see cref="SuppressionRequestBuilder.ScopeLabel"/> 단일 정본을 쓴다 —
    /// 기반 클래스의 표는 <c>operation</c>(서버 5번째 값)을 모른다.
    /// </summary>
    public string ScopeDetailText => Dto.TargetType == SuppressionTargetDrop.ModeDevice
        ? SuppressionRequestBuilder.ScopeLabel(Dto.EventScope)
        : $"{SuppressionRequestBuilder.ScopeLabel(Dto.EventScope)} · {SuppressionRequestBuilder.SideLabel(Dto.TargetSide)}";

    /// <summary>
    /// 목록 '범위' 칸 — 억제 범위 <b>하나만</b>(전체 · 탐지 · 장애 · 연결 · 운영).
    /// </summary>
    /// <remarks>
    /// 예전 목록 칸은 <see cref="ScopeDetailText"/>(범위 · 감지/감시)를 실었는데, 전체 대상 행은 '대상' 칸도
    /// "전체 · 감지+감시" 라 같은 글이 두 칸에 두 번 찍혔고, 그 두 칸이 넓게 자리를 잡아 작업명이 늘 잘렸다(실창 검토 #22).
    /// 감지/감시는 대상의 성질이다 — <see cref="TargetListText"/> 로 옮긴다. 상세 칸은 그대로 <see cref="ScopeDetailText"/> 를 쓴다.
    /// </remarks>
    public string ScopeListText => SuppressionRequestBuilder.ScopeLabel(Dto.EventScope);

    /// <summary>
    /// 목록 '대상' 칸 — 대상 요약. 그룹 대상은 감지/감시를 뒤에 붙인다(전체 대상은 요약에 이미 들어 있다 · 장비는 해당 없음).
    /// </summary>
    public string TargetListText => Dto.TargetType == SuppressionTargetDrop.ModeGroup && TargetSummary != NoTargetsText
        ? $"{TargetSummary} · {SuppressionRequestBuilder.SideLabel(Dto.TargetSide)}"
        : TargetSummary;

    /// <summary>범위 칸 툴팁 — 화면이 모르는 범위면 서버가 보낸 원문을 여기에만 덧붙인다.</summary>
    public string ScopeToolTip => SuppressionRequestBuilder.IsKnownScope(Dto.EventScope) || Dto.EventScope is null
        ? ScopeDetailText
        : $"{ScopeDetailText} (원문: {Dto.EventScope})";
}
