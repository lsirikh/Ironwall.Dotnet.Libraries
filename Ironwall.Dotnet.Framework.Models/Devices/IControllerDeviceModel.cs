namespace Ironwall.Dotnet.Framework.Models.Devices;

public interface IControllerDeviceModel : IBaseDeviceModel
{
    string IpAddress { get; set; }
    int Port { get; set; }
}