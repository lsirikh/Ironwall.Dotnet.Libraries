using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public class ServerThresholdConfigModel : IServerThresholdConfigModel
{
    [JsonProperty("cpu", Order = 1)]
    public ResourceThresholdEntry? Cpu { get; set; }

    [JsonProperty("ram", Order = 2)]
    public ResourceThresholdEntry? Ram { get; set; }

    [JsonProperty("disk", Order = 3)]
    public ResourceThresholdEntry? Disk { get; set; }

    [JsonProperty("network", Order = 4)]
    public NetworkThresholdEntry? Network { get; set; }

    // Interface explicit implementation
    IResourceThresholdEntry? IServerThresholdConfigModel.Cpu
    {
        get => Cpu;
        set => Cpu = value as ResourceThresholdEntry;
    }

    IResourceThresholdEntry? IServerThresholdConfigModel.Ram
    {
        get => Ram;
        set => Ram = value as ResourceThresholdEntry;
    }

    IResourceThresholdEntry? IServerThresholdConfigModel.Disk
    {
        get => Disk;
        set => Disk = value as ResourceThresholdEntry;
    }

    INetworkThresholdEntry? IServerThresholdConfigModel.Network
    {
        get => Network;
        set => Network = value as NetworkThresholdEntry;
    }
}
