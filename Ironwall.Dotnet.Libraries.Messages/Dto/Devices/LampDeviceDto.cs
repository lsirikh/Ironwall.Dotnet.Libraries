using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Lamp(경고등) 디바이스 DTO (§5.11)
/// </summary>
public class LampDeviceDto : BaseDeviceDto
{
    public LampDeviceDto()
    {
        TypeDevice = "Lamp";
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
    public string? UserName { get; set; }

    /// <summary>
    /// 사용자 비밀번호
    /// </summary>
    [JsonProperty("user_password", Order = 14)]
    public string? UserPassword { get; set; }

    /// <summary>
    /// 장비 설명
    /// </summary>
    [JsonProperty("description", Order = 15)]
    public string? Description { get; set; }
}
