using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/// <summary>
/// 이벤트매핑 카메라 DTO (CameraEventPresetDto 대체)
/// </summary>
public class EventMappingCameraDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("target_preset_id")]
    public int TargetPresetId { get; set; }

    [JsonProperty("home_preset_id")]
    public int HomePresetId { get; set; }

    [JsonProperty("delay_time")]
    public int DelayTime { get; set; }

    [JsonProperty("is_enable")]
    public bool IsEnable { get; set; } = true;

    [JsonProperty("priority")]
    public int Priority { get; set; }
}
