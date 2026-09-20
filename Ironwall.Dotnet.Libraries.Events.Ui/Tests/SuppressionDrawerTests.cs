using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 780 서랍 — 초안 · 미적용 변경 · 닫기 차단 · 저장 한 번(suppression-schedule PRD FR-14~FR-19 · FR-28~FR-29 · V-25~V-33).
/// </summary>
public class SuppressionDrawerViewModelTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 9, 20, 10, 0, 0));
    private readonly List<SuppressionDraft> _saved = new();
    private bool _canEdit = true;
    private bool _saveOk = true;
    private readonly SuppressionDrawerViewModel _drawer;

    public SuppressionDrawerViewModelTests()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        _drawer = new SuppressionDrawerViewModel(_clock, null, null, Save, () => _canEdit);
    }

    private Task<SuppressionSaveOutcome> Save(SuppressionDraft draft, CancellationToken token)
    {
        _saved.Add(draft);
        return Task.FromResult(_saveOk
            ? new SuppressionSaveOutcome(true, "저장했습니다.", new EventSuppressionScheduleDto { Id = 1 })
            : new SuppressionSaveOutcome(false, "서버가 거절했습니다.", null));
    }

    private static object Device(int id) => new SuppressionTargetChip(SuppressionTargetKind.Device, id, $"센서-{id}");
    private static object Group(int id) => new SuppressionTargetChip(SuppressionTargetKind.Group, id, $"그룹-{id}");

    private void FillValidNew()
    {
        _drawer.OpenNew();
        _drawer.Name = "정문 보수";
        _drawer.AddSelected(new[] { Device(3) });
    }

    #region - 열고 닫기 -

    [Fact]
    public void should_open_clean_when_a_new_schedule_starts()
    {
        _drawer.OpenNew();

        Assert.True(_drawer.IsOpen);
        Assert.True(_drawer.IsNew);
        Assert.False(_drawer.IsDirty);
        Assert.Equal("새 억제 스케줄", _drawer.HeaderText);
    }

    [Fact]
    public void should_close_when_nothing_was_edited()
    {
        _drawer.OpenNew();

        Assert.True(_drawer.TryClose());
        Assert.False(_drawer.IsOpen);
    }

    [Fact]
    public void should_refuse_to_close_when_there_are_unapplied_edits()
    {
        _drawer.OpenNew();
        var shakeBefore = _drawer.ShakeToken;
        _drawer.Name = "쓰다 만 것";

        Assert.False(_drawer.TryClose());
        Assert.True(_drawer.IsOpen);
        Assert.True(_drawer.ShakeToken > shakeBefore);          // 막았으면 한 번 흔든다
        Assert.Contains("되돌린 뒤", _drawer.StatusLine);
    }

    [Fact]
    public void should_close_after_the_edits_are_reverted()
    {
        _drawer.OpenNew();
        _drawer.Name = "쓰다 만 것";
        _drawer.Revert();

        Assert.False(_drawer.IsDirty);
        Assert.True(_drawer.TryClose());
    }

    [Fact]
    public void should_make_no_server_call_when_reverting()
    {
        FillValidNew();

        _drawer.Revert();

        Assert.Empty(_saved);
        Assert.Empty(_drawer.Tray);
    }

    [Fact]
    public void should_restore_the_original_when_reverting_an_edit()
    {
        _drawer.OpenEdit(Existing());
        _drawer.Name = "다른 이름";
        _drawer.RemoveChip(_drawer.Tray[0]);

        _drawer.Revert();

        Assert.Equal("탄약고 야간 점검", _drawer.Name);
        Assert.Single(_drawer.Tray);
        Assert.False(_drawer.IsDirty);
    }

    #endregion

    #region - 초안 지문(미적용 변경 판정) -

    [Fact]
    public void should_see_a_name_change_as_an_edit()
    {
        _drawer.OpenNew();
        _drawer.Name = "바뀜";

        Assert.True(_drawer.IsDirty);
    }

    [Fact]
    public void should_see_a_target_change_as_an_edit()
    {
        _drawer.OpenNew();
        _drawer.AddSelected(new[] { Device(1) });

        Assert.True(_drawer.IsDirty);
    }

    [Fact]
    public void should_not_see_an_edit_when_a_target_is_added_then_removed()
    {
        _drawer.OpenEdit(Existing());
        var chip = new SuppressionTargetChip(SuppressionTargetKind.Device, 9, "센서-9");

        _drawer.AddSelected(new object[] { chip });
        Assert.True(_drawer.IsDirty);

        _drawer.RemoveChip(chip);
        Assert.False(_drawer.IsDirty);
    }

    [Fact]
    public void should_ignore_target_order_when_comparing_drafts()
    {
        var a = SuppressionDraft.NewSchedule(new DateTimeOffset(_clock.Now));
        a.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, 1, "a"));
        a.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, 2, "b"));

        var b = SuppressionDraft.NewSchedule(new DateTimeOffset(_clock.Now));
        b.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, 2, "b"));
        b.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, 1, "a"));

        // 대상은 집합이다 — 담은 순서는 서버 뜻에 영향을 주지 않는다.
        Assert.Equal(SuppressionDrawerViewModel.Signature(a), SuppressionDrawerViewModel.Signature(b));
    }

    #endregion

    #region - 대상 트레이 -

    [Fact]
    public void should_drop_wrong_kind_chips_when_the_mode_changes()
    {
        _drawer.OpenNew();
        _drawer.AddSelected(new[] { Device(1), Device(2) });

        _drawer.TargetType = SuppressionTargetDrop.ModeGroup;

        // 종류가 바뀌면 보낼 수 없는 칩이 조용히 남아 있으면 안 된다.
        Assert.Empty(_drawer.Tray);
        Assert.Contains("2개를 뺐습니다", _drawer.StatusLine);
    }

    [Fact]
    public void should_take_groups_after_switching_to_group_mode()
    {
        _drawer.OpenNew();
        _drawer.TargetType = SuppressionTargetDrop.ModeGroup;

        _drawer.AddSelected(new[] { Group(4) });

        Assert.Single(_drawer.Tray);
    }

    [Fact]
    public void should_take_no_targets_when_the_mode_is_all()
    {
        _drawer.OpenNew();
        _drawer.TargetType = SuppressionTargetDrop.ModeAll;

        Assert.False(_drawer.AcceptsTargets);

        _drawer.AddSelected(new[] { Device(1) });

        Assert.Empty(_drawer.Tray);
    }

    [Fact]
    public void should_empty_the_tray_when_clearing()
    {
        _drawer.OpenNew();
        _drawer.AddSelected(new[] { Device(1), Device(2) });

        _drawer.ClearChips();

        Assert.Empty(_drawer.Tray);
        Assert.True(_drawer.IsTrayEmpty);
    }

    [Fact]
    public void should_count_the_tray_against_the_cap()
    {
        _drawer.OpenNew();
        _drawer.AddSelected(new[] { Device(1) });

        Assert.Equal($"1 / {SuppressionTargetDrop.MaxTargets}", _drawer.TrayCountText);
    }

    [Fact]
    public void should_keep_the_same_chip_instances_when_syncing_a_bound_collection()
    {
        // Clear()+Add() 는 선택 · 스크롤 · 가상화를 깬다 — 있는 것은 그대로 둬야 한다.
        var live = new ObservableCollection<SuppressionTargetChip>
        {
            new(SuppressionTargetKind.Device, 1, "a"),
            new(SuppressionTargetKind.Device, 2, "b"),
        };
        var kept = live[1];

        SuppressionDrawerViewModel.SyncChips(live, new List<SuppressionTargetChip>
        {
            live[1],
            new(SuppressionTargetKind.Device, 3, "c"),
        });

        Assert.Equal(2, live.Count);
        Assert.Same(kept, live[0]);
        Assert.Equal(3, live[1].Id);
    }

    #endregion

    #region - 반복 -

    [Fact]
    public void should_widen_the_window_when_switching_to_weekly()
    {
        _drawer.OpenNew();                         // 기본은 '지금부터 1시간' 단발

        _drawer.IsWeekly = true;

        // 1시간짜리 유효기간에는 어떤 요일도 들어가지 않는다 — 서버가 422 로 막는 창이 된다.
        Assert.True((_drawer.WindowEnd - _drawer.WindowStart).TotalDays >= 7);
        Assert.True(SuppressionRules.HasAnyDay(_drawer.DaysOfWeekMask));
    }

    [Fact]
    public void should_restore_an_end_when_switching_back_to_one_shot()
    {
        _drawer.OpenNew();
        _drawer.IsWeekly = true;
        _drawer.IsUnlimited = true;

        _drawer.IsWeekly = false;

        // 단발 + 종료 없음은 422 다 — 되돌아오면서 반드시 끝을 만든다.
        Assert.False(_drawer.IsUnlimited);
    }

    [Fact]
    public void should_refuse_unlimited_when_the_schedule_is_one_shot()
    {
        _drawer.OpenNew();

        _drawer.IsUnlimited = true;

        Assert.False(_drawer.CanUnlimited);
        Assert.False(_drawer.IsUnlimited);
    }

    [Fact]
    public void should_lock_the_recurrence_when_editing_an_existing_schedule()
    {
        _drawer.OpenEdit(Existing());

        Assert.False(_drawer.CanEditRecurrence);
        Assert.Contains("반복", _drawer.RecurrenceLockText);

        _drawer.IsWeekly = true;

        // 서버 수정 스키마에 반복 칸이 없다 — 고칠 수 있는 척하면 저장이 422 로 죽는다.
        Assert.False(_drawer.IsWeekly);
    }

    [Fact]
    public void should_toggle_a_day_when_the_recurrence_is_editable()
    {
        _drawer.OpenNew();
        _drawer.IsWeekly = true;
        _drawer.DaysOfWeekMask = 0;

        _drawer.IsWedChecked = true;

        Assert.Equal(SuppressionRules.BitOf(2), _drawer.DaysOfWeekMask);
        Assert.True(_drawer.IsWedChecked);
    }

    #endregion

    #region - 저장 -

    [Fact]
    public async Task should_send_once_when_saving()
    {
        FillValidNew();

        await _drawer.SaveAsync();

        Assert.Single(_saved);
        Assert.False(_drawer.IsOpen);
    }

    [Fact]
    public async Task should_send_once_no_matter_how_many_targets()
    {
        _drawer.OpenNew();
        _drawer.Name = "여러 대상";
        _drawer.AddSelected(Enumerable.Range(1, 40).Select(Device).ToArray());

        await _drawer.SaveAsync();

        Assert.Single(_saved);
        Assert.Equal(40, _saved[0].Targets.Count);
    }

    [Fact]
    public async Task should_not_send_when_the_form_is_invalid()
    {
        _drawer.OpenNew();
        _drawer.Name = "이름만 있고 대상이 없다";

        Assert.False(_drawer.CanSave);

        await _drawer.SaveAsync();

        Assert.Empty(_saved);
        Assert.Contains("대상", _drawer.StatusLine);
    }

    [Fact]
    public async Task should_not_send_when_the_edit_permission_is_missing()
    {
        _canEdit = false;
        FillValidNew();

        await _drawer.SaveAsync();

        Assert.Empty(_saved);
        Assert.Contains("권한", _drawer.StatusLine);
    }

    [Fact]
    public async Task should_stay_open_when_the_server_refuses()
    {
        _saveOk = false;
        FillValidNew();
        var shakeBefore = _drawer.ShakeToken;

        await _drawer.SaveAsync();

        Assert.True(_drawer.IsOpen);
        Assert.Contains("거절", _drawer.StatusLine);
        Assert.True(_drawer.ShakeToken > shakeBefore);
    }

    [Fact]
    public async Task should_report_the_error_when_the_save_path_throws()
    {
        var thrower = new SuppressionDrawerViewModel(_clock, null, null,
            (_, _) => throw new InvalidOperationException("연결이 끊겼습니다"), () => true);
        thrower.OpenNew();
        thrower.Name = "던질 것";
        thrower.AddSelected(new[] { Device(1) });

        await thrower.SaveAsync();

        // 예외도 조용히 사라지면 안 된다.
        Assert.True(thrower.IsOpen);
        Assert.Contains("연결이 끊겼습니다", thrower.StatusLine);
    }

    [Fact]
    public async Task should_raise_saved_with_the_server_answer()
    {
        EventSuppressionScheduleDto? seen = null;
        _drawer.Saved += dto => seen = dto;
        FillValidNew();

        await _drawer.SaveAsync();

        Assert.Equal(1, seen?.Id);
    }

    [Fact]
    public async Task should_not_send_twice_when_nothing_changed_after_an_edit_was_loaded()
    {
        _drawer.OpenEdit(Existing());

        Assert.False(_drawer.CanSave);       // 고친 것이 없으면 보낼 것도 없다

        await _drawer.SaveAsync();

        Assert.Empty(_saved);
    }

    #endregion

    private static EventSuppressionScheduleDto Existing() => new()
    {
        Id = 42,
        Name = "탄약고 야간 점검",
        TargetType = SuppressionTargetDrop.ModeDevice,
        TargetDeviceIds = new List<int> { 3 },
        TargetGroupIds = new List<int>(),
        TargetSide = "both",
        EventScope = "all",
        WindowStart = "2026-09-20T09:00:00.000+09:00",
        WindowEnd = "2026-09-20T18:00:00.000+09:00",
        RecurrenceType = "none",
        Status = "pending",
    };
}
