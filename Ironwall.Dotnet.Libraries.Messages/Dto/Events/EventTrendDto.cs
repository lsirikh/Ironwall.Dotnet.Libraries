using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 이벤트 추이 응답 (라인 차트용)
/// GET /api/events/statistics/trend
/// </summary>
public class EventTrendDto
{
    [JsonProperty("interval", Order = 1)]
    public string Interval { get; set; } = "hour";

    [JsonProperty("start_date", Order = 2)]
    public string StartDate { get; set; } = string.Empty;

    [JsonProperty("end_date", Order = 3)]
    public string EndDate { get; set; } = string.Empty;

    [JsonProperty("series", Order = 4)]
    public List<EventTrendItemDto> Series { get; set; } = new();
}
