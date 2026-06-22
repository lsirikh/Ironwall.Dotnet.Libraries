using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IBaseDeviceModel : IBaseModel
{
    List<int>? DeviceGroups { get; set; }
    string? DeviceName { get; set; }
    int DeviceNumber { get; set; }
    EnumDeviceType DeviceType { get; set; }
    EnumDeviceStatus Status { get; set; }
    string? Version { get; set; }
    string? Location { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    bool IsEnable { get; set; }
    double? Heading { get; set; }
    double? Altitude { get; set; }
}