using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 카메라 하드웨어 스펙 DTO (9필드)
/// </summary>
public class HardwareSpecDto
{
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("location", Order = 2)]
    public string Location { get; set; } = string.Empty;

    [JsonProperty("manufacturer", Order = 3)]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonProperty("model", Order = 4)]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("hardware", Order = 5)]
    public string Hardware { get; set; } = string.Empty;

    [JsonProperty("firmware", Order = 6)]
    public string Firmware { get; set; } = string.Empty;

    [JsonProperty("device_id", Order = 7)]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty("mac_address", Order = 8)]
    public string MacAddress { get; set; } = string.Empty;

    [JsonProperty("onvif_version", Order = 9)]
    public string OnvifVersion { get; set; } = string.Empty;
}
