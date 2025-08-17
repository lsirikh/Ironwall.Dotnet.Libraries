using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/13/2025 1:21:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 첫 번째 값을 절반으로 나누고 나머지를 더하는 컨버터 (위치 계산용)
/// </summary>
public class HalfAndAddConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 1) return 0.0;

        double result = 0;

        // 첫 번째 값은 절반으로 나누기
        if (values[0] is double firstValue)
        {
            result = firstValue / 2.0;
        }

        // 나머지 값들은 그대로 더하기
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] is double doubleValue)
            {
                result += doubleValue;
            }
            else if (values[i] is int intValue)
            {
                result += intValue;
            }
            else if (values[i] is string strValue && double.TryParse(strValue, out double parsedValue))
            {
                result += parsedValue;
            }
        }

        return result;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}