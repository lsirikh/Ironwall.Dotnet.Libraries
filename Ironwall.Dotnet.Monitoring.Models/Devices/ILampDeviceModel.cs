namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ILampDeviceModel : IBaseDeviceModel
{
    string IpAddress { get; set; }
    int IpPort { get; set; }
    string? UserName { get; set; }
    string? UserPassword { get; set; }
    string? Description { get; set; }
}
