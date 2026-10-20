using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>새 오버레이 이미지 처음 놓기 — 지도 회전과 무관하게 그림 비율 · 방향(왼쪽 &lt; 오른쪽)을 지킨다(2026-10-20 회전 지도에서 비율 깨짐 수정).</summary>
public class OverlayPlacementMathTests
{
    private const double Lat = 37.3948, Lng = 126.9676;

    [Fact]
    public void should_keep_pixel_aspect_in_meters_when_placed_north_up()
    {
        var b = OverlayPlacementMath.NorthUpBounds(Lat, Lng, 774, 948, 1.2)!.Value;
        double widthM = OverlayPlacementMath.MetersBetween(Lat, b.Left, Lat, b.Right);
        double heightM = OverlayPlacementMath.MetersBetween(b.Bottom, Lng, b.Top, Lng);
        Assert.Equal(774 * 1.2, widthM, 1);      // 0.05 m 안
        Assert.Equal(948 * 1.2, heightM, 1);
        Assert.Equal(774d / 948d, widthM / heightM, 3);
    }

    [Fact]
    public void should_keep_left_below_right_and_top_above_bottom_when_placed()
    {
        var b = OverlayPlacementMath.NorthUpBounds(Lat, Lng, 774, 948, 0.6)!.Value;
        Assert.True(b.Left < b.Right, $"왼쪽 {b.Left} < 오른쪽 {b.Right}");
        Assert.True(b.Top > b.Bottom, $"위 {b.Top} > 아래 {b.Bottom}");
        Assert.Equal(Lng, (b.Left + b.Right) / 2, 9);   // 지도 중심에 놓인다
        Assert.Equal(Lat, (b.Top + b.Bottom) / 2, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]   // 실기에서 비율이 깨진 회전
    [InlineData(-25)]
    [InlineData(180)]
    public void should_measure_same_meters_per_pixel_when_screen_is_rotated(double bearing)
    {
        // 화면 가로 100 px 가 지도에서 어느 방향을 가리키든(회전) 두 점 사이 거리는 같다 — 놓는 범위가 회전에 따라 바뀌지 않는다
        const double mpp = 0.8;
        double rad = bearing * System.Math.PI / 180d;
        double dxM = 100 * mpp * System.Math.Cos(rad), dyM = 100 * mpp * System.Math.Sin(rad);
        double probeLat = Lat + dyM / OverlayPlacementMath.MetersPerDegree;
        double probeLng = Lng + dxM / (OverlayPlacementMath.MetersPerDegree * System.Math.Cos(Lat * System.Math.PI / 180d));
        double measured = OverlayPlacementMath.MetersBetween(Lat, Lng, probeLat, probeLng) / 100d;
        Assert.Equal(mpp, measured, 2);
        var b = OverlayPlacementMath.NorthUpBounds(Lat, Lng, 774, 948, measured)!.Value;
        Assert.True(b.Left < b.Right && b.Top > b.Bottom);
    }

    [Theory]
    [InlineData(0, 100, 1)]
    [InlineData(100, 0, 1)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 100, double.NaN)]
    [InlineData(100, 100, double.PositiveInfinity)]
    public void should_return_null_when_size_or_scale_invalid(double w, double h, double mpp)
        => Assert.Null(OverlayPlacementMath.NorthUpBounds(Lat, Lng, w, h, mpp));
}
