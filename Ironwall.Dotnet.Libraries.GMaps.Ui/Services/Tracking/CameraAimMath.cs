namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 카메라 "특정 위치 확인" 반경 판정 순수 수식 — WPF/GMap 무의존
                  클릭 좌표가 카메라 중심 기준 반경 내인지 지오 도메인에서 판정
                  (줌 무관). 픽셀 변환은 오버레이 그리기 전용이며 판정과 분리.
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 카메라 중심 반경 클릭 판정 순수 함수. <see cref="TrackingMath"/> 재사용.
/// <para>WPF/GMap 무의존 — 단위 테스트(net8.0 격리)에서 동치 검증 가능.</para>
/// </summary>
public static class CameraAimMath
{
    /// <summary>
    /// 클릭 좌표가 카메라 중심(center) 기준 반경 <paramref name="radiusMeters"/> 이내인지(경계 포함).
    /// <para>반경 0 이하·무효 좌표(NaN/Inf/범위초과)는 false.</para>
    /// </summary>
    public static bool IsWithinRadius(
        double centerLat, double centerLng,
        double clickLat, double clickLng,
        double radiusMeters)
    {
        if (radiusMeters <= 0d) return false;
        if (!TrackingMath.IsValidLatLng(centerLat, centerLng)) return false;
        if (!TrackingMath.IsValidLatLng(clickLat, clickLng)) return false;
        return TrackingMath.HaversineMeters(centerLat, centerLng, clickLat, clickLng) <= radiusMeters;
    }

    /// <summary>
    /// aim 반경(m) 선택 — 우선순위: ①카메라 최대탐지거리 → ②심볼 탐지범위 → ③글로벌 폴백.
    /// 각 단계 값이 0 이하/null이면 다음으로. 결과는 [1, maxClamp]로 클램프.
    /// </summary>
    public static double ResolveAimRadius(double? maxDetectionRange, double detectionRange, double globalDefault, double maxClamp = 5000d)
    {
        double r = maxDetectionRange.GetValueOrDefault();
        if (r <= 0d) r = detectionRange;
        if (r <= 0d) r = globalDefault;
        if (r <= 0d) r = 30d;                  // 최후 기본값
        if (r < 1d) r = 1d;
        if (r > maxClamp) r = maxClamp;
        return r;
    }
}
