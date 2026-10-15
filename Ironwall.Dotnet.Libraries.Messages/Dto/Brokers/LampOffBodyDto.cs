using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class LampOffBodyDto : ControlCommandBodyDto
{
    [JsonProperty("lamp_ids")]
    public List<int> LampIds { get; set; } = new();
}
