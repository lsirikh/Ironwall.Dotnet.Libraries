using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Api.Messages.Events;

/// <summary>
/// 조치 이벤트 생성 DTO (POST 요청용)
/// </summary>
public class ActionEventCreateDto
{
    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Action")
    /// </summary>
    [JsonProperty("type_event", Order = 1)]
    public string TypeEvent { get; set; } = "Action";

    /// <summary>
    /// 조치 세부 내용
    /// </summary>
    [JsonProperty("content", Order = 2)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 조치자
    /// </summary>
    [JsonProperty("user", Order = 3)]
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// 원본 이벤트 ID
    /// </summary>
    [JsonProperty("from_event", Order = 4)]
    public int FromEvent { get; set; }

    /// <summary>
    /// 원본 이벤트 타입
    /// </summary>
    [JsonProperty("from_event_type", Order = 5)]
    public string FromEventType { get; set; } = string.Empty;

    /// <summary>
    /// 이벤트 발생 일시 (ISO 8601)
    /// </summary>
    [JsonProperty("datetime", Order = 6)]
    public string Datetime { get; set; } = string.Empty;
}
