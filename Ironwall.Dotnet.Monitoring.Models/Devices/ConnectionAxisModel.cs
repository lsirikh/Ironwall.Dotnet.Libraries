using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <inheritdoc cref="IConnectionAxisModel"/>
public class ConnectionAxisModel : IConnectionAxisModel
{
    public string? Type { get; set; }
    public string? IpAddress { get; set; }
    public int? IpPort { get; set; }
    public string? UserName { get; set; }
    public string? UserPassword { get; set; }
    public int? ParentDeviceId { get; set; }
    public int? Channel { get; set; }
    public string? Protocol { get; set; }
    public IDictionary<string, string?> Urls { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
