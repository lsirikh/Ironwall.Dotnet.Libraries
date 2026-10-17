using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;

/// <summary>끌어서 고른 기간. <see cref="IsCommittable"/> 이 거짓이면 아무 일도 일어나지 않는다(서버 미호출).</summary>
/// <param name="From">시작(포함).</param>
/// <param name="To">끝(포함).</param>
/// <param name="IsCommittable">한 버킷보다 좁으면 거짓 — 클릭을 기간 선택으로 오해하지 않는다.</param>
public readonly record struct TrendRange(DateTime From, DateTime To, bool IsCommittable)
{
    public static readonly TrendRange None = new(default, default, false);

    public TimeSpan Span => To - From;

    /// <summary>끄는 동안 머리에 찍는 글자 — "09-13 14:00 ~ 09-13 19:00".</summary>
    public string Label(string format = "MM-dd HH:mm")
        => IsCommittable ? $"{From.ToString(format)} ~ {To.ToString(format)}" : string.Empty;
}

/// <summary>
/// 추이 차트에서 <b>좌우로 끌어 기간을 고르는</b> 판정 — 순수 함수(events-console PRD FR-35).
/// </summary>
/// <remarks>
/// <para>WPF 시각 트리에 기대지 않는다. UIA 에 드래그 패턴이 없어(.NET 8 WPF) 이 함수들의 헤드리스 테스트와
/// 기간 칩 폴백이 회귀망의 전부다 — 그래서 판정을 뷰 코드비하인드에 두지 않고 여기로 뺀다.</para>
/// <para>데드존은 <c>DragMath.DeadZone</c>(8.0 DIU)을 그대로 쓴다 — 새 상수를 만들지 않는다.</para>
/// </remarks>
public static class EventTrendRangeMath
{
    /// <summary>플롯 안에서 <paramref name="x"/> 가 놓인 비율(0~1). 플롯 밖이면 끝으로 붙인다.</summary>
    public static double Fraction(double x, double plotLeft, double plotWidth)
    {
        if (plotWidth <= 0) return 0;
        var f = (x - plotLeft) / plotWidth;
        return f < 0 ? 0 : f > 1 ? 1 : f;
    }

    /// <summary>플롯 안의 <paramref name="x"/> 가 가리키는 시각. 좌표계는 픽셀이든 DIU 든 상관없다(비율만 쓴다).</summary>
    public static DateTime TimeAt(double x, double plotLeft, double plotWidth, DateTime start, DateTime end)
    {
        if (end <= start) return start;
        var ticks = (end - start).Ticks;
        return start.AddTicks((long)Math.Round(ticks * Fraction(x, plotLeft, plotWidth)));
    }

    /// <summary><paramref name="time"/> 를 <paramref name="bucket"/> 경계로 내림한다(버킷이 0 이하면 그대로).</summary>
    public static DateTime SnapDown(DateTime time, DateTime origin, TimeSpan bucket)
    {
        if (bucket <= TimeSpan.Zero) return time;
        var delta = time - origin;
        var steps = (long)Math.Floor(delta.Ticks / (double)bucket.Ticks);
        return origin.AddTicks(steps * bucket.Ticks);
    }

    /// <summary><paramref name="time"/> 를 <paramref name="bucket"/> 경계로 올림한다.</summary>
    public static DateTime SnapUp(DateTime time, DateTime origin, TimeSpan bucket)
    {
        if (bucket <= TimeSpan.Zero) return time;
        var down = SnapDown(time, origin, bucket);
        return down == time ? time : down + bucket;
    }

    /// <summary>
    /// 누른 자리와 놓은 자리에서 기간을 정한다.
    /// </summary>
    /// <param name="xPress">누른 x.</param>
    /// <param name="xRelease">놓은 x(왼쪽으로 끌었으면 <paramref name="xPress"/> 보다 작다 — 정규화한다).</param>
    /// <param name="plotLeft">플롯 영역 왼쪽.</param>
    /// <param name="plotWidth">플롯 영역 너비.</param>
    /// <param name="start">지금 보고 있는 기간의 시작.</param>
    /// <param name="end">지금 보고 있는 기간의 끝.</param>
    /// <param name="bucket">한 칸의 폭(1시간 · 1일). 결과는 이 경계에 맞춰지고, 이보다 좁으면 커밋하지 않는다.</param>
    public static TrendRange Resolve(double xPress, double xRelease,
                                     double plotLeft, double plotWidth,
                                     DateTime start, DateTime end,
                                     TimeSpan bucket)
    {
        if (plotWidth <= 0 || end <= start) return TrendRange.None;

        var lo = Math.Min(xPress, xRelease);
        var hi = Math.Max(xPress, xRelease);

        var from = TimeAt(lo, plotLeft, plotWidth, start, end);
        var to = TimeAt(hi, plotLeft, plotWidth, start, end);

        // 한 버킷보다 좁으면 그것은 클릭이다 — 기간을 바꾸지 않는다(서버 호출 0).
        // 판정은 반드시 '스냅 전의 생짜 구간'으로 한다 — 스냅 뒤에 재면 버킷 경계를 걸친 2px 흔들림이
        // 한 시간짜리 구간으로 부풀어 클릭마다 기간이 바뀐다.
        var minimum = bucket > TimeSpan.Zero ? bucket : TimeSpan.FromMinutes(1);
        if (to - from < minimum) return TrendRange.None;

        if (bucket > TimeSpan.Zero)
        {
            from = SnapDown(from, start, bucket);
            to = SnapUp(to, start, bucket);
        }

        if (from < start) from = start;
        if (to > end) to = end;

        return new TrendRange(from, to, true);
    }

    /// <summary>끄는 동안 그릴 띠의 (왼쪽, 너비). 플롯 밖으로 새지 않는다.</summary>
    public static (double Left, double Width) BandOf(double xPress, double xCurrent, double plotLeft, double plotWidth)
    {
        if (plotWidth <= 0) return (plotLeft, 0);

        var lo = Math.Max(plotLeft, Math.Min(xPress, xCurrent));
        var hi = Math.Min(plotLeft + plotWidth, Math.Max(xPress, xCurrent));
        return hi <= lo ? (lo, 0) : (lo, hi - lo);
    }

    /// <summary>기간 길이에 맞는 한 칸의 폭 — 하루 이하면 1시간, 넘으면 1일(서버 <c>trend.interval</c> 과 같은 규칙).</summary>
    public static TimeSpan BucketFor(DateTime start, DateTime end)
        => (end - start) <= TimeSpan.FromHours(25) ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);

    /// <summary><see cref="BucketFor"/> 와 같은 규칙을 서버 <c>interval</c> 쿼리 값으로 — <c>"hour"</c> | <c>"day"</c>.</summary>
    public static string IntervalFor(DateTime start, DateTime end)
        => BucketFor(start, end) >= TimeSpan.FromDays(1) ? "day" : "hour";
}
