using Ironwall.Dotnet.Framework.Enums;
using System;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public interface IBaseDeviceModel : IBaseModel
{
    int DeviceNumber { get; set; }
    int DeviceGroup { get; set; }
    string DeviceName { get; set; }
    EnumDeviceType DeviceType { get; set; }
    string Version { get; set; }
    EnumDeviceStatus Status { get; set; }
}