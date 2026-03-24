using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Sensor 디바이스 DTO
/// </summary>
public class SensorDeviceDto : BaseDeviceDto
{
    /// <summary>
    /// 소속 Controller ID
    /// </summary>
    [JsonProperty("controller_id", Order = 14)]
    public int ControllerId { get; set; }

    /// <summary>
    /// 소속 Controller (선택적, include_controller=true 시)
    /// </summary>
    [JsonProperty("controller", Order = 15)]
    public ControllerDeviceDto? Controller { get; set; }
}
