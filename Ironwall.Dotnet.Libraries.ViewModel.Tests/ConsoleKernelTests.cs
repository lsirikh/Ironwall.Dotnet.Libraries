using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.ViewModel.Tests;

/// <summary>
/// console-kernel FR-08~13 — 상세 칸 여섯 상태 · 손댄 칸만 적용 · 미적용 이동 차단.
/// (설계 정본 window-layout-system-storyboard.html L920-929 · L2103-2168)
/// </summary>
public class ConsoleDetailStateTests
{
    [Theory]
    [InlineData(0, false, 0, false, ConsoleDetailState.None)]
    [InlineData(1, false, 0, false, ConsoleDetailState.Single)]
    [InlineData(5, false, 0, false, ConsoleDetailState.Multiple)]
    [InlineData(0, true, 0, false, ConsoleDetailState.Create)]
    [InlineData(2, true, 3, false, ConsoleDetailState.Create)]      // 등록이 선택보다 앞선다
    [InlineData(1, false, 2, false, ConsoleDetailState.Dirty)]
    [InlineData(4, false, 1, false, ConsoleDetailState.Dirty)]
    [InlineData(1, false, 0, true, ConsoleDetailState.ReadOnly)]
    [InlineData(1, false, 2, true, ConsoleDetailState.ReadOnly)]    // 읽기 전용이 미적용보다 앞선다
    [InlineData(0, false, 0, true, ConsoleDetailState.None)]        // 고른 게 없으면 읽기 전용이어도 안내
    public void should_resolve_detail_state_by_priority(int selected, bool creating, int dirty, bool readOnly, ConsoleDetailState expected)
    {
        Assert.Equal(expected, ConsoleDetailStateMachine.Resolve(selected, creating, dirty, readOnly));
    }

    [Theory]
    [InlineData(ConsoleNavigation.SelectRow, true)]
    [InlineData(ConsoleNavigation.SwitchRail, true)]
    [InlineData(ConsoleNavigation.BeginCreate, true)]
    [InlineData(ConsoleNavigation.Refresh, true)]
    [InlineData(ConsoleNavigation.Search, false)]        // 검색은 선택을 바꾸지 않는다
    [InlineData(ConsoleNavigation.ChangeView, false)]
    public void should_block_only_selection_changing_navigation_when_dirty(ConsoleNavigation navigation, bool expected)
    {
        Assert.Equal(expected, ConsoleDetailStateMachine.IsBlocked(navigation, dirtyCount: 1));
        Assert.False(ConsoleDetailStateMachine.IsBlocked(navigation, dirtyCount: 0));
    }

    [Fact]
    public void should_raise_blocked_and_refuse_when_guard_sees_dirty_fields()
    {
        var dirty = 2;
        var guard = new NavigationGuard(() => dirty);
        var raised = new List<ConsoleNavigation>();
        guard.Blocked += (_, n) => raised.Add(n);

        Assert.False(guard.TryNavigate(ConsoleNavigation.SelectRow));
        Assert.True(guard.TryNavigate(ConsoleNavigation.Search));
        dirty = 0;
        Assert.True(guard.TryNavigate(ConsoleNavigation.SelectRow));

        Assert.Equal(new[] { ConsoleNavigation.SelectRow }, raised);
    }

    [Theory]
    [InlineData(ConsoleDetailState.None, 0, null, "선택 대기")]
    [InlineData(ConsoleDetailState.Single, 0, null, "변경 없음")]
    [InlineData(ConsoleDetailState.Single, 0, "되돌렸습니다", "되돌렸습니다")]
    [InlineData(ConsoleDetailState.Dirty, 3, "되돌렸습니다", "변경 3건 미적용")]
    [InlineData(ConsoleDetailState.ReadOnly, 0, null, "읽기 전용")]
    [InlineData(ConsoleDetailState.Create, 0, null, "아직 등록 전입니다")]
    public void should_word_footer_by_state(ConsoleDetailState state, int dirty, string? last, string expected)
    {
        Assert.Equal(expected, ConsoleDetailStateMachine.FooterText(state, dirty, last));
    }

    [Theory]
    [InlineData(ConsoleDetailState.Dirty, 1, true)]
    [InlineData(ConsoleDetailState.Single, 0, false)]
    [InlineData(ConsoleDetailState.ReadOnly, 2, false)]
    [InlineData(ConsoleDetailState.Create, 0, false)]    // 아무것도 안 채웠으면 [등록] 은 꺼져 있다
    [InlineData(ConsoleDetailState.Create, 2, true)]
    public void should_enable_apply_only_when_there_is_something_to_send(ConsoleDetailState state, int dirty, bool expected)
    {
        Assert.Equal(expected, ConsoleDetailStateMachine.CanApply(state, dirty));
    }

    [Theory]
    [InlineData(1, 2, "2건 적용했습니다")]
    [InlineData(4, 1, "4개에 1건 적용했습니다")]
    public void should_word_applied_message(int selected, int fields, string expected)
    {
        Assert.Equal(expected, ConsoleDetailStateMachine.AppliedMessage(selected, fields));
    }

    [Fact]
    public void should_show_mixed_values_when_selection_disagrees()
    {
        Assert.Equal("— 여러 값 —", MixedValue<string>.Of(new[] { "A", "B", "A" }).Display());
        Assert.Equal("A", MixedValue<string>.Of(new[] { "A", "A" }).Display());
        Assert.Equal(string.Empty, MixedValue<string>.Of(Array.Empty<string>()).Display());
        Assert.True(MixedValue<int?>.Of(new int?[] { 1, null }).IsMixed);
        Assert.False(MixedValue<int?>.Of(new int?[] { null, null }).IsMixed);
    }

    [Fact]
    public void should_count_only_fields_that_differ_from_original()
    {
        var tracker = new DirtyFieldTracker();

        tracker.Touch("name", "정문", "정문 1");
        tracker.Touch("ip", "10.0.0.1", "10.0.0.2");
        Assert.Equal(2, tracker.Count);

        tracker.Touch("name", "정문", "정문");          // 원래 값으로 되돌려 놓으면 손대지 않은 것
        Assert.Equal(1, tracker.Count);
        Assert.False(tracker.IsTouched("name"));
    }

    [Fact]
    public void should_treat_any_input_as_change_when_there_is_no_single_original()
    {
        var tracker = new DirtyFieldTracker();

        tracker.Touch("unit", original: null, current: null, hasOriginal: false);   // 여러 값 칸을 건드렸다

        Assert.True(tracker.IsTouched("unit"));
    }

    [Fact]
    public void should_never_bulk_overwrite_identity_fields()
    {
        var tracker = new DirtyFieldTracker();
        tracker.MarkIdentity("number_device");
        tracker.Touch("number_device", "C-001", "C-999");
        tracker.Touch("name", "a", "b");

        Assert.Equal(new[] { "number_device", "name" }, tracker.ChangesFor(1).Select(c => c.Key));
        Assert.Equal(new[] { "name" }, tracker.ChangesFor(3).Select(c => c.Key));
    }

    [Fact]
    public void should_notify_when_dirty_set_changes()
    {
        var tracker = new DirtyFieldTracker();
        var count = 0;
        tracker.Changed += (_, _) => count++;

        tracker.Touch("a", 1, 2);
        tracker.Touch("a", 1, 3);
        tracker.Clear();
        tracker.Clear();      // 비어 있으면 울리지 않는다

        Assert.Equal(3, count);
    }
}

/// <summary>
/// console-kernel FR-23 — Draft 트레이. N회 호출로 번지는 드래그는 여기에 쌓았다가 [적용] 때 모아 보낸다.
/// </summary>
public class DraftTrayTests
{
    private static DraftEntry Entry(string target, string call, DraftOutcome outcome, List<string>? log = null)
        => new(target, call, $"{target} {call}", _ =>
        {
            log?.Add($"{target}|{call}");
            return Task.FromResult(outcome);
        });

    [Fact]
    public void should_replace_entry_when_same_target_and_call_is_dropped_again()
    {
        var tray = new DraftTrayViewModel();

        tray.Add(new DraftEntry("camera:1", "PATCH unit_id", "→ 1소대", _ => Task.FromResult(DraftOutcome.Applied)));
        tray.Add(Entry("camera:2", "PATCH unit_id", DraftOutcome.Applied));
        tray.Add(new DraftEntry("camera:1", "PATCH unit_id", "→ 2소대", _ => Task.FromResult(DraftOutcome.Applied)));
        tray.Add(Entry("camera:1", "PATCH group_ids", DraftOutcome.Applied));     // 호출이 다르면 따로

        Assert.Equal(3, tray.Count);
        Assert.Equal("→ 2소대", tray.Entries[0].Description);                       // 자리는 그대로, 내용은 나중 것
    }

    [Fact]
    public void should_send_nothing_when_reverted()
    {
        var log = new List<string>();
        var tray = new DraftTrayViewModel();
        tray.Add(Entry("a", "x", DraftOutcome.Applied, log));

        tray.Revert();

        Assert.False(tray.HasEntries);
        Assert.Empty(log);
        Assert.Contains("서버 호출 0", tray.Message);
    }

    [Fact]
    public async Task should_apply_in_order_and_summarise_four_outcomes()
    {
        var log = new List<string>();
        var tray = new DraftTrayViewModel();
        tray.Add(Entry("a", "x", DraftOutcome.Applied, log));
        tray.Add(Entry("b", "x", DraftOutcome.Failed, log));
        tray.Add(Entry("c", "x", DraftOutcome.Skipped, log));
        tray.Add(Entry("d", "x", DraftOutcome.Missing, log));
        tray.Add(Entry("e", "x", DraftOutcome.Applied, log));

        var summary = await tray.ApplyAsync();

        Assert.Equal(new[] { "a|x", "b|x", "c|x", "d|x", "e|x" }, log);
        Assert.Equal(new DraftApplySummary(2, 1, 1, 1, false), summary);
        Assert.Equal("적용 완료 — 적용 2 · 실패 1 · 건너뜀 1 · 없음 1", tray.Message);
        Assert.Equal(5, tray.ProgressDone);
    }

    [Fact]
    public async Task should_keep_only_failed_entries_for_retry()
    {
        var attempts = 0;
        var tray = new DraftTrayViewModel();
        tray.Add(Entry("ok", "x", DraftOutcome.Applied));
        tray.Add(new DraftEntry("flaky", "x", "flaky", _ =>
            Task.FromResult(++attempts == 1 ? DraftOutcome.Failed : DraftOutcome.Applied)));

        await tray.ApplyAsync();
        Assert.Equal("flaky", Assert.Single(tray.Entries).TargetKey);
        Assert.Equal("실패", tray.Entries[0].FailureReason);

        var retry = await tray.ApplyAsync();
        Assert.Equal(1, retry.Applied);
        Assert.False(tray.HasEntries);
    }

    [Fact]
    public async Task should_record_exception_message_as_failure_reason()
    {
        var tray = new DraftTrayViewModel();
        tray.Add(new DraftEntry("a", "x", "a", _ => throw new InvalidOperationException("422 VALUE_NOT_ALLOWED")));

        var summary = await tray.ApplyAsync();

        Assert.Equal(1, summary.Failed);
        Assert.Equal("422 VALUE_NOT_ALLOWED", tray.Entries[0].FailureReason);
        Assert.False(tray.IsApplying);
    }

    [Fact]
    public async Task should_leave_unsent_entries_in_tray_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var tray = new DraftTrayViewModel();
        tray.Add(new DraftEntry("a", "x", "a", _ => { cts.Cancel(); return Task.FromResult(DraftOutcome.Applied); }));
        tray.Add(Entry("b", "x", DraftOutcome.Applied));
        tray.Add(Entry("c", "x", DraftOutcome.Applied));

        var summary = await tray.ApplyAsync(cts.Token);

        Assert.True(summary.WasCancelled);
        Assert.Equal(1, summary.Applied);
        Assert.Equal(new[] { "b", "c" }, tray.Entries.Select(e => e.TargetKey));
        Assert.StartsWith("중단", tray.Message);
    }

    [Fact]
    public async Task should_refuse_new_drafts_while_applying()
    {
        var gate = new TaskCompletionSource<DraftOutcome>();
        var tray = new DraftTrayViewModel();
        tray.Add(new DraftEntry("a", "x", "a", _ => gate.Task));

        var running = tray.ApplyAsync();

        Assert.True(tray.IsApplying);
        Assert.False(tray.CanApply);
        Assert.False(tray.CanRevert);
        Assert.Throws<InvalidOperationException>(() => tray.Add(Entry("b", "x", DraftOutcome.Applied)));

        gate.SetResult(DraftOutcome.Applied);
        await running;
        Assert.False(tray.IsApplying);
    }

    [Fact]
    public async Task should_do_nothing_when_tray_is_empty()
    {
        var summary = await new DraftTrayViewModel().ApplyAsync();

        Assert.Equal(0, summary.Total);
    }
}
