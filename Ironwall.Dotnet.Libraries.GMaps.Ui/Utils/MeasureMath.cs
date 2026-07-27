using System;
using System.Collections.Generic;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 측정 툴(길이·넓이) 지오데식 계산 — 위경도 도메인, 줌·위도 무관 정확.
                  Measure_Tools FR-01. 화면 픽셀 shoelace 금지(MBTiles EPSG:3857 이중 왜곡).
   Note         : 거리·면적 모두 R=6378137(GMap.NET Axis)로 일관(기존 GeoPointConverter는
                  R=6371000이라 라인 표시용과 0.1% 차 — 측정툴은 자체 일관값 사용, 공용 헬퍼 미변경).
   Created On   : 2026-07-24 · Sensorway Co., Ltd.
****************************************************************************/
public static class MeasureMath
{
    /// <summary>지구 반경(m) — GMap.NET MercatorProjection.Axis와 동일. 길이·면적 공통.</summary>
    public const double EarthRadius = 6378137.0;

    private const double Deg2Rad = Math.PI / 180.0;

    /// <summary>두 점 사이 측지거리(m) — Haversine(R=6378137).</summary>
    public static double DistanceMeters(PointLatLng a, PointLatLng b)
    {
        double lat1 = a.Lat * Deg2Rad, lat2 = b.Lat * Deg2Rad;
        double dLat = (b.Lat - a.Lat) * Deg2Rad;
        double dLng = (b.Lng - a.Lng) * Deg2Rad;
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(Math.Max(0d, 1 - h)));
        return EarthRadius * c;
    }

    /// <summary>폴리라인 총 길이(m) — 연속 구간 측지거리 합. 점 2개 미만=0.</summary>
    public static double TotalLengthMeters(IReadOnlyList<PointLatLng> pts)
    {
        if (pts == null || pts.Count < 2) return 0d;
        double sum = 0d;
        for (int i = 1; i < pts.Count; i++)
            sum += DistanceMeters(pts[i - 1], pts[i]);
        return sum;
    }

    /// <summary>닫힌 고리 둘레(m) — 폴리라인 길이 + 마지막→첫 점 닫는 변. 점 3개 미만=0.</summary>
    public static double PerimeterMeters(IReadOnlyList<PointLatLng> pts)
    {
        if (pts == null || pts.Count < 3) return 0d;
        double sum = TotalLengthMeters(pts);
        sum += DistanceMeters(pts[pts.Count - 1], pts[0]);   // 닫는 변
        return sum;
    }

    /// <summary>다각형 면적(m²) — 지오데식 구면초과 shoelace(Google computeArea 방식).
    /// A = |Σ (λ_{i+1}−λ_i)·(2 + sinφ_i + sinφ_{i+1})| · R²/2. 위도·범위·줌 무관 정확.
    /// 자동으로 고리를 닫으며(마지막→첫), 와인딩 무관(절대값). 점 3개 미만=0. NaN/Inf=0.</summary>
    public static double AreaSquareMeters(IReadOnlyList<PointLatLng> pts)
    {
        if (pts == null || pts.Count < 3) return 0d;
        int n = pts.Count;
        double total = 0d;
        for (int i = 0; i < n; i++)
        {
            var p1 = pts[i];
            var p2 = pts[(i + 1) % n];          // 마지막→첫으로 자동 닫기
            double lng1 = p1.Lng * Deg2Rad, lng2 = p2.Lng * Deg2Rad;
            double lat1 = p1.Lat * Deg2Rad, lat2 = p2.Lat * Deg2Rad;
            total += (lng2 - lng1) * (2 + Math.Sin(lat1) + Math.Sin(lat2));
        }
        double area = Math.Abs(total * EarthRadius * EarthRadius / 2.0);
        return (double.IsNaN(area) || double.IsInfinity(area)) ? 0d : area;
    }
}
