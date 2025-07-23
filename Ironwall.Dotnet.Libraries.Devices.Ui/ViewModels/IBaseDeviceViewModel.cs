using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
public interface IBaseDeviceViewModel<T> : IBaseCustomViewModel<T> where T : IBaseDeviceModel
{
    int DeviceGroup { get; set; }
    string? DeviceName { get; set; }
    int DeviceNumber { get; set; }
    EnumDeviceType DeviceType { get; set; }
    EnumDeviceStatus Status { get; set; }
    string? Version { get; set; }
}