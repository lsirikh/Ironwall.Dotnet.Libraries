using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/21/2025 4:30:49 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// EnumColorType을 Brush로 변환하는 컨버터
/// </summary>
public class ColorTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is EnumColorType colorType)
        {
            return colorType.ToBrush();
        }

        // 기본값 반환
        return Brushes.Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 역변환은 필요 없으므로 NotImplemented
        throw new NotImplementedException("ColorTypeToBrushConverter는 단방향 변환만 지원합니다.");
    }
}