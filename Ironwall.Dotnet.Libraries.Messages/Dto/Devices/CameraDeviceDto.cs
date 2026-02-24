using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Camera 디바이스 DTO
/// </summary>
public class CameraDeviceDto : BaseDeviceDto
{
    public CameraDeviceDto()
    {
        TypeDevice = "IpCamera";
    }

    /// <summary>
    /// IP 주소
    /// </summary>
    [JsonProperty("ip_address", Order = 11)]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// IP 포트
    /// </summary>
    [JsonProperty("ip_port", Order = 12)]
    public int IpPort { get; set; }

    /// <summary>
    /// 사용자 이름
    /// </summary>
    [JsonProperty("user_name", Order = 13)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 사용자 비밀번호
    /// </summary>
    [JsonProperty("user_password", Order = 14)]
    public string UserPassword { get; set; } = string.Empty;

    /// <summary>
    /// RTSP URI (레거시, urls.rtsp로 대체 — 서버 v2.3에서 제거됨)
    /// </summary>
    [JsonProperty("rtsp_uri", Order = 15, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? RtspUri { get; set; }

    /// <summary>
    /// RTSP 포트 (레거시, urls.rtsp에 포함 — 서버 v2.3에서 제거됨)
    /// </summary>
    [JsonProperty("rtsp_port", Order = 16, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int? RtspPort { get; set; }

    /// <summary>
    /// 카메라 모드 (EnumCameraMode: "NONE", "ONVIF", "EMSTONE_API", "INNODEP_API", "ETC")
    /// </summary>
    [JsonProperty("mode", Order = 17)]
    public string Mode { get; set; } = "NONE";

    /// <summary>
    /// 카메라 종류 (EnumCameraType: "NONE", "FIXED", "PTZ", "FISHEYES", "THERMAL")
    /// </summary>
    [JsonProperty("category", Order = 18)]
    public string Category { get; set; } = "NONE";

    /// <summary>
    /// 카메라 URL 묶음 (신규)
    /// </summary>
    [JsonProperty("urls", Order = 19, NullValueHandling = NullValueHandling.Ignore)]
    public CameraUrlsDto? Urls { get; set; }

    /// <summary>
    /// 녹화 여부 (신규)
    /// </summary>
    [JsonProperty("is_record", Order = 20, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsRecord { get; set; }

    /// <summary>
    /// 하드웨어 스펙 (신규)
    /// </summary>
    [JsonProperty("hardware_spec", Order = 21, NullValueHandling = NullValueHandling.Ignore)]
    public HardwareSpecDto? HardwareSpec { get; set; }
}
