using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 새 오버레이 이미지를 지도에 처음 놓는 범위 — 지도 회전과 무관하게 그림 비율을 지킨다(순수 · WPF/GMap 무의존).
   Note         : ★ 2026-10-20 실기: 지도를 65° 돌린 채 그림을 올리자 화면 왼쪽 위 · 오른쪽 아래 두 점을 위경도로 바꾼 범위가
                    왼쪽 > 오른쪽으로 뒤집히고 비율이 깨졌다(회전된 화면의 두 모서리는 북쪽 위 상자의 모서리가 아니다).
                  ★ 지금 화면의 1 px = 몇 m(두 화면 점 사이 실제 거리 — 회전해도 같다) × 그림 픽셀 크기 = 실제 가로 · 세로(m),
                    지도 중심에 북쪽 위로 놓는다. 좁은 범위라 동서 · 남북 같은 m/도 상수로 비율이 그대로 남는다(메르카토르 등각).
   Created On   : 2026-10-20 · Sensorway Co., Ltd.
****************************************************************************/
public static class OverlayPlacementMath
{
    /// <summary>지구 평균 반지름(m) — 거리(하버사인)와 1도 길이를 같은 값으로 셈해 서로 맞게 한다.</summary>
    public const double EarthRadiusM = 6_371_008.8;

    /// <summary>위도 1도의 길이(m) — 동서는 여기에 cos(위도)를 곱한다.</summary>
    public const double MetersPerDegree = EarthRadiusM * Math.PI / 180d;

    /// <summary>두 위경도 사이 실제 거리(m, 하버사인).</summary>
    public static double MetersBetween(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = EarthRadiusM;
        double p1 = lat1 * Math.PI / 180d, p2 = lat2 * Math.PI / 180d;
        double dp = p2 - p1, dl = (lng2 - lng1) * Math.PI / 180d;
        double a = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 2 * R * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }

    /// <summary>
    /// 중심(위경도)에 그림(widthPx × heightPx)을 북쪽 위로 놓는 범위. metersPerPixel = 지금 화면 1 px 의 실제 길이.
    /// 돌려주는 값은 늘 Left &lt; Right · Top &gt; Bottom. 크기 · 축척이 0 이하면 null.
    /// </summary>
    public static (double Left, double Top, double Right, double Bottom)? NorthUpBounds(
        double centerLat, double centerLng, double widthPx, double heightPx, double metersPerPixel)
    {
        if (!(widthPx > 0) || !(heightPx > 0) || !(metersPerPixel > 0) || double.IsInfinity(metersPerPixel)) return null;
        double cos = Math.Max(1e-6, Math.Cos(centerLat * Math.PI / 180d));
        double halfLng = widthPx * metersPerPixel / 2d / (MetersPerDegree * cos);
        double halfLat = heightPx * metersPerPixel / 2d / MetersPerDegree;
        return (centerLng - halfLng, centerLat + halfLat, centerLng + halfLng, centerLat - halfLat);
    }
}
