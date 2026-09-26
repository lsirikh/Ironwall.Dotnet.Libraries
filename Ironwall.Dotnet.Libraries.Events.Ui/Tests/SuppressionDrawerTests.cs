using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
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
    public void should_refuse_to_leave_when_there_are_unapplied_edits()
    {
        _drawer.OpenNew();
        _drawer.Name = "쓰다 만 것";

        Assert.False(_drawer.CanLeave);
        Assert.False(_drawer.TryLeave());
    }

    [Fact]
    public void should_allow_leaving_a_closed_drawer()
    {
        // 열리지도 않은 서랍은 아무것도 막지 않는다 — 레일을 옮기는 것이 막히면 안 된다.
        Assert.True(_drawer.CanLeave);
        Assert.True(_drawer.TryLeave());
    }

    [Fact]
    public void should_not_reopen_over_a_dirty_draft()
    {
        _drawer.OpenNew();
        _drawer.Name = "지키고 싶은 초안";

        Assert.False(_drawer.OpenNew());
        Assert.Equal("지키고 싶은 초안", _drawer.Name);

        Assert.False(_drawer.OpenEdit(Existing()));
        Assert.Equal("지키고 싶은 초안", _drawer.Name);
    }

    [Fact]
    public void should_reopen_once_the_draft_is_reverted()
    {
        _drawer.OpenNew();
        _drawer.Name = "쓰다 만 것";
        _drawer.Revert();

        Assert.True(_drawer.OpenEdit(Existing()));
        Assert.Equal("탄약고 야간 점검", _drawer.Name);
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
        Assert.True((_drawer.Draft.WindowEnd!.Value - _drawer.Draft.WindowStart).TotalDays >= 7);
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

        // 예외도 조용히 사라지면 안 된다 — 다만 문장은 고정이다(예외 본문은 로그로만 간다).
        Assert.True(thrower.IsOpen);
        Assert.Contains("저장하지 못했습니다", thrower.StatusLine);
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

    [Fact]
    public async Task should_say_it_is_busy_when_save_is_pressed_twice()
    {
        // CanSave 가 !_isSaving 을 품고 있어, 순서를 틀리면 "바뀐 것이 없습니다" 라는 거짓말이 뜬다.
        var gate = new TaskCompletionSource<SuppressionSaveOutcome>();
        var slow = new SuppressionDrawerViewModel(_clock, null, null, (_, _) => gate.Task, () => true);
        slow.OpenNew();
        slow.Name = "느린 저장";
        slow.AddSelected(new[] { Device(1) });

        var first = slow.SaveAsync();
        Assert.True(slow.IsSaving);

        await slow.SaveAsync();                       // 두 번째 누름
        Assert.Contains("저장하는 중", slow.StatusLine);
        Assert.DoesNotContain("바뀐 것이 없습니다", slow.StatusLine);

        gate.SetResult(new SuppressionSaveOutcome(true, "저장했습니다.", null));
        await first;
    }

    [Fact]
    public void should_keep_an_unknown_scope_instead_of_widening_it()
    {
        // 서버가 새 event_scope 를 추가하면 콤보가 매칭에 실패해 null 을 되밀고,
        // 그것을 "all" 로 바꾸면 억제 범위가 조용히 넓어진다 — 안전 방향의 반대다.
        var dto = Existing();
        dto.EventScope = "something_new";
        _drawer.OpenEdit(dto);

        _drawer.EventScope = null!;                   // WPF Selector 가 하는 짓

        Assert.Equal("something_new", _drawer.EventScope);
        Assert.True(_drawer.IsScopeUnknown);
        Assert.False(_drawer.CanSave);
    }

    [Fact]
    public async Task should_refuse_to_save_an_unknown_scope()
    {
        var dto = Existing();
        dto.EventScope = "something_new";
        _drawer.OpenEdit(dto);
        _drawer.Name = "이름만 고친다";

        await _drawer.SaveAsync();

        Assert.Empty(_saved);
        Assert.Contains("지원하지 않는 억제 범위", _drawer.StatusLine);
        Assert.DoesNotContain("something_new", _drawer.StatusLine);      // 서버 원문은 화면에 싣지 않는다
    }

    [Fact]
    public void should_accept_the_operation_scope()
    {
        _drawer.OpenNew();

        _drawer.EventScope = "operation";

        Assert.False(_drawer.IsScopeUnknown);
    }

    [Fact]
    public async Task should_not_leak_the_server_address_when_the_save_path_throws()
    {
        // HttpRequestException 은 호스트 · 포트를 문장에 담는다 — 운영자 화면에 가면 안 된다.
        Exception? logged = null;
        var thrower = new SuppressionDrawerViewModel(_clock, null, null,
            (_, _) => throw new HttpRequestException("No connection could be made to 10.20.30.40:8000"),
            () => true, null, (_, ex) => logged = ex);
        thrower.OpenNew();
        thrower.Name = "던질 것";
        thrower.AddSelected(new[] { Device(1) });

        await thrower.SaveAsync();

        Assert.DoesNotContain("10.20.30.40", thrower.StatusLine);
        Assert.DoesNotContain("8000", thrower.StatusLine);
        Assert.NotNull(logged);                       // 사라지지는 않는다 — 로그에는 남는다
    }

    #endregion

    #region - 시각 입력 (V2: 표기 하나로 · 읽을 수 없으면 버리지 않는다) -

    [Theory]
    [InlineData("2026-09-20 09:00", 2026, 9, 20, 9, 0)]
    [InlineData("2026-9-2 9:05", 2026, 9, 2, 9, 5)]
    [InlineData("2026/09/20 09:00", 2026, 9, 20, 9, 0)]
    [InlineData("  2026-09-20 09:00  ", 2026, 9, 20, 9, 0)]
    public void should_read_a_date_time_in_the_one_format(string text, int y, int mo, int d, int h, int mi)
    {
        Assert.True(SuppressionTimeText.TryParseDateTime(text, out var value));
        Assert.Equal(new DateTime(y, mo, d, h, mi, 0), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("내일 아침")]
    public void should_refuse_a_date_time_it_cannot_read(string? text)
        => Assert.False(SuppressionTimeText.TryParseDateTime(text, out _));

    [Theory]
    [InlineData("08:00", 8, 0)]
    [InlineData("8:5", 8, 5)]
    [InlineData("0830", 8, 30)]
    [InlineData("23:59", 23, 59)]
    public void should_read_a_wall_clock_time(string text, int h, int m)
    {
        Assert.True(SuppressionTimeText.TryParseTime(text, out var value));
        Assert.Equal(new TimeSpan(h, m, 0), value);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("2560")]
    [InlineData("아침")]
    public void should_refuse_a_time_it_cannot_read(string text)
        => Assert.False(SuppressionTimeText.TryParseTime(text, out _));

    [Fact]
    public void should_round_trip_the_one_format()
    {
        var at = new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.FromHours(9));

        Assert.Equal("2026-09-20 09:00", SuppressionTimeText.Format(at));
        Assert.True(SuppressionTimeText.TryParseDateTime(SuppressionTimeText.Format(at), out var back));
        Assert.Equal(at.DateTime, back);
    }

    [Fact]
    public void should_keep_unreadable_text_instead_of_discarding_it()
    {
        _drawer.OpenNew();

        _drawer.WindowEndText = "내일 아침";

        // 지우면 고쳐 쓸 수 없고, 그대로 보내면 엉뚱한 시각이 나간다 — 두고 막는다.
        Assert.Equal("내일 아침", _drawer.WindowEndText);
        Assert.True(_drawer.HasUnreadableTime);
        Assert.False(_drawer.CanSave);
    }

    [Fact]
    public async Task should_not_save_while_a_time_is_unreadable()
    {
        FillValidNew();
        _drawer.WindowEndText = "???";

        await _drawer.SaveAsync();

        Assert.Empty(_saved);
        Assert.Contains("시각을 읽을 수 없습니다", _drawer.StatusLine);
    }

    [Fact]
    public void should_recover_once_the_time_becomes_readable()
    {
        _drawer.OpenNew();
        _drawer.WindowEndText = "???";
        Assert.True(_drawer.HasUnreadableTime);

        _drawer.WindowEndText = "2026-09-21 18:00";

        Assert.False(_drawer.HasUnreadableTime);
        Assert.Equal(new DateTime(2026, 9, 21, 18, 0, 0), _drawer.Draft.WindowEnd!.Value.DateTime);
    }

    [Fact]
    public void should_drop_unreadable_text_when_another_draft_is_loaded()
    {
        _drawer.OpenNew();
        _drawer.WindowEndText = "???";
        _drawer.Revert();

        Assert.False(_drawer.HasUnreadableTime);
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
