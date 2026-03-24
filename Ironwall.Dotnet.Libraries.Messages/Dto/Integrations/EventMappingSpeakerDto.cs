using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/// <summary>
/// 이벤트매핑 스피커 DTO
/// </summary>
public class EventMappingSpeakerDto
{
    [JsonProperty("speaker_id")]
    public int SpeakerId { get; set; }

    [JsonProperty("file_group_id")]
    public int FileGroupId { get; set; }

    [JsonProperty("repeat_count")]
    public int RepeatCount { get; set; }

    [JsonProperty("priority")]
    public int Priority { get; set; }
}
