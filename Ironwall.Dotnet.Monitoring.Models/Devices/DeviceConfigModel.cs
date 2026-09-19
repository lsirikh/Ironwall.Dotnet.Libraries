using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <inheritdoc cref="IDeviceConfigModel"/>
public class DeviceConfigModel : IDeviceConfigModel
{
    public int? Schema { get; set; }
    public JObject? Thresholds { get; set; }
    public JObject? Modes { get; set; }
    public JObject? ComponentOverrides { get; set; }
}
