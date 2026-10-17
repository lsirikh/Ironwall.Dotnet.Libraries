using System;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// DateRangeCalendar(Playback 기간 캘린더 순수 로직) 단위 테스트.
/// 실제 소스를 &lt;Compile Include&gt;로 링크해 검증(net8.0 격리, WPF 무의존).
/// 2클릭 범위 선택(시작/끝/역순 swap) + 월 그리드(42칸·하이라이트 플래그).
/// </summary>
public class DateRangeCalendarTests
{
    private static readonly DateTime Today = new(2026, 6, 29);

    // ── 2클릭 상태머신 ──────────────────────────────────────────

    [Fact(DisplayName = "should_set_start_and_wait_when_first_click")]
    public void Click_First_SetsStartAndPends()
    {
        var (s, e, pending, completed) = DateRangeCalendar.Click(null, new DateTime(2026, 6, 10));

        Assert.Equal(new DateTime(2026, 6, 10), s);
        Assert.Equal(new DateTime(2026, 6, 10), e);
        Assert.Equal(new DateTime(2026, 6, 10), pending);
        Assert.False(completed);
    }

    [Fact(DisplayName = "should_complete_range_when_second_click_in_order")]
    public void Click_SecondInOrder_CompletesRange()
    {
        var first = DateRangeCalendar.Click(null, new DateTime(2026, 6, 10));
        var (s, e, pending, completed) = DateRangeCalendar.Click(first.pending, new DateTime(2026, 6, 15));

        Assert.Equal(new DateTime(2026, 6, 10), s);
        Assert.Equal(new DateTime(2026, 6, 15), e);
        Assert.Null(pending);
        Assert.True(completed);
    }

    [Fact(DisplayName = "should_swap_when_second_click_before_first")]
    public void Click_SecondBeforeFirst_Swaps()
    {
        var (s, e, pending, completed) = DateRangeCalendar.Click(new DateTime(2026, 6, 20), new DateTime(2026, 6, 5));

        Assert.Equal(new DateTime(2026, 6, 5), s);
        Assert.Equal(new DateTime(2026, 6, 20), e);
        Assert.Null(pending);
        Assert.True(completed);
    }

    [Fact(DisplayName = "should_select_single_day_when_same_date_twice")]
    public void Click_SameDateTwice_SingleDayRange()
    {
        var (s, e, _, completed) = DateRangeCalendar.Click(new DateTime(2026, 6, 10), new DateTime(2026, 6, 10));

        Assert.Equal(new DateTime(2026, 6, 10), s);
        Assert.Equal(new DateTime(2026, 6, 10), e);
        Assert.True(completed);
    }

    [Fact(DisplayName = "should_strip_time_when_click")]
    public void Click_StripsTimeComponent()
    {
        var (s, _, _, _) = DateRangeCalendar.Click(null, new DateTime(2026, 6, 10, 15, 30, 45));
        Assert.Equal(new DateTime(2026, 6, 10), s);
    }

    // ── 월 그리드 ──────────────────────────────────────────────

    [Fact(DisplayName = "should_build_42_cells_when_grid")]
    public void BuildGrid_Has42Cells()
    {
        var grid = DateRangeCalendar.BuildGrid(new DateTime(2026, 6, 1), new DateTime(2026, 6, 10), new DateTime(2026, 6, 12), Today);
        Assert.Equal(42, grid.Count);
    }

    [Fact(DisplayName = "should_start_grid_on_sunday")]
    public void BuildGrid_StartsOnSunday()
    {
        var grid = DateRangeCalendar.BuildGrid(new DateTime(2026, 6, 1), Today, Today, Today);
        Assert.Equal(DayOfWeek.Sunday, grid[0].Date.DayOfWeek);
        // 2026-06-01 = 월요일 → 그리드 첫 칸은 직전 일요일 2026-05-31
        Assert.Equal(new DateTime(2026, 5, 31), grid[0].Date);
        Assert.False(grid[0].IsCurrentMonth);
    }

    [Fact(DisplayName = "should_flag_current_month_only_for_in_month_days")]
    public void BuildGrid_CurrentMonthFlag()
    {
        var grid = DateRangeCalendar.BuildGrid(new DateTime(2026, 6, 1), Today, Today, Today);
        Assert.True(grid.Single(c => c.Date == new DateTime(2026, 6, 1)).IsCurrentMonth);
        Assert.False(grid.Single(c => c.Date == new DateTime(2026, 5, 31)).IsCurrentMonth);
        Assert.Equal(30, grid.Count(c => c.IsCurrentMonth)); // 6월 = 30일
    }

    [Fact(DisplayName = "should_mark_start_end_and_inRange_when_grid_built")]
    public void BuildGrid_HighlightFlags()
    {
        var grid = DateRangeCalendar.BuildGrid(new DateTime(2026, 6, 1), new DateTime(2026, 6, 10), new DateTime(2026, 6, 12), Today);

        var d10 = grid.Single(c => c.Date == new DateTime(2026, 6, 10));
        Assert.True(d10.IsStart);
        Assert.False(d10.IsInRange);

        var d11 = grid.Single(c => c.Date == new DateTime(2026, 6, 11));
        Assert.True(d11.IsInRange);
        Assert.False(d11.IsStart);
        Assert.False(d11.IsEnd);

        var d12 = grid.Single(c => c.Date == new DateTime(2026, 6, 12));
        Assert.True(d12.IsEnd);

        var d13 = grid.Single(c => c.Date == new DateTime(2026, 6, 13));
        Assert.False(d13.IsInRange);
        Assert.False(d13.IsStart);
        Assert.False(d13.IsEnd);
    }

    [Fact(DisplayName = "should_normalize_when_grid_start_after_end")]
    public void BuildGrid_NormalizesReversedRange()
    {
        var grid = DateRangeCalendar.BuildGrid(new DateTime(2026, 6, 1), new DateTime(2026, 6, 15), new DateTime(2026, 6, 10), Today);
        Assert.True(grid.Single(c => c.Date == new DateTime(2026, 6, 10)).IsStart);
        Assert.True(grid.Single(c => c.Date == new DateTime(2026, 6, 15)).IsEnd);
    }

    [Fact(DisplayName = "should_flag_today_only_for_today_cell")]
    public void BuildGrid_TodayFlag()
    {
        var grid = DateRangeCalendar.BuildGrid(new DateTime(2026, 6, 1), new DateTime(2026, 6, 10), new DateTime(2026, 6, 12), Today);
        Assert.True(grid.Single(c => c.Date == Today).IsToday);
        Assert.All(grid.Where(c => c.Date != Today), c => Assert.False(c.IsToday));
    }

    [Fact(DisplayName = "should_normalize_to_month_first_when_MonthOf")]
    public void MonthOf_ReturnsFirstOfMonth()
        => Assert.Equal(new DateTime(2026, 6, 1), DateRangeCalendar.MonthOf(new DateTime(2026, 6, 29, 13, 45, 0)));
}
