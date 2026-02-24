namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public interface IServerThresholdConfigModel
{
    IResourceThresholdEntry? Cpu { get; set; }
    IResourceThresholdEntry? Ram { get; set; }
    IResourceThresholdEntry? Disk { get; set; }
    INetworkThresholdEntry? Network { get; set; }
}
