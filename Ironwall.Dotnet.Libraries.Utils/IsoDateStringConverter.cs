using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

/// <summary>
/// ISO 8601 날짜 문자열(예: "2026-07-22T14:30:00+09:00")을 표시용 "yyyy-MM-dd HH:mm" 로 변환.
/// <para>GOP 세션/감사 DTO 날짜는 <c>string?</c>(KST +09:00) 이라 <c>StringFormat</c> 이 통하지 않는다.</para>
/// <para>파싱 실패·null·공백이면 원문(또는 빈 문자열)을 그대로 반환 — 절대 크래시하지 않는다(R-3).</para>
/// </summary>
public sealed class IsoDateStringConverter : IValueConverter
{
    private const string DISPLAY_FORMAT = "yyyy-MM-dd HH:mm";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        // DateTimeStyles 없이 관대하게 파싱 — 오프셋(+09:00) 포함 ISO 도 로컬 시각으로 정규화.
        return DateTime.TryParse(s, culture, DateTimeStyles.None, out var dt)
            ? dt.ToString(DISPLAY_FORMAT, culture)
            : s;   // 파싱 실패 시 원문 폴백(표시 손실 방지)
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException($"{nameof(IsoDateStringConverter)} 는 단방향(표시 전용) 컨버터입니다.");
}
