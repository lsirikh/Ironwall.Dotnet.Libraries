namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>
/// 부품 한 줄 — 선언(형상 축) + 관측(상태 축) + 설정(의도 축)을 한글로 편 <b>읽기 전용</b> 값. 장비 콘솔 표 · 지도 카드 ·
/// 상세 보기 부품 탭 · 툴팁이 같은 줄을 쓴다.
/// </summary>
/// <param name="Key">장비 안에서만 유일한 부품 key(계약 아님 — 자동화 식별자 · 보조 표시용).</param>
/// <param name="Type">카탈로그 유형 코드. 선언 없이 관측만 온 key 는 빈 문자열.</param>
/// <param name="Name">표시 이름 — label → 카탈로그 한글 → 내장 사전 → key.</param>
/// <param name="TypeName">유형 한글(선언 절의 "종류" 칸).</param>
/// <param name="DeclaredIndex">선언 순서(0부터). 관측만 온 줄은 선언 수 뒤로 이어 붙인다.</param>
/// <param name="IsDeclared">형상 축에 선언된 부품인가.</param>
/// <param name="StateCode">동작 상태 원문. 상태 축이 없는 유형 · 관측 없음이면 null.</param>
/// <param name="Health">건강 단계. 관측이 없으면 <see cref="ComponentHealthLevel.Unknown"/>.</param>
/// <param name="HealthRaw">건강 원문(어휘 밖 값을 괄호로 보이기 위해 보존).</param>
/// <param name="FaultCode">고장 사유 원문.</param>
/// <param name="InService">false 면 "달려 있지만 쓰지 않는 부품" — 건강 집계에서 뺀다.</param>
/// <param name="IsObserved">관측 축에 이 key 의 항목이 있었는가.</param>
/// <param name="ObservedAt">관측 시각 원문(마지막 "변화" 시각 — 워치독이 없다).</param>
/// <param name="IntentEnabled">설정 <c>component_overrides.&lt;key&gt;.enabled</c>. 없으면 null.</param>
/// <param name="Channel">선언의 채널.</param>
/// <param name="Position">선언의 위치.</param>
public sealed record ComponentRowInfo(
    string Key,
    string Type,
    string Name,
    string TypeName,
    int DeclaredIndex,
    bool IsDeclared,
    string? StateCode,
    ComponentHealthLevel Health,
    string? HealthRaw,
    string? FaultCode,
    bool InService,
    bool IsObserved,
    string? ObservedAt,
    bool? IntentEnabled,
    int? Channel = null,
    string? Position = null)
{
    /// <summary>상태 칸 — 설정과 관측이 다르면 <c>설정 켬 / 관측 꺼짐</c>, 없으면 "—".</summary>
    public string StateText => ComponentDisplay.StateWithIntent(StateCode, IntentEnabled);

    /// <summary>설정과 관측이 어긋난다(상태 칸을 경고 글자색으로).</summary>
    public bool IsIntentMismatch => InService && ComponentDisplay.IsIntentMismatch(StateCode, IntentEnabled);

    /// <summary>건강 칸 — 사용 안 함이면 "사용 안 함", 관측이 없으면 "미상", 어휘 밖이면 "미상 (원문)".</summary>
    public string HealthText => !InService ? ComponentDisplay.OutOfServiceText
        : IsObserved ? ComponentDisplay.HealthText(HealthRaw) : ComponentDisplay.HealthName(ComponentHealthLevel.Unknown);

    /// <summary>건강 점 분류.</summary>
    public ComponentHealthKind HealthKind => ComponentDisplay.KindOf(Health, InService);

    /// <summary>사유 칸 — 고장 · 저하일 때만. 없으면 빈 글(칸을 비운다).</summary>
    public string FaultText => InService && Health is ComponentHealthLevel.Fault or ComponentHealthLevel.Degraded
        ? ComponentDisplay.FaultName(FaultCode) ?? string.Empty
        : string.Empty;

    /// <summary>가동 중인가(켜짐 · 구동 중 · 추적 중) — 지도 칸 줄의 채운 칸.</summary>
    public bool IsActive => InService && ComponentDisplay.IsActiveState(StateCode);

    /// <summary>
    /// 고장 먼저 순서 — 고장 0 · 저하 1 · 나머지(정상 · 미상) 2 · 사용 안 함 9. 정상과 미상은 가르지 않고 선언 순서를 지킨다
    /// (스토리보드 §A 조립 카드: 레이더(고장) → EO 카메라 → 진동 감지부 → PIR 감지부(미상)).
    /// </summary>
    public int FaultFirstRank => !InService ? 9 : Health switch
    {
        ComponentHealthLevel.Fault => 0,
        ComponentHealthLevel.Degraded => 1,
        _ => 2,
    };

    /// <summary>마지막 변화 칸(오늘이면 시:분:초).</summary>
    public string ObservedTimeText(DateTime? today) => ComponentDisplay.ObservedTimeText(ObservedAt, today);

    /// <summary>
    /// 한 줄 요약 — <c>히터: 고장 · 과열 · 켜짐</c>. 사용 안 함이면 <c>히터: 사용 안 함</c>, 관측이 없으면 <c>히터: 미수신</c>.
    /// </summary>
    public string ToLine()
    {
        if (!InService) return $"{Name}: {ComponentDisplay.OutOfServiceText}";
        if (!IsObserved) return $"{Name}: 미수신";

        var parts = new List<string>(3) { HealthText };
        if (FaultText.Length > 0) parts.Add(FaultText);
        var state = StateCode is null && IntentEnabled is null ? null : StateText;
        if (!string.IsNullOrEmpty(state) && state != ComponentDisplay.Dash) parts.Add(state);
        return $"{Name}: {string.Join(" · ", parts)}";
    }
}
