using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 연결 이벤트 DTO (Nested device 구조)
/// </summary>
public class ConnectionEventDto : BaseDto, IDeviceEventDto
{
    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Connection")
    /// </summary>
    [JsonProperty("type_event", Order = 2)]
    public string TypeEvent { get; set; } = "Connection";

    /// <summary>
    /// 장치 ID (Create/Update 시 사용, FK → Device)
    /// </summary>
    [JsonProperty("device_id", Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int DeviceId { get; set; }

    /// <summary>
    /// 중첩 Device 객체 (서버 응답에서 반환, 공통 Base 타입)
    /// </summary>
    [JsonProperty("device", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public BaseDeviceDto? Device { get; set; }

    /// <summary>
    /// 장비 설명 문자열
    /// </summary>
    [JsonProperty("device_description", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? DeviceDescription { get; set; }
}
