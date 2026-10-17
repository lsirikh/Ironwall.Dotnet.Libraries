using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>입력칸 글자를 읽은 결과 — 비었는가 · 읽을 수 없는가 · 값인가.</summary>
public enum DateTimeFieldParseKind
{
    /// <summary>빈 칸(공백만 포함).</summary>
    Empty,

    /// <summary>날짜로 읽을 수 없다 — 아직 입력 중이거나 잘못된 글자.</summary>
    Invalid,

    /// <summary>값이 됐다.</summary>
    Valid,
}

/// <summary><see cref="DateTimeFieldText.Parse"/> 의 결과.</summary>
public readonly record struct DateTimeFieldParseResult(DateTimeFieldParseKind Kind, DateTime? Value)
{
    public static DateTimeFieldParseResult Empty => new(DateTimeFieldParseKind.Empty, null);
    public static DateTimeFieldParseResult Invalid => new(DateTimeFieldParseKind.Invalid, null);
    public static DateTimeFieldParseResult Of(DateTime value) => new(DateTimeFieldParseKind.Valid, value);
}

/// <summary>
/// <see cref="DateTimeField"/>(단일 날짜 · 날짜+시각 칸) 뒤의 순수 규칙 — 표기 · 읽기 · 팝업 초안. UI 없이 단위테스트로 잡는다.
/// </summary>
/// <remarks>
/// <para>표기는 <see cref="DateTimeRangeText"/> 와 같은 고정 문화권 규칙이다 — 날짜는 <c>yyyy-MM-dd</c>, 시각까지면 <c>yyyy-MM-dd HH:mm</c>
/// (범위 칸과 한 글자도 다르지 않게 <see cref="DateTimeRangeText.Format(DateTime)"/> 을 그대로 부른다).</para>
/// <para>읽기는 <b>엄격</b>하다 — 월 · 일 · 시 · 분은 두 자리. 그래야 입력 도중의 "2026-09-1" 이 1일로 먼저 확정됐다가 12일로 바뀌는 일이 없다
/// (칸은 글자가 온전한 값이 되는 순간 바로 확정한다 — 자동화의 ValuePattern.SetValue 가 포커스 이동 없이 값을 넣어도 뷰모델까지 닿게).</para>
/// </remarks>
public static class DateTimeFieldText
{
    /// <summary>날짜 표기.</summary>
    public const string DatePattern = "yyyy-MM-dd";

    private static readonly CultureInfo Fixed = CultureInfo.InvariantCulture;

    private static readonly string[] DateFormats = { "yyyy-MM-dd", "yyyy.MM.dd", "yyyy/MM/dd" };

    private static readonly string[] DateTimeFormats =
    {
        "yyyy-MM-dd HH:mm", "yyyy.MM.dd HH:mm", "yyyy/MM/dd HH:mm",
        "yyyy-MM-dd HH:mm:ss", "yyyy.MM.dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss",
    };

    /// <summary>칸에 찍는 글자 — 값이 없으면 빈 문자열.</summary>
    public static string Format(DateTime? value, bool includeTime)
    {
        if (value is not { } v) return string.Empty;
        return includeTime ? DateTimeRangeText.Format(v) : v.ToString(DatePattern, Fixed);
    }

    /// <summary>
    /// 입력 글자를 읽는다. 시각 칸(<paramref name="includeTime"/>)에 날짜만 적으면 지금 값(<paramref name="current"/>)의 시각을 그대로 둔다
    /// (없으면 00:00) — 날짜만 고쳐 쓰는 흔한 경우에 시각이 날아가지 않게.
    /// </summary>
    public static DateTimeFieldParseResult Parse(string? text, bool includeTime, DateTime? current)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return DateTimeFieldParseResult.Empty;

        if (includeTime && DateTime.TryParseExact(trimmed, DateTimeFormats, Fixed, DateTimeStyles.None, out var dateTime))
            return DateTimeFieldParseResult.Of(Normalize(dateTime, includeTime: true));

        if (DateTime.TryParseExact(trimmed, DateFormats, Fixed, DateTimeStyles.None, out var date))
        {
            var time = includeTime && current is { } c ? c.TimeOfDay : TimeSpan.Zero;
            return DateTimeFieldParseResult.Of(Normalize(date.Date.Add(time), includeTime));
        }

        return DateTimeFieldParseResult.Invalid;
    }

    /// <summary>칸이 다루는 정밀도로 자른다 — 날짜 칸은 자정, 시각 칸은 초 · 밀리초를 버린다.</summary>
    public static DateTime Normalize(DateTime value, bool includeTime)
        => includeTime
            ? new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind)
            : value.Date;

    /// <summary>팝업을 열 때의 초안 — 지금 값, 없으면 오늘(시각 칸은 지금 시각의 분 단위).</summary>
    public static DateTime SeedDraft(DateTime? value, bool includeTime, DateTime now)
        => Normalize(value ?? now, includeTime);

    /// <summary>달력이 고른 날에 초안의 시각을 얹는다(날짜 칸은 자정).</summary>
    public static DateTime WithDate(DateTime draft, DateTime pickedDate, bool includeTime)
        => includeTime ? pickedDate.Date.Add(draft.TimeOfDay) : pickedDate.Date;

    /// <summary>초안의 날짜에 시각 선택기가 고른 시:분을 얹는다.</summary>
    public static DateTime WithTime(DateTime draft, DateTime pickedTime)
        => Normalize(draft.Date.Add(pickedTime.TimeOfDay), includeTime: true);

    /// <summary>입력 안내 — 잘못된 글자를 적었을 때 칸의 도움말이 된다(존댓말 · 예시는 고정 표기).</summary>
    public static string InputHint(bool includeTime)
        => includeTime ? "2026-09-27 14:30 처럼 입력하세요" : "2026-09-27 처럼 입력하세요";
}
