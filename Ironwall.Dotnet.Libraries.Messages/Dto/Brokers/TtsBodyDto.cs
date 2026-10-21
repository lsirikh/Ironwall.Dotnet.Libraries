using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// TTS 방송 Body
/// </summary>
public class TtsBodyDto : ControlCommandBodyDto
{
    [JsonProperty("speaker_ids")]
    public List<int> SpeakerIds { get; set; } = new();

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>음량(0~100) — 늘 싣는다. 기본값 · 범위 규칙은 <see cref="BroadcastPlayBodyDto.Volume"/> 과 같다.</summary>
    [JsonProperty("volume")]
    public int Volume
    {
        get => _volume;
        set => _volume = BroadcastPlayBodyDto.ClampVolume(value);
    }

    public const int VolumeDefault = BroadcastPlayBodyDto.VolumeDefault;
    private int _volume = VolumeDefault;
}
