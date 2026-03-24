using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class SyncDeviceBodyDto
{
    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("type_device")]
    public string TypeDevice { get; set; } = string.Empty;

    [JsonProperty("resource_id")]
    public int ResourceId { get; set; }
}
