using System.Globalization;

namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>
/// 부품 어휘(유형 · 상태 · 건강 · 고장 사유) → 화면 한글의 <b>단일 정본</b>(component-display-unify FR-01).
/// 지도(아이콘 칸 줄 · 조립 카드 · 상세 보기 부품 탭 · 툴팁)와 장비 콘솔(상세 "부품 · 부품 상태" 절 · "부품으로 찾기")이
/// 모두 이 표 하나를 읽는다 — 같은 부품을 두 화면이 다르게 말하던 네 군데(모름 · RUNNING · 이름 · 순서)를 하나로 합친 자리다.
/// </summary>
/// <remarks>
/// <para><b>어휘의 출처</b>: 서버 카탈로그 <c>api-test-server app/utils/init_component_catalog.py</c>(유형 32 · 상태 10 · 사유 6).
/// 유형 이름은 서버 카탈로그 한글이 이기고(<see cref="IComponentTypeLabels"/>), 없을 때만 여기 내장 사전을 쓴다.
/// 상태 · 사유 · 건강은 화면 말투를 맞추려고 여기 표가 정본이다(서버의 RUNNING="동작 중"이 아니라 "구동 중",
/// POWER_LOSS="전원 상실"이 아니라 "전원 끊김" — 스토리보드 §A 합친 뒤 표).</para>
/// <para><b>어휘 밖 값은 숨기지 않는다</b>: "알 수 없음 (원문)" · "미상 (원문)"처럼 원문을 괄호로 붙인다 —
/// 새 어휘가 와도 빈칸이 되지 않고, 운영자가 무엇이 왔는지 읽을 수 있다.</para>
/// </remarks>
public static class ComponentDisplay
{
    /// <summary>어휘 밖 상태 · 사유의 머리 글.</summary>
    public const string UnknownText = "알 수 없음";
    /// <summary>축 미수신(6.3 서버 · 부품 축 미수신) — 그리지 않고 이 한 줄만 적는다(FR-08).</summary>
    public const string NoInfoText = "부품 정보 없음";
    /// <summary>사용 중인 부품이 전부 정상이다.</summary>
    public const string NoIssueText = "부품 이상 없음";
    /// <summary>선언된 부품이 하나도 없다.</summary>
    public const string NoComponentsText = "부품 없음";
    /// <summary><c>in_service=false</c> — 달려 있지만 쓰지 않는 부품.</summary>
    public const string OutOfServiceText = "사용 안 함";
    /// <summary>값이 없는 칸.</summary>
    public const string Dash = "—";

    // ── 유형 32종 — 서버 카탈로그 한글과 같다(카탈로그를 못 읽을 때의 폴백) ──
    private static readonly IReadOnlyDictionary<string, string> TypeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DOOR_SENSOR"] = "도어 센서",
        ["DOOR_LOCK"] = "도어 잠금",
        ["DOOR_ACTUATOR"] = "문 구동부",
        ["LIMIT_SWITCH"] = "리미트 스위치",
        ["TEMPERATURE_SENSOR"] = "온도 센서",
        ["HUMIDITY_SENSOR"] = "습도 센서",
        ["VOLTAGE_SENSOR"] = "전압 센서",
        ["CURRENT_SENSOR"] = "전류 센서",
        ["VIBRATION_METER"] = "진동 계측기",
        ["UPS"] = "무정전 전원장치",
        ["HEATER"] = "히터",
        ["FAN"] = "팬",
        ["HEADLIGHT"] = "전조등",
        ["PIR_SENSOR"] = "PIR 센서",
        ["ULTRASONIC_SENSOR"] = "초음파 센서",
        ["RADAR_UNIT"] = "레이더",
        ["VIBRATION_SENSOR"] = "진동 센서",
        ["OPTICAL_FIBER_SENSOR"] = "광케이블 감지부",
        ["THERMAL_CAMERA"] = "열영상 카메라",
        ["EO_CAMERA"] = "EO 카메라",
        ["THERMAL_SENSOR"] = "열화상 센서",
        ["CONTACT_INPUT"] = "접점 입력",
        ["LAMP_LIGHT"] = "경광등",
        ["BUZZER"] = "부저",
        ["AMPLIFIER"] = "앰프",
        ["MIC"] = "마이크",
        ["PTZ_UNIT"] = "PTZ 구동부",
        ["IR_LED"] = "적외선 LED",
        ["WIPER"] = "와이퍼",
        ["OPTICAL_LENS"] = "광학 렌즈",
        ["TRACKER"] = "트래커",
        ["NETWORK_INTERFACE"] = "네트워크 인터페이스",
    };

    // ── 상태 10종 — 서버 component_state 어휘(건강 단어는 상태 어휘에 없다 — 서버 D8) ──
    private static readonly IReadOnlyDictionary<string, string> StateNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["IDLE"] = "대기",
        ["RUNNING"] = "구동 중",
        ["ON"] = "켜짐",
        ["OFF"] = "꺼짐",
        ["OPEN"] = "열림",
        ["CLOSED"] = "닫힘",
        ["LOCKED"] = "잠김",
        ["UNLOCKED"] = "풀림",
        ["ACTIVE"] = "추적 중",
        ["LOST"] = "놓침",
    };

    // ── 고장 사유 6종 — 서버 component_fault 어휘 ──
    private static readonly IReadOnlyDictionary<string, string> FaultNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SENSOR_TIMEOUT"] = "센서 응답 없음",
        ["OVER_CURRENT"] = "과전류",
        ["OVER_TEMP"] = "과열",
        ["COMM_ERROR"] = "통신 오류",
        ["POWER_LOSS"] = "전원 끊김",
        ["ETC"] = "기타",
    };

    /// <summary>"가동"으로 보는 상태 — 지도 칸 줄에서 채운 칸으로 그린다(켜짐 · 구동 중 · 추적 중).</summary>
    private static readonly HashSet<string> ActiveStates = new(StringComparer.OrdinalIgnoreCase) { "ON", "RUNNING", "ACTIVE" };

    /// <summary>내장 사전의 유형 코드들(시험 · 전수 대조용).</summary>
    public static IEnumerable<string> KnownTypes => TypeNames.Keys;
    /// <summary>내장 사전의 상태 코드들.</summary>
    public static IEnumerable<string> KnownStates => StateNames.Keys;
    /// <summary>내장 사전의 고장 사유 코드들.</summary>
    public static IEnumerable<string> KnownFaultReasons => FaultNames.Keys;

    #region - 건강 -
    /// <summary>서버 건강 문자열(<c>OK · DEGRADED · FAULT · UNKNOWN</c>, 대소문자 무관) → 단계. 비었거나 모르는 값은 <see cref="ComponentHealthLevel.Unknown"/>.</summary>
    public static ComponentHealthLevel ParseHealth(string? health)
    {
        if (string.IsNullOrWhiteSpace(health)) return ComponentHealthLevel.Unknown;
        return health.Trim().ToUpperInvariant() switch
        {
            "OK" => ComponentHealthLevel.Ok,
            "DEGRADED" => ComponentHealthLevel.Degraded,
            "FAULT" => ComponentHealthLevel.Fault,
            _ => ComponentHealthLevel.Unknown,
        };
    }

    /// <summary>건강 단계 한글 — 정상 · 저하 · 고장 · 미상. <see cref="ComponentHealthLevel.None"/> 은 "—".</summary>
    public static string HealthName(ComponentHealthLevel health) => health switch
    {
        ComponentHealthLevel.Ok => "정상",
        ComponentHealthLevel.Degraded => "저하",
        ComponentHealthLevel.Fault => "고장",
        ComponentHealthLevel.Unknown => "미상",
        _ => Dash,
    };

    /// <summary>
    /// 서버 건강 원문 → 한글. 비었으면 "미상", 어휘 밖이면 "미상 (원문)" — 어휘 밖 값을 거짓 고장이나 거짓 정상으로 바꾸지 않되 원문은 보인다.
    /// </summary>
    public static string HealthText(string? health)
    {
        var level = ParseHealth(health);
        var name = HealthName(level);
        return level == ComponentHealthLevel.Unknown && IsOutOfVocabularyHealth(health) ? $"{name} ({health!.Trim()})" : name;
    }

    /// <summary>건강 원문이 서버 4값 어휘 밖인가(빈 값은 어휘 밖이 아니다 — 그냥 "미상").</summary>
    public static bool IsOutOfVocabularyHealth(string? health)
        => !string.IsNullOrWhiteSpace(health)
           && health.Trim().ToUpperInvariant() is not ("OK" or "DEGRADED" or "FAULT" or "UNKNOWN");

    /// <summary>건강 점 분류. 사용 안 함이면 건강과 무관하게 <see cref="ComponentHealthKind.OutOfService"/>.</summary>
    public static ComponentHealthKind KindOf(ComponentHealthLevel health, bool inService = true)
    {
        if (!inService) return ComponentHealthKind.OutOfService;
        return health switch
        {
            ComponentHealthLevel.Ok => ComponentHealthKind.Ok,
            ComponentHealthLevel.Degraded => ComponentHealthKind.Warn,
            ComponentHealthLevel.Fault => ComponentHealthKind.Crit,
            _ => ComponentHealthKind.Unknown,
        };
    }
    #endregion

    #region - 상태 · 사유 -
    /// <summary>
    /// 동작 상태 원문 → 한글. 비었으면 <c>null</c>(상태 축이 없는 유형 — "꺼짐"이 아니다), 어휘 밖이면 "알 수 없음 (원문)".
    /// </summary>
    public static string? StateName(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        var trimmed = state.Trim();
        return StateNames.TryGetValue(trimmed, out var name) ? name : $"{UnknownText} ({trimmed})";
    }

    /// <summary>상태 원문이 어휘 안인가.</summary>
    public static bool IsKnownState(string? state) => !string.IsNullOrWhiteSpace(state) && StateNames.ContainsKey(state.Trim());

    /// <summary>가동 상태인가(켜짐 · 구동 중 · 추적 중).</summary>
    public static bool IsActiveState(string? state) => !string.IsNullOrWhiteSpace(state) && ActiveStates.Contains(state.Trim());

    /// <summary>고장 사유 원문 → 한글. 비었으면 <c>null</c>, 어휘 밖이면 "알 수 없음 (원문)".</summary>
    public static string? FaultName(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var trimmed = reason.Trim();
        return FaultNames.TryGetValue(trimmed, out var name) ? name : $"{UnknownText} ({trimmed})";
    }
    #endregion

    #region - 이름 -
    /// <summary>
    /// 부품 유형 한글 이름 — 서버 카탈로그 한글 → 내장 사전 → 코드 그대로. 비었으면 "부품".
    /// 카탈로그 라벨이 코드와 같으면(한글 라벨이 없는 판) 카탈로그를 건너뛴다.
    /// </summary>
    public static string TypeName(string? type, IComponentTypeLabels? catalog = null)
    {
        if (string.IsNullOrWhiteSpace(type)) return "부품";
        return TryTypeName(type, catalog) ?? type.Trim();
    }

    /// <summary>
    /// 부품 이름 규칙 — <b>label → 카탈로그 한글 → 내장 사전 → key</b>. 유형도 key 도 모르면 유형 코드, 그것도 없으면 "부품".
    /// </summary>
    public static string ComponentName(string? label, string? type, string? key, IComponentTypeLabels? catalog = null)
    {
        if (!string.IsNullOrWhiteSpace(label)) return label.Trim();
        if (!string.IsNullOrWhiteSpace(type) && TryTypeName(type, catalog) is { } typeName) return typeName;
        if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
        return string.IsNullOrWhiteSpace(type) ? "부품" : type.Trim();
    }

    private static string? TryTypeName(string type, IComponentTypeLabels? catalog)
    {
        var trimmed = type.Trim();
        string? fromCatalog = null;
        try { fromCatalog = catalog?.TypeLabel(trimmed); }
        catch (Exception) { /* 카탈로그 실패 = 내장 사전으로(FR-07) — 표시 경로는 예외로 죽지 않는다 */ }
        if (!string.IsNullOrWhiteSpace(fromCatalog) && !string.Equals(fromCatalog.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            return fromCatalog.Trim();
        return TypeNames.TryGetValue(trimmed, out var name) ? name : null;
    }
    #endregion

    #region - 설정(의도) · 관측 -
    /// <summary>
    /// 동작 상태 칸 — 설정(<c>component_overrides.&lt;key&gt;.enabled</c>)과 관측(state ON/OFF)을 한 줄에.
    /// 설정이 없으면 관측 한글만, 둘이 같으면 관측 한글만, 다르면 <c>설정 켬 / 관측 꺼짐</c>.
    /// 관측이 없으면 <c>설정 켬 / 관측 없음</c>. 둘 다 없으면 "—".
    /// </summary>
    public static string StateWithIntent(string? observedState, bool? intentEnabled)
    {
        var observed = StateName(observedState);
        if (intentEnabled is not { } intent) return observed ?? Dash;
        if (!IsIntentMismatch(observedState, intentEnabled)) return observed!;
        return $"설정 {(intent ? "켬" : "끔")} / 관측 {observed ?? "없음"}";
    }

    /// <summary>
    /// 설정과 관측이 어긋나는가 — 설정이 있고(켬/끔), 관측이 ON/OFF 가 아니거나 반대일 때.
    /// 관측이 ON/OFF 어휘가 아닌 유형(구동 중 등)에 설정이 있으면 비교하지 않는다(어긋남으로 보지 않는다).
    /// </summary>
    public static bool IsIntentMismatch(string? observedState, bool? intentEnabled)
    {
        if (intentEnabled is not { } intent) return false;
        if (string.IsNullOrWhiteSpace(observedState)) return true;
        var upper = observedState.Trim().ToUpperInvariant();
        return upper switch
        {
            "ON" => !intent,
            "OFF" => intent,
            _ => false,
        };
    }
    #endregion

    #region - 요약 -
    /// <summary>
    /// 절 머리 요약 줄 — <c>고장 1 · 저하 1 · 정상 3</c>. 사용 중인 부품이 전부 정상이면 "부품 이상 없음".
    /// 0 인 단계는 적지 않는다. 사용 안 함이 있으면 끝에 붙인다. 부품이 하나도 없으면 "부품 없음".
    /// </summary>
    public static string SummaryText(int fault, int degraded, int ok, int unknown, int outOfService)
    {
        if (fault + degraded + ok + unknown + outOfService <= 0) return NoComponentsText;

        var parts = new List<string>(5);
        if (fault == 0 && degraded == 0 && unknown == 0 && ok > 0)
            parts.Add(NoIssueText);
        else
        {
            if (fault > 0) parts.Add($"고장 {fault}");
            if (degraded > 0) parts.Add($"저하 {degraded}");
            if (ok > 0) parts.Add($"정상 {ok}");
            if (unknown > 0) parts.Add($"미상 {unknown}");
        }
        if (outOfService > 0) parts.Add($"{OutOfServiceText} {outOfService}");
        return string.Join(" · ", parts);
    }
    #endregion

    #region - 시각 -
    /// <summary>
    /// 관측 시각(서버 ISO-8601 aware 문자열) → "마지막 변화" 칸. 오늘이면 <c>HH:mm:ss</c>, 아니면 <c>MM-dd HH:mm</c>(현지 시각).
    /// 비었으면 "—", 못 읽으면 원문 그대로(지어내지 않는다).
    /// </summary>
    /// <param name="observedAt">서버 원문.</param>
    /// <param name="today">"오늘"의 기준 날짜(현지). null 이면 날짜와 무관하게 <c>MM-dd HH:mm</c>.</param>
    public static string ObservedTimeText(string? observedAt, DateTime? today)
    {
        if (string.IsNullOrWhiteSpace(observedAt)) return Dash;
        if (!DateTimeOffset.TryParse(observedAt.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) return observedAt.Trim();
        var local = at.ToLocalTime();
        return today is { } day && local.Date == day.Date
            ? local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            : local.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>관측 시각 원문을 비교 가능한 값으로. 못 읽으면 null.</summary>
    public static DateTimeOffset? ParseObservedAt(string? observedAt)
        => !string.IsNullOrWhiteSpace(observedAt)
           && DateTimeOffset.TryParse(observedAt.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at : null;
    #endregion
}
