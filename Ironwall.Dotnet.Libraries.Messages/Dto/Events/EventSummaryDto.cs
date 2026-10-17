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

    /// <summary>사전 경보(접근, <c>type_event=Alert</c>) — 서버는 침입과 따로 세고 <c>total</c> 에 <b>넣는다</b>(detection-alert FR-04).
    /// 이 키가 없던 판본(6.3)은 0 으로 읽힌다.</summary>
    [JsonProperty("alert", Order = 12)]
    public int Alert { get; set; }

    /// <summary>운영 이벤트(함체 · 통문 개폐, 환경 경보) — 서버가 <b>별도 집계</b>하고 <c>total</c> 에 넣지 않는다(X12).
    /// 이 키가 없던 판본은 0 으로 읽힌다.</summary>
    [JsonProperty("operation", Order = 13)]
    public int Operation { get; set; }

    [JsonProperty("daily_averages", Order = 10)]
    public DailyAveragesDto DailyAverages { get; set; } = new();

    [JsonProperty("active_devices", Order = 11)]
    public ActiveDevicesDto ActiveDevices { get; set; } = new();
}
