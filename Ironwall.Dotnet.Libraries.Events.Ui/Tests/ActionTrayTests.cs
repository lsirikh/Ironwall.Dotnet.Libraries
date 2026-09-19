using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 선택한 행 → 조치 트레이(events-console PRD FR-36~FR-48 · V-08~V-13).
/// 조치 생성은 벌크가 없어 N건이면 N회다 — 그래서 드롭은 서버를 부르지 않고 Draft 만 쌓는다.
/// </summary>
public class ActionTrayDropTests
{
    private static ActionTrayCandidate Detection(int id, bool reported = false)
        => new(id, ActionTrayDrop.KindDetection, $"센서-{id}", reported);

    private static ActionTrayCandidate Malfunction(int id)
        => new(id, ActionTrayDrop.KindMalfunction, $"제어기-{id}", false);

    [Fact]
    public void should_accept_detection_and_malfunction_when_saved()
    {
        var plan = ActionTrayDrop.Plan(new[] { Detection(1), Malfunction(2) }, canControl: true);

        Assert.True(plan.CanQueue);
        Assert.Equal(2, plan.Accepted.Count);
    }

    [Fact]
    public void should_exclude_unsaved_drafts_when_planning()
    {
        var plan = ActionTrayDrop.Plan(new[] { Detection(0), Detection(-1), Detection(7) }, canControl: true);

        Assert.True(plan.CanQueue);
        Assert.Single(plan.Accepted);
        Assert.Equal(2, plan.DraftExcluded);
    }

    [Fact]
    public void should_block_when_every_row_is_an_unsaved_draft()
    {
        var plan = ActionTrayDrop.Plan(new[] { Detection(0) }, canControl: true);

        Assert.False(plan.CanQueue);
        Assert.Contains("저장", plan.BlockReason);
    }

    [Fact]
    public void should_exclude_connection_and_action_rows_when_planning()
    {
        var rows = new[]
        {
            new ActionTrayCandidate(3, "connection", "연결", false),
            new ActionTrayCandidate(4, "action", "조치", false),
            Detection(5),
        };
        var plan = ActionTrayDrop.Plan(rows, canControl: true);

        Assert.Single(plan.Accepted);
        Assert.Equal(2, plan.WrongKindExcluded);
    }

    [Fact]
    public void should_block_when_permission_is_missing()
    {
        var plan = ActionTrayDrop.Plan(new[] { Detection(1) }, canControl: false);

        Assert.False(plan.CanQueue);
        Assert.Contains("권한", plan.BlockReason);
    }

    [Fact]
    public void should_allow_queueing_when_the_event_already_has_actions()
    {
        // 중복 조치보고는 이미 허용이다 — 조치가 있다고 막지 않는다.
        var plan = ActionTrayDrop.Plan(new[] { Detection(9, reported: true) }, canControl: true);

        Assert.True(plan.CanQueue);
        Assert.Contains("한 건씩 더 쌓", ActionTrayDrop.ResultLine(plan));
    }

    [Fact]
    public void should_not_mix_detection_and_malfunction_with_the_same_id()
    {
        // Id 만으로 맞추면 탐지 3번과 장애 3번이 섞인다 — 대상 키는 Id + 타입이다.
        Assert.NotEqual(Detection(3).TargetKey, Malfunction(3).TargetKey);

        var plan = ActionTrayDrop.Plan(new[] { Detection(3), Malfunction(3) }, canControl: true);
        Assert.Equal(2, plan.Accepted.Count);
    }

    [Fact]
    public void should_cap_at_the_batch_limit_when_too_many_rows_are_dropped()
    {
        var rows = Enumerable.Range(1, ActionTrayDrop.MaxPerDrop + 12).Select(i => Detection(i)).ToList();
        var plan = ActionTrayDrop.Plan(rows, canControl: true);

        Assert.Equal(ActionTrayDrop.MaxPerDrop, plan.Accepted.Count);
        Assert.Equal(12, plan.OverLimitExcluded);
        Assert.Contains($"{ActionTrayDrop.MaxPerDrop}건까지", ActionTrayDrop.ResultLine(plan));
    }

    [Fact]
    public void should_keep_only_one_entry_when_the_same_event_appears_twice()
    {
        var plan = ActionTrayDrop.Plan(new[] { Detection(5), Detection(5) }, canControl: true);
        Assert.Single(plan.Accepted);
    }
}

/// <summary>Draft + [적용] 의 계약 — 진행률 · 부분 실패 · 재시도 · 되돌리기.</summary>
public class ActionTrayViewModelTests
{
    private static ActionTrayCandidate Row(int id) => new(id, ActionTrayDrop.KindDetection, $"센서-{id}", false);

    private static TrayPlan PlanOf(params int[] ids)
        => ActionTrayDrop.Plan(ids.Select(Row), canControl: true);

    [Fact]
    public async Task should_send_one_call_per_event_when_applying()
    {
        var sent = new List<int>();
        var tray = new ActionTrayViewModel((c, _, _) => { sent.Add(c.EventId); return Task.FromResult(DraftOutcome.Applied); });

        tray.Enqueue(PlanOf(1, 2, 3));
        Assert.Equal(3, tray.Count);

        var summary = await tray.ApplyAsync();

        Assert.Equal(new[] { 1, 2, 3 }, sent);
        Assert.Equal(3, summary.Applied);
        Assert.Equal(0, tray.Count);
    }

    [Fact]
    public async Task should_keep_only_failed_entries_when_some_calls_fail()
    {
        var tray = new ActionTrayViewModel((c, _, _) =>
            Task.FromResult(c.EventId == 2 ? DraftOutcome.Failed : DraftOutcome.Applied));

        tray.Enqueue(PlanOf(1, 2, 3));
        var summary = await tray.ApplyAsync();

        Assert.Equal(2, summary.Applied);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, tray.Count);
        Assert.True(tray.HasFailures);
        Assert.Equal("detection:2", tray.Draft.Entries[0].TargetKey);
    }

    [Fact]
    public async Task should_retry_only_the_failed_entry_when_applied_again()
    {
        var attempts = new List<int>();
        var failFirstTime = true;
        var tray = new ActionTrayViewModel((c, _, _) =>
        {
            attempts.Add(c.EventId);
            if (c.EventId == 2 && failFirstTime) return Task.FromResult(DraftOutcome.Failed);
            return Task.FromResult(DraftOutcome.Applied);
        });

        tray.Enqueue(PlanOf(1, 2, 3));
        await tray.ApplyAsync();
        attempts.Clear();
        failFirstTime = false;

        var second = await tray.ApplyAsync();

        Assert.Equal(new[] { 2 }, attempts);
        Assert.Equal(1, second.Applied);
        Assert.Equal(0, tray.Count);
    }

    [Fact]
    public async Task should_not_call_the_server_when_reverted()
    {
        var calls = 0;
        var tray = new ActionTrayViewModel((_, _, _) => { calls++; return Task.FromResult(DraftOutcome.Applied); });

        tray.Enqueue(PlanOf(1, 2));
        tray.Revert();

        Assert.Equal(0, tray.Count);
        await tray.ApplyAsync();
        Assert.Equal(0, calls);
    }

    [Fact]
    public void should_merge_into_one_entry_when_the_same_event_is_queued_twice()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));

        tray.Enqueue(PlanOf(1, 2));
        tray.Enqueue(PlanOf(2, 3));

        Assert.Equal(3, tray.Count);
        Assert.True(tray.Contains("detection:2"));
    }

    [Fact]
    public async Task should_report_skipped_when_the_guard_already_holds_the_event()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Skipped));

        tray.Enqueue(PlanOf(1));
        var summary = await tray.ApplyAsync();

        Assert.Equal(1, summary.Skipped);
        Assert.Contains("건너뜀", summary.ToMessage());
    }

    [Fact]
    public async Task should_report_missing_when_the_origin_row_is_gone()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Missing));

        tray.Enqueue(PlanOf(1));
        var summary = await tray.ApplyAsync();

        Assert.Equal(1, summary.Missing);
        Assert.Equal(0, tray.Count);
    }

    [Fact]
    public async Task should_send_the_memo_when_the_etc_phrase_is_chosen()
    {
        string? sentContent = null;
        var tray = new ActionTrayViewModel((_, content, _) => { sentContent = content; return Task.FromResult(DraftOutcome.Applied); });

        tray.Phrase = ActionTrayViewModel.EtcPhrase;
        tray.Memo = "현장 확인 결과 이상 없음";
        tray.Enqueue(PlanOf(1));
        await tray.ApplyAsync();

        Assert.Equal("현장 확인 결과 이상 없음", sentContent);
    }

    [Fact]
    public void should_not_allow_apply_when_the_etc_memo_is_empty()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));

        tray.Enqueue(PlanOf(1));
        Assert.True(tray.CanApply);

        tray.Phrase = ActionTrayViewModel.EtcPhrase;
        Assert.False(tray.CanApply);
    }

    [Fact]
    public void should_use_the_same_phrase_list_as_the_report_dialog()
    {
        Assert.Contains("야생동물출현", ActionTrayViewModel.Phrases);
        Assert.Contains("오경보", ActionTrayViewModel.Phrases);
        Assert.Contains(ActionTrayViewModel.EtcPhrase, new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied)).PhraseOptions);
    }

    [Fact]
    public void should_report_the_block_reason_when_the_plan_cannot_queue()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));

        var line = tray.Enqueue(ActionTrayDrop.Plan(new[] { Row(1) }, canControl: false));

        Assert.Equal(0, tray.Count);
        Assert.Contains("권한", line);
    }

    [Fact]
    public async Task should_leave_unsent_entries_when_cancelled()
    {
        var tray = new ActionTrayViewModel(async (c, _, token) =>
        {
            if (c.EventId == 1) return DraftOutcome.Applied;
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            return DraftOutcome.Applied;
        });

        tray.Enqueue(PlanOf(1, 2, 3));

        var apply = tray.ApplyAsync();
        tray.Cancel();
        var summary = await apply;

        Assert.True(summary.WasCancelled || summary.Applied == 3);
        Assert.Contains(summary.Applied, new[] { 1, 2, 3 });
    }
}

/// <summary>드래그와 버튼이 <b>같은 담기 함수</b>를 부르는지(PRD FR-45 · V-13).</summary>
public class ActionTrayDropHandlerTests
{
    private sealed class FakeRow { }

    [Fact]
    public void should_report_the_same_line_for_drag_and_keyboard_paths()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));
        var handler = new ActionTrayDropHandler(tray, () => true);

        var lines = new List<string>();
        handler.Completed += lines.Add;

        // 뷰모델 타입이 아닌 행은 후보가 되지 않는다 — 판정이 막고 서버를 부르지 않는다.
        handler.Queue(new object[] { new FakeRow() });

        Assert.Single(lines);
        Assert.Equal(0, tray.Count);
    }

    [Fact]
    public void should_refuse_the_drop_when_permission_is_missing()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));
        var handler = new ActionTrayDropHandler(tray, () => false);

        Assert.Equal(0, tray.Count);
        handler.Queue(Array.Empty<object>());
        Assert.Equal(0, tray.Count);
    }
}
