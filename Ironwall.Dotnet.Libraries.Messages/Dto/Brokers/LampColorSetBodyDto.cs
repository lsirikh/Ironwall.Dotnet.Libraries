using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// LAMP_COLOR_SET body — 경광등 색상 직접 설정 (GIS.md v1.5.2 §2.4, REQ).
/// 와이어 값은 EnumMember 문자열(color: Red|Orange|Green|Blue|White, mode: steady|blinking).
/// </summary>
public class LampColorSetBodyDto
{
    [JsonProperty("lamp_ids")]
    public List<int> LampIds { get; set; } = new();

    [JsonProperty("color")]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLampColor Color { get; set; }

    [JsonProperty("mode")]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumLightMode Mode { get; set; }
}
