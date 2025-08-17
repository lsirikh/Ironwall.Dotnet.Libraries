using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/13/2025 11:09:20 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 문자열의 null/empty 상태를 boolean으로 변환하는 컨버터
/// - null이거나 빈 문자열인 경우: false 반환
/// - 값이 있는 경우: true 반환
/// </summary>
public class StringNullOrEmptyToBooleanConverter : IValueConverter
{
    /// <summary>
    /// 문자열 null/empty를 boolean으로 변환
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    /// <summary>
    /// boolean을 다시 원래 타입으로 변환 (지원하지 않음)
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("StringNullOrEmptyToBooleanConverter는 단방향 변환만 지원합니다.");
    }
}