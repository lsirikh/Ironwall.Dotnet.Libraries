using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>PRD FR-06 — LOD 는 줌 숫자가 아니라 간격 픽셀(분석 C-10). z18·위도 37.5° 에서 3m = 6.3px.</summary>
public class FenceLodTests
{
    [Theory]
    [InlineData(3.9, FenceLodLevel.Line2D)]
    [InlineData(4.0, FenceLodLevel.PostsAndThickLine)]
    [InlineData(9.9, FenceLodLevel.PostsAndThickLine)]
    [InlineData(10.0, FenceLodLevel.Full3D)]
    [InlineData(40.0, FenceLodLevel.Full3D)]
    [InlineData(double.NaN, FenceLodLevel.Line2D)]
    [InlineData(double.PositiveInfinity, FenceLodLevel.Line2D)]
    public void should_select_level_when_px_per_spacing_is(double px, FenceLodLevel expected)
        => Assert.Equal(expected, FenceLod.Select(px));

    [Fact]
    public void should_compute_meters_per_pixel_like_line_drawing_service()
    {
        // 156543.03392·cos(37.5°)/2^18 ≈ 0.4738 m/px
        Assert.Equal(0.4738, FenceLod.MetersPerPixel(37.5, 18), 3);
        Assert.Equal(0.9476, FenceLod.MetersPerPixel(37.5, 17), 3);
    }

    [Fact]
    public void should_report_3m_spacing_as_6px_at_z18_and_posts_level()
    {
        double px = FenceLod.PxPerSpacing(3.0, 1.0, FenceLod.MetersPerPixel(37.5, 18));
        Assert.Equal(6.33, px, 2);
        Assert.Equal(FenceLodLevel.PostsAndThickLine, FenceLod.Select(px));
        // 소프트밴드 2× (18.5++) 에서 12.7px → Full3D
        Assert.Equal(FenceLodLevel.Full3D, FenceLod.Select(37.5, 18, 2.0, 3.0));
        // z16 에서 3m = 1.6px → 2D
        Assert.Equal(FenceLodLevel.Line2D, FenceLod.Select(37.5, 16, 1.0, 3.0));
    }

    [Theory]
    [InlineData(0, 1, 0.5)]
    [InlineData(3, 0, 0.5)]
    [InlineData(3, 1, 0)]
    public void should_return_nan_when_inputs_invalid(double spacing, double scale, double mpp)
        => Assert.True(double.IsNaN(FenceLod.PxPerSpacing(spacing, scale, mpp)));

    [Theory]
    [InlineData(true, true, FenceLodLevel.Full3D, true)]
    [InlineData(false, true, FenceLodLevel.Full3D, false)]
    [InlineData(true, false, FenceLodLevel.Full3D, false)]
    [InlineData(true, true, FenceLodLevel.PostsAndThickLine, false)]
    public void should_gate_3d_by_flag_and_group_and_lod(bool flag, bool group, FenceLodLevel lod, bool expected)
        => Assert.Equal(expected, FenceLod.Show3D(flag, group, lod));
}
