using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Camera 디바이스 DTO
/// </summary>
public class CameraDeviceDto
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
    /// 디바이스 타입 (EnumDeviceType: "IpCamera")
    /// </summary>
    [JsonProperty("type_device", Order = 5)]
    public string TypeDevice { get; set; } = "IpCamera";

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
    /// 사용자 이름
    /// </summary>
    [JsonProperty("user_name", Order = 10)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 사용자 비밀번호
    /// </summary>
    [JsonProperty("user_password", Order = 11)]
    public string UserPassword { get; set; } = string.Empty;

    /// <summary>
    /// RTSP URI
    /// </summary>
    [JsonProperty("rtsp_uri", Order = 12)]
    public string RtspUri { get; set; } = string.Empty;

    /// <summary>
    /// RTSP 포트
    /// </summary>
    [JsonProperty("rtsp_port", Order = 13)]
    public int RtspPort { get; set; }

    /// <summary>
    /// 카메라 모드 (EnumCameraMode: "NONE", "ONVIF", "EMSTONE_API", "INNODEP_API", "ETC")
    /// </summary>
    [JsonProperty("mode", Order = 14)]
    public string Mode { get; set; } = "NONE";

    /// <summary>
    /// 카메라 종류 (EnumCameraType: "NONE", "FIXED", "PTZ", "FISHEYES", "THERMAL")
    /// </summary>
    [JsonProperty("category", Order = 15)]
    public string Category { get; set; } = "NONE";

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 16)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 17)]
    public string? UpdatedAt { get; set; }
}
