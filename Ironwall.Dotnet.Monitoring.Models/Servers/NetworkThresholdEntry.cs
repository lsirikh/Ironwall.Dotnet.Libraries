using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public class NetworkThresholdEntry : INetworkThresholdEntry
{
    [JsonProperty("warning_mbps", Order = 1)]
    public double WarningMbps { get; set; }

    [JsonProperty("critical_mbps", Order = 2)]
    public double CriticalMbps { get; set; }
}
