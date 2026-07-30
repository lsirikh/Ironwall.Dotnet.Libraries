using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// LAMP_BUZZER_SET body — 경광등 부저 직접 설정 (GIS.md v1.5.2 §2.5, REQ).
/// 와이어 값은 EnumMember 문자열("Fire A-WANG"|"Emergency"|"Ambulance"|"PI-PI-PI"|"PI_continue").
/// </summary>
public class LampBuzzerSetBodyDto
{
    [JsonProperty("lamp_ids")]
    public List<int> LampIds { get; set; } = new();

    [JsonProperty("buzzer")]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumBuzzerSound Buzzer { get; set; }
}
