using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 오버레이 순수 수식 (속도·베어링·역전 판정) — WPF 무의존
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 마커의 속도(m/s)·방향(bearing)·observed_at 역전을 계산하는 순수 함수 모음.
/// <para>WPF/GMap 무의존 — 단위 테스트(net8.0 격리)에서 동치 검증 가능(GMaps.Ui.Tests 복제 패턴).</para>
/// </summary>
public static class TrackingMath
{
    private const double EarthRadiusM = 6_371_000d;

    /// <summary>WGS84 두 점 사이 하버사인 거리(m).</summary>
    public static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        double dLat = ToRad(lat2 - lat1);
        double dLng = ToRad(lng2 - lng1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                 * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusM * c;
    }

    /// <summary>
    /// 속도(m/s). 가드: dt&lt;=0 또는 maxSpeedMs 초과 → null(호출자가 이전값 유지 + Warning).
    /// 첫 메시지(prev 없음)는 호출자가 0 처리.
    /// </summary>
    public static double? ComputeSpeedMps(double distanceMeters, double dtSeconds, double maxSpeedMs)
    {
        if (dtSeconds <= 0d) return null;                 // δt≤0 → 이전 유지
        double speed = distanceMeters / dtSeconds;
        if (maxSpeedMs > 0d && speed > maxSpeedMs) return null;  // 이상치 → 이전 유지
        return speed;
    }

    /// <summary>이동 방향(0~360°, 북=0, 시계방향). atan2 기반.</summary>
    public static double BearingDegrees(double lat1, double lng1, double lat2, double lng2)
    {
        double φ1 = ToRad(lat1), φ2 = ToRad(lat2);
        double dλ = ToRad(lng2 - lng1);
        double y = Math.Sin(dλ) * Math.Cos(φ2);
        double x = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(dλ);
        double θ = Math.Atan2(y, x);
        double deg = θ * 180d / Math.PI;
        return (deg + 360d) % 360d;
    }

    /// <summary>
    /// observed_at 역전 판정 — incoming이 stored보다 같거나 과거면 true(=skip 대상, §8.3.7 line 2202).
    /// stored가 null(첫 관측)이면 false(수용).
    /// </summary>
    public static bool IsReversed(DateTime incomingObservedAt, DateTime? storedObservedAt)
        => storedObservedAt.HasValue && incomingObservedAt <= storedObservedAt.Value;

    /// <summary>위경도 유효성(범위·NaN·Inf) — FR-P1-10.</summary>
    public static bool IsValidLatLng(double lat, double lng)
        => !double.IsNaN(lat) && !double.IsNaN(lng)
           && !double.IsInfinity(lat) && !double.IsInfinity(lng)
           && lat is >= -90d and <= 90d
           && lng is >= -180d and <= 180d;

    /// <summary>
    /// 트레일 세그먼트 페이드 Opacity — 최신(<paramref name="segmentIndex"/>=n-2)=1.0 → 오래됨(0)=0.15 선형감쇠.
    /// 점이 2개 이하면 1.0. (FR-P2-01)
    /// </summary>
    public static double TrailFadeOpacity(int segmentIndex, int pointCount)
    {
        if (pointCount <= 2) return 1.0;
        double t = (double)segmentIndex / (pointCount - 2);   // 0(오래됨)~1(최신)
        if (t < 0d) t = 0d; else if (t > 1d) t = 1d;
        return 0.15 + 0.85 * t;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180d;
}
