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
}
