using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// <see cref="DateTime"/> → <c>yyyy-MM-dd HH:mm</c>(고정 문화권) 표시용 문자열.
/// </summary>
/// <remarks>
/// Binding 의 <c>StringFormat</c> 은 <c>ConverterCulture</c> 를 안 주면 스레드 UI 문화권(en-US 기본)을
/// 그대로 쓴다 — 콘솔 곳곳에서 겪은 "미국식 날짜" 결함과 같은 경로다. 이 변환기는 문화권을 아예 받지 않고
/// <see cref="DateTimeRangeText.Format(DateTime)"/> 을 직접 불러 항상 같은 표기를 낸다.
/// </remarks>
public sealed class FixedDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime dt ? DateTimeRangeText.Format(dt) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
