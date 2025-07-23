using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class LevelStringToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        switch ((EnumLevelType)value)
        {
            case EnumLevelType.UNDEFINED:
                return "UNDEFINED";
            case EnumLevelType.ADMIN:
                return "ADMIN";
            case EnumLevelType.USER:
                return "USER";
            default:
                return "UNDEFINED";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value.ToString() == EnumLevelType.USER.ToString())
        {
            return (int)EnumLevelType.USER;
        }
        else if (value.ToString() == EnumLevelType.ADMIN.ToString())
        {
            return (int)EnumLevelType.ADMIN;
        }
        else if (value.ToString() == EnumLevelType.UNDEFINED.ToString())
        {
            return (int)EnumLevelType.UNDEFINED;
        }
        else
        {
            return (int)EnumLevelType.UNDEFINED;
        }
    }
}
