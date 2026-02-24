using Ironwall.Dotnet.Monitoring.Models.Servers;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public interface ISpeakerDeviceViewModel : IDeviceViewModel
{
    string SpeakerType { get; set; }
    string? Description { get; set; }
    IServerModel? Server { get; set; }
}
