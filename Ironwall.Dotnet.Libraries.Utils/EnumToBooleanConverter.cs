using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
       Purpose      : Enum 값과 Boolean 값 간의 변환을 담당하는 컨버터
                      주로 RadioButton의 IsChecked 바인딩에 사용
       Created By   : GHLee                                                
       Created On   : 9/8/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
public class EnumToBooleanConverter : IValueConverter
{
    /// <summary>
    /// Enum 값을 Boolean으로 변환
    /// </summary>
    /// <param name="value">변환할 Enum 값</param>
    /// <param name="targetType">대상 타입 (Boolean)</param>
    /// <param name="parameter">비교할 Enum 값</param>
    /// <param name="culture">문화권 정보</param>
    /// <returns>값이 같으면 true, 다르면 false</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        if (parameter is string paramStr && value is Enum enumValue)
        {
            try
            {
                var parsed = Enum.Parse(enumValue.GetType(), paramStr, ignoreCase: true);
                return enumValue.Equals(parsed);
            }
            catch
            {
                return false;
            }
        }

        return value.Equals(parameter);
    }

    /// <summary>
    /// Boolean 값을 Enum으로 역변환
    /// </summary>
    /// <param name="value">변환할 Boolean 값</param>
    /// <param name="targetType">대상 Enum 타입</param>
    /// <param name="parameter">설정할 Enum 값</param>
    /// <param name="culture">문화권 정보</param>
    /// <returns>true면 parameter 값, false면 Binding.DoNothing</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            if (parameter is string paramStr && targetType.IsEnum)
            {
                try { return Enum.Parse(targetType, paramStr, ignoreCase: true); }
                catch { return Binding.DoNothing; }
            }
            return parameter;
        }

        return Binding.DoNothing;
    }
}