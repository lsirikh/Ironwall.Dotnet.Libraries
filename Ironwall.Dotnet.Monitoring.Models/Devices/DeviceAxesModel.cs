using System;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <inheritdoc cref="IDeviceAxesModel"/>
public class DeviceAxesModel : IDeviceAxesModel
{
    public IConnectionAxisModel? Connection { get; set; }
    public IHardwareSpecModel? HardwareSpec { get; set; }
    public IDeviceStatusModel? DeviceStatus { get; set; }
    public IDeviceConfigModel? DeviceConfig { get; set; }
    public ResponseMeta? Meta { get; set; }

    public bool IsSectionReceived(string section)
        => Meta != null && Meta.Sections.Any(s => string.Equals(s, section, StringComparison.OrdinalIgnoreCase));

    public ComponentStatusModel? FindStatusByType(string componentType)
    {
        if (HardwareSpec == null || DeviceStatus == null) return null;

        var definition = HardwareSpec.Components
            .FirstOrDefault(c => string.Equals(c.Type, componentType, StringComparison.OrdinalIgnoreCase));
        if (definition == null) return null;

        return DeviceStatus.Components.TryGetValue(definition.Key, out var status) ? status : null;
    }
}
