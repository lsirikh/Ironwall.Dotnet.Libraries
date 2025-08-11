using System;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      : GIS 좌표 변환 정보                                                       
   Created By   : GHLee                                                
   Created On   : 7/28/2025 7:43:11 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GeoTransformInfo
{
    public bool HasGeoReference { get; set; }
    public string CoordinateSystem { get; set; } = "WGS84";
    public string EpsgCode { get; set; } = "4326";

    // 경계 정보
    public double MinLongitude { get; set; }
    public double MaxLongitude { get; set; }
    public double MinLatitude { get; set; }
    public double MaxLatitude { get; set; }

    // 해상도 정보
    public double PixelSizeX { get; set; }
    public double PixelSizeY { get; set; }

    // GDAL 스타일 GeoTransform 배열 (6개 원소)
    // [originX, pixelWidth, rotationX, originY, rotationY, -pixelHeight]
    public double[] GeoTransformArray { get; set; }

    /// <summary>
    /// 지리적 중심점 계산
    /// </summary>
    public PointLatLng GetCenter()
    {
        return new PointLatLng(
            (MinLatitude + MaxLatitude) / 2.0,
            (MinLongitude + MaxLongitude) / 2.0
        );
    }

    /// <summary>
    /// 지리적 경계를 RectLatLng으로 변환
    /// </summary>
    public RectLatLng ToRect()
    {
        return new RectLatLng(
            MaxLatitude,
            MinLongitude,
            MaxLongitude - MinLongitude,
            MaxLatitude - MinLatitude
        );
    }

    /// <summary>
    /// 좌표계 정보 문자열
    /// </summary>
    public override string ToString()
    {
        if (!HasGeoReference)
            return "지리참조 없음";

        return $"GeoTIFF: {CoordinateSystem} (EPSG:{EpsgCode}), " +
               $"경계: ({MinLongitude:F6}, {MinLatitude:F6}) ~ ({MaxLongitude:F6}, {MaxLatitude:F6}), " +
               $"해상도: {PixelSizeX:F8} x {PixelSizeY:F8}";
    }
}