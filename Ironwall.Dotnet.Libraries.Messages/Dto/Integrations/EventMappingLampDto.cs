using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/// <summary>
/// 이벤트매핑 경광등 DTO
/// </summary>
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
}
