using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 제어기별/카메라별 이벤트 통계 (막대 그래프용)
/// GET /api/events/statistics/by-device
/// </summary>
public class EventByDeviceDto
{
    [JsonProperty("start_date", Order = 1)]
    public string StartDate { get; set; } = string.Empty;

    [JsonProperty("end_date", Order = 2)]
    public string EndDate { get; set; } = string.Empty;

    [JsonProperty("controllers", Order = 3)]
    public List<ControllerStatsDto> Controllers { get; set; } = new();

    [JsonProperty("cameras", Order = 4)]
    public List<CameraStatsDto> Cameras { get; set; } = new();

    /// <summary>함체별 건수(운영 이벤트 포함) — 서버 7.0+ <c>by_device.enclosures</c>. 없던 판본은 빈 목록.</summary>
    [JsonProperty("enclosures", Order = 5)]
    public List<DeviceEventStatsDto> Enclosures { get; set; } = new();

    /// <summary>통문별 건수(운영 이벤트 포함) — 서버 7.0+ <c>by_device.gates</c>. 없던 판본은 빈 목록.</summary>
    [JsonProperty("gates", Order = 6)]
    public List<DeviceEventStatsDto> Gates { get; set; } = new();
}
