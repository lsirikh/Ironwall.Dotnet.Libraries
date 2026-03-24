using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 시간 버킷별 이벤트 건수 (라인 차트 데이터 포인트)
/// </summary>
public class EventTrendItemDto
{
    [JsonProperty("time_bucket", Order = 1)]
    public string TimeBucket { get; set; } = string.Empty;

    [JsonProperty("sensor_detection", Order = 2)]
    public int SensorDetection { get; set; }

    [JsonProperty("camera_detection", Order = 3)]
    public int CameraDetection { get; set; }

    [JsonProperty("malfunction", Order = 4)]
    public int Malfunction { get; set; }

    [JsonProperty("connection", Order = 5)]
    public int Connection { get; set; }

    [JsonProperty("action", Order = 6)]
    public int Action { get; set; }
}
