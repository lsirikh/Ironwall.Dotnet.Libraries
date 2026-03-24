using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// TTS 방송 Body
/// </summary>
public class TtsBodyDto
{
    [JsonProperty("speaker_ids")]
    public List<int> SpeakerIds { get; set; } = new();

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}
