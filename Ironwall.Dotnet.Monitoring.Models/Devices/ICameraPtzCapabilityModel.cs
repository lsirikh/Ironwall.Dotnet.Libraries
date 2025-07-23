using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ICameraPtzCapabilityModel : IBaseModel
{
    float HorizontalFov { get; set; }
    float MaxPan { get; set; }
    float MaxTilt { get; set; }
    float MaxVisibleDistance { get; set; }
    float MaxZoom { get; set; }
    float MinPan { get; set; }
    float MinTilt { get; set; }
    float MinZoom { get; set; }
    float VerticalFov { get; set; }
    int ZoomLevel { get; set; }
}