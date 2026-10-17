using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>PRD FR-05 — 35° 피치 카메라 + Z 신장(1/sin35) 으로 지면 평면이 2D 타일과 1:1 이어야 한다(분석 C-1).</summary>
public class FenceMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    [InlineData(-37.5, 12.25)]
    public void should_keep_ground_plane_identity_when_projecting(double x, double z)
    {
        var (sx, sy) = FenceMath.Project(x, 0, z, 500, 300);
        Assert.Equal(500 + x, sx, 9);
        Assert.Equal(300 - z, sy, 9);   // 북(+z) 은 화면 위(−y)
    }

    [Fact]
    public void should_lift_height_by_cos_pitch()
    {
        var (_, sy0) = FenceMath.Project(0, 0, 0, 0, 0);
        var (_, sy1) = FenceMath.Project(0, 10, 0, 0, 0);
        Assert.Equal(-10 * FenceMath.CosPitch, sy1 - sy0, 9);
        Assert.Equal(0.8192, FenceMath.CosPitch, 4);
        Assert.Equal(1.7434, FenceMath.GroundStretch, 4);   // 1/sin35
    }

    [Fact]
    public void should_convert_fence_height_to_tile_pixels_without_digital_scale()
    {
        Assert.Equal(2.4 / 0.4738, FenceMath.HeightPx(2.4, 0.4738), 6);
        Assert.Equal(0, FenceMath.HeightPx(2.4, 0));
    }

    [Theory]
    [InlineData(333, false, 1000, false)]   // 333·38 = 12.6k ≤ 20k
    [InlineData(333, true, 1000, false)]    // 333·50 = 16.6k ≤ 20k
    [InlineData(1000, false, 1000, true)]   // 1.0m 간격 1km: 38k > 20k
    [InlineData(1000, true, 3000, false)]   // 3km 라인은 예산 3배
    public void should_flag_budget_when_segments_exceed(int segments, bool sensor, double lengthM, bool expected)
        => Assert.Equal(expected, FenceMath.ExceedsBudget(segments, sensor, lengthM));
}
