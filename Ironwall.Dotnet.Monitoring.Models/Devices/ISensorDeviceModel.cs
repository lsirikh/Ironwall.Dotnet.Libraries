namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ISensorDeviceModel : IBaseDeviceModel
{
    IControllerDeviceModel? Controller { get; set; }
}