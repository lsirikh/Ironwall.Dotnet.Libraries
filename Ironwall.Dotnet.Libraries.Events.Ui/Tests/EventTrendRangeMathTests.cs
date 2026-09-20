using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 추이 차트에서 기간을 끌어 고르는 판정(events-console PRD FR-35 · V-05 · V-06).
/// UIA 에 드래그 패턴이 없어 이 헤드리스 테스트와 기간 칩 폴백이 회귀망의 전부다.
/// </summary>
public class EventTrendRangeMathTests
{
    private static readonly DateTime Start = new(2026, 9, 20, 0, 0, 0);
    private static readonly DateTime End = new(2026, 9, 21, 0, 0, 0);   // 24시간 · 버킷 1시간

    private const double PlotLeft = 40;
    private const double PlotWidth = 480;                                 // 시간당 20 DIU

    [Fact]
    public void should_return_same_range_when_dragged_right_or_left()
    {
        var rightward = EventTrendRangeMath.Resolve(PlotLeft + 40, PlotLeft + 200, PlotLeft, PlotWidth, Start, End, TimeSpan.FromHours(1));
        var leftward = EventTrendRangeMath.Resolve(PlotLeft + 200, PlotLeft + 40, PlotLeft, PlotWidth, Start, End, TimeSpan.FromHours(1));

        Assert.True(rightward.IsCommittable);
        Assert.Equal(rightward.From, leftward.From);
        Assert.Equal(rightward.To, leftward.To);
    }

    [Fact]
    public void should_not_commit_when_span_is_shorter_than_one_bucket()
    {
        // 20 DIU = 정확히 한 시간이므로 그보다 좁게 끈다.
        var range = EventTrendRangeMath.Resolve(PlotLeft + 100, PlotLeft + 105, PlotLeft, PlotWidth, Start, End, TimeSpan.FromHours(1));

        Assert.False(range.IsCommittable);
        Assert.Equal(string.Empty, range.Label());
    }

    [Fact]
    public void should_clamp_to_visible_range_when_dragged_outside_the_plot()
    {
        var range = EventTrendRangeMath.Resolve(PlotLeft - 500, PlotLeft + PlotWidth + 500, PlotLeft, PlotWidth, Start, End, TimeSpan.FromHours(1));

        Assert.True(range.IsCommittable);
        Assert.Equal(Start, range.From);
        Assert.Equal(End, range.To);
    }

    [Fact]
    public void should_snap_to_bucket_boundaries_when_committing()
    {
        // 한 시간 = 20 DIU. 30 DIU(1.5시간) 지점에서 시작해 110 DIU(5.5시간) 지점에서 놓는다.
        var range = EventTrendRangeMath.Resolve(PlotLeft + 30, PlotLeft + 110, PlotLeft, PlotWidth, Start, End, TimeSpan.FromHours(1));

        Assert.True(range.IsCommittable);
        Assert.Equal(Start.AddHours(1), range.From);      // 내림
        Assert.Equal(Start.AddHours(6), range.To);        // 올림
        Assert.Equal(0, range.From.Minute);
        Assert.Equal(0, range.To.Minute);
    }

    [Fact]
    public void should_return_none_when_plot_has_no_width()
    {
        var range = EventTrendRangeMath.Resolve(0, 100, 0, 0, Start, End, TimeSpan.FromHours(1));
        Assert.False(range.IsCommittable);
    }

    [Fact]
    public void should_return_none_when_range_is_inverted()
    {
        var range = EventTrendRangeMath.Resolve(PlotLeft, PlotLeft + 200, PlotLeft, PlotWidth, End, Start, TimeSpan.FromHours(1));
        Assert.False(range.IsCommittable);
    }

    [Fact]
    public void should_use_hourly_bucket_when_range_is_one_day_and_daily_bucket_when_longer()
    {
        Assert.Equal(TimeSpan.FromHours(1), EventTrendRangeMath.BucketFor(Start, Start.AddDays(1)));
        Assert.Equal(TimeSpan.FromDays(1), EventTrendRangeMath.BucketFor(Start, Start.AddDays(7)));
    }

    [Fact]
    public void should_keep_band_inside_the_plot_when_pointer_leaves_it()
    {
        var (left, width) = EventTrendRangeMath.BandOf(PlotLeft - 100, PlotLeft + PlotWidth + 100, PlotLeft, PlotWidth);

        Assert.Equal(PlotLeft, left);
        Assert.Equal(PlotWidth, width);
    }

    [Fact]
    public void should_report_zero_band_width_when_pointer_has_not_moved()
    {
        var (_, width) = EventTrendRangeMath.BandOf(PlotLeft + 50, PlotLeft + 50, PlotLeft, PlotWidth);
        Assert.Equal(0, width);
    }

    [Fact]
    public void should_not_be_a_drag_when_movement_is_under_the_shared_dead_zone()
    {
        // 새 상수를 만들지 않는다 — 전 앱이 8.0 DIU 다.
        Assert.False(DragMath.IsDrag(5, 0));
        Assert.True(DragMath.IsDrag(9, 0));
    }

    [Fact]
    public void should_format_label_only_when_committable()
    {
        var committable = EventTrendRangeMath.Resolve(PlotLeft, PlotLeft + 200, PlotLeft, PlotWidth, Start, End, TimeSpan.FromHours(1));
        Assert.Contains("~", committable.Label());
        Assert.Equal(string.Empty, TrendRange.None.Label());
    }

    [Fact]
    public void should_clamp_fraction_between_zero_and_one()
    {
        Assert.Equal(0, EventTrendRangeMath.Fraction(-100, 0, 100));
        Assert.Equal(1, EventTrendRangeMath.Fraction(999, 0, 100));
        Assert.Equal(0.5, EventTrendRangeMath.Fraction(50, 0, 100));
    }
}
