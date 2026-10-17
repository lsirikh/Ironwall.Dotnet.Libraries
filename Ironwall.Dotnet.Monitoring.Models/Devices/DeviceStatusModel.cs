using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <inheritdoc cref="IDeviceStatusModel"/>
public class DeviceStatusModel : IDeviceStatusModel
{
    public int? Schema { get; set; }
    public IDictionary<string, ComponentStatusModel> Components { get; } = new Dictionary<string, ComponentStatusModel>(StringComparer.Ordinal);
}
