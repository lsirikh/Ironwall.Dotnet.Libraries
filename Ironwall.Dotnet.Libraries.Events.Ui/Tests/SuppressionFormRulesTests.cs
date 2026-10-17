using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 억제 편집 폼 검증(suppression-schedule PRD FR-30~FR-36 · V-07~V-12).
/// 시간은 전부 인자로 받는다 — <c>DateTime.Now</c> 를 부르는 곳이 없다.
/// </summary>
public class SuppressionFormRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 10, 0, 0, TimeSpan.FromHours(9));

    private static SuppressionDraft OneShot(params int[] deviceIds)
    {
        var draft = SuppressionDraft.NewSchedule(Now);
        draft.Name = "정문 보수";
        foreach (var id in deviceIds)
            draft.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, id, $"센서-{id}"));
        return draft;
    }

    private static SuppressionDraft Weekly(params int[] deviceIds)
    {
        var draft = OneShot(deviceIds);
        draft.IsWeekly = true;
        draft.WindowEnd = Now.AddDays(30);
        draft.DaysOfWeekMask = SuppressionRules.DaysWeekdayPreset;
        draft.DailyStart = TimeSpan.FromHours(8);
        draft.DailyEnd = TimeSpan.FromHours(21);
        return draft;
    }

    [Fact]
    public void should_pass_when_a_one_shot_draft_is_complete()
    {
        var verdict = SuppressionFormRules.Validate(OneShot(1), Now);

        Assert.True(verdict.CanSave);
        Assert.Empty(verdict.Errors);
    }

    [Fact]
    public void should_fail_when_the_name_is_blank()
    {
        var draft = OneShot(1);
        draft.Name = "   ";

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.False(verdict.CanSave);
        Assert.True(verdict.Has(SuppressionFormRules.FieldName));
    }

    [Fact]
    public void should_fail_when_a_device_draft_has_no_targets()
    {
        var verdict = SuppressionFormRules.Validate(OneShot(), Now);

        Assert.False(verdict.CanSave);
        Assert.True(verdict.Has(SuppressionFormRules.FieldTargets));
    }

    [Fact]
    public void should_pass_with_no_targets_when_the_mode_is_all()
    {
        var draft = OneShot();
        draft.TargetType = SuppressionTargetDrop.ModeAll;

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.True(verdict.CanSave);
    }

    [Fact]
    public void should_fail_when_targets_exceed_the_cap()
    {
        var draft = OneShot(Enumerable.Range(1, SuppressionTargetDrop.MaxTargets + 1).ToArray());

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.False(verdict.CanSave);
        Assert.Contains(SuppressionTargetDrop.MaxTargets.ToString(), verdict.FirstErrorText);
    }

    [Fact]
    public void should_pass_when_targets_are_exactly_at_the_cap()
    {
        // 경계 — 상한은 '넘을 때' 걸려야지 '닿을 때' 걸리면 안 된다.
        var draft = OneShot(Enumerable.Range(1, SuppressionTargetDrop.MaxTargets).ToArray());

        Assert.True(SuppressionFormRules.Validate(draft, Now).CanSave);
    }

    [Fact]
    public void should_fail_when_the_end_is_not_after_the_start()
    {
        var draft = OneShot(1);
        draft.WindowEnd = draft.WindowStart;

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.False(verdict.CanSave);
        Assert.True(verdict.Has(SuppressionFormRules.FieldWindow));
    }

    [Fact]
    public void should_fail_when_the_end_is_before_the_start()
    {
        var draft = OneShot(1);
        draft.WindowEnd = draft.WindowStart.AddMinutes(-1);

        Assert.False(SuppressionFormRules.Validate(draft, Now).CanSave);
    }

    [Fact]
    public void should_fail_when_a_one_shot_draft_has_no_end()
    {
        // 단발 + 종료 없음은 서버가 422 로 막는다 — 화면에서 먼저 거른다.
        var draft = OneShot(1);
        draft.WindowEnd = null;

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.False(verdict.CanSave);
        Assert.Contains("단발", verdict.FirstErrorText);
    }

    [Fact]
    public void should_pass_when_a_weekly_draft_has_no_end()
    {
        var draft = Weekly(1);
        draft.WindowEnd = null;

        Assert.True(SuppressionFormRules.Validate(draft, Now).CanSave);
    }

    [Fact]
    public void should_fail_when_a_one_shot_window_is_longer_than_the_client_cap()
    {
        var draft = OneShot(1);
        draft.WindowEnd = draft.WindowStart.AddDays(SuppressionRules.DefaultMaxWindowDays + 1);

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.False(verdict.CanSave);
        Assert.Contains(SuppressionRules.DefaultMaxWindowDays.ToString(), verdict.FirstErrorText);
    }

    [Fact]
    public void should_fail_when_a_weekly_draft_has_no_day()
    {
        var draft = Weekly(1);
        draft.DaysOfWeekMask = 0;

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.False(verdict.CanSave);
        Assert.True(verdict.Has(SuppressionFormRules.FieldRecurrence));
    }

    [Fact]
    public void should_fail_when_a_weekly_occurrence_can_never_happen()
    {
        // 유효기간이 화요일 하루뿐인데 요일이 '월' 이면 영원히 발동하지 않는다(서버 422).
        var monday = new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.FromHours(9));   // 2026-09-22 = 화요일
        var draft = Weekly(1);
        draft.WindowStart = monday;
        draft.WindowEnd = monday.AddHours(20);
        draft.DaysOfWeekMask = SuppressionRules.BitOf(0);                                // 월만

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.False(verdict.CanSave);
        Assert.True(verdict.Has(SuppressionFormRules.FieldRecurrence));
    }

    [Fact]
    public void should_warn_when_the_daily_window_crosses_midnight()
    {
        var draft = Weekly(1);
        draft.DailyStart = TimeSpan.FromHours(22);
        draft.DailyEnd = TimeSpan.FromHours(6);

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.True(verdict.CanSave);                       // 막지 않는다 — 정상적인 야간 정비다
        Assert.Contains(verdict.Warnings, w => w.Contains("자정"));
    }

    [Fact]
    public void should_warn_when_a_one_shot_window_is_already_over()
    {
        var draft = OneShot(1);
        draft.WindowStart = Now.AddHours(-5);
        draft.WindowEnd = Now.AddHours(-4);

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.True(verdict.CanSave);
        Assert.Contains(verdict.Warnings, w => w.Contains("이미 지났습니다"));
    }

    [Fact]
    public void should_report_the_first_error_only()
    {
        var draft = OneShot();                              // 이름 없음 + 대상 없음
        draft.Name = string.Empty;

        var verdict = SuppressionFormRules.Validate(draft, Now);

        Assert.Equal(2, verdict.Errors.Count);
        Assert.Equal(verdict.Errors[0].Message, verdict.FirstErrorText);
    }

    #region - 겹침 경고: 자기 자신은 세지 않는다 -

    private static EventSuppressionScheduleDto Active(int id, params int[] deviceIds) => new()
    {
        Id = id,
        Name = $"#{id}",
        Status = "active",
        TargetType = SuppressionTargetDrop.ModeDevice,
        TargetDeviceIds = deviceIds.ToList(),
        IsSuppressingNow = true,
    };

    [Fact]
    public void should_warn_when_another_active_window_covers_the_same_device()
    {
        var verdict = SuppressionFormRules.Validate(OneShot(7), Now, new[] { Active(99, 7) });

        Assert.True(verdict.CanSave);                       // 경고일 뿐 막지 않는다
        Assert.Contains(verdict.Warnings, w => w.Contains("중복"));
    }

    [Fact]
    public void should_not_warn_about_itself_when_editing()
    {
        // 수정 화면에서 자기 id 를 세면 고칠 때마다 "이미 중복" 이 떠 경고가 쓸모없어진다.
        var draft = OneShot(7);
        draft.Id = 99;

        var verdict = SuppressionFormRules.Validate(draft, Now, new[] { Active(99, 7) });

        Assert.Empty(verdict.Warnings.Where(w => w.Contains("중복")));
    }

    [Fact]
    public void should_still_warn_about_other_windows_when_editing()
    {
        var draft = OneShot(7);
        draft.Id = 99;

        var verdict = SuppressionFormRules.Validate(draft, Now, new[] { Active(99, 7), Active(100, 7) });

        Assert.Contains(verdict.Warnings, w => w.Contains("1건"));
    }

    [Fact]
    public void should_count_nothing_when_there_are_no_other_schedules()
        => Assert.Equal(0, SuppressionFormRules.CountOverlapping(OneShot(7), null));

    #endregion

    #region - 요약 한 줄 -

    [Fact]
    public void should_summarise_a_weekly_draft_with_its_days_and_span()
    {
        var recap = SuppressionFormRules.Recap(Weekly(1));

        Assert.StartsWith("→", recap);
        Assert.Contains("월~금", recap);
        Assert.Contains("08:00", recap);
    }

    [Fact]
    public void should_summarise_an_unlimited_weekly_draft_as_unlimited()
    {
        var draft = Weekly(1);
        draft.WindowEnd = null;

        Assert.Contains("무제한", SuppressionFormRules.Recap(draft));
    }

    [Fact]
    public void should_summarise_a_one_shot_draft_as_a_single_window()
        => Assert.Contains("단발", SuppressionFormRules.Recap(OneShot(1)));

    [Fact]
    public void should_ask_for_a_day_when_a_weekly_draft_has_none()
    {
        var draft = Weekly(1);
        draft.DaysOfWeekMask = 0;

        Assert.Contains("요일", SuppressionFormRules.Recap(draft));
    }

    #endregion
}
