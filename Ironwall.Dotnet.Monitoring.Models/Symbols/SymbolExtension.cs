using System;
using System.Buffers;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 5:30:04 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 심볼 전용 확장 메서드
/// </summary>
public static class SymbolExtension
{

    #region - 유틸리티 메서드 -

    /// <summary>
    /// 위치 유효성 검사
    /// </summary>
    /// <returns>위치가 유효하면 true</returns>
    public static bool IsValidLocation(this ISymbolModel symbol)
    {
        return symbol.Latitude >= -90.0 && symbol.Latitude <= 90.0 &&
                symbol.Longitude >= -180.0 && symbol.Longitude <= 180.0;
    }

    /// <summary>
    /// 두 지점 간의 거리 계산 (Haversine formula)
    /// </summary>
    public static double DistanceTo(this ISymbolModel symbol, SymbolModel other)
    {
        if (other == null)
            return double.MaxValue;

        const double R = 6371000; // 지구 반지름 (미터)

        double lat1Rad = symbol.Latitude * Math.PI / 180.0;
        double lat2Rad = other.Latitude * Math.PI / 180.0;
        double deltaLatRad = (other.Latitude - symbol.Latitude) * Math.PI / 180.0;
        double deltaLngRad = (other.Longitude - symbol.Longitude) * Math.PI / 180.0;

        double a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c; // 미터 단위로 반환
    }

    /// <summary>
    /// 방향각 계산 (다른 심볼로의)
    /// </summary>
    public static double BearingTo(this ISymbolModel symbol, SymbolModel other)
    {
        if (other == null)
            return 0.0;

        double lat1Rad = symbol.Latitude * Math.PI / 180.0;
        double lat2Rad = other.Latitude * Math.PI / 180.0;
        double deltaLngRad = (other.Longitude - symbol.Longitude) * Math.PI / 180.0;

        double y = Math.Sin(deltaLngRad) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                    Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(deltaLngRad);

        double bearingRad = Math.Atan2(y, x);
        double bearingDeg = bearingRad * 180.0 / Math.PI;

        return (bearingDeg + 360.0) % 360.0; // 0-360도 범위로 정규화
    }

    /// <summary>
    /// 위치 설정 (편의 메서드)
    /// </summary>
    public static void SetLocation(this ISymbolModel symbol, double latitude, double longitude)
    {
        symbol.Latitude = latitude;
        symbol.Longitude = longitude;
    }

    /// <summary>
    /// 위치 오프셋 적용
    /// </summary>
    public static void OffsetLocation(this ISymbolModel symbol, double latOffset, double lngOffset)
    {
        symbol.Latitude += latOffset;
        symbol.Longitude += lngOffset;
    }

    public static SymbolModel Clone(this ISymbolModel symbol)
    {
        return new SymbolModel
        {
            Id = symbol.Id,
            Pid = symbol.Pid,
            Title = symbol.Title,
            Latitude = symbol.Latitude,
            Longitude = symbol.Longitude,
            Altitude = symbol.Altitude,
            Pitch = symbol.Pitch,
            Roll = symbol.Roll,
            Bearing = symbol.Bearing,
            OperationState = symbol.OperationState,
            Width = symbol.Width,
            Height = symbol.Height,
            Category = symbol.Category,
            Visibility = symbol.Visibility,
        };
    }

    public static bool Equals(this ISymbolModel symbol, object obj)
    {
        if (obj is ISymbolModel other)
            return symbol.Id == other.Id && symbol.Category == other.Category;
        return false;
    }

    public static string ToString(this ISymbolModel symbol)
    {
        var locationStr = $"({symbol.Latitude:F6}, {symbol.Longitude:F6})";
        return $"Symbol[{symbol.Id}] {symbol.Title} ({symbol.Category}) - {symbol.OperationState} at {locationStr}";
    }
    #endregion
}
