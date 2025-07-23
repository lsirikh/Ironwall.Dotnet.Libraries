using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class LevelIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        ///ComboBox Categories
        ///ADMIN = 0
        ///USER = 1
        ///UNDEFINED = 2

        if (value != null)
        {

            switch ((EnumLevelType)value)
            {
                case EnumLevelType.UNDEFINED:
                    return 0;
                case EnumLevelType.ADMIN:
                    return 1;
                case EnumLevelType.USER:
                    return 2;
                default:
                    return 0;
            }
        }
        else
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {

        if (value != null)
        {
            switch (value)
            {
                case (int)EnumLevelType.ADMIN:
                    return EnumLevelType.ADMIN;

                case (int)EnumLevelType.USER:
                    return EnumLevelType.USER;

                case (int)EnumLevelType.UNDEFINED:
                    return EnumLevelType.UNDEFINED;

                default:
                    return EnumLevelType.UNDEFINED;
            }
        }
        else
        {
            return EnumLevelType.UNDEFINED;
        }
    }
}
