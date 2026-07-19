using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : SubPixelGeo 회귀 테스트 — 디지털줌 드래그 서브픽셀 보간
                  (심볼 이동 뻑뻑함/양자화 방지)
   Created By   : GHLee
   Created On   : 7/19/2026
****************************************************************************/
public class SubPixelGeoTests
{
    // 4 코너: lng은 x축(fx), lat은 y축(fy)으로 1씩 증가하도록 구성
    private static readonly (double lat, double lng) P00 = (10, 100);   // (x0,y0)
    private static readonly (double lat, double lng) P10 = (10, 101);   // (x0+1,y0)
    private static readonly (double lat, double lng) P01 = (11, 100);   // (x0,y0+1)
    private static readonly (double lat, double lng) P11 = (11, 101);   // (x0+1,y0+1)

    [Fact]
    public void should_return_corner_p00_when_fractions_zero()
    {
        var (lat, lng) = SubPixelGeo.Bilinear(P00, P10, P01, P11, 0.0, 0.0);
        Assert.Equal(10.0, lat, 9);
        Assert.Equal(100.0, lng, 9);
    }

    [Fact]
    public void should_return_corner_p11_when_fractions_one()
    {
        var (lat, lng) = SubPixelGeo.Bilinear(P00, P10, P01, P11, 1.0, 1.0);
        Assert.Equal(11.0, lat, 9);
        Assert.Equal(101.0, lng, 9);
    }

    [Fact]
    public void should_interpolate_subpixel_smoothly()
    {
        // fx=0.25(경도), fy=0.5(위도) → lng=100.25, lat=10.5 (정수 반올림이면 100/101·10/11로 튐 = 뻑뻑함)
        var (lat, lng) = SubPixelGeo.Bilinear(P00, P10, P01, P11, 0.25, 0.5);
        Assert.Equal(10.5, lat, 9);
        Assert.Equal(100.25, lng, 9);
    }

    [Fact]
    public void should_be_center_average_at_half_half()
    {
        var (lat, lng) = SubPixelGeo.Bilinear(P00, P10, P01, P11, 0.5, 0.5);
        Assert.Equal(10.5, lat, 9);
        Assert.Equal(100.5, lng, 9);
    }
}
