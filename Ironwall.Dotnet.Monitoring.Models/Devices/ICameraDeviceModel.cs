using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
public interface ICameraDeviceModel : IBaseDeviceModel
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