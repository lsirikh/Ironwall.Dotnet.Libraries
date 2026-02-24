using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// 방송 재생 Body
/// </summary>
public class BroadcastPlayBodyDto
{
    [JsonProperty("speaker_ids")]
    public List<int> SpeakerIds { get; set; } = new();

    [JsonProperty("file_group_id")]
    public int FileGroupId { get; set; }

    [JsonProperty("repeat")]
    public int Repeat { get; set; }
}
