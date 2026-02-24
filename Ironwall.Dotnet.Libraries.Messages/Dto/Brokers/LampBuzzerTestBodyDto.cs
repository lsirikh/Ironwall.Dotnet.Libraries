using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class LampBuzzerTestBodyDto
{
    [JsonProperty("lamp_ids")]
    public List<int> LampIds { get; set; } = new();

    [JsonProperty("buzzer")]
    public string Buzzer { get; set; } = string.Empty;

    [JsonProperty("duration_sec")]
    public int DurationSec { get; set; }
}
