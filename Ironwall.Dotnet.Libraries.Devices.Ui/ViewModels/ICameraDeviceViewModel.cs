using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
public interface ICameraDeviceViewModel : IDeviceViewModel
{
    EnumCameraType Category { get; set; }
    ICameraInfoModel? HardwareSpec { get; set; }
    string IpAddress { get; set; }
    EnumCameraMode Mode { get; set; }
    string? UserPassword { get; set; }
    int IpPort { get; set; }
string? UserName { get; set; }
    ICameraUrlsModel? Urls { get; set; }
    ICameraSettingModel? Setting { get; set; }
    bool? IsRecord { get; set; }
}