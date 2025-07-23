
namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IControllerDeviceModel: IBaseDeviceModel
{
    string IpAddress { get; set; }
    int Port { get; set; }
    List<IBaseDeviceModel>? Devices { get; set; }
}