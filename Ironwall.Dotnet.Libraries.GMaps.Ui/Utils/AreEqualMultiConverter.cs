using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      : 두 바인딩 값의 동일성 → bool (map-topbar-trafficlight FR-C3).
                  지도(M)>지도 전환 라디오 서브메뉴에서 항목==SelectedMapItem 체크 표시에 사용.
   Created On   : 2026-08-06 · Sensorway Co., Ltd.
 ****************************************************************************/
public class AreEqualMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values is { Length: 2 } && values[0] != null && Equals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
