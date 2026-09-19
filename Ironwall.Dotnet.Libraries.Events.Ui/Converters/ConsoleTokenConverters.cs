using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Converters;

/// <summary>
/// 토큰 <b>이름</b>을 브러시로 푼다 — 뷰모델이 <c>Brush</c> 를 쥐지 않게 한다.
/// </summary>
/// <remarks>
/// 값을 캐싱하지 않는다(<c>static readonly</c> Frozen 브러시 금지) — 캐싱하면 테마 전환 때 옛 색으로 고착된다.
/// 다만 컨버터는 바인딩이 다시 평가될 때만 돌므로, 실행 중 테마를 바꾸면 <b>차트 계열색은 다음 갱신에서</b> 따라온다.
/// </remarks>
public sealed class TokenBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as string ?? parameter as string;
        if (string.IsNullOrEmpty(key)) return Brushes.Transparent;
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용입니다.");
}

/// <summary>비율(0~1)을 폭으로 — 곱할 전체 폭은 <c>ConverterParameter</c> 또는 두 번째 바인딩으로 준다.</summary>
public sealed class RatioToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0d;
        var ratio = values[0] is double r ? r : 0;
        var total = values[1] is double t ? t : 0;
        if (double.IsNaN(total) || total <= 0) return 0d;
        return Math.Max(0, Math.Min(total, total * ratio));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용입니다.");
}

/// <summary>참이면 접는다(반대 방향 <c>BooleanToVisibility</c>).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용입니다.");
}

/// <summary>
/// 빗금 무늬 — <b>색이 아니라 형태로</b> 계열을 가른다(카메라 탐지). 브러시 토큰 이름을 받아 그 색의 사선 무늬를 만든다.
/// </summary>
public sealed class HatchBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key)) return Brushes.Transparent;
        var baseBrush = Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
        var stripe = Application.Current?.TryFindResource("SurfaceBrush") as Brush ?? Brushes.White;

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(baseBrush, null, new RectangleGeometry(new Rect(0, 0, 5, 5))));
        drawing.Children.Add(new GeometryDrawing(stripe, null, new RectangleGeometry(new Rect(0, 0, 1.8, 5))));

        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 5, 5),
            ViewportUnits = BrushMappingMode.Absolute,
            Transform = new RotateTransform(45),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용입니다.");
}

/// <summary>x 좌표를 왼쪽 여백으로 — 끄는 동안의 구간 띠를 놓는다.</summary>
public sealed class LeftThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new Thickness(value is double x && !double.IsNaN(x) ? Math.Max(0, x) : 0, 0, 0, 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용입니다.");
}
