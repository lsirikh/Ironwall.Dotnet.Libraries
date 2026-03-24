using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 장애 이벤트 DTO (Nested device + detail 구조)
/// </summary>
public class MalfunctionEventDto : BaseDto, IDeviceEventDto, IActionReportableEventDto
{
    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Fault")
    /// </summary>
    [JsonProperty("type_event", Order = 2)]
    public string TypeEvent { get; set; } = "Fault";

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

    /// <summary>
    /// 조치 여부 (EnumTrueFalse: "True", "False") - 서버가 자동 관리
    /// </summary>
    [JsonProperty("action_reported", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? ActionReported { get; set; }

    /// <summary>
    /// 장애 원인 (EnumFaultType: "FAULT_FENCE", "FAULT_CONTROLLER", etc.)
    /// </summary>
    [JsonProperty("reason", Order = 7)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 장애 상세 정보 (JSONB, first_start/first_end/second_start/second_end)
    /// </summary>
    [JsonProperty("detail", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public MalfunctionDetailDto? Detail { get; set; }
}
