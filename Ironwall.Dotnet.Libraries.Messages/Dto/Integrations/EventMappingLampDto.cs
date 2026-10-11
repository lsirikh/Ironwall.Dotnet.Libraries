using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/// <summary>
/// 이벤트매핑 경광등 DTO
/// </summary>
/// <remarks>
/// 응답은 최상위 <c>lamp_id</c> · <c>event_mapping_id</c> 또는 6.3.0 의 중첩 <c>lamp:{id,…}</c> · <c>event_mapping:{id,…}</c>
/// (6.3.0 <c>event_mapping_lamps.py</c> <c>_build_response</c> — 경광등만 부모도 객체다) 로 온다 — 둘 다 읽고 스칼라가 정본이다(F2 와 같은 결함).
/// 중첩은 쓰기 전용 속성이라 요청 본문에는 실리지 않는다.
/// </remarks>
public class EventMappingLampDto
{
    [JsonProperty("event_mapping_id", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int EventMappingId { get; set; }

    [JsonProperty("lamp_id")]
    public int LampId { get; set; }

    [JsonProperty("color")]
    public string Color { get; set; } = "Red";

    [JsonProperty("buzzer_time")]
    public int BuzzerTime { get; set; } = 5;

    [JsonProperty("buzzer_sound")]
    public string BuzzerSound { get; set; } = "PI-PI-PI";

    [JsonProperty("light_mode")]
    public string LightMode { get; set; } = "steady";

    [JsonProperty("is_enable")]
    public bool IsEnable { get; set; } = true;

    [JsonProperty("priority")]
    public int Priority { get; set; } = 1;

    // ── 응답 전용 중첩(쓰기 전용 = 직렬화되지 않음) ──
    [JsonProperty("lamp")]
    private MappingDeviceRefDto? LampRef { set => _lampRef = value; }

    [JsonProperty("event_mapping")]
    private MappingParentRefDto? EventMappingRef { set => _eventMappingRef = value; }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (LampId <= 0 && _lampRef is { Id: > 0 }) LampId = _lampRef.Id;
        if (EventMappingId <= 0 && _eventMappingRef is { Id: > 0 }) EventMappingId = _eventMappingRef.Id;
    }

    private MappingDeviceRefDto? _lampRef;
    private MappingParentRefDto? _eventMappingRef;
}
