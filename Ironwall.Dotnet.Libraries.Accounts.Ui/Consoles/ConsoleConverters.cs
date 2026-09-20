using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;

/// <summary>
/// 여러 조건이 <b>전부</b> 참일 때만 참. 칸의 <c>IsEnabled</c> 를 "그 모듈에 그 동작이 있는가" 와
/// "지금 편집할 수 있는가(권한 · 저장 중)" 둘 다로 묶는 데 쓴다.
/// </summary>
public sealed class AllTrueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values is not null && values.All(v => v is bool b && b);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>두 값이 <b>같은 객체</b>인가 — 고른 칩을 형태로 표시하는 데 쓴다(값 비교가 아니다).</summary>
public sealed class SameReferenceConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values is { Length: 2 } && ReferenceEquals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
