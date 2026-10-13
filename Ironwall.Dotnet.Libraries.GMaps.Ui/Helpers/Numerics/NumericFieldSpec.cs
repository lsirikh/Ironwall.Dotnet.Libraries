using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Numerics;

/// <summary>
/// 숫자 칸 하나의 명세 — 최소 · 최대 · 단위(step). 정수 칸 = step 이 1 의 배수, 실수 칸 = step 의 소수 자리수(예 0.1 → 1자리).
/// <para>값은 언제나 <see cref="Normalize"/> 를 지나 "범위 안 + 단위 배수"로만 저장된다. 반올림은 decimal 로 계산해
/// 0.1+0.2 = 0.30000000000000004 · 11.9999999 같은 이진 꼬리가 남지 않는다(중간값은 0 에서 먼 쪽).
/// NaN 은 그룹 편집 "값 서로 다름" 표지라 그대로 통과시킨다.</para>
/// 명세 원본 표: docs/prds/map-property-numeric-precision-prd.md
/// </summary>
public sealed class NumericFieldSpec : IValueConverter
{
    public NumericFieldSpec(string name, double min, double max, double step)
    {
        if (!(min <= max) || !(step > 0)) throw new ArgumentException($"{name}: 범위/단위가 잘못됨");
        Name = name; Min = min; Max = max; Step = step;
        for (decimal s = (decimal)step; s % 1 != 0 && Digits < 10; s *= 10) Digits++;
        Format = Digits == 0 ? "0" : "0." + new string('0', Digits);
    }

    public string Name { get; }
    public double Min { get; }
    public double Max { get; }
    public double Step { get; }

    /// <summary>소수 자리수(정수 칸 = 0).</summary>
    public int Digits { get; }

    /// <summary>표시 형식("0" · "0.0" · "0.00").</summary>
    public string Format { get; }

    public bool IsInteger => Digits == 0;
    public bool AllowsNegative => Min < 0;

    /// <summary>범위로 자르고 단위 배수로 반올림. NaN 은 그대로(그룹 Pending), ±무한은 끝값.</summary>
    public double Normalize(double value)
    {
        if (double.IsNaN(value)) return value;
        double clamped = Math.Clamp(value, Min, Max);
        decimal step = (decimal)Step;
        decimal snapped = Math.Round((decimal)clamped / step, MidpointRounding.AwayFromZero) * step;
        return Math.Clamp((double)snapped, Min, Max);
    }

    /// <summary>정수 칸(int 속성)용.</summary>
    public int Normalize(int value) => (int)Normalize((double)value);

    /// <summary>DependencyProperty CoerceValueCallback 으로 그대로 쓴다(double · int 모두).</summary>
    public object Coerce(DependencyObject d, object value) => value switch
    {
        double x => Normalize(x),
        int i => Normalize(i),
        _ => value,
    };

    /// <summary>표시 글 — 정해진 자리수로만(NaN = 빈칸).</summary>
    public string ToText(double value, CultureInfo? culture = null)
        => double.IsNaN(value) ? string.Empty : Normalize(value).ToString(Format, culture ?? CultureInfo.CurrentCulture);

    public override string ToString() => $"{Name}[{Min}~{Max} / {Step}]";

    // ── 칸 → 마커 바인딩 변환기 ──
    // 칸에 SetValue 하면 TwoWay 바인딩은 강제(Coerce) 전 원시값(11.9999999)을 마커에 먼저 쓴다 — 그래서 바인딩에도 이 명세를 건다.

    /// <summary>마커 → 칸: 형식만 맞춘다(정규화는 칸의 강제 함수가 한다).</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => ChangeType(value, targetType);

    /// <summary>칸 → 마커: 범위 · 단위로 정규화해 마커 속성 형식(double · float · int)으로 넘긴다.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IConvertible and not string ? ChangeType(Normalize(System.Convert.ToDouble(value, CultureInfo.InvariantCulture)), targetType) : value;

    private static object? ChangeType(object? value, Type targetType)
    {
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is not IConvertible || value is string || !type.IsPrimitive || value.GetType() == type) return value;
        return System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}
