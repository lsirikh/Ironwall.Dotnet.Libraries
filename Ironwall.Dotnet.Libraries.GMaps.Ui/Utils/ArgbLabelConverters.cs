using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 라벨 스타일용 packed ARGB int 컨버터 2종 (Overlay_Title_ZoomStyle FR-07).
                  ArgbHexConverter: int? ↔ "#AARRGGBB" (편집 콤보/텍스트). null(그룹 Pending)=빈칸.
                  ArgbToBrushConverter: int? → Frozen SolidColorBrush (스와치 미리보기).
   Note         : 기존 EnumColorType 파이프라인(ColorTypeToBrushConverter)과 별개 — 라벨 색은 임의 ARGB(architect Q3).
   Created On   : 2026-07-23 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>packed ARGB int? ↔ "#AARRGGBB" 문자열. 잘못된 입력은 DoNothing(기존값 유지).</summary>
public sealed class ArgbHexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int argb) return string.Empty;   // null=그룹 Pending → 빈칸
        unchecked { return $"#{(uint)argb:X8}"; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string)?.Trim();
        if (string.IsNullOrEmpty(s)) return Binding.DoNothing;   // 빈칸(Pending) 입력은 무시
        if (s.StartsWith("#", StringComparison.Ordinal)) s = s[1..];
        if (s.Length == 6) s = "FF" + s;                          // RGB만 입력 시 불투명 처리
        if (s.Length != 8 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
            return Binding.DoNothing;                             // 형식 오류 → 기존값 유지
        unchecked { return (int)u; }
    }
}

/// <summary>packed ARGB int? → SolidColorBrush(Frozen). null=투명(스와치 빈칸).</summary>
public sealed class ArgbToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int argb) return Brushes.Transparent;
        unchecked
        {
            var c = Color.FromArgb((byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF),
                                   (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
