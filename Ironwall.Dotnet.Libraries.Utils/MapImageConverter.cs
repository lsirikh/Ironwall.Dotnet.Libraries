using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class MapImageConverter: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
        string uri = null;
        if (value != null)
        {
            try
            {
                uri = $"{di.FullName}/{value as string}";
                FileInfo fi = new FileInfo(uri);
                if (!fi.Exists)
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex.Message} ImageConverter Exception!!!!!!!");
            }
        }
        /*else
        {
            uri = "pack://application:,,,/Ironwall.Monitoring.UI;Component/Resources/Images/baseBackground.png";
        }*/
        return uri;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
