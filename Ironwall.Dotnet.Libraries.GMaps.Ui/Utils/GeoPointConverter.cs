using System;
using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/12/2025 10:50:44 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// GeoPoint와 PointLatLng 간 변환 헬퍼
    /// </summary>
    public static class GeoPointConverter
    {
        /// <summary>
        /// GeoPoint를 PointLatLng로 변환
        /// </summary>
        public static PointLatLng ToPointLatLng(GeoPoint geoPoint)
        {
            return new PointLatLng(geoPoint.Latitude, geoPoint.Longitude);
        }

        /// <summary>
        /// PointLatLng를 GeoPoint로 변환
        /// </summary>
        public static GeoPoint ToGeoPoint(PointLatLng point, double altitude = 0.0)
        {
            return new GeoPoint(point.Lat, point.Lng, altitude);
        }

        /// <summary>
        /// GeoPoint 리스트를 PointLatLng 리스트로 변환
        /// </summary>
        public static List<PointLatLng> ToPointLatLngList(List<GeoPoint> geoPoints)
        {
            var result = new List<PointLatLng>();
            foreach (var point in geoPoints ?? new List<GeoPoint>())
            {
                result.Add(ToPointLatLng(point));
            }
            return result;
        }

        /// <summary>
        /// PointLatLng 리스트를 GeoPoint 리스트로 변환
        /// </summary>
        public static List<GeoPoint> ToGeoPointList(List<PointLatLng> points)
        {
            var result = new List<GeoPoint>();
            foreach (var point in points ?? new List<PointLatLng>())
            {
                result.Add(ToGeoPoint(point));
            }
            return result;
        }

        /// <summary>
        /// 두 GeoPoint 간의 거리 계산 (미터 단위)
        /// </summary>
        public static double CalculateDistance(GeoPoint point1, GeoPoint point2)
        {
            const double earthRadius = 6371000; // 지구 반지름 (미터)

            var lat1Rad = point1.Latitude * Math.PI / 180;
            var lat2Rad = point2.Latitude * Math.PI / 180;
            var deltaLat = (point2.Latitude - point1.Latitude) * Math.PI / 180;
            var deltaLng = (point2.Longitude - point1.Longitude) * Math.PI / 180;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLng / 2) * Math.Sin(deltaLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadius * c;
        }

        /// <summary>
        /// 라인의 총 길이 계산 (미터 단위)
        /// </summary>
        public static double CalculateTotalDistance(List<GeoPoint> points)
        {
            if (points == null || points.Count < 2)
                return 0.0;

            double totalDistance = 0.0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalDistance += CalculateDistance(points[i], points[i + 1]);
            }

            return totalDistance;
        }
    }
}