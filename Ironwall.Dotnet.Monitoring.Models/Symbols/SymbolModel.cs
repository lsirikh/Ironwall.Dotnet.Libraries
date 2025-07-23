using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/22/2025 6:09:29 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지도의 GMap 심볼용 기본 모델
/// </summary>
public class SymbolModel : BaseModel
{
    #region - Ctors -
    public SymbolModel()
    {
        // 기본값 설정
        Id = 0;
        Pid = 0;
        Title = "Unknown";
        OperationState = EnumOperationState.NONE;
        Latitude = 0.0;       // 순수 double 타입
        Longitude = 0.0;      // 순수 double 타입
        Altitude = 0;
        Pitch = 0;
        Roll = 0;
        Width = 30;
        Height = 30;
        Bearing = 0;
        Category = EnumMarkerCategory.BASIC_SHAPES;
        Visibility = true;
    }

    public SymbolModel(uint id, string title, double latitude, double longitude)
    {
        Id = id;
        Title = title;
        Latitude = latitude;
        Longitude = longitude;

        // 기본값
        Pid = 0;
        OperationState = EnumOperationState.NONE;
        Altitude = 0;
        Pitch = 0;
        Roll = 0;
        Width = 30;
        Height = 30;
        Bearing = 0;
        Category = EnumMarkerCategory.BASIC_SHAPES;
        Visibility = true;
    }
    #endregion

    #region - 기본 식별 속성 -
    public uint Id { get; set; }
    public uint Pid { get; set; }
    public string Title { get; set; }
    #endregion

    #region - 타입 및 상태 속성 -
    public EnumOperationState OperationState { get; set; }
    #endregion

    #region - 위치 및 방향 속성 -
    /// <summary>
    /// 위도 (편의 속성)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// 경도 (편의 속성)
    /// </summary>
    public double Longitude { get; set; }

    public float Altitude { get; set; }
    public float Pitch { get; set; }
    public float Roll { get; set; }
    public double Bearing { get; set; }
    #endregion

    #region - 시각적 표현 속성 -
    public double Width { get; set; }
    public double Height { get; set; }
    public EnumMarkerCategory Category { get; set; }
    public bool Visibility { get; set; }
    #endregion


    #region - 유틸리티 메서드 -

    /// <summary>
    /// 위치 유효성 검사
    /// </summary>
    /// <returns>위치가 유효하면 true</returns>
    public bool IsValidLocation()
    {
        return Latitude >= -90.0 && Latitude <= 90.0 &&
                Longitude >= -180.0 && Longitude <= 180.0;
    }

    /// <summary>
    /// 두 지점 간의 거리 계산 (Haversine formula)
    /// </summary>
    public double DistanceTo(SymbolModel other)
    {
        if (other == null)
            return double.MaxValue;

        const double R = 6371000; // 지구 반지름 (미터)

        double lat1Rad = Latitude * Math.PI / 180.0;
        double lat2Rad = other.Latitude * Math.PI / 180.0;
        double deltaLatRad = (other.Latitude - Latitude) * Math.PI / 180.0;
        double deltaLngRad = (other.Longitude - Longitude) * Math.PI / 180.0;

        double a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c; // 미터 단위로 반환
    }

    /// <summary>
    /// 방향각 계산 (다른 심볼로의)
    /// </summary>
    public double BearingTo(SymbolModel other)
    {
        if (other == null)
            return 0.0;

        double lat1Rad = Latitude * Math.PI / 180.0;
        double lat2Rad = other.Latitude * Math.PI / 180.0;
        double deltaLngRad = (other.Longitude - Longitude) * Math.PI / 180.0;

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
    public void SetLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// 위치 오프셋 적용
    /// </summary>
    public void OffsetLocation(double latOffset, double lngOffset)
    {
        Latitude += latOffset;
        Longitude += lngOffset;
    }

    public SymbolModel Clone()
    {
        return new SymbolModel
        {
            Id = this.Id,
            Pid = this.Pid,
            Title = this.Title,
            Latitude = this.Latitude,
            Longitude = this.Longitude,
            Altitude = this.Altitude,
            Pitch = this.Pitch,
            Roll = this.Roll,
            Bearing = this.Bearing,
            OperationState = this.OperationState,
            Width = this.Width,
            Height = this.Height,
            Category = this.Category,
            Visibility = this.Visibility,
        };
    }

    public override bool Equals(object obj)
    {
        if (obj is SymbolModel other)
            return Id == other.Id && Category == other.Category;
        return false;
    }

    public override string ToString()
    {
        var locationStr = $"({Latitude:F6}, {Longitude:F6})";
        return $"Symbol[{Id}] {Title} ({Category}) - {OperationState} at {locationStr}";
    }
    #endregion
}