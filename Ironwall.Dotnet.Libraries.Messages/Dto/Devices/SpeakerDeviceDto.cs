using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Speaker(방송 장비) 디바이스 DTO (§5.4)
/// </summary>
public class SpeakerDeviceDto : BaseDeviceDto
{
    public SpeakerDeviceDto()
    {
        TypeDevice = "IpSpeaker";
    }

    /// <summary>
    /// 스피커 타입 (EnumSpeakerType: NORMAL, ADMIN, MONITOR, DEV)
    /// </summary>
    [JsonProperty("speaker_type", Order = 11)]
    public string SpeakerType { get; set; } = "NORMAL";

    /// <summary>
    /// 장비 설명
    /// </summary>
    [JsonProperty("description", Order = 12)]
    public string? Description { get; set; }

    /// <summary>
    /// 방송서버 참조 (read-only, nested)
    /// </summary>
    [JsonProperty("server", Order = 13)]
    public ServerDto? Server { get; set; }
}
