using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class AdminAllowedWIthIsItemExistMutliValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (!(values[0] is int firstValue))
        {
            firstValue = (int)values[0];
        }

        if (!(values[1] is bool sencondValue))
        {
            sencondValue = (bool)values[1];
        }

        if (firstValue == (int)EnumLevelType.ADMIN && !sencondValue)
            return true;
        else
            return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
