using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : CompassMath 헤드리스 테스트 (GMap_Compass_Control NFR-04)
   Created On   : 2026-07-30 · Sensorway Co., Ltd.
****************************************************************************/
public class CompassMathTests
{
    // ── FormatReadout — canonical 부호+3자리+소수1 ──

    [Theory]
    [InlineData(0, "+000.0°")]
    [InlineData(35, "+035.0°")]
    [InlineData(-90, "-090.0°")]
    [InlineData(179.94, "+179.9°")]
    [InlineData(-180, "-180.0°")]
    public void should_format_canonical_readout_when_bearing_given(double bearing, string expected)
        => Assert.Equal(expected, CompassMath.FormatReadout(bearing));

    [Fact]
    public void should_format_zero_when_negative_zero_or_nan()
    {
        Assert.Equal("+000.0°", CompassMath.FormatReadout(-0.0));
        Assert.Equal("+000.0°", CompassMath.FormatReadout(double.NaN));
        Assert.Equal("+000.0°", CompassMath.FormatReadout(double.PositiveInfinity));
    }

    [Fact]
    public void should_wrap_to_canonical_when_bearing_out_of_range()
    {
        Assert.Equal("-160.0°", CompassMath.FormatReadout(200));    // 200 → -160
        Assert.Equal("-180.0°", CompassMath.FormatReadout(180));    // 180 → -180 ([-180,180))
        Assert.Equal("+005.0°", CompassMath.FormatReadout(365));
    }

    // ── ParseSize/ParseVariant — 관대 파싱(appsettings 오타 → 기본값, 부팅 크래시 금지) ──

    [Theory]
    [InlineData("S", CompassDialSize.S)]
    [InlineData("m", CompassDialSize.M)]
    [InlineData("L", CompassDialSize.L)]
    [InlineData("XL", CompassDialSize.M)]     // 오타 → 기본 M
    [InlineData(null, CompassDialSize.M)]
    [InlineData("", CompassDialSize.M)]
    public void should_parse_size_leniently_when_string_given(string? input, CompassDialSize expected)
        => Assert.Equal(expected, CompassMath.ParseSize(input));

    [Theory]
    [InlineData("Rose", CompassVariant.Rose)]
    [InlineData("ring", CompassVariant.Ring)]
    [InlineData("Badge", CompassVariant.Rose)] // 제외된 C/오타 → 기본 Rose
    [InlineData(null, CompassVariant.Rose)]
    public void should_parse_variant_leniently_when_string_given(string? input, CompassVariant expected)
        => Assert.Equal(expected, CompassMath.ParseVariant(input));

    [Theory]
    [InlineData(CompassDialSize.S, 64)]
    [InlineData(CompassDialSize.M, 96)]
    [InlineData(CompassDialSize.L, 128)]
    public void should_map_size_to_pixels_when_enum_given(CompassDialSize size, double expected)
        => Assert.Equal(expected, CompassMath.SizeToPixels(size));

    // ── RingBearingFromPointer — 12시=0, 시계방향 포인터=음수 bearing ──

    [Fact]
    public void should_return_zero_when_pointer_at_top()
        => Assert.Equal(0, CompassMath.RingBearingFromPointer(0, -50), 3);

    [Fact]
    public void should_return_minus90_when_pointer_at_right()
        => Assert.Equal(-90, CompassMath.RingBearingFromPointer(50, 0), 3);

    [Fact]
    public void should_return_plus90_when_pointer_at_left()
        => Assert.Equal(90, CompassMath.RingBearingFromPointer(-50, 0), 3);

    [Fact]
    public void should_return_minus180_when_pointer_at_bottom()
        => Assert.Equal(-180, CompassMath.RingBearingFromPointer(0, 50), 3);

    // ── IsRingHit — r<80%=이동, [80%,108%]=링 ──

    [Theory]
    [InlineData(0, 0, false)]        // 중심 = 이동
    [InlineData(40, 0, false)]       // 74% = 이동
    [InlineData(44, 0, true)]        // 81.5% = 링
    [InlineData(54, 0, true)]        // 100% = 링
    [InlineData(60, 0, false)]       // 111% = 밖
    public void should_detect_ring_zone_when_offset_given(double dx, double dy, bool expected)
        => Assert.Equal(expected, CompassMath.IsRingHit(dx, dy, 54));

    [Fact]
    public void should_return_false_when_radius_invalid()
        => Assert.False(CompassMath.IsRingHit(10, 10, 0));

    // ── DefaultDockTopRight ──

    [Fact]
    public void should_dock_top_right_when_canvas_size_given()
    {
        var (x, y) = CompassMath.DefaultDockTopRight(1000, 96, 16);
        Assert.Equal(888, x);
        Assert.Equal(16, y);
    }

    [Fact]
    public void should_clamp_to_zero_when_canvas_narrower_than_control()
    {
        var (x, _) = CompassMath.DefaultDockTopRight(80, 96, 16);
        Assert.Equal(0, x);
    }
}
