using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ICameraPresetModel : IBaseModel
{
    int Delay { get; set; }
    string? Description { get; set; }
    string? Name { get; set; }
    float Pitch { get; set; }
    int Preset { get; set; }
    float Tilt { get; set; }
    float Zoom { get; set; }
}