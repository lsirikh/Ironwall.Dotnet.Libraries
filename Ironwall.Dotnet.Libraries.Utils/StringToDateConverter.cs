using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class StringToDateConverter : IValueConverter
{
    // string ("1990-10-10") -> DateTime?
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;

        if (string.IsNullOrWhiteSpace(s))
            return null;                                         // DatePicker.SelectedDate = null

        return DateTime.TryParse(s, culture, DateTimeStyles.None, out var dt)
               ? dt                                              // 정상 변환
               : DependencyProperty.UnsetValue;                  // 바인딩 오류 표시
    }

    // DateTime? -> string ("yyyy-MM-dd")
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
            return dt.ToString("yyyy-MM-dd", culture);           // 원하는 포맷으로

        return null;                                             // DatePicker가 비워진 경우
    }
}