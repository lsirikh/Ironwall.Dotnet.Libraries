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
