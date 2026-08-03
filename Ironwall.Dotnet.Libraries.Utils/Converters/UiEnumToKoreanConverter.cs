using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils.Converters;
/****************************************************************************
   Purpose      : enum → 한글 라벨 표시 전용 컨버터(공용). 바인딩 값 자체는 변환하지 않고
                  **화면 렌더 문자열만** 한글로 바꾼다(단방향).
   ⚠ 하드 제약   : ConvertBack 미지원 — TwoWay(SelectedItem 등)에 쓰지 말 것.
                  저장/전송 값은 raw enum 을 유지해야 한다.
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class UiEnumToKoreanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => UiKoreanMap.To(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException(
            $"{nameof(UiEnumToKoreanConverter)}는 표시 전용(단방향)입니다. TwoWay 바인딩에 사용하지 마세요.");
}
