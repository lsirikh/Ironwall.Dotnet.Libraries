using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
public interface ICameraDeviceViewModel : IDeviceViewModel
{
    EnumCameraType Category { get; set; }
    ICameraInfoModel? Identification { get; set; }
    string IpAddress { get; set; }
    EnumCameraMode Mode { get; set; }
    ICameraOpticsModel? Optics { get; set; }
    string? Password { get; set; }
    int Port { get; set; }
    ICameraPositionModel? Position { get; set; }
    List<ICameraPresetModel>? Presets { get; set; }
    ICameraPtzCapabilityModel? PtzCapability { get; set; }
    int RtspPort { get; set; }
    string? RtspUri { get; set; }
    string? Username { get; set; }
}