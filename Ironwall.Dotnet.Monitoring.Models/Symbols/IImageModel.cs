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
    /// <summary>잠금 상태 — true면 맵에서 클릭/선택 불가(레이어 패널에서만 토글). 기본 false.</summary>
    bool IsLocked { get; set; }
    double Width { get; set; }

    /// <summary>
    /// 이미지가 표시되는 최소 줌 레벨.
    /// 지도 줌이 이 값 이상일 때만 이미지가 표시됨.
    /// </summary>
    double Zoom { get; set; }

    /// <summary>
    /// 런타임 Z-Order 홀더. MapLayers.ZOrder에서 주입받음 (DB 컬럼 없음).
    /// </summary>
    int ZOrder { get; set; }
}