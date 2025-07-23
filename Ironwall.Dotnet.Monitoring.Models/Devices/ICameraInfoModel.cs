using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ICameraInfoModel : IBaseModel
{
    string? DeviceId { get; set; }
    string? Firmware { get; set; }
    string? Hardware { get; set; }
    string? Location { get; set; }
    string? MacAddress { get; set; }
    string? Manufacturer { get; set; }
    string? Model { get; set; }
    string? Name { get; set; }
    string? OnvifVersion { get; set; }
    string? Uri { get; set; }
}