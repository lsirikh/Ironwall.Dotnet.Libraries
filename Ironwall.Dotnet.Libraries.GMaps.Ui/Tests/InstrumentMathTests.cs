using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : InstrumentMath 헤드리스 테스트 (GMap_Map_Instruments NFR-04)
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
****************************************************************************/
public class InstrumentMathTests
{
    // ── 강풍 모드 라벨 ──
    [Theory]
    [InlineData(EnumWindyMode.wind0, "보통 바람")]
    [InlineData(EnumWindyMode.wind1, "약한 바람")]
    [InlineData(EnumWindyMode.wind2, "강한 바람")]
    [InlineData(EnumWindyMode.wind3, "태풍 바람")]
    public void should_map_windy_mode_name_when_mode_given(EnumWindyMode mode, string expected)
        => Assert.Equal(expected, InstrumentMath.WindyModeName(mode));

    [Fact]
    public void should_detect_normal_wind_when_wind0()
    {
        Assert.True(InstrumentMath.IsNormalWind(EnumWindyMode.wind0));
        Assert.False(InstrumentMath.IsNormalWind(EnumWindyMode.wind2));
    }

    // ── 관대 파싱 ──
    [Theory]
    [InlineData("IconLabel", WindyDisplayStyle.IconLabel)]
    [InlineData("icononly", WindyDisplayStyle.IconOnly)]
    [InlineData("garbage", WindyDisplayStyle.IconLabel)]
    [InlineData(null, WindyDisplayStyle.IconLabel)]
    public void should_parse_windy_style_leniently_when_string_given(string? input, WindyDisplayStyle expected)
        => Assert.Equal(expected, InstrumentMath.ParseWindyStyle(input));

    [Theory]
    [InlineData("Vertical", InstrumentOrientation.Vertical)]
    [InlineData("horizontal", InstrumentOrientation.Horizontal)]
    [InlineData("xyz", InstrumentOrientation.Vertical)]
    [InlineData(null, InstrumentOrientation.Vertical)]
    public void should_parse_orientation_leniently_when_string_given(string? input, InstrumentOrientation expected)
        => Assert.Equal(expected, InstrumentMath.ParseOrientation(input));

    // ── 카운트 배지 ──
    [Theory]
    [InlineData(0, "0")]
    [InlineData(-3, "0")]
    [InlineData(7, "7")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(1500, "99+")]
    public void should_format_count_badge_when_count_given(int count, string expected)
        => Assert.Equal(expected, InstrumentMath.CountBadgeText(count));

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void should_report_active_when_count_positive(int count, bool expected)
        => Assert.Equal(expected, InstrumentMath.IsActive(count));

    // ── 기본 도킹(마진) ──
    [Fact]
    public void should_dock_windy_top_left_below_label_with_margin()
    {
        var (x, y) = InstrumentMath.DefaultDockWindy(1000, 800);
        Assert.Equal(16, x);
        Assert.Equal(56, y);   // 16 margin + 40 label
    }

    [Fact]
    public void should_dock_detfault_below_windy()
    {
        var (x, y) = InstrumentMath.DefaultDockDetFault(1000, 800);
        Assert.Equal(16, x);
        Assert.Equal(112, y);  // 16 + 96
    }

    // ── 클램프(마진 안으로) ──
    [Fact]
    public void should_clamp_inside_margin_when_out_of_bounds()
    {
        var (x, y) = InstrumentMath.ClampWithMargin(-50, 5000, 100, 40, 1000, 800, 16);
        Assert.Equal(16, x);              // 좌측 마진
        Assert.Equal(800 - 40 - 16, y);   // 하단 마진 안
    }

    [Fact]
    public void should_default_to_margin_when_nan()
    {
        var (x, y) = InstrumentMath.ClampWithMargin(double.NaN, double.NaN, 100, 40, 1000, 800, 16);
        Assert.Equal(16, x);
        Assert.Equal(16, y);
    }

    [Fact]
    public void should_keep_within_when_canvas_smaller_than_control()
    {
        // 캔버스가 컨트롤보다 작아도 최소 마진 유지(음수·역전 방지)
        var (x, y) = InstrumentMath.ClampWithMargin(500, 500, 200, 200, 100, 100, 16);
        Assert.Equal(16, x);
        Assert.Equal(16, y);
    }
}
