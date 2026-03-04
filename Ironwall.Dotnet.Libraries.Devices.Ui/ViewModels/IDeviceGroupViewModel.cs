using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public interface IDeviceGroupViewModel
{
    int Id { get; }
    string Name { get; set; }
    string? Description { get; set; }
    int DeviceCount { get; }
    IDeviceGroupModel Model { get; }
}
