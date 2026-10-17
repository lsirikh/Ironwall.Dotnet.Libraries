using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 조치 이벤트 생성 DTO (POST 요청용)
/// </summary>
public class ActionEventCreateDto : BaseDto
{
    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Action")
    /// </summary>
    [JsonProperty("type_event", Order = 2)]
    public string TypeEvent { get; set; } = "Action";

    /// <summary>
    /// 조치 세부 내용
    /// </summary>
    [JsonProperty("content", Order = 3)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 조치자
    /// </summary>
    [JsonProperty("user", Order = 4)]
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// 원본 이벤트 ID (events.id FK)
    /// </summary>
    [JsonProperty("from_event_id", Order = 5)]
    public int FromEventId { get; set; }
}
