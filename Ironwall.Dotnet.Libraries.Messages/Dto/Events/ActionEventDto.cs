using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
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

    /// <summary>
    /// 조치 대상 장비 (공통 Base 타입)
    /// </summary>
    [JsonProperty("device", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public BaseDeviceDto? Device { get; set; }

    /// <summary>
    /// 장비 설명
    /// </summary>
    [JsonProperty("device_description", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public string? DeviceDescription { get; set; }
}
