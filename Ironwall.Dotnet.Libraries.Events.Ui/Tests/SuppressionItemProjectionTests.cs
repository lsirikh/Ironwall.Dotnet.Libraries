using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 억제 목록 행 표시 투영 테스트 — status 배지와 "억제중" 표식의 분리,
                  무제한/파싱실패/값없음 3종 구분, 반복 요약, 회차 정보.
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary><see cref="EventSuppressionScheduleItemViewModel"/> 표시 투영 단위 테스트.</summary>
public class SuppressionItemProjectionTests
{
    private static EventSuppressionScheduleItemViewModel Row(EventSuppressionScheduleDto dto)
        => new(dto, null, null);

    private static EventSuppressionScheduleDto Base(string status = "active") => new()
    {
        Name = "정기 점검",
        TargetType = "all",
        Status = status,
        WindowStart = "2026-08-09T00:00:00+09:00",
        WindowEnd = "2026-09-20T23:59:59+09:00",
    };

    // ══════ FR-03  status 배지와 "억제중" 표식은 다른 축이다 ══════

    [Fact]
    public void should_not_mark_suppressing_when_active_but_outside_occurrence()
    {
        var dto = Base();
        dto.RecurrenceType = "weekly";
        dto.IsSuppressingNow = false;          // 토요일 새벽 — 유효기간 안이지만 미억제

        var vm = Row(dto);
        Assert.Equal("진행중", vm.StatusText);  // 배지는 그대로
        Assert.False(vm.IsSuppressingNow);      // 표식은 없다
    }

    [Fact]
    public void should_mark_suppressing_when_server_says_so()
    {
        var dto = Base();
        dto.RecurrenceType = "weekly";
        dto.IsSuppressingNow = true;

        var vm = Row(dto);
        Assert.Equal("진행중", vm.StatusText);
        Assert.True(vm.IsSuppressingNow);
    }

    [Fact]
    public void should_fall_back_to_status_when_server_omits_suppressing_flag()
    {
        var dto = Base();                       // 구버전 서버 — is_suppressing_now 없음
        Assert.Null(dto.IsSuppressingNow);

        Assert.True(Row(dto).IsSuppressingNow); // status=="active" 로 폴백
    }

    [Fact]
    public void should_not_mark_suppressing_when_pending_and_flag_absent()
        => Assert.False(Row(Base("pending")).IsSuppressingNow);

    // ══════ FR-04  무제한 / 파싱실패 / 값없음 3종 구분 ══════

    [Fact]
    public void should_show_unlimited_when_window_end_is_null()
    {
        var dto = Base();
        dto.WindowEnd = null;

        var vm = Row(dto);
        Assert.Equal("무제한", vm.WindowEndText);
        Assert.True(vm.IsUnlimited);
    }

    [Fact]
    public void should_show_dash_when_window_end_is_empty()
    {
        var dto = Base();
        dto.WindowEnd = string.Empty;

        var vm = Row(dto);
        Assert.Equal("—", vm.WindowEndText);
        Assert.False(vm.IsUnlimited);           // 값 없음 ≠ 무제한
    }

    [Fact]
    public void should_echo_raw_text_when_window_end_is_unparsable()
    {
        var dto = Base();
        dto.WindowEnd = "깨진값";

        Assert.Equal("깨진값", Row(dto).WindowEndText);
    }

    // ══════ FR-05  반복 요약 ══════

    [Fact]
    public void should_summarize_weekly_rule()
    {
        var dto = Base();
        dto.RecurrenceType = "weekly";
        dto.DaysOfWeek = SuppressionRules.DaysWeekdayPreset;
        dto.DailyStart = "08:00:00";
        dto.DailyEnd = "21:00:00";

        var vm = Row(dto);
        Assert.True(vm.IsRecurring);
        Assert.Equal("월~금 08:00~21:00", vm.RecurrenceSummary);
    }

    [Fact]
    public void should_mark_next_day_in_summary_when_overnight()
    {
        var dto = Base();
        dto.RecurrenceType = "weekly";
        dto.DaysOfWeek = SuppressionRules.DaysWeekdayPreset;
        dto.DailyStart = "22:00:00";
        dto.DailyEnd = "06:00:00";

        Assert.Equal("월~금 22:00~익일 06:00", Row(dto).RecurrenceSummary);
    }

    [Fact]
    public void should_leave_summary_empty_for_one_shot()
    {
        var vm = Row(Base());
        Assert.False(vm.IsRecurring);
        Assert.Equal(string.Empty, vm.RecurrenceSummary);
    }

    [Fact]
    public void should_leave_summary_empty_when_no_day_selected()
    {
        var dto = Base();
        dto.RecurrenceType = "weekly";
        dto.DaysOfWeek = 0;
        dto.DailyStart = "08:00:00";
        dto.DailyEnd = "21:00:00";

        Assert.Equal(string.Empty, Row(dto).RecurrenceSummary);
    }

    // ══════ FR-06  회차 정보는 서버 값을 그대로 쓴다 ══════

    [Fact]
    public void should_show_occurrence_end_when_running()
    {
        var dto = Base();
        dto.OccurrenceEnd = "2026-09-07T21:00:00+09:00";

        Assert.Equal("~21:00 까지", Row(dto).OccurrenceText);
    }

    [Fact]
    public void should_show_next_occurrence_when_waiting()
    {
        var dto = Base();
        dto.NextOccurrenceStart = "2026-08-11T08:00:00+09:00";

        Assert.Equal("다음 08-11 08:00", Row(dto).OccurrenceText);
    }

    [Fact]
    public void should_prefer_occurrence_end_over_next_start()
    {
        var dto = Base();
        dto.OccurrenceEnd = "2026-09-07T21:00:00+09:00";
        dto.NextOccurrenceStart = "2026-09-08T08:00:00+09:00";

        Assert.Equal("~21:00 까지", Row(dto).OccurrenceText);
    }

    [Fact]
    public void should_leave_occurrence_empty_when_server_gives_nothing()
        => Assert.Equal(string.Empty, Row(Base()).OccurrenceText);

    // ══════ 무제한 창은 terminal 이 되지 않아 하드삭제 대상이 아니다 ══════

    [Fact]
    public void should_not_be_deletable_when_unlimited_window_stays_active()
    {
        var dto = Base();
        dto.WindowEnd = null;                   // 서버에서 영원히 expired 가 되지 않는다

        var vm = Row(dto);
        Assert.True(vm.IsUnlimited);
        Assert.False(vm.IsDeletable);           // 정리하려면 먼저 취소해야 한다
        Assert.True(vm.IsCancellable);
    }

    [Fact]
    public void should_be_deletable_after_unlimited_window_is_cancelled()
    {
        var dto = Base("cancelled");
        dto.WindowEnd = null;
        dto.RevokedAt = "2026-09-08T00:00:00+09:00";

        var vm = Row(dto);
        Assert.True(vm.IsDeletable);
        Assert.False(vm.IsCancellable);
    }
}
