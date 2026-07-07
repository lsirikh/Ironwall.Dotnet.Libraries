using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Xunit;

namespace Ironwall.Dotnet.Libraries.ViewModel.Tests;

/****************************************************************************
   Purpose   : CRUD 표준 오케스트레이션 봉투(RunCrudOperationAsync/RunDeleteOperationAsync)
               런타임 검증 — PRD DataGridPanel_CRUD_Standard_Convention §5 INV / §9 DoD.
   호출 스레드 : xUnit(헤드리스). EA 발행은 RecordingEventAggregator가 순서대로 캡처.
   범위      : Delete 봉투 파일럿(Camera/Action)이 실제로 의존하는 base 제어흐름만 실행 검증.
****************************************************************************/

// ───────────────────────── Fakes ─────────────────────────

/// <summary>발행 메시지를 발행 순서대로 기록하는 가짜 EA — INV-4/INV-5 순서 검증용.</summary>
internal sealed class RecordingEventAggregator : IEventAggregator
{
    private readonly object _lock = new();
    public List<object> Published { get; } = new();

    public bool HandlerExistsFor(Type messageType) => false;
    public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) { }
    public void Unsubscribe(object subscriber) { }

    public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default)
    {
        lock (_lock) { Published.Add(message); }
        return Task.CompletedTask;
    }
}

internal sealed class NullLogService : ILogService
{
#pragma warning disable CS0067 // 이벤트는 테스트에서 사용하지 않음
    public event EventHandler<LogEventArgs>? LogEvent;
#pragma warning restore CS0067
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
}

/// <summary>ISelectableBaseViewModel 최소 구현 — 삭제 대상 행 스텁.</summary>
internal sealed class FakeRow : ISelectableBaseViewModel
{
    public bool IsSelected { get; set; }
    public int Id { get; init; }
}

/// <summary>protected 봉투 멤버를 테스트에 노출하는 최소 구상 패널.</summary>
internal sealed class TestPanel : BaseDataGridMultiPanelViewModel<FakeRow>
{
    public TestPanel(IEventAggregator ea, ILogService log) : base(ea, log) { }

    public Task RunDelete(
        IReadOnlyList<FakeRow> snapshot,
        Func<int, CancellationToken, Task<bool>> deleteApi,
        Action<FakeRow> removeLocal,
        Func<CancellationToken, Task> fetchAndReinit,
        CancellationToken ct)
        => RunDeleteOperationAsync(snapshot, r => r.Id, deleteApi, removeLocal, fetchAndReinit, ct);

    public Task RunCrud(CrudOperationSpec spec) => RunCrudOperationAsync(spec);

    public void SetTearingDown(bool value) => _isTearingDown = value;

    public bool CheckDeleteBatch(int count) => IsDeleteBatchExceeded(count);

    public override void OnClickInsertButton(object sender, RoutedEventArgs e) { }
    public override void OnClickDeleteButton(object sender, RoutedEventArgs e) { }
    public override void OnClickSaveButton(object sender, RoutedEventArgs e) { }
    public override void OnClickReloadButton(object sender, RoutedEventArgs e) { }
    public override void OnSelectionChanged(IList<FakeRow> selectedItems) { }
}

// ───────────────────────── Tests ─────────────────────────

public class CrudEnvelopeTests
{
    private static TestPanel NewPanel(out RecordingEventAggregator ea)
    {
        ea = new RecordingEventAggregator();
        return new TestPanel(ea, new NullLogService());
    }

    private static Task NoopReinit(CancellationToken _) => Task.CompletedTask;

    // ── Delete: 전건 성공 = Progress→Close, Info 무통지(§11-8) ──
    [Fact]
    public async Task should_publish_progress_then_close_and_no_info_when_all_deletes_succeed()
    {
        var vm = NewPanel(out var ea);
        var removed = new List<int>();
        var rows = new[] { new FakeRow { Id = 1 }, new FakeRow { Id = 2 } };

        await vm.RunDelete(
            rows,
            deleteApi: (id, ct) => Task.FromResult(true),
            removeLocal: r => removed.Add(r.Id),
            fetchAndReinit: NoopReinit,
            ct: CancellationToken.None);

        Assert.Equal(new[] { 1, 2 }, removed);
        Assert.Equal(2, ea.Published.Count);
        Assert.IsType<OpenProgressPopupMessageModel>(ea.Published[0]);   // INV-16 Progress 먼저
        Assert.IsType<ClosePopupMessageModel>(ea.Published[1]);          // INV-4 Close
        Assert.DoesNotContain(ea.Published, m => m is OpenInfoPopupMessageModel); // 전건성공 무통지
    }

    // ── Delete: 부분실패 = Close 이후 단일 Info(INV-5, 중첩 금지) ──
    [Fact]
    public async Task should_publish_close_before_info_when_partial_delete_failure()
    {
        var vm = NewPanel(out var ea);
        var removed = new List<int>();
        var rows = new[] { new FakeRow { Id = 1 }, new FakeRow { Id = 2 }, new FakeRow { Id = 3 } };

        // Id=2만 서버 삭제 실패
        await vm.RunDelete(
            rows,
            deleteApi: (id, ct) => Task.FromResult(id != 2),
            removeLocal: r => removed.Add(r.Id),
            fetchAndReinit: NoopReinit,
            ct: CancellationToken.None);

        Assert.Equal(new[] { 1, 3 }, removed);                  // verify-after-success: 실패건은 로컬 유지
        var closeIdx = ea.Published.FindIndex(m => m is ClosePopupMessageModel);
        var infoIdx = ea.Published.FindIndex(m => m is OpenInfoPopupMessageModel);
        Assert.True(closeIdx >= 0, "ClosePopup 발행돼야 함");
        Assert.True(infoIdx >= 0, "부분실패 Info 발행돼야 함");
        Assert.True(closeIdx < infoIdx, "INV-5: Info는 Progress Close 이후여야 함(중첩 금지)");
        Assert.Single(ea.Published, m => m is OpenInfoPopupMessageModel); // 단일 통지
    }

    // ── Delete: Draft(Id<=0)는 API 미호출 로컬 제거(temp-state 정본) ──
    [Fact]
    public async Task should_not_call_delete_api_for_draft_rows_when_id_not_positive()
    {
        var vm = NewPanel(out _);
        var apiCalledWith = new List<int>();
        var removed = new List<int>();
        var rows = new[] { new FakeRow { Id = -1 }, new FakeRow { Id = 0 }, new FakeRow { Id = 7 } };

        await vm.RunDelete(
            rows,
            deleteApi: (id, ct) => { apiCalledWith.Add(id); return Task.FromResult(true); },
            removeLocal: r => removed.Add(r.Id),
            fetchAndReinit: NoopReinit,
            ct: CancellationToken.None);

        Assert.Equal(new[] { 7 }, apiCalledWith);           // Id>0만 API 호출
        Assert.Equal(new[] { -1, 0, 7 }, removed);          // Draft 포함 전부 로컬 제거
    }

    // ── Timeout(INV-12) → 취소 표기 + Close(INV-4) + 게이트 해제(INV-2) ──
    [Fact]
    public async Task should_mark_canceled_and_close_progress_when_work_times_out()
    {
        var vm = NewPanel(out var ea);
        OperationOutcome? notified = null;

        await vm.RunCrud(new CrudOperationSpec
        {
            OperationName = "타임아웃테스트",
            ShowProgress = true,
            Timeout = TimeSpan.FromMilliseconds(50),
            Work = async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return new OperationOutcome(); },
            Notify = o => { notified = o; return Task.CompletedTask; },
        });

        Assert.NotNull(notified);
        Assert.True(notified!.Canceled, "INV-12: 타임아웃은 Canceled로 표기돼야 함");
        Assert.IsType<OpenProgressPopupMessageModel>(ea.Published[0]);
        Assert.Contains(ea.Published, m => m is ClosePopupMessageModel);   // INV-4: 취소에도 Close 도달

        // INV-2: 게이트가 해제됐어야 다음 오퍼레이션이 진입 가능
        var secondRan = false;
        await vm.RunCrud(new CrudOperationSpec
        {
            OperationName = "후속",
            Work = ct => { secondRan = true; return Task.FromResult(new OperationOutcome()); },
        });
        Assert.True(secondRan, "INV-2: 타임아웃 후 게이트가 해제돼 후속 진입 가능해야 함");
    }

    // ── 재진입 배타(INV-1) — 게이트 점유 중 두 번째 오퍼레이션은 Work 미실행 ──
    [Fact]
    public async Task should_block_reentrant_operation_when_gate_is_held()
    {
        var vm = NewPanel(out _);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var op1 = vm.RunCrud(new CrudOperationSpec
        {
            OperationName = "op1",
            ShowProgress = true,
            Work = async ct => { entered.SetResult(); await release.Task; return new OperationOutcome(); },
        });

        await entered.Task;   // op1이 게이트 안으로 진입 완료

        var op2Ran = false;
        await vm.RunCrud(new CrudOperationSpec
        {
            OperationName = "op2",
            Work = ct => { op2Ran = true; return Task.FromResult(new OperationOutcome()); },
        });

        Assert.False(op2Ran, "INV-1: 게이트 점유 중 재진입 오퍼레이션은 Work를 실행하면 안 됨");

        release.SetResult();
        await op1;
    }

    // ── teardown 가드(INV-15) — _isTearingDown이면 진입 자체 차단 ──
    [Fact]
    public async Task should_not_enter_operation_when_tearing_down()
    {
        var vm = NewPanel(out var ea);
        vm.SetTearingDown(true);

        var workRan = false;
        await vm.RunCrud(new CrudOperationSpec
        {
            OperationName = "teardown중",
            ShowProgress = true,
            Work = ct => { workRan = true; return Task.FromResult(new OperationOutcome()); },
        });

        Assert.False(workRan, "INV-15: teardown 중에는 Work 미실행");
        Assert.Empty(ea.Published);   // Progress조차 열리면 안 됨
    }

    // ── 게이트 해제(INV-2) — 정상 완료 후 다음 오퍼레이션 진입 가능 ──
    [Fact]
    public async Task should_allow_next_operation_after_previous_completes()
    {
        var vm = NewPanel(out _);

        await vm.RunCrud(new CrudOperationSpec
        {
            OperationName = "first",
            Work = ct => Task.FromResult(new OperationOutcome()),
        });

        var secondRan = false;
        await vm.RunCrud(new CrudOperationSpec
        {
            OperationName = "second",
            Work = ct => { secondRan = true; return Task.FromResult(new OperationOutcome()); },
        });

        Assert.True(secondRan, "INV-2: 정상 완료 후 게이트 해제로 후속 진입 가능해야 함");
    }

    // ── 삭제 개수 상한(MAX_DELETE_COUNT) 가드 ──
    [Fact]
    public void should_block_and_notify_when_delete_count_exceeds_limit()
    {
        var vm = NewPanel(out var ea);

        var blocked = vm.CheckDeleteBatch(TestPanel.MAX_DELETE_COUNT + 1);

        Assert.True(blocked, "상한 초과 시 true 반환(호출부 즉시 return)");
        Assert.Single(ea.Published, m => m is OpenInfoPopupMessageModel);   // 안내 팝업 1회
    }

    [Fact]
    public void should_allow_when_delete_count_within_limit()
    {
        var vm = NewPanel(out var ea);

        var blocked = vm.CheckDeleteBatch(TestPanel.MAX_DELETE_COUNT);   // 경계값 = 허용

        Assert.False(blocked);
        Assert.Empty(ea.Published);   // 팝업 없음
    }
}
