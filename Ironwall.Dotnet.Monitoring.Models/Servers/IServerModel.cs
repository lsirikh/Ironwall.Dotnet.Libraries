using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public interface IServerModel : IBaseModel
{
    int CategoryId { get; set; }
    string Name { get; set; }
    string Status { get; set; }
    string IpAddress { get; set; }
    int Port { get; set; }
    string? Hostname { get; set; }
    string? UserName { get; set; }
    string? UserPassword { get; set; }
    IServerThresholdConfigModel? ThresholdConfig { get; set; }
}
