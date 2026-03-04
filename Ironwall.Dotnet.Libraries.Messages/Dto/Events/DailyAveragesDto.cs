using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 일평균 이벤트 건수 (EventSummaryDto 하위 객체)
/// </summary>
public class DailyAveragesDto
{
    [JsonProperty("sensor_detection", Order = 1)]
    public double SensorDetection { get; set; }

    [JsonProperty("camera_detection", Order = 2)]
    public double CameraDetection { get; set; }

    [JsonProperty("malfunction", Order = 3)]
    public double Malfunction { get; set; }

    [JsonProperty("connection", Order = 4)]
    public double Connection { get; set; }

    [JsonProperty("action", Order = 5)]
    public double Action { get; set; }
}
