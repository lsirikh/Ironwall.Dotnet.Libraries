using System.Globalization;
using System.Windows;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// map-tilt-25d PRD FR-04 — MainMap Height/Margin MultiBinding 컨버터(WPF 어댑터). 값 변환만이라 STA 불요.
/// 수식·입력 해석은 GMaps.Ui.Tests/TiltOverscanMathTests 가 실코드로 검증하고, 여기서는 Thickness 매핑과
/// DependencyProperty.UnsetValue 안전 기본값(NaN=Auto·0 마진)만 확인한다. 이 프로젝트에 둔 이유: GMaps.Ui.Tests 는 net8.0(WPF 미참조).
/// </summary>
public class TiltOverscanConverterTests
{
    private static readonly TiltOverscanConverter Converter = new();
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Fact]
    public void should_return_overscan_height_when_tilted()
    {
        object result = Converter.Convert(new object[] { 1080.0, 20.0 }, typeof(double), "Height", Culture);
        double h = Assert.IsType<double>(result);
        Assert.Equal(TiltOverscanMath.Height(1080.0, 20.0), h, 9);
        Assert.Equal(1149.34, h, 1);   // SIM-T1041 want H'=1149
    }

    [Fact]
    public void should_return_symmetric_negative_margin_when_tilted()
    {
        object result = Converter.Convert(new object[] { 1080.0, 20.0 }, typeof(Thickness), "Margin", Culture);
        var m = Assert.IsType<Thickness>(result);
        double d = TiltOverscanMath.Delta(1080.0, 20.0);
        Assert.Equal(0.0, m.Left, 9);
        Assert.Equal(0.0, m.Right, 9);
        Assert.Equal(-d, m.Top, 9);
        Assert.Equal(-d, m.Bottom, 9);
        Assert.Equal(34.67, -m.Top, 1);   // SIM-T1041 want Δ=35
    }

    [Fact]
    public void should_release_height_and_margin_when_phi_layout_is_zero()
    {
        // FR-04: φ=0 이면 Height=Auto(NaN)·Margin=0 해제
        double h = Assert.IsType<double>(Converter.Convert(new object[] { 1080.0, 0.0 }, typeof(double), "Height", Culture));
        Assert.True(double.IsNaN(h));
        var m = Assert.IsType<Thickness>(Converter.Convert(new object[] { 1080.0, 0.0 }, typeof(Thickness), "Margin", Culture));
        Assert.Equal(new Thickness(0), m);
    }

    [Fact]
    public void should_return_safe_defaults_when_values_are_unset()
    {
        // MultiBinding 초기 평가: 소스 미해결 → DependencyProperty.UnsetValue 유입 — 예외 없이 Auto/0
        var unset = new object[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
        Assert.True(double.IsNaN(Assert.IsType<double>(Converter.Convert(unset, typeof(double), "Height", Culture))));
        Assert.Equal(new Thickness(0), Assert.IsType<Thickness>(Converter.Convert(unset, typeof(Thickness), "Margin", Culture)));

        Assert.True(double.IsNaN(Assert.IsType<double>(Converter.Convert(null, typeof(double), "Height", Culture))));
        Assert.True(double.IsNaN(Assert.IsType<double>(Converter.Convert(new object[] { 1080.0 }, typeof(double), "Height", Culture))));
        Assert.Equal(new Thickness(0), Assert.IsType<Thickness>(Converter.Convert(System.Array.Empty<object>(), typeof(Thickness), "Margin", Culture)));
    }

    [Fact]
    public void should_default_to_height_when_parameter_missing()
    {
        object result = Converter.Convert(new object[] { 1080.0, 20.0 }, typeof(double), null, Culture);
        Assert.Equal(TiltOverscanMath.Height(1080.0, 20.0), Assert.IsType<double>(result), 9);
    }

    [Fact]
    public void should_throw_when_convert_back_called()
        => Assert.Throws<System.NotSupportedException>(
            () => Converter.ConvertBack(1149.0, new[] { typeof(double), typeof(double) }, "Height", Culture));
}
