using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Controller 디바이스 DTO
/// </summary>
public class ControllerDeviceDto : BaseDeviceDto
{
    public ControllerDeviceDto()
    {
        TypeDevice = "Controller";
    }

    /// <summary>
    /// IP 주소
    /// </summary>
    [JsonProperty("ip_address", Order = 11)]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// IP 포트
    /// </summary>
    [JsonProperty("ip_port", Order = 12)]
    public int IpPort { get; set; }

    /// <summary>
    /// 연결된 센서 목록 (선택적, include_sensors=true 시)
    /// </summary>
    [JsonProperty("sensors", Order = 13)]
    public List<SensorDeviceDto>? Sensors { get; set; }
}
