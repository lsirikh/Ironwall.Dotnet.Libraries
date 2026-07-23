using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Converters;
/****************************************************************************
   Purpose      : 탐지 신호(detail.signal) 미니바 표시용 컨버터 (FR-04/FR-06)
   Created By   : GHLee
   Created On   : 2026-07-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// (signal, maxSignal) → 바 폭(px). 스펙에 signal 절대 최대값이 없어
/// 현재 로드된 목록 내 최대값 기준 상대 폭으로 그린다. parameter=전체 폭(기본 64).
/// </summary>
public class SignalBarWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double total = 64d;
        if (parameter is string p && double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            total = parsed;

        int? signal = values.Length > 0 ? values[0] as int? : null;
        int max = values.Length > 1 && values[1] is int m ? m : 0;

        if (signal is not > 0 || max <= 0) return 0d;
        return Math.Clamp((double)signal.Value / max, 0d, 1d) * total;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>(signal, maxSignal) → 목록 내 최대 행 여부 — 최대 신호 행 바를 critical 색으로 강조.</summary>
public class SignalIsMaxConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        int? signal = values.Length > 0 ? values[0] as int? : null;
        int max = values.Length > 1 && values[1] is int m ? m : 0;
        return signal is > 0 && max > 0 && signal.Value >= max;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
