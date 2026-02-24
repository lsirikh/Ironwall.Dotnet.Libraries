using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class EnclosureDeviceModel : BaseDeviceModel, IEnclosureDeviceModel
{
    public EnclosureDeviceModel()
    {
        DeviceType = EnumDeviceType.Enclosure;
    }

    public EnclosureDeviceModel(IEnclosureDeviceModel model) : base(model)
    {
        DoorStatus = model.DoorStatus;
        ThresholdConfig = model.ThresholdConfig as EnclosureThresholdConfigModel;
        HeaterEnabled = model.HeaterEnabled;
        FanEnabled = model.FanEnabled;
    }

    [JsonProperty("door_status", Order = 7)]
    public string DoorStatus { get; set; } = "CLOSED";

    [JsonProperty("threshold_config", Order = 8)]
    public EnclosureThresholdConfigModel? ThresholdConfig { get; set; }

    IEnclosureThresholdConfigModel? IEnclosureDeviceModel.ThresholdConfig
    {
        get => ThresholdConfig;
        set => ThresholdConfig = value as EnclosureThresholdConfigModel;
    }

    [JsonProperty("heater_enabled", Order = 9)]
    public bool HeaterEnabled { get; set; }

    [JsonProperty("fan_enabled", Order = 10)]
    public bool FanEnabled { get; set; }
}
