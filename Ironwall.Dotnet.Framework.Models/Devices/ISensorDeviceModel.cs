namespace Ironwall.Dotnet.Framework.Models.Devices;
public interface ISensorDeviceModel : IBaseDeviceModel
{
    ControllerDeviceModel? Controller { get; set; }
}