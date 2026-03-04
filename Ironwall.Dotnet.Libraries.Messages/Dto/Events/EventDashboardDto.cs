using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 대시보드 통합 응답 (summary + trend + by_device)
/// GET /api/events/statistics/dashboard
/// </summary>
public class EventDashboardDto
{
    [JsonProperty("summary", Order = 1)]
    public EventSummaryDto Summary { get; set; } = new();

    [JsonProperty("trend", Order = 2)]
    public EventTrendDto Trend { get; set; } = new();

    [JsonProperty("by_device", Order = 3)]
    public EventByDeviceDto ByDevice { get; set; } = new();
}
