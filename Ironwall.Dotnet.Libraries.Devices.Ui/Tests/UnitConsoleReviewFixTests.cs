using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 부대 관계도 적대 검토 REVIEW-01 — 부대 콘솔(진짜 뷰모델) ↔ 관계도 결선 몫.
/// HIGH-1(최상위 되돌리기 · 트리 [이동 되돌리기]와 분리) · MEDIUM-4(바쁜 콘솔) · MEDIUM-5(보여 주기) · MEDIUM-6(실시간 꼬리표 · 나) ·
/// MEDIUM-8(배치 GET 을 기다리지 않고 창을 연다) · L-7(쓰기 중 닫기).
/// </summary>
[Collection("CaliburnIoC")]
public class UnitConsoleReviewFixTests
{
    private static async Task ConfirmMapMoveAsync(ConsoleKit kit, int moving, int target)
    {
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(moving, 0, 0, target, false));
        Assert.True(kit.Console.Map.IsConfirming);
        await kit.Console.Map.ConfirmAsync().WaitAsync(ConsoleKit.Timeout);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
    }

    private static async Task UndoMapAsync(ConsoleKit kit)
    {
        await kit.Console.Map.UndoAsync().WaitAsync(ConsoleKit.Timeout);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
    }

    /// <summary>배치 통로를 마음대로 끼울 수 있는 콘솔(열기 전).</summary>
    private static UnitConsoleViewModel NewConsole(IUnitLayoutApi layout, ManualDelay delay, FakeConsoleUnitApi? units = null)
        => new(units ?? new FakeConsoleUnitApi(), new FakeConsoleDeviceApi(), log: null, myUnitCode: () => "c06",
               canEdit: () => true, canDelete: () => true, canView: () => true,
               events: new CapturingEventAggregator(new List<object>()), isDragging: () => false, delay: delay.Run,
               layoutApi: layout, clock: new MutableClock());

    #region - HIGH-1 — 관계도 되돌리기는 정확한 반대 이동, 트리 [이동 되돌리기]는 트리 이동만 -
    [Fact]
    public async Task should_return_root_unit_to_root_when_map_undoes_it_after_another_map_move()
    {
        var kit = ConsoleKit.Create();
        kit.Units.AddNode(9, "c09", "Company", null);          // 최상위 중대
        await kit.ActivateAsync();
        await ConfirmMapMoveAsync(kit, 9, 3);
        await ConfirmMapMoveAsync(kit, 6, 7);

        await UndoMapAsync(kit);
        await UndoMapAsync(kit);

        Assert.Null(kit.Console.Tree.Find(9)!.ParentId);
        Assert.Equal(3, kit.Console.Tree.Find(6)!.ParentId);
        Assert.False(kit.Console.Map.CanUndo);
    }

    [Fact]
    public async Task should_return_both_root_units_to_root_when_map_undoes_two_root_moves()
    {
        var kit = ConsoleKit.Create();
        kit.Units.AddNode(9, "c09", "Company", null);
        kit.Units.AddNode(10, "c10", "Company", null);
        await kit.ActivateAsync();
        await ConfirmMapMoveAsync(kit, 9, 3);
        await ConfirmMapMoveAsync(kit, 10, 7);

        await UndoMapAsync(kit);
        await UndoMapAsync(kit);

        Assert.Null(kit.Console.Tree.Find(10)!.ParentId);
        Assert.Null(kit.Console.Tree.Find(9)!.ParentId);
    }

    [Fact]
    public async Task should_not_arm_tree_undo_when_move_comes_from_map()
    {
        var kit = await ConsoleKit.OpenAsync();

        await ConfirmMapMoveAsync(kit, 6, 7);

        Assert.Equal(7, kit.Console.Tree.Find(6)!.ParentId);
        Assert.False(kit.Console.CanUndoMove);                     // [이동 되돌리기]는 트리에서 한 이동만
    }

    [Fact]
    public async Task should_disarm_tree_undo_when_map_moves_the_same_unit_again()
    {
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.MoveAsync(6, 7);                         // 트리 이동 — 되돌리기 준비
        Assert.True(kit.Console.CanUndoMove);

        await ConfirmMapMoveAsync(kit, 6, 3);                      // 관계도가 같은 부대를 다시 옮겼다

        Assert.False(kit.Console.CanUndoMove);                     // 트리 되돌리기가 관계도의 이동을 말없이 지우지 않게
        await kit.Console.UndoMoveAsync();
        Assert.Equal(3, kit.Console.Tree.Find(6)!.ParentId);
    }
    #endregion

    #region - MEDIUM-4 — 바쁜 콘솔 -
    [Fact]
    public async Task should_report_busy_reason_not_stale_failure_when_map_move_hits_busy_console()
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Units.FailNextPatchWith = ApiErrorCodes.Forbidden;
        await kit.Console.MoveAsync(6, 7);                         // 앞선 실패 사유 "권한이 없습니다(403)."
        kit.Units.HoldDetails = true;
        var busy = kit.Console.ChangeAdjacencyAsync(6, 5, null);   // 상세 GET 을 기다리는 동안 콘솔이 바쁘다
        Assert.True(kit.Console.IsBusy);

        var moved = await ((IUnitMapCommands)kit.Console).MoveAsync(4, 7);

        Assert.False(moved);
        var reason = ((IUnitMapConsoleBridge)kit.Console).LastWriteFailureReason;
        Assert.NotEqual("권한이 없습니다(403).", reason);
        Assert.Contains("앞선 작업", reason);

        kit.Units.ReleaseDetails();
        await busy.WaitAsync(ConsoleKit.Timeout);
    }

    [Fact]
    public async Task should_actually_reread_graph_for_map_when_console_was_busy()
    {
        var kit = await ConsoleKit.OpenAsync();
        var reads = kit.Units.GraphReads;
        kit.Units.HoldGraph = true;
        var refresh = kit.Console.ReloadAsync();                   // [갱신] — 편제 GET 대기 중
        Assert.True(kit.Console.IsBusy);

        var reload = ((IUnitMapCommands)kit.Console).ReloadAsync(quiet: true);

        Assert.False(reload.IsCompleted);                          // 바쁘다고 읽지 않고 돌아오지 않는다
        kit.Units.ReleaseGraph();
        await refresh.WaitAsync(ConsoleKit.Timeout);
        await reload.WaitAsync(ConsoleKit.Timeout);
        Assert.Equal(reads + 2, kit.Units.GraphReads);             // 관계도의 재판정이 새 편제를 실제로 읽었다
    }
    #endregion

    #region - MEDIUM-5 — 레일 전환이 막히면 "보였다" 고 하지 않는다 -
    [Fact]
    public async Task should_report_blocked_when_reveal_cannot_switch_to_map_rail()
    {
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(6);
        kit.Console.Form.Description = "손댄 설명";

        var outcome = await kit.Console.TryRevealAsync(6, openMap: true);

        Assert.Equal(OpenUnitConsoleOutcome.BlockedByUnsavedEdit, outcome);
        Assert.False(kit.Console.IsAdjacencyView);
    }
    #endregion

    #region - MEDIUM-6 — 실시간 꼬리표 · "나" 를 관계도에 넘긴다 -
    [Fact]
    public async Task should_pass_live_off_and_operator_name_to_map_status()
    {
        var layout = new FakeUnitLayoutApi { ActorName = _ => "나운영" };
        var delay = new ManualDelay();
        var console = new UnitConsoleViewModel(new FakeConsoleUnitApi(), new FakeConsoleDeviceApi(), myUnitCode: () => "c06",
                                               canEdit: () => true, canDelete: () => true, canView: () => true,
                                               events: new CapturingEventAggregator(new List<object>()), isDragging: () => false,
                                               delay: delay.Run, layoutApi: layout, clock: new MutableClock(),
                                               isLiveOff: () => true, operatorName: () => "나운영");
        await ((IActivate)console).ActivateAsync();
        await console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);

        console.Map.CompleteDrag(new UnitMapDropRequest(6, 30, 0, null, false));
        await console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);

        Assert.StartsWith("배치: 모든 운영자 공유 · 마지막 변경 나 ", console.Map.LayoutStatusText);
        Assert.EndsWith(UnitMapText.LiveOffSuffix, console.Map.LayoutStatusText);
    }

    [Fact]
    public void should_treat_live_reflection_as_off_when_console_has_no_event_bus()
    {
        var console = new UnitConsoleViewModel(new FakeConsoleUnitApi(), new FakeConsoleDeviceApi(), canEdit: () => true, canView: () => true,
                                               events: null, layoutApi: new FakeUnitLayoutApi(), clock: new MutableClock());

        Assert.EndsWith(UnitMapText.LiveOffSuffix, console.Map.LayoutStatusText);
    }
    #endregion

    #region - MEDIUM-8 — 창은 배치 GET 을 기다리지 않는다 -
    [Fact]
    public async Task should_finish_activation_before_layout_read_returns()
    {
        var layout = new GatedLayoutApi(new FakeUnitLayoutApi()) { HoldReads = true };
        var console = NewConsole(layout, new ManualDelay());

        await ((IActivate)console).ActivateAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(8, console.Tree.Count);
        Assert.Equal(UnitMapLayoutState.Loading, console.Map.LayoutState);   // 자동 배치로 먼저 그린다
        layout.ReleaseRead();
        await console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
        Assert.Equal(UnitMapLayoutState.Shared, console.Map.LayoutState);
    }
    #endregion

    #region - L-7 — 쓰기가 남은 채 닫기 -
    [Fact]
    public async Task should_wait_briefly_then_ask_when_closing_with_map_write_in_flight()
    {
        var layout = new GatedLayoutApi(new FakeUnitLayoutApi());
        var delay = new ManualDelay();
        var console = NewConsole(layout, delay);
        await ((IActivate)console).ActivateAsync();
        await console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
        var asked = new List<string>();
        console.Confirm = (_, message) => { asked.Add(message); return Task.FromResult(false); };
        layout.HoldWrites = true;
        console.Map.CompleteDrag(new UnitMapDropRequest(6, 30, 0, null, false));
        Assert.True(console.Map.IsWriting);

        var closing = console.CanCloseAsync();

        Assert.False(closing.IsCompleted);                         // 쓰기를 모른 채 곧바로 닫지 않는다
        delay.ElapseAll();                                         // 잠깐 기다림이 끝났는데도 쓰는 중
        Assert.False(await closing.WaitAsync(ConsoleKit.Timeout));  // 물었고, 운영자는 머물기를 골랐다
        Assert.Single(asked);

        layout.ReleaseNextWrite();
        await console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
    }

    [Fact]
    public async Task should_close_without_asking_when_map_write_finishes_during_the_wait()
    {
        var layout = new GatedLayoutApi(new FakeUnitLayoutApi());
        var console = NewConsole(layout, new ManualDelay());
        await ((IActivate)console).ActivateAsync();
        await console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
        var asked = 0;
        console.Confirm = (_, _) => { asked++; return Task.FromResult(false); };
        layout.HoldWrites = true;
        console.Map.CompleteDrag(new UnitMapDropRequest(6, 30, 0, null, false));

        var closing = console.CanCloseAsync();
        layout.ReleaseNextWrite();

        Assert.True(await closing.WaitAsync(ConsoleKit.Timeout));
        Assert.Equal(0, asked);
    }
    #endregion
}
