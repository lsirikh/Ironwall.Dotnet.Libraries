using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ICameraOpticsModel : IBaseModel
{
    float FocalLength { get; set; }
    float HorizontalFOV { get; }
    float SensorHeight { get; set; }
    float SensorWidth { get; set; }
    float ViewDistance { get; }
    float ZoomLevel { get; set; }
}