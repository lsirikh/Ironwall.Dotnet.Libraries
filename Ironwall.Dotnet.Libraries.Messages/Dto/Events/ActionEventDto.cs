using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 조치 이벤트 DTO
/// </summary>
public class ActionEventDto : BaseDto
{
    /// <summary>
    /// 데이터베이스 ID (자동 생성)
    /// </summary>
    [JsonProperty("id", Order = 1, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Id { get; set; }

    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Action")
    /// </summary>
    [JsonProperty("type_event", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeEvent { get; set; }

    /// <summary>
    /// 소속 부대 id(서버 8.0+ 응답 <c>required</c>). 6.3.2·7.0.1 응답에는 없어 <c>null</c> 이다. <b>읽기 전용</b>.
    /// </summary>
    [JsonProperty("unit_id", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

    /// <summary>요청 미전송 보증 — 조치 쓰기 스키마에 이 키가 없다(보내면 422 <c>extra_forbidden</c>).</summary>
    public bool ShouldSerializeUnitId() => false;

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
    /// 원본 이벤트 정보 (DetectionEventDto 또는 MalfunctionEventDto)
    /// </summary>
    [JsonProperty("from_event", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(FromEventConverter))]
    public IEventDto? FromEvent { get; set; }
}
