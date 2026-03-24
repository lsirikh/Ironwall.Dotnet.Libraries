using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class LampBuzzerSetBodyDto
{
    [JsonProperty("lamp_ids")]
    public List<int> LampIds { get; set; } = new();

    [JsonProperty("buzzer")]
    public string Buzzer { get; set; } = string.Empty;
}
