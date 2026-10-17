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

        double? signal = values.Length > 0 ? SignalValue.From(values[0]) : null;
        double max = values.Length > 1 ? SignalValue.From(values[1]) ?? 0d : 0d;

        if (signal is not > 0 || max <= 0) return 0d;
        return Math.Clamp(signal.Value / max, 0d, 1d) * total;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>(signal, maxSignal) → 목록 내 최대 행 여부 — 최대 신호 행 바를 critical 색으로 강조.</summary>
public class SignalIsMaxConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double? signal = values.Length > 0 ? SignalValue.From(values[0]) : null;
        double max = values.Length > 1 ? SignalValue.From(values[1]) ?? 0d : 0d;
        return signal is > 0 && max > 0 && signal.Value >= max;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 바인딩 값 → 신호 수치. 신호는 정수 · 실수 모두라(<c>double?</c>) 형식을 가리지 않고 숫자면 받는다 —
/// <c>as int?</c> 로만 받으면 실수 신호의 바가 조용히 0 폭이 된다(호스트 미리보기는 아직 <c>int</c> 를 바인딩한다).
/// </summary>
internal static class SignalValue
{
    public static double? From(object? value) => value switch
    {
        double d => d,
        int i => i,
        long l => l,
        float f => f,
        decimal m => (double)m,
        _ => null
    };
}
