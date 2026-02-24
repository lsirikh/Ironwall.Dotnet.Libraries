using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class LampColorSetBodyDto
{
    [JsonProperty("lamp_ids")]
    public List<int> LampIds { get; set; } = new();

    [JsonProperty("color")]
    public string Color { get; set; } = string.Empty;

    [JsonProperty("mode")]
    public string Mode { get; set; } = string.Empty;
}
