using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;

/// <summary>추이 차트의 한 칸 — 그 칸이 시작하는 시각과 서버 집계(없으면 0 으로 채운 칸).</summary>
public readonly record struct TrendBucket(DateTime Start, EventTrendItemDto Item, bool IsFilled);

/// <summary>
/// 서버 추이(<c>trend.series</c>)를 <b>화면이 고르게 그릴 수 있는 모양</b>으로 옮긴다 — 순수 함수.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 서버는 이벤트가 있는 칸만 준다(<c>routers/event_statistics.py _build_trend_series</c>:
/// <c>defaultdict</c> 에 모인 키만 정렬해 돌려준다). 차트는 받은 점을 <b>등간격</b>으로 놓고, 끌어 기간을 고르는
/// <see cref="EventTrendRangeMath"/> 는 "x 위치 ∝ 시각" 을 가정한다. 칸이 빠지면 둘이 어긋나 끈 자리와 다른 시각이
/// 조회된다(실서버 왕복 E3e: 25시간 기간에 서버 칸 1개 → 화면 점 1개).</para>
/// <para><b>라벨</b> — <c>time_bucket</c> 은 <c>"yyyy-MM-dd HH"</c>(시간) · <c>"yyyy-MM-dd"</c>(일)이다.
/// <c>DateTime.TryParse</c> 는 분이 없는 <c>"yyyy-MM-dd HH"</c> 를 읽지 못해 라벨이 <c>"24 01"</c> 로 찍혔다(E3g) —
/// 정확한 형식으로 읽는다.</para>
/// <para><b>데이터를 버리지 않는다</b> — 읽지 못한 칸이나 기간 밖 칸이 하나라도 있으면 채우지 않고 서버 칸을 그대로 쓴다.</para>
/// </remarks>
public static class EventTrendBuckets
{
    private static readonly string[] HourFormats = { "yyyy-MM-dd HH", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH", "yyyy-MM-dd'T'HH:mm:ss" };
    private static readonly string[] DayFormats = { "yyyy-MM-dd" };

    /// <summary>서버 <c>time_bucket</c> 을 읽는다. 읽지 못하면 <c>false</c>.</summary>
    public static bool TryParseBucket(string? timeBucket, out DateTime start)
    {
        start = default;
        if (string.IsNullOrWhiteSpace(timeBucket)) return false;
        var text = timeBucket.Trim();
        return DateTime.TryParseExact(text, HourFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out start)
            || DateTime.TryParseExact(text, DayFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out start);
    }

    /// <summary>서버 <c>trend.interval</c> → 한 칸의 폭. 모르는 값이면 <c>null</c>.</summary>
    public static TimeSpan? BucketOf(string? interval) => interval?.Trim().ToLowerInvariant() switch
    {
        "hour" => TimeSpan.FromHours(1),
        "day" => TimeSpan.FromDays(1),
        _ => null,
    };

    /// <summary><paramref name="time"/> 이 들어 있는 칸의 시작(시간 · 일 경계로 내림).</summary>
    public static DateTime Floor(DateTime time, TimeSpan bucket)
        => bucket >= TimeSpan.FromDays(1)
            ? time.Date
            : new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0, time.Kind);

    /// <summary>
    /// 기간 [<paramref name="start"/>, <paramref name="end"/>] 의 모든 칸을 채운다 — 서버가 준 칸은 그 값, 없는 칸은 0.
    /// 서버 칸이 기간 밖이거나 읽히지 않으면 채우지 않고 서버 칸 그대로 돌려준다(버리지 않는다).
    /// </summary>
    public static IReadOnlyList<TrendBucket> Densify(IReadOnlyList<EventTrendItemDto>? sparse, DateTime start, DateTime end, TimeSpan bucket)
    {
        sparse ??= Array.Empty<EventTrendItemDto>();
        if (bucket <= TimeSpan.Zero || end < start) return AsIs(sparse);

        var first = Floor(start, bucket);
        var last = Floor(end, bucket);
        var count = (int)Math.Round((last - first).Ticks / (double)bucket.Ticks) + 1;
        if (count <= 0 || count > 24 * 400) return AsIs(sparse);   // 비정상 기간 — 채우지 않는다

        var byStart = new Dictionary<DateTime, EventTrendItemDto>();
        foreach (var item in sparse)
        {
            if (!TryParseBucket(item.TimeBucket, out var at)) return AsIs(sparse);
            at = Floor(at, bucket);
            if (at < first || at > last) return AsIs(sparse);
            byStart[at] = item;
        }

        var dense = new List<TrendBucket>(count);
        for (var i = 0; i < count; i++)
        {
            var at = first.AddTicks(bucket.Ticks * i);
            dense.Add(byStart.TryGetValue(at, out var item)
                ? new TrendBucket(at, item, false)
                : new TrendBucket(at, new EventTrendItemDto { TimeBucket = Key(at, bucket) }, true));
        }
        return dense;
    }

    /// <summary>X 축 라벨 — 시간 칸은 <c>"HH시"</c>(자정은 <c>"MM-dd"</c>), 일 칸은 <c>"MM-dd"</c>.</summary>
    public static string Label(TrendBucket b, TimeSpan bucket)
    {
        if (b.Start == default) return b.Item.TimeBucket ?? string.Empty;
        if (bucket >= TimeSpan.FromDays(1)) return b.Start.ToString("MM-dd", CultureInfo.InvariantCulture);
        return b.Start.Hour == 0 ? b.Start.ToString("MM-dd", CultureInfo.InvariantCulture) : b.Start.ToString("HH", CultureInfo.InvariantCulture) + "시";
    }

    private static string Key(DateTime at, TimeSpan bucket)
        => at.ToString(bucket >= TimeSpan.FromDays(1) ? "yyyy-MM-dd" : "yyyy-MM-dd HH", CultureInfo.InvariantCulture);

    private static IReadOnlyList<TrendBucket> AsIs(IReadOnlyList<EventTrendItemDto> sparse)
        => sparse.Select(s => new TrendBucket(TryParseBucket(s.TimeBucket, out var at) ? at : default, s, false)).ToList();
}
