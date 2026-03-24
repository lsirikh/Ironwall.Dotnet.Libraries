using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public interface IEnclosureDeviceViewModel : IDeviceViewModel
{
    string DoorStatus { get; set; }
    bool HeaterEnabled { get; set; }
    bool FanEnabled { get; set; }
    IEnclosureThresholdConfigModel? ThresholdConfig { get; }
    string ThresholdSummary { get; }
}
