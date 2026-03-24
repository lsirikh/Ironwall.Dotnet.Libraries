using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public class ResourceThresholdEntry : IResourceThresholdEntry
{
    [JsonProperty("warning", Order = 1)]
    public double Warning { get; set; }

    [JsonProperty("critical", Order = 2)]
    public double Critical { get; set; }
}
