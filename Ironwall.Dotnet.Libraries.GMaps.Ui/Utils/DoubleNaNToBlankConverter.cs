using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/// <summary>
/// 그룹(멀티셀렉트) 속성 Pending 표시용 — double.NaN(=선택 항목들의 값이 서로 다름)을
/// 문자열 타깃(TextBox/TextBlock)에서는 <see cref="BlankText"/>로, double 타깃(Slider.Value)에서는 0.0으로 변환.
/// ConvertBack: 빈 문자열/파싱 실패 = Binding.DoNothing(값 미기록 — 사용자가 실제 입력할 때만 반영).
/// 단일 선택 모드에서는 NaN이 유입되지 않으므로 기존 동작과 동일(값 포맷만 담당).
/// </summary>
public class DoubleNaNToBlankConverter : IValueConverter
{
    /// <summary>NaN일 때 문자열 타깃에 표시할 텍스트(기본 빈칸, 최소줌 표시엔 "—" 등).</summary>
    public string BlankText { get; set; } = string.Empty;

    /// <summary>숫자 포맷(예: "F0"). 비면 기본 ToString.</summary>
    public string Format { get; set; } = string.Empty;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double d = value is double dv ? dv : double.NaN;
        if (targetType == typeof(string))
        {
            if (double.IsNaN(d)) return BlankText;
            return string.IsNullOrEmpty(Format) ? d.ToString(culture) : d.ToString(Format, culture);
        }
        // double 타깃(Slider.Value 등): NaN은 RangeBase 검증에 거부되므로 0.0으로 파킹(표시값은 문자열 쪽이 담당)
        return double.IsNaN(d) ? 0.0 : d;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double dv) return dv;   // Slider → DP 패스스루
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return Binding.DoNothing;   // 빈칸 유지 = 미기록
        return double.TryParse(s, NumberStyles.Float, culture, out var d) ? d : Binding.DoNothing;
    }
}
