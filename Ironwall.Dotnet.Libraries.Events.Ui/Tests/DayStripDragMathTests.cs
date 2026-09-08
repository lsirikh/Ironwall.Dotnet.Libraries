using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 요일 스트립 드래그 판정 순수 함수 테스트.
                  .NET 8 WPF 에는 UIA 드래그 패턴 자체가 없어 제스처를 자동화로
                  단언할 수 없다 — 판정부를 순수 함수로 뽑은 이 테스트가
                  드래그 로직의 유일한 회귀 방어선이다.
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary><see cref="DayStripDragMath"/> 단위 테스트.</summary>
public class DayStripDragMathTests
{
    // ══════ 데드존 — 경계는 배타다 ══════

    [Fact]
    public void should_use_repo_standard_deadzone()
        => Assert.Equal(8.0, DayStripDragMath.DragThresholdDiu);

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(3, 3, false)]        // 4.24 < 8
    [InlineData(8, 0, false)]        // 🔴 정확히 8.0 은 '드래그 아님'(정본 테스트가 고정한 경계)
    [InlineData(0, 8, false)]
    [InlineData(8.001, 0, true)]
    [InlineData(6, 6, true)]         // 8.49 > 8
    [InlineData(-9, 0, true)]        // 방향 무관
    public void should_classify_drag_by_deadzone(double dx, double dy, bool expected)
        => Assert.Equal(expected, DayStripDragMath.IsDrag(dx, dy));

    [Fact]
    public void should_honor_custom_threshold()
    {
        Assert.False(DayStripDragMath.IsDrag(3, 0, 3.0));   // 경계 배타
        Assert.True(DayStripDragMath.IsDrag(3.1, 0, 3.0));
    }

    // ══════ 좌표 → 인덱스 ══════

    [Theory]
    [InlineData(0, 0)]
    [InlineData(31, 0)]
    [InlineData(32, 1)]
    [InlineData(112, 3)]
    [InlineData(223, 6)]
    public void should_map_x_to_cell_index(double x, int expected)
        => Assert.Equal(expected, DayStripDragMath.IndexFromX(x, 224));

    [Theory]
    [InlineData(-50, 0)]             // 스트립 왼쪽 밖 → 첫 칸
    [InlineData(9999, 6)]            // 오른쪽 밖 → 마지막 칸
    public void should_clamp_out_of_range_x(double x, int expected)
        => Assert.Equal(expected, DayStripDragMath.IndexFromX(x, 224));

    [Fact]
    public void should_return_zero_when_width_is_invalid()
        => Assert.Equal(0, DayStripDragMath.IndexFromX(100, 0));

    // ══════ 의도 결정 — 시작 셀 상태가 방향을 정한다 ══════

    [Fact]
    public void should_paint_when_start_cell_is_off()
        => Assert.True(DayStripDragMath.DecidePaintIntent(0, 0));

    [Fact]
    public void should_erase_when_start_cell_is_on()
        => Assert.False(DayStripDragMath.DecidePaintIntent(SuppressionRules.DaysWeekdayPreset, 0));

    // ══════ 범위 적용 ══════

    [Fact]
    public void should_paint_range_forward()
        => Assert.Equal(1 | 2 | 4, DayStripDragMath.ApplyRange(0, 0, 2, paint: true));

    [Fact]
    public void should_paint_range_backward_identically()
        => Assert.Equal(
            DayStripDragMath.ApplyRange(0, 0, 2, true),
            DayStripDragMath.ApplyRange(0, 2, 0, true));

    [Fact]
    public void should_erase_range()
        => Assert.Equal(
            SuppressionRules.DaysWeekdayPreset & ~(1 | 2),
            DayStripDragMath.ApplyRange(SuppressionRules.DaysWeekdayPreset, 0, 1, paint: false));

    [Fact]
    public void should_preserve_cells_outside_range()
    {
        // 월(1) 켜진 상태에서 수~목만 칠한다 — 월은 유지, 화는 그대로 꺼짐
        var result = DayStripDragMath.ApplyRange(1, 2, 3, paint: true);
        Assert.Equal(1 | 4 | 8, result);
    }

    [Fact]
    public void should_apply_single_cell_when_from_equals_to()
        => Assert.Equal(16, DayStripDragMath.ApplyRange(0, 4, 4, paint: true));

    [Theory]
    [InlineData(-3, 2)]
    [InlineData(0, 99)]
    public void should_clamp_range_indexes(int from, int to)
    {
        var result = DayStripDragMath.ApplyRange(0, from, to, paint: true);
        Assert.True(result >= 0 && result <= SuppressionRules.DaysEveryDayPreset);
    }

    [Fact]
    public void should_produce_weekday_preset_by_dragging_mon_to_fri()
        => Assert.Equal(SuppressionRules.DaysWeekdayPreset,
            DayStripDragMath.ApplyRange(0, 0, 4, paint: true));

    [Fact]
    public void should_produce_every_day_by_dragging_whole_strip()
        => Assert.Equal(SuppressionRules.DaysEveryDayPreset,
            DayStripDragMath.ApplyRange(0, 0, 6, paint: true));

    // ══════ 범위 시각 표식 ══════

    [Fact]
    public void should_not_mark_range_when_not_dragging()
        => Assert.False(DayStripDragMath.IsInRange(dragging: false, 0, 4, 2));

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void should_mark_cells_inside_range(int cell, bool expected)
        => Assert.Equal(expected, DayStripDragMath.IsInRange(true, 0, 4, cell));

    [Fact]
    public void should_mark_range_regardless_of_direction()
        => Assert.True(DayStripDragMath.IsInRange(true, 4, 0, 2));

    // ══════ 클릭 폴백이 토글과 같은 결과를 내는가 ══════

    [Fact]
    public void click_fallback_should_behave_like_a_toggle()
    {
        var mask = 0;

        // 첫 클릭 — 켠다
        mask = DayStripDragMath.ApplyRange(mask, 3, 3, DayStripDragMath.DecidePaintIntent(mask, 3));
        Assert.Equal(8, mask);

        // 같은 칸 다시 클릭 — 끈다
        mask = DayStripDragMath.ApplyRange(mask, 3, 3, DayStripDragMath.DecidePaintIntent(mask, 3));
        Assert.Equal(0, mask);
    }
}

/// <summary><see cref="SuppressionPollThrottle"/> 단위 테스트 — 스로틀·TTL·경과 문구.</summary>
public class SuppressionPollThrottleTests
{
    private static readonly System.DateTime T0 = new(2026, 9, 8, 12, 0, 0);

    // ══════ 스로틀 — 리딩 엣지 + 트레일링 보장 ══════

    [Fact]
    public void should_poll_now_on_first_request()
        => Assert.Equal(PollDecision.PollNow,
            SuppressionPollThrottle.Decide(T0, null, false));

    [Fact]
    public void should_defer_inside_cooldown()
        => Assert.Equal(PollDecision.Defer,
            SuppressionPollThrottle.Decide(T0.AddSeconds(2), T0, false));

    [Fact]
    public void should_poll_now_after_cooldown()
        => Assert.Equal(PollDecision.PollNow,
            SuppressionPollThrottle.Decide(T0.AddSeconds(5), T0, false));

    [Fact]
    public void should_not_schedule_twice()
        => Assert.Equal(PollDecision.AlreadyScheduled,
            SuppressionPollThrottle.Decide(T0.AddSeconds(1), T0, hasPendingSchedule: true));

    [Fact]
    public void should_poll_now_when_clock_goes_backwards()
        // 시계 역행(수동 조정·DST)에도 멈추지 않아야 한다.
        => Assert.Equal(PollDecision.PollNow,
            SuppressionPollThrottle.Decide(T0.AddSeconds(-30), T0, false));

    [Fact]
    public void should_report_remaining_cooldown()
        => Assert.Equal(3.0,
            SuppressionPollThrottle.RemainingCooldown(T0.AddSeconds(2), T0).TotalSeconds, 3);

    // ══════ TTL — 한 번도 성공 못 한 경우가 핵심이다 ══════

    [Fact]
    public void should_not_be_stale_right_after_success()
        => Assert.False(SuppressionPollThrottle.IsStale(T0.AddSeconds(10), T0, 90));

    [Fact]
    public void should_be_stale_after_ttl()
        => Assert.True(SuppressionPollThrottle.IsStale(T0.AddSeconds(91), T0, 90));

    [Fact]
    public void should_be_stale_when_never_succeeded_but_started_long_ago()
    {
        // 🔴 회귀 고정 — Monitor 가 기산점(_startedAt)을 넘겨주지 않으면
        //    서버가 계속 실패할 때 배너가 영원히 침묵해
        //    '억제 0건'과 '서버에 물어본 적 없음'이 구분 불가가 된다.
        var startedAt = T0;
        Assert.True(SuppressionPollThrottle.IsStale(T0.AddSeconds(91), startedAt, 90));
    }

    [Fact]
    public void should_hold_stale_verdict_when_no_reference_time()
        => Assert.False(SuppressionPollThrottle.IsStale(T0.AddSeconds(9999), null, 90));

    // ══════ 경과 문구 ══════

    [Theory]
    [InlineData(30, "30초 전")]
    [InlineData(180, "3분 전")]
    [InlineData(7200, "2시간 전")]
    public void should_describe_age(int seconds, string expected)
        => Assert.Equal(expected, SuppressionPollThrottle.DescribeAge(T0.AddSeconds(seconds), T0));

    [Fact]
    public void should_describe_unknown_when_never_succeeded()
        => Assert.Equal("확인 안 됨", SuppressionPollThrottle.DescribeAge(T0, null));
}
