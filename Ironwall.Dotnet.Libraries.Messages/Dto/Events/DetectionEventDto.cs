using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 침입 탐지 이벤트 DTO (Nested device 구조)
/// </summary>
public class DetectionEventDto : BaseDto, IDeviceEventDto, IActionReportableEventDto
{
    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Intrusion")
    /// </summary>
    [JsonProperty("type_event", Order = 2)]
    public string TypeEvent { get; set; } = "Intrusion";

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
    /// 탐지 결과 (EnumDetectionType: "THERMAL_SENSOR", "PIR_SENSOR", etc.)
    /// </summary>
    [JsonProperty("result", Order = 7)]
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// 탐지 상세 정보 (JSONB)
    /// </summary>
    [JsonProperty("detail", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public DetectionDetailDto? Detail { get; set; }
}
