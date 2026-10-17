using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/12/2025 10:47:35 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지리적 포인트를 표현하는 기본 구조체 (GMap 독립적)
/// </summary>
public struct GeoPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }

    public GeoPoint(double latitude, double longitude, double altitude = 0.0)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
    }

    public override string ToString()
    {
        return $"({Latitude:F6}, {Longitude:F6})";
    }
}