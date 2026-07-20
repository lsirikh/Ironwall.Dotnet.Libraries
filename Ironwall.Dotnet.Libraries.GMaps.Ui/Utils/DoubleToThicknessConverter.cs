using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/// <summary>
/// double → 균일 <see cref="Thickness"/> 변환기.
/// 심볼 <c>StrokeThickness</c>(double)를 <c>Border.BorderThickness</c>(Thickness)에 바인딩할 때 사용.
/// (PIDS 심볼 디스크 Edge 두께를 MarkerStrokeThickness에 연결 — PidsMarkerStyle.xaml)
/// </summary>
public class DoubleToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? new Thickness(d) : new Thickness(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Thickness t ? t.Left : 0d;
}
