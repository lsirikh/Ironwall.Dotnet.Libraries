using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class NoImageNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {

        var imageName = value as string;

        if (string.IsNullOrEmpty(imageName))
        {
            return "NoImage";
        }
        else
        {
            if (imageName.Length > 15)
                return imageName.Substring(0, 15) + "…";
            else
                return imageName;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
