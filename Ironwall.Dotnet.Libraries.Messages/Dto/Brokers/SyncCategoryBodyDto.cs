using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class SyncCategoryBodyDto
{
    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("resource_id")]
    public int ResourceId { get; set; }
}
