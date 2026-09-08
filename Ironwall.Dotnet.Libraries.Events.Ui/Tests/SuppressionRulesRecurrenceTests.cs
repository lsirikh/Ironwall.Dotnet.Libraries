using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 억제 주간 반복 순수 규칙 헤드리스 테스트.
                  서버가 422로 막지 않는 입력(요일 0개 · 24시간 종일)과
                  요일 비트 원점(월=0 vs .NET 일=0)을 여기서 고정한다.
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>주간 반복 규칙(요일 마스크·요약·검증·중복판정) 단위 테스트.</summary>
public class SuppressionRulesRecurrenceTests
{
    // ══════ 요일 비트 원점 — 서버 월=0 / .NET 일=0 ══════

    [Theory]
    [InlineData(DayOfWeek.Monday, 0)]
    [InlineData(DayOfWeek.Tuesday, 1)]
    [InlineData(DayOfWeek.Wednesday, 2)]
    [InlineData(DayOfWeek.Thursday, 3)]
    [InlineData(DayOfWeek.Friday, 4)]
    [InlineData(DayOfWeek.Saturday, 5)]
    [InlineData(DayOfWeek.Sunday, 6)]
    public void should_convert_dotnet_dayofweek_to_server_mon0_origin(DayOfWeek day, int expected)
        => Assert.Equal(expected, SuppressionRules.ToMon0(day));

    [Theory]
    [InlineData(DayOfWeek.Monday, 1)]
    [InlineData(DayOfWeek.Tuesday, 2)]
    [InlineData(DayOfWeek.Wednesday, 4)]
    [InlineData(DayOfWeek.Thursday, 8)]
    [InlineData(DayOfWeek.Friday, 16)]
    [InlineData(DayOfWeek.Saturday, 32)]
    [InlineData(DayOfWeek.Sunday, 64)]
    public void should_map_each_day_to_server_bit(DayOfWeek day, int bit)
        => Assert.Equal(bit, SuppressionRules.BitOf(SuppressionRules.ToMon0(day)));

    [Fact]
    public void should_match_server_presets_when_building_mask()
    {
        Assert.Equal(31, SuppressionRules.DaysWeekdayPreset);    // 월~금
        Assert.Equal(96, SuppressionRules.DaysWeekendPreset);    // 토+일
        Assert.Equal(127, SuppressionRules.DaysEveryDayPreset);  // 매일
        Assert.Equal(31, SuppressionRules.ToMask(new[] { 0, 1, 2, 3, 4 }));
        Assert.Equal(21, SuppressionRules.ToMask(new[] { 0, 2, 4 }));   // 월수금
    }

    [Fact]
    public void should_roundtrip_mask_and_indexes()
    {
        var mask = SuppressionRules.ToMask(new[] { 0, 2, 6 });
        Assert.Equal(new[] { 0, 2, 6 }, SuppressionRules.ToIndexes(mask));
    }

    [Fact]
    public void should_ignore_out_of_range_indexes_when_building_mask()
        => Assert.Equal(1, SuppressionRules.ToMask(new[] { 0, -1, 7, 99 }));

    [Theory]
    [InlineData(31, DayOfWeek.Monday, true)]
    [InlineData(31, DayOfWeek.Friday, true)]
    [InlineData(31, DayOfWeek.Saturday, false)]
    [InlineData(96, DayOfWeek.Sunday, true)]
    [InlineData(1, DayOfWeek.Monday, true)]
    [InlineData(1, DayOfWeek.Sunday, false)]
    public void should_test_day_membership_with_server_origin(int mask, DayOfWeek day, bool expected)
        => Assert.Equal(expected, SuppressionRules.HasDay(mask, day));

    // ══════ 요일 0개 — 서버가 막지 않는다. UI가 유일 방어선 ══════

    [Fact]
    public void should_report_no_day_selected_when_mask_is_zero()
    {
        Assert.False(SuppressionRules.HasAnyDay(0));
        Assert.True(SuppressionRules.HasAnyDay(1));
    }

    [Fact]
    public void should_block_creation_when_no_day_selected()
    {
        var msg = SuppressionRules.ValidateWeeklyForm(0, TimeSpan.FromHours(8), TimeSpan.FromHours(21));
        Assert.NotNull(msg);
        Assert.Contains("요일", msg);
    }

    // ══════ 일일 시각 — start==end 는 24시간 종일(서버 422 아님) ══════

    [Fact]
    public void should_classify_equal_daily_times_as_all_day()
        => Assert.Equal(SuppressionRules.DailyTimeVerdict.AllDay,
            SuppressionRules.ClassifyDailyTime(TimeSpan.FromHours(8), TimeSpan.FromHours(8)));

    [Fact]
    public void should_classify_reversed_daily_times_as_overnight()
        => Assert.Equal(SuppressionRules.DailyTimeVerdict.Overnight,
            SuppressionRules.ClassifyDailyTime(TimeSpan.FromHours(22), TimeSpan.FromHours(6)));

    [Fact]
    public void should_classify_normal_daily_times_as_ok()
        => Assert.Equal(SuppressionRules.DailyTimeVerdict.Ok,
            SuppressionRules.ClassifyDailyTime(TimeSpan.FromHours(8), TimeSpan.FromHours(21)));

    [Fact]
    public void should_block_creation_when_daily_times_are_equal()
    {
        var msg = SuppressionRules.ValidateWeeklyForm(31, TimeSpan.FromHours(8), TimeSpan.FromHours(8));
        Assert.NotNull(msg);
        Assert.Contains("24시간", msg);
    }

    [Fact]
    public void should_allow_creation_when_overnight()
        => Assert.Null(SuppressionRules.ValidateWeeklyForm(31, TimeSpan.FromHours(22), TimeSpan.FromHours(6)));

    [Fact]
    public void should_block_creation_when_daily_time_missing()
        => Assert.NotNull(SuppressionRules.ValidateWeeklyForm(31, null, TimeSpan.FromHours(21)));

    // ══════ 시각 포맷 — offset 이 붙으면 즉시 422 ══════

    [Fact]
    public void should_format_daily_time_without_any_offset()
    {
        var s = SuppressionRules.FormatDailyTime(new TimeSpan(8, 0, 0));
        Assert.Equal("08:00:00", s);
        Assert.DoesNotContain("+", s);
        Assert.DoesNotContain("Z", s);
    }

    [Fact]
    public void should_drop_date_part_when_formatting_from_datetime()
        => Assert.Equal("21:30:00",
            SuppressionRules.FormatDailyTime(new DateTime(2026, 9, 8, 21, 30, 0)));

    [Fact]
    public void should_return_null_when_formatting_null_datetime()
        => Assert.Null(SuppressionRules.FormatDailyTime((DateTime?)null));

    // ══════ 요약 문자열 ══════

    [Theory]
    [InlineData(31, "월~금")]
    [InlineData(96, "주말")]
    [InlineData(127, "매일")]
    [InlineData(1, "월")]
    [InlineData(21, "월·수·금")]
    [InlineData(7, "월~수")]
    [InlineData(0, "")]
    public void should_summarize_days(int mask, string expected)
        => Assert.Equal(expected, SuppressionRules.SummarizeDays(mask));

    [Fact]
    public void should_summarize_full_rule()
        => Assert.Equal("월~금 08:00~21:00",
            SuppressionRules.Summarize(31, new TimeSpan(8, 0, 0), new TimeSpan(21, 0, 0)));

    [Fact]
    public void should_mark_next_day_when_rule_crosses_midnight()
        => Assert.Equal("월~금 22:00~익일 06:00",
            SuppressionRules.Summarize(31, new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0)));

    [Fact]
    public void should_return_empty_summary_when_no_day_selected()
        => Assert.Equal(string.Empty,
            SuppressionRules.Summarize(0, new TimeSpan(8, 0, 0), new TimeSpan(21, 0, 0)));

    // ══════ 중복 경고 — 오늘 해당하지 않는 반복 창은 세지 않는다 ══════

    private static EventSuppressionScheduleDto Weekly(int mask, string ds = "08:00:00", string de = "21:00:00")
        => new()
        {
            TargetType = "all",
            Status = "active",
            RecurrenceType = "weekly",
            DaysOfWeek = mask,
            DailyStart = ds,
            DailyEnd = de,
        };

    [Fact]
    public void should_not_warn_when_weekly_window_does_not_apply_today()
    {
        var saturday = new DateTime(2026, 9, 12, 3, 0, 0);       // 토요일
        var active = new List<EventSuppressionScheduleDto> { Weekly(31) };  // 월~금

        var n = SuppressionRules.CountOverlappingActive(active, "all", null, null, saturday);
        Assert.Equal(0, n);
    }

    [Fact]
    public void should_warn_when_weekly_window_applies_today()
    {
        var monday = new DateTime(2026, 9, 7, 12, 0, 0);         // 월요일
        var active = new List<EventSuppressionScheduleDto> { Weekly(31) };

        Assert.Equal(1, SuppressionRules.CountOverlappingActive(active, "all", null, null, monday));
    }

    [Fact]
    public void should_warn_on_saturday_dawn_when_friday_window_crosses_midnight()
    {
        // 금 22:00 시작분이 토 06:00 까지 이어진다 — 토요일 미선택이어도 유효
        var saturdayDawn = new DateTime(2026, 9, 12, 3, 0, 0);
        var active = new List<EventSuppressionScheduleDto> { Weekly(31, "22:00:00", "06:00:00") };

        Assert.Equal(1, SuppressionRules.CountOverlappingActive(active, "all", null, null, saturdayDawn));
    }

    [Fact]
    public void should_not_warn_on_saturday_evening_when_friday_window_already_ended()
    {
        var saturdayEvening = new DateTime(2026, 9, 12, 23, 0, 0);
        var active = new List<EventSuppressionScheduleDto> { Weekly(31, "22:00:00", "06:00:00") };

        Assert.Equal(0, SuppressionRules.CountOverlappingActive(active, "all", null, null, saturdayEvening));
    }

    [Fact]
    public void should_never_warn_when_weekly_window_has_no_day()
    {
        var monday = new DateTime(2026, 9, 7, 12, 0, 0);
        var active = new List<EventSuppressionScheduleDto> { Weekly(0) };   // 영원히 발동 안 함

        Assert.Equal(0, SuppressionRules.CountOverlappingActive(active, "all", null, null, monday));
    }

    [Fact]
    public void should_prefer_server_flag_over_local_day_computation()
    {
        var saturday = new DateTime(2026, 9, 12, 3, 0, 0);
        var dto = Weekly(31);
        dto.IsSuppressingNow = true;        // 서버가 억제 중이라 말하면 그것이 권위

        Assert.True(SuppressionRules.IsRelevantToday(dto, saturday));
    }

    [Fact]
    public void should_treat_one_shot_window_as_always_relevant()
    {
        var anyTime = new DateTime(2026, 9, 12, 3, 0, 0);
        var dto = new EventSuppressionScheduleDto { TargetType = "all", Status = "active" };

        Assert.True(SuppressionRules.IsRelevantToday(dto, anyTime));
    }

    [Fact]
    public void should_keep_existing_device_overlap_behaviour_for_one_shot()
    {
        var active = new List<EventSuppressionScheduleDto>
        {
            new() { TargetType = "device", Status = "active", TargetDeviceIds = new() { 1802, 1803 } },
        };

        Assert.Equal(1, SuppressionRules.CountOverlappingActive(
            active, "device", new[] { 1802 }, null, new DateTime(2026, 9, 12, 3, 0, 0)));
    }
}
