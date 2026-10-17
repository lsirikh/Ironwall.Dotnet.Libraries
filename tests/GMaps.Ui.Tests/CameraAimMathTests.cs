using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 카메라 "특정 위치 확인" 반경 판정(<see cref="CameraAimMath"/>) 단위 테스트.
/// </summary>
public class CameraAimMathTests
{
    private const double CenterLat = 37.5665;   // 서울시청 인근
    private const double CenterLng = 126.9780;

    [Fact]
    public void should_return_true_when_click_at_center()
    {
        Assert.True(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, CenterLat, CenterLng, 30d));
    }

    [Fact]
    public void should_return_true_when_click_inside_radius()
    {
        var (lat, lng) = OffsetEastMeters(CenterLat, CenterLng, 10d);   // 동쪽 ~10m
        Assert.True(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, lat, lng, 30d));
    }

    [Fact]
    public void should_return_true_when_click_just_inside_boundary()
    {
        var (lat, lng) = OffsetEastMeters(CenterLat, CenterLng, 29.9d);  // 경계 포함(<=) 검증
        Assert.True(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, lat, lng, 30d));
    }

    [Fact]
    public void should_return_false_when_click_outside_radius()
    {
        var (lat, lng) = OffsetEastMeters(CenterLat, CenterLng, 50d);   // 반경 30m 밖
        Assert.False(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, lat, lng, 30d));
    }

    [Fact]
    public void should_return_false_when_radius_is_zero_or_negative()
    {
        Assert.False(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, CenterLat, CenterLng, 0d));
        Assert.False(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, CenterLat, CenterLng, -5d));
    }

    [Fact]
    public void should_return_false_when_center_coords_invalid()
    {
        Assert.False(CameraAimMath.IsWithinRadius(double.NaN, CenterLng, CenterLat, CenterLng, 30d));
        Assert.False(CameraAimMath.IsWithinRadius(200d, CenterLng, CenterLat, CenterLng, 30d));
    }

    [Fact]
    public void should_return_false_when_click_coords_invalid()
    {
        Assert.False(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, double.NaN, CenterLng, 30d));
        Assert.False(CameraAimMath.IsWithinRadius(CenterLat, CenterLng, CenterLat, 999d, 30d));
    }

    // ── ResolveAimRadius: 카메라 최대탐지거리 → 심볼 탐지범위 → 글로벌 폴백 우선순위 ──

    [Fact]
    public void should_use_max_detection_range_when_present()
    {
        Assert.Equal(120d, CameraAimMath.ResolveAimRadius(120d, 30d, 50d));
    }

    [Fact]
    public void should_fallback_to_detection_range_when_max_null_or_zero()
    {
        Assert.Equal(30d, CameraAimMath.ResolveAimRadius(null, 30d, 50d));
        Assert.Equal(30d, CameraAimMath.ResolveAimRadius(0d, 30d, 50d));
    }

    [Fact]
    public void should_fallback_to_global_when_max_and_detection_zero()
    {
        Assert.Equal(50d, CameraAimMath.ResolveAimRadius(null, 0d, 50d));
        Assert.Equal(50d, CameraAimMath.ResolveAimRadius(0d, 0d, 50d));
    }

    [Fact]
    public void should_default_30_when_all_zero()
    {
        Assert.Equal(30d, CameraAimMath.ResolveAimRadius(0d, 0d, 0d));
    }

    [Fact]
    public void should_clamp_to_max_when_exceeds()
    {
        Assert.Equal(5000d, CameraAimMath.ResolveAimRadius(99999d, 30d, 50d));
    }

    /// <summary>위도 고정, 경도만 동쪽으로 meters 이동(테스트용 근사).</summary>
    private static (double lat, double lng) OffsetEastMeters(double lat, double lng, double meters)
    {
        const double earthRadius = 6_371_000d;
        double latRad = lat * System.Math.PI / 180d;
        double dLng = meters / (earthRadius * System.Math.Cos(latRad)) * 180d / System.Math.PI;
        return (lat, lng + dLng);
    }
}
