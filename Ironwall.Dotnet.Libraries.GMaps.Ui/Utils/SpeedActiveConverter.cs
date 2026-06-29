using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      : [현재 Speed(double), 버튼 Tag] → 활성 여부(bool) — 배속 버튼 선택 표시용
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Playback 배속 칩의 활성 상태 판정. MultiBinding [현재 Speed, 버튼 Tag(배속 문자열)] →
/// 두 값이 같으면 <c>true</c>. DataTrigger가 이 결과로 활성 하이라이트한다.
/// </summary>
public sealed class SpeedActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return false;
        if (values[0] is not double current) return false;
        var tag = values[1]?.ToString();
        return double.TryParse(tag, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
               && Math.Abs(current - v) < 0.001;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
