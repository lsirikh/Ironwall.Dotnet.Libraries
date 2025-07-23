using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
public interface ISensorDeviceViewModel :IDeviceViewModel
{
    IControllerDeviceModel? Controller { get; set; }
}