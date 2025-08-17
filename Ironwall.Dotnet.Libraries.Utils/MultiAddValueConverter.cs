using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/13/2025 1:21:36 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 여러 숫자값을 모두 더하는 MultiValue 컨버터
/// </summary>
public class MultiAddValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double result = 0;

        foreach (var value in values)
        {
            if (value is double doubleValue)
            {
                result += doubleValue;
            }
            else if (value is int intValue)
            {
                result += intValue;
            }
            else if (value is string strValue && double.TryParse(strValue, out double parsedValue))
            {
                result += parsedValue;
            }
        }

        return result;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("MultiAddValueConverter는 단방향 변환만 지원합니다.");
    }
}