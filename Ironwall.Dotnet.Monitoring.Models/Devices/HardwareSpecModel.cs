using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <inheritdoc cref="IHardwareSpecModel"/>
public class HardwareSpecModel : IHardwareSpecModel
{
    public int? Schema { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Serial { get; set; }
    public string? Firmware { get; set; }
    public string? HardwareRev { get; set; }
    public string? MacAddress { get; set; }
    public double? MaxDetectionRange { get; set; }
    public string? OnvifVersion { get; set; }
    public JObject? Spec { get; set; }
    public IList<ComponentDefinitionModel> Components { get; } = new List<ComponentDefinitionModel>();
}
