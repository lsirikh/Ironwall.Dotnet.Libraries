using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/8/2025 10:03:36 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// Enum 값을 Description 어트리뷰트의 텍스트로 변환하는 컨버터
/// </summary>
public class EnumToDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        if (value is Enum enumValue)
        {
            // Description 어트리뷰트 찾기
            FieldInfo fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            if (fieldInfo != null)
            {
                DescriptionAttribute[] attributes =
                    (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attributes.Length > 0)
                {
                    return attributes[0].Description;
                }
            }

            // Description이 없으면 기본 ToString() 사용
            return enumValue.ToString();
        }

        return value.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("EnumToDescriptionConverter는 단방향 변환만 지원합니다.");
    }
}