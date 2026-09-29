namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 부품 어휘(유형 · 상태 · 건강 · 고장 사유) → 화면용 한글.
/// </summary>
/// <remarks>
/// <para>정본은 서버 카탈로그(<c>api-test-server app/utils/init_component_catalog.py</c> — 유형 32종 · 상태 10종 · 사유 6종)다.
/// 지도는 조립기 전용 카탈로그 리더(<c>IComponentCatalog</c>)를 주입받지 않으므로 같은 한글 이름을 여기 둔다.
/// 모르는 코드는 <b>원문 그대로</b> 보인다 — 새 유형이 와도 빈칸이 되지 않는다.</para>
/// </remarks>
public static class ComponentVocabulary
{
    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly Dictionary<string, string> StateNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ON"] = "켜짐",
        ["OFF"] = "꺼짐",
        ["OPEN"] = "열림",
        ["CLOSED"] = "닫힘",
        ["LOCKED"] = "잠김",
        ["UNLOCKED"] = "풀림",
        ["IDLE"] = "대기",
        ["RUNNING"] = "동작 중",
        ["ACTIVE"] = "추적 중",
        ["LOST"] = "놓침",
    };

    private static readonly Dictionary<string, string> FaultNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SENSOR_TIMEOUT"] = "센서 응답 없음",
        ["OVER_CURRENT"] = "과전류",
        ["OVER_TEMP"] = "과열",
        ["COMM_ERROR"] = "통신 오류",
        ["POWER_LOSS"] = "전원 상실",
        ["ETC"] = "기타",
    };

    /// <summary>부품 유형 한글 이름. 모르면 원문, 비었으면 "부품".</summary>
    public static string TypeName(string? type)
        => string.IsNullOrWhiteSpace(type) ? "부품" : TypeNames.TryGetValue(type.Trim(), out var name) ? name : type.Trim();

    /// <summary>동작 상태 한글. 비었으면 null(상태 축이 없는 유형).</summary>
    public static string? StateName(string? state)
        => string.IsNullOrWhiteSpace(state) ? null : StateNames.TryGetValue(state.Trim(), out var name) ? name : state.Trim();

    /// <summary>고장 사유 한글. 비었으면 null.</summary>
    public static string? FaultName(string? reason)
        => string.IsNullOrWhiteSpace(reason) ? null : FaultNames.TryGetValue(reason.Trim(), out var name) ? name : reason.Trim();

    /// <summary>건강 단계 한글.</summary>
    public static string HealthName(ComponentHealthLevel health) => health switch
    {
        ComponentHealthLevel.Fault => "고장",
        ComponentHealthLevel.Degraded => "저하",
        ComponentHealthLevel.Ok => "정상",
        ComponentHealthLevel.Unknown => "확인 안 됨",
        _ => "-",
    };

    /// <summary>서버 건강 문자열(<c>OK · DEGRADED · FAULT · UNKNOWN</c>) → 단계. 비었거나 모르는 값은 <see cref="ComponentHealthLevel.Unknown"/>.</summary>
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
}
