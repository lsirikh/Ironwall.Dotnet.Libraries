using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      : enum → [Display(Name)] 표시 문자열 (ComboBox 항목)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// enum 값을 <c>[Display(Name=...)]</c> 문자열로 변환(없으면 <c>ToString</c>). ComboBox 항목 표시용.
/// </summary>
public sealed class EnumDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        var type = value.GetType();
        if (!type.IsEnum) return value.ToString() ?? string.Empty;

        var name = value.ToString()!;
        var member = type.GetField(name);
        var display = member?.GetCustomAttribute<DisplayAttribute>();
        return display?.Name ?? name;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
