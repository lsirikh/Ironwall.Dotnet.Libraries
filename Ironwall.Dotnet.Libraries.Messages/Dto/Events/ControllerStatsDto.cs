using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 제어기별 이벤트 집계 (EventByDeviceDto 하위)
/// </summary>
public class ControllerStatsDto
{
    [JsonProperty("controller_id", Order = 1)]
    public int ControllerId { get; set; }

    [JsonProperty("controller_name", Order = 2)]
    public string ControllerName { get; set; } = string.Empty;

    [JsonProperty("controller_number", Order = 3)]
    public int ControllerNumber { get; set; }

    [JsonProperty("sensor_detection", Order = 4)]
    public int SensorDetection { get; set; }

    [JsonProperty("malfunction", Order = 5)]
    public int Malfunction { get; set; }

    [JsonProperty("connection", Order = 6)]
    public int Connection { get; set; }

    [JsonProperty("action", Order = 7)]
    public int Action { get; set; }

    /// <summary>사전 경보(접근, <c>type_event=Alert</c>) — 서버는 침입과 따로 세고 <c>total</c> 에 <b>넣는다</b>(detection-alert FR-04).
    /// 이 키가 없던 판본(6.3)은 0 으로 읽힌다.</summary>
    [JsonProperty("alert", Order = 8)]
    public int Alert { get; set; }

    /// <summary>운영 이벤트(함체 · 통문 개폐, 환경 경보) — 서버가 <b>별도 집계</b>하고 <c>total</c> 에 넣지 않는다(X12).
    /// 이 키가 없던 판본은 0 으로 읽힌다.</summary>
    [JsonProperty("operation", Order = 9)]
    public int Operation { get; set; }
}
