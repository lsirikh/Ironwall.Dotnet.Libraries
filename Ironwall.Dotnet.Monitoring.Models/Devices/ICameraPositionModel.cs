using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ICameraPositionModel : IBaseModel
{
    double Altitude { get; set; }
    float Heading { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
}