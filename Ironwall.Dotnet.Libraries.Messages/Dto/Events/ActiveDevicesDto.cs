using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 활성 장비 수 (EventSummaryDto 하위 객체)
/// </summary>
public class ActiveDevicesDto
{
    [JsonProperty("sensors", Order = 1)]
    public int Sensors { get; set; }

    [JsonProperty("cameras", Order = 2)]
    public int Cameras { get; set; }

    [JsonProperty("controllers", Order = 3)]
    public int Controllers { get; set; }
}
