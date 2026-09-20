using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
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

    /// <summary>문구는 이제 명시적으로 골라야 한다(R14) — 전송을 보는 시험은 먼저 고른다.</summary>
    private static ActionTrayViewModel TrayWithPhrase(ActionReportSender send)
    {
        var tray = new ActionTrayViewModel(send) { Phrase = ActionTrayViewModel.Phrases[0] };
        return tray;
    }

    [Fact]
    public async Task should_send_one_call_per_event_when_applying()
    {
        var sent = new List<int>();
        var tray = TrayWithPhrase((c, _, _) => { sent.Add(c.EventId); return Task.FromResult(DraftOutcome.Applied); });

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
        var tray = TrayWithPhrase((c, _, _) =>
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
        var tray = TrayWithPhrase((c, _, _) =>
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
        var tray = TrayWithPhrase((_, _, _) => { calls++; return Task.FromResult(DraftOutcome.Applied); });

        tray.Enqueue(PlanOf(1, 2));
        tray.Revert();

        Assert.Equal(0, tray.Count);
        await tray.ApplyAsync();
        Assert.Equal(0, calls);
    }

    [Fact]
    public void should_merge_into_one_entry_when_the_same_event_is_queued_twice()
    {
        var tray = TrayWithPhrase((_, _, _) => Task.FromResult(DraftOutcome.Applied));

        tray.Enqueue(PlanOf(1, 2));
        tray.Enqueue(PlanOf(2, 3));

        Assert.Equal(3, tray.Count);
        Assert.True(tray.Contains("detection:2"));
    }

    [Fact]
    public async Task should_report_skipped_when_the_guard_already_holds_the_event()
    {
        var tray = TrayWithPhrase((_, _, _) => Task.FromResult(DraftOutcome.Skipped));

        tray.Enqueue(PlanOf(1));
        var summary = await tray.ApplyAsync();

        Assert.Equal(1, summary.Skipped);
        Assert.Contains("건너뜀", summary.ToMessage());
    }

    [Fact]
    public async Task should_report_missing_when_the_origin_row_is_gone()
    {
        var tray = TrayWithPhrase((_, _, _) => Task.FromResult(DraftOutcome.Missing));

        tray.Enqueue(PlanOf(1));
        var summary = await tray.ApplyAsync();

        Assert.Equal(1, summary.Missing);
        Assert.Equal(0, tray.Count);
    }

    [Fact]
    public async Task should_send_the_memo_when_the_etc_phrase_is_chosen()
    {
        string? sentContent = null;
        var tray = TrayWithPhrase((_, content, _) => { sentContent = content; return Task.FromResult(DraftOutcome.Applied); });

        tray.Phrase = ActionTrayViewModel.EtcPhrase;
        tray.Memo = "현장 확인 결과 이상 없음";
        tray.Enqueue(PlanOf(1));
        await tray.ApplyAsync();

        Assert.Equal("현장 확인 결과 이상 없음", sentContent);
    }

    [Fact]
    public void should_not_allow_apply_when_no_phrase_is_chosen_or_the_etc_memo_is_empty()
    {
        // (R14) 아무도 고르지 않은 문구로 기록이 남지 않게 한다.
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));
        tray.Enqueue(PlanOf(1));
        Assert.False(tray.CanApply);
        Assert.Contains("문구", tray.ApplyBlockedReason);

        tray.Phrase = ActionTrayViewModel.Phrases[0];
        Assert.True(tray.CanApply);

        tray.Phrase = ActionTrayViewModel.EtcPhrase;
        Assert.False(tray.CanApply);
        Assert.Contains("기타", tray.ApplyBlockedReason);
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
        var tray = TrayWithPhrase((_, _, _) => Task.FromResult(DraftOutcome.Applied));

        var line = tray.Enqueue(ActionTrayDrop.Plan(new[] { Row(1) }, canControl: false));

        Assert.Equal(0, tray.Count);
        Assert.Contains("권한", line);
    }

    [Fact]
    public async Task should_leave_unsent_entries_and_mark_the_inflight_one_when_cancelled()
    {
        // 1번은 보내고, 2번을 보내는 중에 취소한다 — 3번은 손도 대지 않는다.
        var started = new List<int>();
        var gate = new TaskCompletionSource();
        ActionTrayViewModel tray = null!;
        tray = TrayWithPhrase(async (c, _, token) =>
        {
            started.Add(c.EventId);
            if (c.EventId == 1) return DraftOutcome.Applied;

            tray.Cancel();                       // 2번을 보내는 도중에 중단
            gate.TrySetResult();
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            return DraftOutcome.Applied;
        });

        tray.Enqueue(PlanOf(1, 2, 3));
        var summary = await tray.ApplyAsync();

        Assert.True(summary.WasCancelled);
        Assert.Equal(1, summary.Applied);
        Assert.Equal(new[] { 1, 2 }, started);           // 3번은 보내지 않았다
        Assert.Equal(2, tray.Count);                     // 2 · 3번은 트레이에 남는다
        Assert.Equal("detection:2", tray.UnverifiedKey); // 보내는 중이던 줄은 "결과 미확인"
    }
}

/// <summary>드래그와 버튼이 <b>같은 담기 함수</b>를 부르는지(PRD FR-45 · V-13) + 상한 계약(R7).</summary>
[Collection("IoC-Dependent")]   // 행 뷰모델이 생성자에서 IoC 를 본다 — 전역 정적이라 직렬화한다
public class ActionTrayDropHandlerTests : IDisposable
{
    public ActionTrayDropHandlerTests()
    {
        var events = new Caliburn.Micro.EventAggregator();
        var log = new Moq.Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>().Object;
        Caliburn.Micro.IoC.GetInstance = (type, _) =>
            type == typeof(Caliburn.Micro.IEventAggregator) ? events
            : type == typeof(Ironwall.Dotnet.Libraries.Base.Services.ILogService) ? log
            : null!;
        Caliburn.Micro.IoC.GetAllInstances = _ => Array.Empty<object>();
        Caliburn.Micro.IoC.BuildUp = _ => { };
    }

    public void Dispose()
    {
        Caliburn.Micro.IoC.GetInstance = null!;
        Caliburn.Micro.IoC.GetAllInstances = null!;
        Caliburn.Micro.IoC.BuildUp = null!;
    }

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
    public void should_refuse_real_rows_when_permission_is_missing()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));
        var handler = new ActionTrayDropHandler(tray, () => false);
        var lines = new List<string>();
        handler.Completed += lines.Add;

        // 진짜 행을 넣는다 — 빈 배열로는 권한 거절인지 빈 입력인지 구분할 수 없다.
        var row = new DetectionEventViewModel(TestEvents.Detection(11));
        handler.Queue(new object[] { row });

        Assert.Equal(0, tray.Count);
        Assert.Single(lines);
        Assert.Contains("권한", lines[0]);
    }

    [Fact]
    public void should_accept_the_same_real_rows_when_permission_is_granted()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));
        var handler = new ActionTrayDropHandler(tray, () => true);

        handler.Queue(new object[] { new DetectionEventViewModel(TestEvents.Detection(11)) });

        Assert.Equal(1, tray.Count);
        Assert.True(tray.Contains("detection:11"));
    }

    [Fact]
    public void should_cap_the_tray_across_several_drops()
    {
        // (R7) 상한은 드롭 한 번이 아니라 트레이 전체에 걸린다.
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));
        var handler = new ActionTrayDropHandler(tray, () => true);

        handler.Queue(Enumerable.Range(1, 30).Select(i => (object)new DetectionEventViewModel(TestEvents.Detection(i))).ToList());
        Assert.Equal(30, tray.Count);

        handler.Queue(Enumerable.Range(31, 30).Select(i => (object)new DetectionEventViewModel(TestEvents.Detection(i))).ToList());
        Assert.Equal(ActionTrayDrop.MaxPerDrop, tray.Count);      // 50 에서 멈춘다(60 이 아니다)

        var line = handler.Queue(new object[] { new DetectionEventViewModel(TestEvents.Detection(999)) });
        Assert.Equal(ActionTrayDrop.MaxPerDrop, tray.Count);      // 51번째는 들어가지 않는다
        Assert.Contains("가득", line);
    }
}
