using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Device 공통 기반 DTO
/// <para>모든 장비 타입(Controller, Sensor, Camera, Speaker, Enclosure, Lamp)의 공통 속성</para>
/// <para>Event DTO의 nested device 필드에 사용</para>
/// </summary>
public class BaseDeviceDto : BaseDto
{
    /// <summary>
    /// 디바이스 번호
    /// </summary>
    [JsonProperty("number_device", Order = 2)]
    public int NumberDevice { get; set; }

    /// <summary>
    /// 디바이스 이름
    /// </summary>
    [JsonProperty("name_device", Order = 3)]
    public string NameDevice { get; set; } = string.Empty;

    /// <summary>
    /// 디바이스 타입 (discriminator: "Controller", "Sensor", "IpCamera", "IpSpeaker", "Enclosure", "Lamp")
    /// </summary>
    [JsonProperty("type_device", Order = 4)]
    public string TypeDevice { get; set; } = string.Empty;

    /// <summary>
    /// 디바이스 상태 (EnumDeviceStatus: "ACTIVATED", "ERROR", "DEACTIVATED")
    /// </summary>
    [JsonProperty("status", Order = 5)]
    public string Status { get; set; } = "DEACTIVATED";

    /// <summary>
    /// 활성화 여부
    /// </summary>
    [JsonProperty("is_enable", Order = 6)]
    public bool IsEnable { get; set; }

    /// <summary>
    /// 복수 그룹 소속 (응답 전용 — 서버가 DeviceGroupDto 객체 배열로 반환)
    /// </summary>
    [JsonProperty("device_groups", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public List<DeviceGroupDto>? DeviceGroups { get; set; }

    /// <summary>
    /// 그룹 ID 배열 (요청 전용 — Model.DeviceGroups에서 직접 매핑)
    /// </summary>
    [JsonProperty("group_ids", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? GroupIds { get; set; }

    /// <summary>
    /// 펌웨어 버전
    /// </summary>
    [JsonProperty("version", Order = 9)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 위치 좌표 (위치 설명, 위도, 경도, 고도)
    /// </summary>
    [JsonProperty("geolocation", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public GeolocationDto? Geolocation { get; set; }

    /// <summary>
    /// 소속 컨트롤러 ID (Sensor 등). 설계 Gop_Message_Broker §6.4 device.controller_id. optional.
    /// </summary>
    [JsonProperty("controller_id", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public int? ControllerId { get; set; }

    /// <summary>
    /// 설계 문서 원칙: Event body의 nested device 객체에서는 created_at/updated_at 제외
    /// </summary>
    [JsonIgnore]
    public override string? CreatedAt { get => base.CreatedAt; set => base.CreatedAt = value; }

    [JsonIgnore]
    public override string? UpdatedAt { get => base.UpdatedAt; set => base.UpdatedAt = value; }
}
