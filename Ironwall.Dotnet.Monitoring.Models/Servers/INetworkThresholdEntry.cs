namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public interface INetworkThresholdEntry
{
    double WarningMbps { get; set; }
    double CriticalMbps { get; set; }
}
