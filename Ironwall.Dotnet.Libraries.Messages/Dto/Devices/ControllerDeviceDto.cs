using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Controller 디바이스 DTO
/// </summary>
public class ControllerDeviceDto
{
    /// <summary>
    /// 데이터베이스 ID (자동 생성)
    /// </summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>
    /// 디바이스 번호
    /// </summary>
    [JsonProperty("number_device", Order = 2)]
    public int NumberDevice { get; set; }

    /// <summary>
    /// 디바이스 그룹
    /// </summary>
    [JsonProperty("group_device", Order = 3)]
    public int GroupDevice { get; set; }

    /// <summary>
    /// 디바이스 이름
    /// </summary>
    [JsonProperty("name_device", Order = 4)]
    public string NameDevice { get; set; } = string.Empty;

    /// <summary>
    /// 디바이스 타입 (EnumDeviceType: "Controller")
    /// </summary>
    [JsonProperty("type_device", Order = 5)]
    public string TypeDevice { get; set; } = "Controller";

    /// <summary>
    /// 펌웨어 버전
    /// </summary>
    [JsonProperty("version", Order = 6)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 디바이스 상태 (EnumDeviceStatus: "ACTIVATED", "ERROR", "DEACTIVATED")
    /// </summary>
    [JsonProperty("status", Order = 7)]
    public string Status { get; set; } = "DEACTIVATED";

    /// <summary>
    /// IP 주소
    /// </summary>
    [JsonProperty("ip_address", Order = 8)]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// IP 포트
    /// </summary>
    [JsonProperty("ip_port", Order = 9)]
    public int IpPort { get; set; }

    /// <summary>
    /// 연결된 센서 목록 (선택적, include_sensors=true 시)
    /// </summary>
    [JsonProperty("sensors", Order = 10)]
    public List<SensorDeviceDto>? Sensors { get; set; }

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 11)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 12)]
    public string? UpdatedAt { get; set; }
}
