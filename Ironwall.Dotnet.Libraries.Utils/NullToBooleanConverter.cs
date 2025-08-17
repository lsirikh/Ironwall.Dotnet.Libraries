using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/13/2025 11:07:23 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// null 값을 boolean으로 변환하는 컨버터
/// - null인 경우: false 반환
/// - null이 아닌 경우: true 반환
/// </summary>
public class NullToBooleanConverter : IValueConverter
{
    /// <summary>
    /// null 값을 boolean으로 변환
    /// </summary>
    /// <param name="value">변환할 값</param>
    /// <param name="targetType">대상 타입</param>
    /// <param name="parameter">변환 매개변수</param>
    /// <param name="culture">문화 정보</param>
    /// <returns>null이면 false, 아니면 true</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    /// <summary>
    /// boolean을 다시 원래 타입으로 변환 (지원하지 않음)
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("NullToBooleanConverter는 단방향 변환만 지원합니다.");
    }
}