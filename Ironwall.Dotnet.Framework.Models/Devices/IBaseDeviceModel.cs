using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public interface IBaseDeviceModel : IBaseModel
{
    int DeviceNumber { get; set; }
    List<int>? DeviceGroups { get; set; }
    string DeviceName { get; set; }
    EnumDeviceType DeviceType { get; set; }
    string Version { get; set; }
    EnumDeviceStatus Status { get; set; }
}