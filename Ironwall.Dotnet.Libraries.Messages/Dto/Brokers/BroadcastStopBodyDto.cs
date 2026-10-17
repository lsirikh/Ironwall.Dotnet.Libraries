using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// 방송 정지 Body
/// </summary>
public class BroadcastStopBodyDto : ControlCommandBodyDto
{
    [JsonProperty("speaker_ids")]
    public List<int> SpeakerIds { get; set; } = new();
}
