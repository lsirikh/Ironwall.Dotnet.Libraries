using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Enclosure(함체) 디바이스 DTO (§5.5)
/// </summary>
public class EnclosureDeviceDto : BaseDeviceDto
{
    public EnclosureDeviceDto()
    {
        TypeDevice = "Enclosure";
    }

    /// <summary>
    /// 도어 상태 (EnumDoorStatus: CLOSED, OPEN)
    /// </summary>
    [JsonProperty("door_status", Order = 11)]
    public string DoorStatus { get; set; } = "CLOSED";

    /// <summary>
    /// 임계값 설정 (JSONB) — null이면 직렬화 생략(서버 저장값 null 덮어쓰기 방지)
    /// </summary>
    [JsonProperty("threshold_config", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? ThresholdConfig { get; set; }

    /// <summary>
    /// 히터 활성화 여부
    /// </summary>
    [JsonProperty("heater_enabled", Order = 13)]
    public bool HeaterEnabled { get; set; }

    /// <summary>
    /// 팬 활성화 여부
    /// </summary>
    [JsonProperty("fan_enabled", Order = 14)]
    public bool FanEnabled { get; set; }
}
