using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IBaseDeviceModel : IBaseModel
{
    int DeviceGroup { get; set; }
    string? DeviceName { get; set; }
    int DeviceNumber { get; set; }
    EnumDeviceType DeviceType { get; set; }
    EnumDeviceStatus Status { get; set; }
    string? Version { get; set; }
}