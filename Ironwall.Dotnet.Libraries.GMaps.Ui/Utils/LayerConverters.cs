using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/// <summary>
/// LayerType이 "OverlayMap" 또는 "OverlayImage"면 Visible, 아니면 Collapsed
/// </summary>
public class LayerTypeToSliderVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string layerType)
            return layerType is "OverlayMap" or "OverlayImage" ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Opacity (0.0~1.0) ↔ Slider (0~100) 변환
/// </summary>
public class OpacityToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double opacity) return opacity * 100;
        return 100.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent) return percent / 100.0;
        return 1.0;
    }
}
