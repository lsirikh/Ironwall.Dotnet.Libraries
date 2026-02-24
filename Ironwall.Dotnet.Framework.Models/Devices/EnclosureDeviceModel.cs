using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public class EnclosureDeviceModel : BaseDeviceModel, IEnclosureDeviceModel
{
    public EnclosureDeviceModel()
    {
        DeviceType = EnumDeviceType.Enclosure;
    }

    [JsonProperty("door_status", Order = 7)]
    public string DoorStatus { get; set; } = "CLOSED";

    [JsonProperty("heater_enabled", Order = 8)]
    public bool HeaterEnabled { get; set; }

    [JsonProperty("fan_enabled", Order = 9)]
    public bool FanEnabled { get; set; }
}
