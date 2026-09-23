using System;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// <see cref="DateTimeRangeField"/> 팝업이 커밋 전에 거치는 순수 판정 — UI 없이 단위테스트로 잡는다.
/// </summary>
public static class DateTimeRangeRules
{
    /// <summary>기본 폭 — 끝이 시작보다 앞서거나 같을 때 밀어 올리는 값.</summary>
    public static readonly TimeSpan DefaultSpan = TimeSpan.FromHours(1);

    /// <summary>범위가 유효한가 — 끝이 시작보다 뒤여야 한다.</summary>
    public static bool IsValidRange(DateTime start, DateTime end) => end > start;

    /// <summary>
    /// 끝이 시작 이하면 <see cref="DefaultSpan"/> 만큼 밀어 올린다 — 팝업은 잘못된 범위를 절대 커밋하지 않는다.
    /// </summary>
    public static DateTime ClampEnd(DateTime start, DateTime end)
        => IsValidRange(start, end) ? end : start.Add(DefaultSpan);

    /// <summary>
    /// 달력이 고른 날짜(자정 기준)에 시각을 얹는다 — <paramref name="time"/> 은 그날의 시:분만 쓴다.
    /// </summary>
    public static DateTime Combine(DateTime date, TimeSpan time) => date.Date.Add(time);
}
