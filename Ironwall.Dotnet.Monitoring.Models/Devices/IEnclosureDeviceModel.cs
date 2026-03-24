namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IEnclosureDeviceModel : IBaseDeviceModel
{
    string DoorStatus { get; set; }
    IEnclosureThresholdConfigModel? ThresholdConfig { get; set; }
    bool HeaterEnabled { get; set; }
    bool FanEnabled { get; set; }
}
