using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/8/2025 4:37:43 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 컬렉션의 개수를 boolean으로 변환하는 컨버터
/// - 개수가 0인 경우: false 반환
/// - 개수가 1 이상인 경우: true 반환
/// </summary>
public class CountToBooleanConverter : IValueConverter
{
    /// <summary>
    /// 컬렉션 개수를 boolean으로 변환
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0;
        }

        if (value is System.Collections.ICollection collection)
        {
            return collection.Count > 0;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Any();
        }

        return false;
    }

    /// <summary>
    /// boolean을 다시 원래 타입으로 변환 (지원하지 않음)
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("CountToBooleanConverter는 단방향 변환만 지원합니다.");
    }
}