using Ironwall.Dotnet.Monitoring.Models.Servers;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ISpeakerDeviceModel : IBaseDeviceModel
{
    string SpeakerType { get; set; }
    string? Description { get; set; }
    IServerModel? Server { get; set; }
}
