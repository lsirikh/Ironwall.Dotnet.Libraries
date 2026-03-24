using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// 방송 테스트 Body
/// </summary>
public class BroadcastTestBodyDto
{
    [JsonProperty("speaker_ids")]
    public List<int> SpeakerIds { get; set; } = new();

    [JsonProperty("file_group_id")]
    public int FileGroupId { get; set; }

    [JsonProperty("duration_sec")]
    public int DurationSec { get; set; }
}
