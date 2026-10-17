using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public class LampDeviceModel : BaseDeviceModel, ILampDeviceModel
{
    public LampDeviceModel()
    {
        DeviceType = EnumDeviceType.Lamp;
    }

    [JsonProperty("ip_address", Order = 7)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("ip_port", Order = 8)]
    public int IpPort { get; set; }

    [JsonProperty("user_name", Order = 9)]
    public string? UserName { get; set; }

    [JsonProperty("user_password", Order = 10)]
    public string? UserPassword { get; set; }

    [JsonProperty("description", Order = 11)]
    public string? Description { get; set; }
}
