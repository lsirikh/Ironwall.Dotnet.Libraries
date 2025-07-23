using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class TimeFormatConverter : IValueConverter
{
    int time;
    int hour;
    int min;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {

        time = 0;
        hour = 0;
        min = 0;

        if (value.GetType() == typeof(int))
        {
            time = (int)value;
            SetTime(time);
            return string.Format("{0:d2}:{1:d2}", hour, min);
        }
        else
        {
            Debug.WriteLine($"TimeFormatConverter has exception");
            //Set Default 12Hours
            return "12:00";
        }

    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value.GetType() == typeof(string))
        {
            var timeString = value as string;
            var processedString = timeString.Split(':');

            if (processedString.Length == 2)
            {


                var time = 60 * int.Parse(processedString[0]) + int.Parse(processedString[1]);
                return time;
            }
        }
        //Set Default 24Hours
        Debug.WriteLine($"TimeFormatConverter : Set Default 24Hours({60 * 24})");
        return 60 * 12;
    }

    public void SetTime(int time)
    {
        if (time >= 60)
        {
            hour = time / 60;
            min = time - hour * 60;
        }
        else
        {
            min = time;
        }
    }

}
