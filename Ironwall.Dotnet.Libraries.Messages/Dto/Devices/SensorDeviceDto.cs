using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Sensor 디바이스 DTO
/// </summary>
public class SensorDeviceDto
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
    /// 디바이스 타입 (EnumDeviceType: "Multi", "Fence", "Underground", "Contact", "PIR", etc.)
    /// </summary>
    [JsonProperty("type_device", Order = 5)]
    public string TypeDevice { get; set; } = string.Empty;

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
    /// 소속 Controller ID
    /// </summary>
    [JsonProperty("controller_id", Order = 8)]
    public int ControllerId { get; set; }

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 9)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 10)]
    public string? UpdatedAt { get; set; }
}
