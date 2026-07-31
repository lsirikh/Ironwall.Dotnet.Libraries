using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Converters;
/****************************************************************************
   Purpose      : 이벤트 UI 표시 전용 — enum 값을 한글 라벨로 렌더하는 단방향 컨버터.
                  값/데이터는 불변, 화면 표시만 변환한다. ConvertBack 미지원 —
                  ComboBox SelectedItem 등은 raw enum에 그대로(TwoWay) 바인딩되어
                  저장/전송 값은 영어 enum 원문을 유지한다.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public sealed class EnumToKoreanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => EnumKoreanMap.To(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("EnumToKoreanConverter는 단방향 표시 전용입니다.");
}
