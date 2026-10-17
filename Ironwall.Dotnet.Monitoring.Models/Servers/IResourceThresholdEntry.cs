namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public interface IResourceThresholdEntry
{
    double Warning { get; set; }
    double Critical { get; set; }
}
