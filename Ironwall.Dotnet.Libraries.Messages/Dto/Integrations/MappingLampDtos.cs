using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 경광등 배선 DTO (읽기 / 생성 / 부분수정) — GOP API v8.0.1 §7.5
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>GET …/{mapping_id}/lamps</c> 응답 1행
/// (<c>app/schemas/integration.py:483-503</c> <c>EventMappingLampResponse</c>).
/// </summary>
/// <remarks>
/// 🔴 <b>3종 중 경광등만 다르다</b> — 부모가 스칼라 <c>event_mapping_id</c> 가 아니라
/// nested <c>event_mapping{}</c> 다(<c>integration.py:491</c>). 그리고 <c>priority</c> 가
/// <b>non-nullable, <c>ge=1</c></b> 이다(<c>:279</c>) — 카메라·스피커는 nullable <c>ge=0</c>.
/// </remarks>
public class MappingLampReadDto
{
    /// <summary>배선 행 PK(<c>config_id</c>) — 결함 <b>D1</b> 대응.</summary>
    [JsonProperty("id", Order = 1)]
    public int ConfigId { get; set; }

    /// <summary>부모 매핑 — <b>객체</b>다. 스칼라로 받으면 0 으로 읽힌다.</summary>
    [JsonProperty("event_mapping", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public MappingParentRefDto? EventMapping { get; set; }

    /// <summary>연결된 경광등. <c>null</c> 이면 고아 행.</summary>
    [JsonProperty("lamp", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public MappingDeviceRefDto? Lamp { get; set; }

    /// <summary>점등 색(와이어 5값). 장비가 실제로 내는 색이라 테마와 무관하다.</summary>
    [JsonProperty("color", Order = 4)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLampColor Color { get; set; } = EnumLampColor.Red;

    /// <summary>부저 지속(초) — NOT NULL, 기본 5.</summary>
    [JsonProperty("buzzer_time", Order = 5)]
    public int BuzzerTime { get; set; } = EventMappingRules.BUZZER_TIME_DEFAULT;

    /// <summary>부저음(와이어 5값 — 공백·하이픈·언더스코어 혼재).</summary>
    [JsonProperty("buzzer_sound", Order = 6)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumBuzzerSound BuzzerSound { get; set; } = EnumBuzzerSound.PiPiPi;

    /// <summary>점등 모드(소문자 2값).</summary>
    [JsonProperty("light_mode", Order = 7)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLightMode LightMode { get; set; } = EnumLightMode.Steady;

    /// <summary>행 활성 여부.</summary>
    [JsonProperty("is_enable", Order = 8)]
    public bool IsEnable { get; set; } = true;

    /// <summary>우선순위 — <b>non-null, <c>ge=1</c></b>.</summary>
    [JsonProperty("priority", Order = 9)]
    public int Priority { get; set; } = EventMappingRules.LAMP_PRIORITY_MIN;

    /// <summary>생성 시각(KST 문자열).</summary>
    [JsonProperty("created_at", Order = 98, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    /// <summary>수정 시각(KST 문자열).</summary>
    [JsonProperty("updated_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// 경광등 배선 생성 항목 — 단건 POST 와 벌크 <c>items[]</c> 공용
/// (<c>app/schemas/integration.py:412-436</c> <c>EventMappingLampCreate</c>).
/// </summary>
/// <remarks>
/// <para>🔴 enum 3종은 <b>반드시</b> <c>[JsonConverter(typeof(StringEnumConverter))]</c> 가 붙어야 한다.
/// 와이어 값에 공백·하이픈이 있어(<c>"Fire A-WANG"</c>·<c>"PI-PI-PI"</c>·<c>"PI_continue"</c>,
/// <c>app/utils/enums.py:609-620</c>) 컨버터가 없으면 멤버명(<c>FireAWang</c>)이 나가 <b>422</b> 다.
/// <c>[EnumMember]</c> 는 이미 enum 쪽에 붙어 있다 — 빠진 것은 DTO 쪽 컨버터 지정뿐이었다.</para>
/// <para><c>event_mapping_id</c> 는 <b>보내지 않는다</b>. 보낼 수는 있지만 경로 값과 다르면
/// 422 <c>IMMUTABLE_FIELD</c>(<c>integration.py:87-100</c>)라 이득이 없다 — 경로가 정본이다.</para>
/// </remarks>
public class MappingLampCreateDto
{
    /// <summary>대상 경광등 id — 필수.</summary>
    [JsonProperty("lamp_id", Order = 1)]
    public int LampId { get; set; }

    /// <summary>점등 색 — 기본 Red.</summary>
    [JsonProperty("color", Order = 2)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLampColor Color { get; set; } = EnumLampColor.Red;

    /// <summary>부저 지속(초) — <c>ge=0</c>, 기본 5.</summary>
    [JsonProperty("buzzer_time", Order = 3)]
    public int BuzzerTime { get; set; } = EventMappingRules.BUZZER_TIME_DEFAULT;

    /// <summary>부저음 — 기본 <c>PI-PI-PI</c>.</summary>
    [JsonProperty("buzzer_sound", Order = 4)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumBuzzerSound BuzzerSound { get; set; } = EnumBuzzerSound.PiPiPi;

    /// <summary>점등 모드 — 기본 <c>steady</c>.</summary>
    [JsonProperty("light_mode", Order = 5)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLightMode LightMode { get; set; } = EnumLightMode.Steady;

    /// <summary>활성 여부 — 기본 <c>true</c>.</summary>
    [JsonProperty("is_enable", Order = 6)]
    public bool IsEnable { get; set; } = true;

    /// <summary>우선순위 — <b><c>ge=1</c></b>, 기본 1. <b>0 을 보내면 422</b> 다.</summary>
    [JsonProperty("priority", Order = 7)]
    public int Priority { get; set; } = EventMappingRules.LAMP_PRIORITY_MIN;
}

/// <summary>
/// 경광등 배선 부분 수정 — <c>PATCH …/lamps/{config_id}</c>
/// (<c>app/schemas/integration.py:439-451</c> <c>EventMappingLampUpdate</c>).
/// </summary>
/// <remarks>
/// 경광등은 <b>모든 값 컬럼이 NOT NULL</b> 이다. 그래서 이 타입에는 "의미 있는 null" 이 하나도 없고,
/// 전부 <c>NullValueHandling.Ignore</c> 로 충분하다 — <c>null</c> 을 실으면 422 <c>NULL_NOT_ALLOWED</c> 다.
/// <c>event_mapping_id</c> 는 서버 Update 모델에 <b>아예 없어서</b> 보내면 <c>UNKNOWN_FIELD</c> 다.
/// </remarks>
public class MappingLampUpdateDto
{
    /// <summary>경광등 교체(고아 재지정). <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("lamp_id", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public int? LampId { get; set; }

    /// <summary>점등 색. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("color", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLampColor? Color { get; set; }

    /// <summary>부저 지속(초). <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("buzzer_time", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public int? BuzzerTime { get; set; }

    /// <summary>부저음. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("buzzer_sound", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumBuzzerSound? BuzzerSound { get; set; }

    /// <summary>점등 모드. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("light_mode", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLightMode? LightMode { get; set; }

    /// <summary>활성 여부. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("is_enable", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEnable { get; set; }

    /// <summary>우선순위(<c>ge=1</c>). <c>null</c> 이면 보내지 않는다. 순서 저장이 쓰는 키다.</summary>
    [JsonProperty("priority", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public int? Priority { get; set; }

    /// <summary>보낼 키가 하나도 없는가.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        LampId is null && Color is null && BuzzerTime is null && BuzzerSound is null
        && LightMode is null && IsEnable is null && Priority is null;
}
