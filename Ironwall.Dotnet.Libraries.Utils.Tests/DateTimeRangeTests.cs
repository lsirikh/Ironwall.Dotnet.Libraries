using System;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// <see cref="DateTimeRangeText"/>/<see cref="DateTimeRangeRules"/> — <see cref="DateTimeRangeField"/> 뒤의
/// 순수 로직. UI 없이 판정만 잡는다.
/// </summary>
public class DateTimeRangeTextTests
{
    [Fact]
    public void should_format_with_fixed_pattern_when_given_any_datetime()
    {
        // Arrange
        var value = new DateTime(2026, 9, 16, 15, 52, 12);

        // Act
        var text = DateTimeRangeText.Format(value);

        // Assert — 초는 버리고, 24시간·하이픈 고정
        Assert.Equal("2026-09-16 15:52", text);
    }

    [Fact]
    public void should_stay_fixed_regardless_of_thread_culture()
    {
        // Arrange
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        try
        {
            var value = new DateTime(2026, 1, 5, 9, 5, 0);

            // Act
            var text = DateTimeRangeText.Format(value);

            // Assert — en-US 스레드 문화권이어도 미국식으로 새지 않는다
            Assert.Equal("2026-01-05 09:05", text);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void should_join_start_and_end_with_tilde_when_building_range_text()
    {
        // Arrange
        var start = new DateTime(2026, 9, 22, 16, 35, 0);
        var end = new DateTime(2026, 9, 23, 16, 35, 0);

        // Act
        var text = DateTimeRangeText.RangeText(start, end);

        // Assert
        Assert.Equal("2026-09-22 16:35 ~ 2026-09-23 16:35", text);
    }
}

/// <summary>
/// D-30 — 폭 대응 압축 표기(tier). 실제 창/픽셀 없이 순수 함수만 잡는다
/// (기준: EventDashboardView 의 "직접" 필터가 툴바 Auto 칸을 넘치게 하던 결함).
/// </summary>
public class DateTimeRangeTierTests
{
    private static readonly DateTime Start = new(2026, 9, 22, 17, 0, 0);
    private static readonly DateTime End = new(2026, 9, 23, 17, 0, 0);
    private static readonly DateTime Now = new(2026, 9, 23);

    [Fact]
    public void should_report_same_year_when_both_ends_match_now()
    {
        Assert.True(DateTimeRangeText.IsSameYear(Start, End, Now));
    }

    [Fact]
    public void should_report_not_same_year_when_either_end_differs()
    {
        var pastStart = new DateTime(2025, 12, 31, 17, 0, 0);
        Assert.False(DateTimeRangeText.IsSameYear(pastStart, End, Now));
    }

    [Fact]
    public void should_report_whole_day_aligned_when_time_of_day_matches()
    {
        // 17:00 ~ 17:00 (다음날) — 정확히 하루 간격, 시각이 같다
        Assert.True(DateTimeRangeText.IsWholeDayAligned(Start, End));
    }

    [Fact]
    public void should_report_whole_day_aligned_when_both_are_midnight()
    {
        var start = new DateTime(2026, 9, 22);
        var end = new DateTime(2026, 9, 24);
        Assert.True(DateTimeRangeText.IsWholeDayAligned(start, end));
    }

    [Fact]
    public void should_report_not_whole_day_aligned_when_time_of_day_differs()
    {
        var start = new DateTime(2026, 9, 22, 8, 0, 0);
        var end = new DateTime(2026, 9, 22, 20, 0, 0);
        Assert.False(DateTimeRangeText.IsWholeDayAligned(start, end));
    }

    // 필요폭(순수 모델, RequiredWidth): Full=286 · NoYear=216 · DateOnly(같은 해)=132 · Compact=118.
    [Theory]
    [InlineData(400, DateTimeRangeTier.Full)]
    [InlineData(286, DateTimeRangeTier.Full)]         // 경계 — 정확히 맞으면 더 넓은 tier
    [InlineData(285.9, DateTimeRangeTier.NoYear)]
    [InlineData(216, DateTimeRangeTier.NoYear)]
    [InlineData(215.9, DateTimeRangeTier.DateOnly)]
    [InlineData(132, DateTimeRangeTier.DateOnly)]
    [InlineData(131.9, DateTimeRangeTier.Compact)]
    [InlineData(0.1, DateTimeRangeTier.Compact)]
    [InlineData(0, DateTimeRangeTier.Compact)]
    public void should_pick_widest_fitting_tier_when_range_is_same_year_and_whole_day(double availableWidth, DateTimeRangeTier expected)
    {
        var tier = DateTimeRangeText.ResolveTier(Start, End, availableWidth, Now);
        Assert.Equal(expected, tier);
    }

    [Fact]
    public void should_fall_back_to_compact_when_width_is_negative_or_nan()
    {
        Assert.Equal(DateTimeRangeTier.Compact, DateTimeRangeText.ResolveTier(Start, End, -10, Now));
        Assert.Equal(DateTimeRangeTier.Compact, DateTimeRangeText.ResolveTier(Start, End, double.NaN, Now));
    }

    [Fact]
    public void should_skip_no_year_tier_when_not_same_year_even_with_room()
    {
        var pastStart = new DateTime(2025, 12, 31, 17, 0, 0);
        var pastEnd = new DateTime(2026, 1, 2, 17, 0, 0);

        // NoYear 필요폭(216)보다 넓지만 같은 해가 아니므로 NoYear 를 건너뛴다.
        // DateOnly(다른 해, "yyyy-MM-dd")=202 는 담기고 Full(286)은 안 담기는 폭을 고른다.
        var tier = DateTimeRangeText.ResolveTier(pastStart, pastEnd, 250, Now);

        Assert.Equal(DateTimeRangeTier.DateOnly, tier);
    }

    [Fact]
    public void should_skip_date_only_tier_when_times_are_not_aligned_even_with_room()
    {
        var start = new DateTime(2026, 9, 22, 8, 0, 0);
        var end = new DateTime(2026, 9, 22, 20, 0, 0);

        // DateOnly 필요폭(132)보다 넓게 남았어도(150) 시각이 정렬되지 않아 DateOnly 를 건너뛰고 Compact 로 간다.
        var tier = DateTimeRangeText.ResolveTier(start, end, 150, Now);

        Assert.Equal(DateTimeRangeTier.Compact, tier);
    }

    [Fact]
    public void should_be_idempotent_when_resolving_the_same_inputs_twice()
    {
        var first = DateTimeRangeText.ResolveTier(Start, End, 200, Now);
        var second = DateTimeRangeText.ResolveTier(Start, End, 200, Now);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(DateTimeRangeTier.Full, "2026-09-22 17:00", "2026-09-23 17:00")]
    [InlineData(DateTimeRangeTier.NoYear, "09-22 17:00", "09-23 17:00")]
    [InlineData(DateTimeRangeTier.DateOnly, "09-22", "09-23")]
    [InlineData(DateTimeRangeTier.Compact, "9/22", "9/23")]
    public void should_format_pair_per_tier_when_range_is_same_year(DateTimeRangeTier tier, string expectedStart, string expectedEnd)
    {
        var (start, end) = DateTimeRangeText.FormatPair(Start, End, tier, Now);

        Assert.Equal(expectedStart, start);
        Assert.Equal(expectedEnd, end);
    }

    [Fact]
    public void should_keep_year_in_date_only_tier_when_not_same_year()
    {
        var pastStart = new DateTime(2025, 12, 31, 0, 0, 0);
        var pastEnd = new DateTime(2026, 1, 2, 0, 0, 0);

        var (start, end) = DateTimeRangeText.FormatPair(pastStart, pastEnd, DateTimeRangeTier.DateOnly, Now);

        Assert.Equal("2025-12-31", start);
        Assert.Equal("2026-01-02", end);
    }

    [Fact]
    public void should_keep_full_precision_in_tooltip_text_when_label_is_compacted()
    {
        // 트리거 글자는 Compact("9/22"~"9/23")로 줄어도, ToolTip 이 쓰는 RangeText 는 tier 와 무관하게
        // 항상 전체 정밀도다 — "전체값은 항상 복구 가능하다"(요구사항 2)의 계약을 코드로 고정한다.
        var (compactStart, compactEnd) = DateTimeRangeText.FormatPair(Start, End, DateTimeRangeTier.Compact, Now);
        Assert.Equal("9/22", compactStart);
        Assert.Equal("9/23", compactEnd);

        var tooltip = DateTimeRangeText.RangeText(Start, End);
        Assert.Equal("2026-09-22 17:00 ~ 2026-09-23 17:00", tooltip);
    }

    [Fact]
    public void should_require_more_width_for_longer_formatted_text()
    {
        Assert.True(DateTimeRangeText.RequiredWidth("2026-09-22 17:00", "2026-09-23 17:00")
                    > DateTimeRangeText.RequiredWidth("9/22", "9/23"));
    }
}

public class DateTimeRangeRulesTests
{
    [Fact]
    public void should_report_valid_when_end_is_after_start()
    {
        var start = new DateTime(2026, 9, 22, 10, 0, 0);
        var end = start.AddHours(2);

        Assert.True(DateTimeRangeRules.IsValidRange(start, end));
    }

    [Fact]
    public void should_report_invalid_when_end_equals_start()
    {
        var start = new DateTime(2026, 9, 22, 10, 0, 0);

        Assert.False(DateTimeRangeRules.IsValidRange(start, start));
    }

    [Fact]
    public void should_report_invalid_when_end_is_before_start()
    {
        var start = new DateTime(2026, 9, 22, 10, 0, 0);
        var end = start.AddMinutes(-30);

        Assert.False(DateTimeRangeRules.IsValidRange(start, end));
    }

    [Fact]
    public void should_keep_end_unchanged_when_clamping_a_valid_range()
    {
        var start = new DateTime(2026, 9, 22, 10, 0, 0);
        var end = start.AddHours(3);

        var clamped = DateTimeRangeRules.ClampEnd(start, end);

        Assert.Equal(end, clamped);
    }

    [Fact]
    public void should_push_end_one_hour_after_start_when_end_equals_start()
    {
        var start = new DateTime(2026, 9, 22, 10, 0, 0);

        var clamped = DateTimeRangeRules.ClampEnd(start, start);

        Assert.Equal(start.Add(DateTimeRangeRules.DefaultSpan), clamped);
    }

    [Fact]
    public void should_push_end_one_hour_after_start_when_end_is_before_start()
    {
        var start = new DateTime(2026, 9, 22, 10, 0, 0);
        var end = start.AddDays(-1);

        var clamped = DateTimeRangeRules.ClampEnd(start, end);

        Assert.Equal(start.Add(DateTimeRangeRules.DefaultSpan), clamped);
    }

    [Fact]
    public void should_take_only_the_time_of_day_when_combining_date_and_time()
    {
        var date = new DateTime(2026, 9, 22, 23, 59, 59);
        var time = new TimeSpan(8, 30, 0);

        var combined = DateTimeRangeRules.Combine(date, time);

        Assert.Equal(new DateTime(2026, 9, 22, 8, 30, 0), combined);
    }
}
