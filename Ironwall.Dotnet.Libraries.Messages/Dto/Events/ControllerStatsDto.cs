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
}
