using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : MainMap Height/Margin MultiBinding 컨버터 — map-tilt-25d PRD FR-04(레이아웃 오버스캔).
                  values = [뷰포트(AdornerDecorator) ActualHeight, φ_layout(도)], ConverterParameter = "Height" | "Margin".
   Note         : 수식·입력 해석은 TiltOverscanMath(순수)에 위임 — 이 파일은 WPF 타입(Thickness) 어댑터만.
                  값이 없거나(UnsetValue/null) φ_layout=0 이면 Height=NaN(Auto)·Margin=0 으로 오버스캔을 해제한다.
                  x:Name 무변경 — XAML 에서 StaticResource 로만 참조.
   Created By   : Claude
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <code>
/// &lt;MultiBinding Converter="{StaticResource TiltOverscanConverter}" ConverterParameter="Height"&gt;
///   &lt;Binding ElementName=... Path="ActualHeight"/&gt;  &lt;Binding Path="TiltLayoutDeg"/&gt;
/// &lt;/MultiBinding&gt;
/// </code>
/// </summary>
public sealed class TiltOverscanConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        object? viewportHeight = values is { Length: > 0 } ? values[0] : null;
        object? phiLayout = values is { Length: > 1 } ? values[1] : null;

        if (TiltOverscanMath.ParseTarget(parameter) == TiltOverscanTarget.Margin)
        {
            var m = TiltOverscanMath.MarginOrZero(viewportHeight, phiLayout);
            return new Thickness(m.Left, m.Top, m.Right, m.Bottom);
        }
        return TiltOverscanMath.HeightOrAuto(viewportHeight, phiLayout);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException($"{nameof(TiltOverscanConverter)} is one-way (Height/Margin are derived).");
}
