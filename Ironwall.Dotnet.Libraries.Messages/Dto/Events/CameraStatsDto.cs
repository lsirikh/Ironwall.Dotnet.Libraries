using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 카메라별 AI 탐지 집계 (EventByDeviceDto 하위)
/// </summary>
public class CameraStatsDto
{
    [JsonProperty("camera_id", Order = 1)]
    public int CameraId { get; set; }

    [JsonProperty("camera_name", Order = 2)]
    public string CameraName { get; set; } = string.Empty;

    [JsonProperty("camera_number", Order = 3)]
    public int CameraNumber { get; set; }

    [JsonProperty("camera_detection", Order = 4)]
    public int CameraDetection { get; set; }
}
