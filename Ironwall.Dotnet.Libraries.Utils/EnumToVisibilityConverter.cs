using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/12/2025 4:17:29 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// Enum 값을 Visibility로 변환하는 컨버터
/// - ConverterParameter와 일치하면 Visible, 아니면 Collapsed
/// - 다중 값 비교 지원 (쉼표로 구분)
/// - 반전 모드 지원 (! 접두사)
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Enum을 Visibility로 변환
    /// </summary>
    /// <param name="value">변환할 Enum 값</param>
    /// <param name="targetType">대상 타입 (Visibility)</param>
    /// <param name="parameter">비교할 값 (문자열 또는 Enum)</param>
    /// <param name="culture">문화권 정보</param>
    /// <returns>Visibility 값</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        try
        {
            string parameterString = parameter.ToString();
            bool isInverted = false;

            // 반전 모드 체크 (! 접두사)
            if (parameterString.StartsWith("!"))
            {
                isInverted = true;
                parameterString = parameterString.Substring(1);
            }

            // 다중 값 비교 (쉼표로 구분)
            string[] compareValues = parameterString.Split(',');
            bool isMatch = false;

            foreach (string compareValue in compareValues)
            {
                string trimmedValue = compareValue.Trim();

                if (IsValueMatch(value, trimmedValue))
                {
                    isMatch = true;
                    break;
                }
            }

            // 결과 반환 (반전 모드 고려)
            if (isInverted)
                return isMatch ? Visibility.Collapsed : Visibility.Visible;
            else
                return isMatch ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception)
        {
            // 변환 실패시 기본값
            return Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Visibility를 Enum으로 변환 (ConvertBack은 지원하지 않음)
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException("EnumToVisibilityConverter does not support ConvertBack operation.");
    }


    /// <summary>
    /// 값이 매치되는지 확인
    /// </summary>
    /// <param name="enumValue">비교할 Enum 값</param>
    /// <param name="compareValue">비교 대상 문자열</param>
    /// <returns>매치 여부</returns>
    private bool IsValueMatch(object enumValue, string compareValue)
    {
        // 1. 문자열로 직접 비교
        if (enumValue.ToString().Equals(compareValue, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Enum 타입으로 변환하여 비교
        try
        {
            Type enumType = enumValue.GetType();
            if (enumType.IsEnum && Enum.TryParse(enumType, compareValue, true, out object parsedEnum))
            {
                return enumValue.Equals(parsedEnum);
            }
        }
        catch
        {
            // 파싱 실패시 무시
        }

        // 3. 숫자 값으로 비교 (Enum의 정수값)
        try
        {
            if (int.TryParse(compareValue, out int intValue))
            {
                int enumIntValue = System.Convert.ToInt32(enumValue);
                return enumIntValue == intValue;
            }
        }
        catch
        {
            // 변환 실패시 무시
        }

        return false;
    }

}
