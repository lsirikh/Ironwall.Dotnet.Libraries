namespace Ironwall.Dotnet.Framework.Models.Devices;

public interface IEnclosureDeviceModel : IBaseDeviceModel
{
    string DoorStatus { get; set; }
    bool HeaterEnabled { get; set; }
    bool FanEnabled { get; set; }
}
