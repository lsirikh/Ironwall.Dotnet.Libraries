using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/12/2025 9:46:24 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class BoolToInverseVisibleConverter : IValueConverter
{
    /// <summary>
    /// Used to convert a boolean to a visibility
    /// </summary>
    /// <param name="value">This is the boolean input</param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns>Returns a visibility</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!(value is bool))
        {
            //If there is an issue with the input, return collapsed
            return Visibility.Collapsed;
        }
        return (bool)value ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Used to take a visibility and returns a visibility
    /// </summary>
    /// <param name="value">This is the boolean input</param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns>Returns a visibility</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!(value is Visibility))
        {
            //If there is an issue wtih the input, return collapsed
            return Visibility.Collapsed;
        }
        return (Visibility)value != Visibility.Visible;
    }
}