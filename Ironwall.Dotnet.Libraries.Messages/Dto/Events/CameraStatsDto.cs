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

    /// <summary>사전 경보(접근, <c>type_event=Alert</c>) — 서버는 침입과 따로 세고 <c>total</c> 에 <b>넣는다</b>(detection-alert FR-04).
    /// 이 키가 없던 판본(6.3)은 0 으로 읽힌다.</summary>
    [JsonProperty("alert", Order = 5)]
    public int Alert { get; set; }
}
