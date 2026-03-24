using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class LampColorTestBodyDto
{
    [JsonProperty("lamp_ids")]
    public List<int> LampIds { get; set; } = new();

    [JsonProperty("color")]
    public string Color { get; set; } = string.Empty;

    [JsonProperty("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonProperty("duration_sec")]
    public int DurationSec { get; set; }
}
