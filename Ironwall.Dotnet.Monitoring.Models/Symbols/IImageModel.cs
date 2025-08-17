using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;

public interface IImageModel : IBaseModel
{
    float Altitude { get; set; }
    double Bottom { get; set; }
    string? CoordinateSystem { get; set; }
    string FilePath { get; set; }
    bool HasGeoReference { get; set; }
    double Height { get; set; }
    double Latitude { get; set; }
    double Left { get; set; }
    double Longitude { get; set; }
    double Opacity { get; set; }
    double Right { get; set; }
    double Rotation { get; set; }
    string? Title { get; set; }
    double Top { get; set; }
    bool Visibility { get; set; }
    double Width { get; set; }
}