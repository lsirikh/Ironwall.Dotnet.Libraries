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
    /// 방송서버 참조 (read-only, nested) — 응답 역직렬화 전용. 쓰기는 <see cref="ServerId"/> 사용.
    /// </summary>
    [JsonProperty("server", Order = 13)]
    public ServerDto? Server { get; set; }

    /// <summary>
    /// 방송서버 ID (write) — SpeakerCreate/Update.server_id. 해제 미지원이라 null이면 직렬화 생략.
    /// </summary>
    [JsonProperty("server_id", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public int? ServerId { get; set; }

    /// <summary>요청 본문에 nested server를 직렬화하지 않음(서버는 server_id를 기대 + 민감정보 누출 방지).</summary>
    public bool ShouldSerializeServer() => false;
}
