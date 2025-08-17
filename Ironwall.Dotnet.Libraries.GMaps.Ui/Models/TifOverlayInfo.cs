using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using System;
using System.Windows.Media;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/30/2025 4:47:16 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class TifOverlayInfo
{
    public string Name { get; set; }
    public string FilePath { get; set; }
    public ImageSource ImageSource { get; set; }
    public RectLatLng Bounds { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public bool HasGeoReference { get; set; }
    public GeoTransformInfo GeoTransform { get; set; }
    public DateTime CreatedAt { get; set; }
    public double ZoomLevel { get; set; }
    public double Opacity { get; set; } = 0.7;
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 경계 정보를 문자열로 반환
    /// </summary>
    public string GetBoundsString()
    {
        return $"Top: {Bounds.Top:F6}, Left: {Bounds.Left:F6}, " +
               $"Bottom: {Bounds.Bottom:F6}, Right: {Bounds.Right:F6}";
    }

    /// <summary>
    /// 중심점 계산
    /// </summary>
    public PointLatLng GetCenter()
    {
        return new PointLatLng(
            Bounds.Top - Bounds.HeightLat / 2,
            Bounds.Left + Bounds.WidthLng / 2
        );
    }

    /// <summary>
    /// 커스텀 맵 생성용 옵션으로 변환
    /// </summary>
    public TifProcessingOptions ToProcessingOptions(int minZoom = 10, int maxZoom = 18)
    {
        return new TifProcessingOptions
        {
            UseManualCoordinates = true,
            ManualMinLatitude = Bounds.Bottom,
            ManualMinLongitude = Bounds.Left,
            ManualMaxLatitude = Bounds.Top,
            ManualMaxLongitude = Bounds.Right,
            MinZoom = minZoom,
            MaxZoom = maxZoom,
            TileSize = 256
        };
    }
}