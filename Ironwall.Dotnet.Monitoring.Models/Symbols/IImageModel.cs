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

    /// <summary>
    /// 이미지가 표시되는 최소 줌 레벨.
    /// 지도 줌이 이 값 이상일 때만 이미지가 표시됨.
    /// </summary>
    double Zoom { get; set; }
}