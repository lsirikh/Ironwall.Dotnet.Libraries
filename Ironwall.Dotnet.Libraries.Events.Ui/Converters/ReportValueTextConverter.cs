using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Converters;
/****************************************************************************
   Purpose      : 조치보고 창(B5)의 읽기 전용 속성 값 → 운영자 글.
                  값 하나: enum 은 한글 라벨(EnumKoreanMap), 비었으면 "—".
                  값 둘(MultiBinding): "앞{구분}뒤" — 둘 다 없으면 "—", 한쪽만 없으면 그쪽을 "-" 로.
                  옛 창은 읽기 전용인데도 꺼진 콤보 · 입력칸으로 값을 보였다 — 이제 글(ConsoleText)로 보이므로
                  빈 값 · 한쪽 빈 쌍(프레임 가로×세로, 고장 구간 시작~종료)이 " × " 처럼 깨지지 않게 여기서 정한다.
   Created By   : Claude (window-design-inventory B5)
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class ReportValueTextConverter : IValueConverter, IMultiValueConverter
{
    /// <summary>값이 없을 때의 글 — 옛 속성창의 "—" 와 같다.</summary>
    public const string Missing = "—";

    /// <summary>쌍의 한쪽만 없을 때 그 자리의 글.</summary>
    public const string MissingPart = "-";

    /// <summary>값 하나를 글로. 순수 함수 — 창 없이 시험한다.</summary>
    public static string Text(object? value)
    {
        if (value is null || value == DependencyProperty.UnsetValue) return Missing;
        var text = EnumKoreanMap.To(value);
        return string.IsNullOrWhiteSpace(text) ? Missing : text;
    }

    /// <summary>값 둘을 <paramref name="separator"/> 로 잇는다. 둘 다 없으면 <see cref="Missing"/>.</summary>
    public static string Pair(object? first, object? second, string? separator)
    {
        var a = Text(first);
        var b = Text(second);
        if (a == Missing && b == Missing) return Missing;
        return $"{(a == Missing ? MissingPart : a)}{separator ?? " ~ "}{(b == Missing ? MissingPart : b)}";
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Text(value);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values is { Length: >= 2 } ? Pair(values[0], values[1], parameter as string) : Missing;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("ReportValueTextConverter 는 단방향 표시 전용입니다.");

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException("ReportValueTextConverter 는 단방향 표시 전용입니다.");
}
