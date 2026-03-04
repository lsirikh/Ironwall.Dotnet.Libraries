using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 이벤트 요약 응답 (원형 그래프 + 요약 카드)
/// GET /api/events/statistics/summary
/// </summary>
public class EventSummaryDto
{
    [JsonProperty("start_date", Order = 1)]
    public string StartDate { get; set; } = string.Empty;

    [JsonProperty("end_date", Order = 2)]
    public string EndDate { get; set; } = string.Empty;

    [JsonProperty("days_in_range", Order = 3)]
    public int DaysInRange { get; set; }

    [JsonProperty("total", Order = 4)]
    public int Total { get; set; }

    [JsonProperty("sensor_detection", Order = 5)]
    public int SensorDetection { get; set; }

    [JsonProperty("camera_detection", Order = 6)]
    public int CameraDetection { get; set; }

    [JsonProperty("malfunction", Order = 7)]
    public int Malfunction { get; set; }

    [JsonProperty("connection", Order = 8)]
    public int Connection { get; set; }

    [JsonProperty("action", Order = 9)]
    public int Action { get; set; }

    [JsonProperty("daily_averages", Order = 10)]
    public DailyAveragesDto DailyAverages { get; set; } = new();

    [JsonProperty("active_devices", Order = 11)]
    public ActiveDevicesDto ActiveDevices { get; set; } = new();
}
