using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Helpsers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 조치 이벤트 DTO
/// </summary>
public class ActionEventDto
{
    /// <summary>
    /// 데이터베이스 ID (자동 생성)
    /// </summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Action")
    /// </summary>
    [JsonProperty("type_event", Order = 2)]
    public string TypeEvent { get; set; } = "Action";

    /// <summary>
    /// 조치 세부 내용 (GOP API 요구사항: content)
    /// </summary>
    [JsonProperty("content", Order = 3)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 조치자 (GOP API 요구사항: user)
    /// </summary>
    [JsonProperty("user", Order = 4)]
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// 원본 이벤트 정보 (GOP API 요구사항: from_event)
    /// <para>DetectionEventDto 또는 MalfunctionEventDto</para>
    /// </summary>
    [JsonProperty("from_event", Order = 5)]
    [JsonConverter(typeof(FromEventConverter))]
    public IEventDto? FromEvent { get; set; }

    /// <summary>
    /// 이벤트 발생 일시 (ISO 8601)
    /// </summary>
    [JsonProperty("datetime", Order = 6)]
    public string Datetime { get; set; } = string.Empty;

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 7)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 15)]
    public string? UpdatedAt { get; set; }
}
