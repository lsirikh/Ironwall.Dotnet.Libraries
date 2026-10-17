using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Caliburn.Micro;
using Moq;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 억제 패널 VM 의 주간 반복 폼 상태 헤드리스 테스트.
                  특히 '반복+무제한 → 단발 복귀' 에서 플래그만 남아
                  종료일 없는 단발 창이 전송되는 사고(서버 422, 원인이 화면에서
                  사라져 진단 불가)를 회귀로 고정한다.
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>반복 폼 상태·검증 게이트 단위 테스트(서버 호출 없음).</summary>
public class SuppressionPanelRecurrenceTests
{
    private static EventSuppressionSchedulePanelViewModel Vm()
    {
        var log = new Mock<ILogService>().Object;
        return new(new Mock<IEventAggregator>().Object,
                   log,
                   new Mock<IEventSuppressionApiService>().Object,
                   new DeviceProvider(),
                   new DeviceGroupProvider(log));
    }

    private static EventSuppressionSchedulePanelViewModel Ready()
    {
        var vm = Vm();
        vm.Name = "정기 점검";
        vm.TargetType = "all";
        return vm;
    }

    // ══════ 기본값 — 단발이고 기존 동작이 하나도 안 바뀐다 ══════

    [Fact]
    public void should_start_in_one_shot_mode()
    {
        var vm = Vm();
        Assert.True(vm.IsOneShotMode);
        Assert.False(vm.IsWeeklyMode);
        Assert.Equal(SuppressionRecurrenceMode.None, vm.RecurrenceMode);
        Assert.False(vm.IsUnlimitedEffective);
        Assert.Equal(string.Empty, vm.RecurrenceSummaryText);
    }

    [Fact]
    public void should_use_one_shot_window_label_by_default()
        => Assert.Contains("억제 시간창", Vm().WindowFieldLabelText);

    [Fact]
    public void should_switch_window_label_when_weekly()
    {
        var vm = Vm();
        vm.IsWeeklyMode = true;
        Assert.Contains("유효기간", vm.WindowFieldLabelText);
    }

    // ══════ 모드 전환 시 상태 정리 (적대검증 F-03/07) ══════

    [Fact]
    public void should_preset_weekdays_when_switching_to_weekly()
    {
        var vm = Vm();
        vm.DaysOfWeekMask = 0;

        vm.IsWeeklyMode = true;

        Assert.Equal(SuppressionRules.DaysWeekdayPreset, vm.DaysOfWeekMask);
    }

    [Fact]
    public void should_force_unlimited_off_when_returning_to_one_shot()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.IsWindowEndUnlimited = true;
        Assert.True(vm.IsUnlimitedEffective);

        vm.IsOneShotMode = true;      // 단발로 복귀

        // 🔴 이 정리가 없으면 체크박스만 사라지고 플래그가 남아
        //    종료일 없는 단발 창이 전송된다 → 서버 422, 원인은 화면에서 사라진 뒤다.
        Assert.False(vm.IsWindowEndUnlimited);
        Assert.False(vm.IsUnlimitedEffective);
    }

    [Fact]
    public void should_restore_window_end_when_returning_to_one_shot()
    {
        var vm = Ready();
        var original = vm.WindowEnd;
        vm.IsWeeklyMode = true;
        vm.IsWindowEndUnlimited = true;

        vm.IsOneShotMode = true;

        Assert.True(vm.WindowEnd > vm.WindowStart);
        Assert.Equal(original, vm.WindowEnd);
    }

    [Fact]
    public void should_never_allow_one_shot_with_unlimited_flag()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.IsWindowEndUnlimited = true;
        vm.IsOneShotMode = true;

        // 3중 방어 마지막 — 어떤 경로로도 '단발 + 종료일 없음' 이 생성되면 안 된다.
        Assert.True(vm.CanCreate);
        Assert.False(vm.IsOneShotMode && vm.IsWindowEndUnlimited);
    }

    [Fact]
    public void should_keep_unlimited_gate_false_in_one_shot_even_if_raw_flag_set()
    {
        var vm = Ready();
        vm.IsWindowEndUnlimited = true;      // 단발 상태에서 원시 플래그만 세운 경우

        // 파생 게이트는 반복 모드가 아니면 절대 참이 되지 않는다.
        Assert.False(vm.IsUnlimitedEffective);
    }

    // ══════ 요일 — 서버가 막지 않는 '0개' 를 UI 가 막는다 ══════

    [Fact]
    public void should_block_creation_when_no_day_selected()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.DaysOfWeekMask = 0;

        Assert.False(vm.CanCreate);
        Assert.NotNull(vm.WeeklyFormError);
        Assert.True(vm.HasFormError);
        Assert.Contains("요일", vm.FormErrorText);
    }

    [Fact]
    public void should_toggle_individual_days_through_checkbox_properties()
    {
        var vm = Vm();
        vm.IsWeeklyMode = true;
        vm.DaysOfWeekMask = 0;

        vm.IsMonChecked = true;
        vm.IsWedChecked = true;

        Assert.Equal(1 | 4, vm.DaysOfWeekMask);
        Assert.True(vm.IsMonChecked);
        Assert.False(vm.IsTueChecked);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(96)]
    [InlineData(127)]
    public void should_apply_presets(int expected)
    {
        var vm = Vm();
        vm.IsWeeklyMode = true;
        if (expected == 31) vm.PresetWeekday();
        else if (expected == 96) vm.PresetWeekend();
        else vm.PresetEveryDay();

        Assert.Equal(expected, vm.DaysOfWeekMask);
    }

    [Fact]
    public void should_clear_all_days_with_preset()
    {
        var vm = Vm();
        vm.IsWeeklyMode = true;
        vm.PresetClearDays();
        Assert.Equal(0, vm.DaysOfWeekMask);
    }

    // ══════ 일일 시각 — start==end 는 24시간 종일이라 UI 가 막는다 ══════

    [Fact]
    public void should_block_creation_when_daily_times_are_equal()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.DailyStart = DateTime.Today.AddHours(8);
        vm.DailyEnd = DateTime.Today.AddHours(8);

        Assert.False(vm.CanCreate);
        Assert.Contains("24시간", vm.FormErrorText);
    }

    [Fact]
    public void should_allow_overnight_and_show_notice()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.DailyStart = DateTime.Today.AddHours(22);
        vm.DailyEnd = DateTime.Today.AddHours(6);

        Assert.True(vm.CanCreate);
        Assert.True(vm.HasOvernightNotice);
        Assert.Contains("다음날", vm.OvernightNoticeText);
        Assert.False(vm.HasFormError);      // 오류가 아니라 안내다
    }

    [Fact]
    public void should_not_show_overnight_notice_in_one_shot_mode()
    {
        var vm = Ready();
        vm.DailyStart = DateTime.Today.AddHours(22);
        vm.DailyEnd = DateTime.Today.AddHours(6);

        Assert.False(vm.HasOvernightNotice);
    }

    // ══════ 유효기간 상한 — 모드별 ══════

    [Fact]
    public void should_apply_one_shot_cap_of_30_days()
    {
        var vm = Ready();
        vm.WindowStart = new DateTime(2026, 8, 1);
        vm.WindowEnd = new DateTime(2026, 8, 1).AddDays(31);

        Assert.False(vm.IsWindowLengthValid);
        Assert.Equal(30, vm.EffectiveMaxWindowDays);
    }

    [Fact]
    public void should_apply_weekly_cap_of_366_days()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.WindowStart = new DateTime(2026, 8, 1);
        vm.WindowEnd = new DateTime(2026, 8, 1).AddDays(366);   // 경계 — 서버는 > 366 만 거부

        Assert.True(vm.IsWindowLengthValid);
        Assert.Equal(366, vm.EffectiveMaxWindowDays);
    }

    [Fact]
    public void should_reject_weekly_window_beyond_366_days()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.WindowStart = new DateTime(2026, 8, 1);
        vm.WindowEnd = new DateTime(2026, 8, 1).AddDays(367);

        Assert.False(vm.IsWindowLengthValid);
        Assert.Contains("무제한", vm.WindowLengthWarningText);
    }

    [Fact]
    public void should_skip_length_check_when_unlimited()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.WindowStart = new DateTime(2026, 8, 1);
        vm.WindowEnd = new DateTime(2026, 8, 1).AddDays(9999);
        vm.IsWindowEndUnlimited = true;

        Assert.True(vm.IsWindowLengthValid);
    }

    // ══════ 요약 미러 — 값 단언의 유일한 UIA 경로 ══════

    [Fact]
    public void should_render_summary_mirror_when_weekly()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.PresetWeekday();
        vm.DailyStart = DateTime.Today.AddHours(8);
        vm.DailyEnd = DateTime.Today.AddHours(21);

        Assert.Contains("월~금 08:00~21:00", vm.RecurrenceSummaryText);
    }

    [Fact]
    public void should_mark_unlimited_in_summary_mirror()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.PresetWeekday();
        vm.IsWindowEndUnlimited = true;

        Assert.Contains("무제한", vm.RecurrenceSummaryText);
    }

    [Fact]
    public void should_leave_summary_empty_when_no_day_selected()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.DaysOfWeekMask = 0;

        Assert.Equal(string.Empty, vm.RecurrenceSummaryText);
    }

    // ══════ ResetForm — 반복 잔존 방지 (순서 고정) ══════

    [Fact]
    public void should_clear_recurrence_state_on_reset()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.IsWindowEndUnlimited = true;
        vm.PresetEveryDay();

        vm.ResetForm();

        Assert.True(vm.IsOneShotMode);
        Assert.False(vm.IsWindowEndUnlimited);
        Assert.False(vm.IsUnlimitedEffective);
        Assert.Equal(SuppressionRules.DaysWeekdayPreset, vm.DaysOfWeekMask);
        Assert.True(vm.WindowEnd > vm.WindowStart);
    }

    // ══════ 배너 — stale 일 때도 떠야 한다 (적대검증 F-04) ══════

    [Fact]
    public void should_not_show_banner_when_no_active_and_not_stale()
    {
        var vm = Vm();      // Monitor 미주입 → 폴백 캐시(빈 목록), stale 아님
        Assert.False(vm.HasActiveSuppression);
        Assert.False(vm.IsActiveStale);
        Assert.False(vm.HasActiveBanner);
    }

    // ══════ 적대검토 회귀 고정 ══════

    [Fact]
    public void should_widen_validity_period_when_switching_to_weekly()
    {
        // 🔴 회귀 고정 — 단발 기본 유효기간은 1시간이라 그대로 두면 어떤 요일도 그 안에 없어
        //    '영원히 발동하지 않는 창'이 되고 서버가 422 로 막는다(API 6.3.4).
        var vm = Ready();
        Assert.True((vm.WindowEnd - vm.WindowStart).TotalHours < 2);   // 단발 기본

        vm.IsWeeklyMode = true;

        Assert.True((vm.WindowEnd - vm.WindowStart).TotalDays >= 7);
        Assert.Null(vm.WeeklyFormError);        // 도달 가능해야 한다
        Assert.True(vm.CanCreate);
    }

    [Fact]
    public void should_warn_when_recurrence_never_occurs_in_validity_period()
    {
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.PresetClearDays();
        vm.IsMonChecked = true;                                    // 월요일만
        vm.WindowStart = new DateTime(2026, 9, 8);                 // 화요일
        vm.WindowEnd = new DateTime(2026, 9, 9, 23, 59, 59);       // 수요일

        Assert.NotNull(vm.WeeklyFormError);
        Assert.Contains("발동하지 않습니다", vm.WeeklyFormError);
        Assert.False(vm.CanCreate);
    }

    [Fact]
    public void should_allow_midnight_all_day_recurrence()
    {
        // 자정끼리는 서버가 허용하는 유일한 종일 표기다 — 막으면 종일을 표현할 방법이 없다.
        var vm = Ready();
        vm.IsWeeklyMode = true;
        vm.DailyStart = DateTime.Today;
        vm.DailyEnd = DateTime.Today;

        Assert.Null(vm.WeeklyFormError);
        Assert.True(vm.CanCreate);
    }

    [Fact]
    public void should_not_let_unlimited_toggle_clobber_weekly_backup()
    {
        // 🔴 회귀 고정 — 백업 필드를 겸용하면 무제한 토글이 '반복 전환 전' 종료일을 덮어써
        //    단발 복귀 시 1시간이 아니라 30일짜리 창이 남는다.
        var vm = Ready();
        var original = vm.WindowEnd;

        vm.IsWeeklyMode = true;              // 여기서 기간이 30일로 넓어진다
        vm.IsWindowEndUnlimited = true;      // 이 토글이 백업을 덮으면 안 된다
        vm.IsOneShotMode = true;

        Assert.Equal(original, vm.WindowEnd);
    }

    [Fact]
    public void should_expose_stale_text_binding_target()
    {
        // stale 문구가 비어 있으면 배너가 '글자 없는 빈 테두리'로 뜬다 —
        // 활성 0건 + 폴링 실패에서 ActiveCountText 가 빈 문자열이기 때문이다.
        // XAML 이 이 프로퍼티를 바인딩하므로 계약이 유지되는지 고정한다.
        var vm = Vm();
        Assert.NotNull(vm.ActiveStaleText);      // stale 아니면 빈 문자열(예외 아님)
    }
}
