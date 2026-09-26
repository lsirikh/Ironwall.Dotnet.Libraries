using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
/****************************************************************************
   Purpose      : 부대 콘솔 — 좁은 폭의 "미배치 장비" 보기에서 상세 칸을 접는다(감사 D-8 8.10)
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 상세 칸을 낼지 — [상세 요청, 미배치 장비 보기인가, 상세가 도킹됐는가] → 요청 && (장비 보기가 아니거나 도킹됐거나).
/// </summary>
/// <remarks>
/// 폭 1280 미만(상세가 서랍)에서 장비 칸(320)과 상세(340)가 함께 서면 편제 트리가 130px 남짓으로 짓눌려 부대 이름이 잘린다.
/// 배치에 필요한 것은 트리에서 고른 부대(장비 칸 아래 "'OO'에 배치")뿐이라, 그 보기에서는 상세를 접고 트리에 폭을 준다.
/// 순수 함수(<see cref="Resolve"/>)로 두어 화면 없이 시험한다.
/// </remarks>
public sealed class UnitDetailRequestConverter : IMultiValueConverter
{
    public static bool Resolve(bool requested, bool isDeviceView, bool isDocked) => requested && (!isDeviceView || isDocked);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        static bool At(object[] v, int i) => v.Length > i && v[i] is bool b && b;
        return Resolve(At(values, 0), At(values, 1), At(values, 2));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
