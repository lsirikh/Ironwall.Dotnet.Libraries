using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-25 (FR-02 · FR-31 · FR-32 · FR-34 · FR-35 · NFR-04 · NFR-12) — 선택 다리 · 드롭 · 확인 · 실패 복구 · 되돌리기.
/// 시나리오: SIM-F001 · F009 · F023 · F045 · F056 · F059 · F061 · F064 · F067 · F113 · F114~F116 · F118 · F120 · F121 · F127.
/// 조정자 필수 항목 3(포커스 복귀) · 5(상세 새로 고침 · 더러운 상세의 영향 부대 드롭 막힘).
/// </summary>
public class UnitMapViewModelDropTests
{
    private static UnitMapDropRequest Drop(MapKit kit, string moving, string? hover, double dx = 40, double dy = -20, bool ctrl = false)
        => new(kit.Id(moving), dx, dy, hover is null ? null : kit.Id(hover), ctrl);

    /// <summary>편제에서 한 부대의 상위를 바꾼 새 트리(콘솔이 재조회한 것처럼).</summary>
    internal static UnitTreeModel Reparented(UnitMapFixture f, int unitId, int newParentId)
    {
        var nodes = f.Graph.Nodes.Select(n => UnitMapTestData.Node(n.Id, n.Code, n.Name, n.EchelonRaw, n.Id == unitId ? newParentId : n.ParentId, n.IsEnable)).ToList();
        var adjacency = f.Graph.Edges.AdjacencyPairs.ToList();
        return UnitMapTestData.Tree(UnitMapTestData.Graph(nodes, adjacency: adjacency));
    }

    /// <summary>여러 부대의 상위를 한꺼번에 바꾼 새 트리.</summary>
    internal static UnitTreeModel ReparentedMany(UnitMapFixture f, params (int Unit, int Parent)[] moves)
    {
        var map = moves.ToDictionary(m => m.Unit, m => m.Parent);
        var nodes = f.Graph.Nodes.Select(n => UnitMapTestData.Node(n.Id, n.Code, n.Name, n.EchelonRaw, map.TryGetValue(n.Id, out var p) ? p : n.ParentId, n.IsEnable)).ToList();
        return UnitMapTestData.Tree(UnitMapTestData.Graph(nodes, adjacency: f.Graph.Edges.AdjacencyPairs.ToList()));
    }

    /// <summary>인접 한 쌍을 더하거나 뺀 새 트리.</summary>
    internal static UnitTreeModel WithAdjacency(UnitMapFixture f, int a, int b, bool present)
    {
        var pairs = f.Graph.Edges.AdjacencyPairs.Where(p => !(p == (a, b) || p == (b, a))).ToList();
        if (present) pairs.Add((a, b));
        return UnitMapTestData.Tree(UnitMapTestData.Graph(f.Graph.Nodes, adjacency: pairs));
    }

    #region - 선택 다리 (FR-02) -
    [Fact]
    public async Task should_select_through_console_guard_when_node_clicked()
    {
        var kit = await MapKit.OpenAsync();

        kit.Vm.RequestSelect(kit.Id("7중대"));

        Assert.Equal(new[] { $"Select:{kit.Id("7중대")}" }, kit.Commands.Calls);
        Assert.Equal(kit.Id("7중대"), kit.Vm.SelectedUnitId);
    }

    [Fact]
    public async Task should_keep_selection_when_guard_refuses()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("7중대"));
        kit.Commands.AllowSelect = false;

        kit.Vm.RequestSelect(kit.Id("9중대"));

        Assert.Equal(kit.Id("7중대"), kit.Vm.SelectedUnitId);
    }

    [Fact]
    public async Task should_follow_console_selection_when_tree_selects()
    {
        var kit = await MapKit.OpenAsync();

        kit.Commands.RaiseSelected(kit.Id("3대대"));

        Assert.Equal(kit.Id("3대대"), kit.Vm.SelectedUnitId);
    }
    #endregion

    #region - 위치 (FR-31) -
    [Fact]
    public async Task should_draw_first_then_patch_once_with_one_item_when_position_drop_on_shared_layout()
    {
        // Arrange — 서버 버전 7, 쓰기 응답을 붙잡는다
        var api = new FakeUnitLayoutApi(version: 7);
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("6중대");
        var child = kit.Id("61소초");
        var before = kit.Vm.Scene.Positions[moving];
        var childBefore = kit.Vm.Scene.Positions[child];
        kit.Gate.HoldWrites = true;

        // Act
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 40, -20));

        // Assert — 응답 전에 이미 새 자리(예하도 함께)
        Assert.Equal(before + new Vector(40, -20), kit.Vm.Scene.Positions[moving]);
        Assert.Equal(childBefore + new Vector(40, -20), kit.Vm.Scene.Positions[child]);
        Assert.True(kit.Vm.IsWriting);

        kit.Gate.ReleaseNextWrite();
        await kit.Vm.WhenIdleAsync();

        var write = Assert.Single(api.Writes);
        Assert.Equal(7, write.IfMatch);
        Assert.Equal(new Dictionary<int, Vector> { [moving] = new(40, -20) }, write.Change.Set);
        Assert.Empty(write.Change.Clear);
        Assert.False(write.Change.ClearAll);
        Assert.Equal("‘6중대’ 위치를 옮겼습니다 — 모든 운영자에게 보입니다.", kit.Vm.BarText);
        Assert.False(kit.Vm.BarIsError);
        Assert.True(kit.Vm.CanUndo);
        Assert.Equal(before + new Vector(40, -20), kit.Vm.Scene.Positions[moving]);
        Assert.True(kit.Vm.Scene.FactsOf(moving).IsMoved);
    }

    [Fact]
    public async Task should_add_to_existing_delta_when_unit_already_moved()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 40, -20));
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 5, 5));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new Vector(45, -15), api.Current.Deltas[kit.Id("6중대")]);
        Assert.Equal(2, api.Writes.Count);
        Assert.Equal(api.Writes[0].VersionAfter, api.Writes[1].IfMatch);
    }

    [Fact]
    public async Task should_keep_position_in_memory_only_when_session_only()
    {
        var api = new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Unsupported };
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("6중대");
        var before = kit.Vm.Scene.Positions[moving];

        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 40, -20));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(UnitMapLayoutState.SessionOnly, kit.Vm.LayoutState);
        Assert.Equal(0, kit.Gate.StartedWrites);
        Assert.Equal(before + new Vector(40, -20), kit.Vm.Scene.Positions[moving]);
        Assert.Contains("창을 닫으면 사라집니다", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_clamp_delta_to_server_limit_when_dragged_far()
    {
        // ISSUE-40 — 서버 한계 ±1,000,000 을 넘는 Δ 는 보내지 않는다
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);

        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 5_000_000, -3_000_000));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new Vector(1_000_000, -1_000_000), api.Writes.Single().Change.Set[kit.Id("6중대")]);
    }

    [Fact]
    public async Task should_reread_layout_and_draw_server_state_when_position_write_fails()
    {
        // FR-34 · SIM-F064 — 낙관적으로 그린 것을 되돌린다(로컬에 남겨 두지 않는다)
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("6중대");
        var before = kit.Vm.Scene.Positions[moving];
        var reads = api.ReadCount;
        api.FailNextWrite(new UnitLayoutWrite.Failed(UnitLayoutFailureKind.Server, "500"));

        kit.Vm.CompleteDrag(Drop(kit, "6중대", null));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(reads + 1, api.ReadCount);
        Assert.Equal(before, kit.Vm.Scene.Positions[moving]);
        Assert.True(kit.Vm.BarIsError);
        Assert.StartsWith("‘6중대’ 위치를 저장하지 못했습니다.", kit.Vm.BarText);
        Assert.False(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_say_result_unknown_when_position_write_times_out()
    {
        // ISSUE-6 · SIM-F065 — 시간 초과는 반영됐을 수 있다: 다시 읽은 서버 상태로 그리고 그렇게 말한다
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        api.FailNextWrite(new UnitLayoutWrite.Failed(UnitLayoutFailureKind.Timeout, "timeout"));

        kit.Vm.CompleteDrag(Drop(kit, "6중대", null));
        await kit.Vm.WhenIdleAsync();

        Assert.Contains("결과를 확인하지 못했습니다", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_switch_to_session_only_when_layout_route_disappears_on_write()
    {
        // SIM-F059 — 지원 중 404: 세션 전용 전환 + 위치는 메모리에
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("6중대");
        var before = kit.Vm.Scene.Positions[moving];
        api.FailNextWrite(new UnitLayoutWrite.Unsupported("404"));

        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 40, -20));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(UnitMapLayoutState.SessionOnly, kit.Vm.LayoutState);
        Assert.Equal(before + new Vector(40, -20), kit.Vm.Scene.Positions[moving]);
        Assert.Contains("창을 닫으면 사라집니다", kit.Vm.BarText);
    }
    #endregion

    #region - 상위 · 인접 (FR-32 · FR-34) -
    [Fact]
    public async Task should_move_once_and_show_bar_when_reparent_confirmed()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new[] { $"Move:{kit.Id("8중대")}->{kit.Id("3대대")}" }, kit.Commands.Writes);
        Assert.Equal("‘8중대’를 ‘3대대’ 밑으로 옮겼습니다.", kit.Vm.BarText);
        Assert.True(kit.Vm.CanUndo);
        Assert.Empty(kit.Api.Writes);   // FR-08 정리는 콘솔 MoveAsync 성공 경로 한 곳(§5-B)
    }

    [Fact]
    public async Task should_change_adjacency_of_dragged_unit_without_changing_selection()
    {
        // SIM-F116 — 끈 부대(선택 아님) 기준 · 선택 불변
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("7중대"));
        kit.Vm.CompleteDrag(Drop(kit, "6중대", "9중대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new[] { $"Adj:{kit.Id("6중대")}+{kit.Id("9중대")}--" }, kit.Commands.Writes);
        Assert.Equal(1, kit.Commands.SelectCalls);   // 처음 한 번뿐
        Assert.Equal(kit.Id("7중대"), kit.Vm.SelectedUnitId);
        Assert.Equal("‘6중대’와 ‘9중대’를 인접 부대로 이었습니다.", kit.Vm.BarText);
    }

    [Theory]
    [InlineData("reparent")]
    [InlineData("adjoin")]
    public async Task should_reload_quietly_and_show_error_bar_when_org_write_fails(string kind)
    {
        var kit = await MapKit.OpenAsync();
        kit.Commands.MoveResult = false;
        kit.Commands.AdjacencyResult = false;
        kit.Vm.CompleteDrag(kind == "reparent" ? Drop(kit, "8중대", "3대대") : Drop(kit, "6중대", "9중대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(1, kit.Commands.Calls.Count(c => c == "Reload:quiet"));
        Assert.True(kit.Vm.BarIsError);
        Assert.False(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_refresh_detail_when_selected_unit_is_in_affected_set()
    {
        // 필수 항목 5 — 선택이 끈 부대 · 대상 · 옛 상위 · 새 상위 중 하나면 상세를 새로 읽는다
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("2대대"));      // 8중대의 옛 상위
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(1, kit.Bridge.DetailRefreshes);
    }

    [Fact]
    public async Task should_not_refresh_detail_when_selected_unit_is_unrelated()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("1중대"));
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(0, kit.Bridge.DetailRefreshes);
    }

    [Theory]
    [InlineData("8중대")]     // 끈 부대
    [InlineData("3대대")]     // 대상(새 상위)
    [InlineData("2대대")]     // 옛 상위
    public async Task should_block_org_drop_when_dirty_detail_is_in_affected_set(string selected)
    {
        // SIM-F114 · ISSUE-20 — 미적용 편집이 말없이 사라지지 않게
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id(selected));
        kit.Bridge.IsDetailDirty = true;

        var decision = kit.Vm.Classify(kit.Id("8중대"), kit.Id("3대대"), ctrl: false);
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Null(kit.Vm.PendingConfirm);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Contains("적용하지 않은 변경", kit.Vm.StatusText);
    }

    [Fact]
    public async Task should_allow_org_drop_when_dirty_detail_is_unrelated()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("1중대"));
        kit.Bridge.IsDetailDirty = true;

        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        Assert.Equal(UnitMapConfirmKind.Reparent, kit.Vm.PendingConfirm!.Kind);
    }
    #endregion

    #region - 되돌리기 (FR-35) -
    [Fact]
    public async Task should_send_reverse_delta_once_when_position_undone()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("6중대");
        var before = kit.Vm.Scene.Positions[moving];
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 40, -20));
        await kit.Vm.WhenIdleAsync();

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(2, api.Writes.Count);
        Assert.Equal(new[] { moving }, api.Writes[1].Change.Clear);   // 원래 Δ 가 없었으면 clear
        Assert.Equal(api.Writes[0].VersionAfter, api.Writes[1].IfMatch);
        Assert.Equal(before, kit.Vm.Scene.Positions[moving]);
        Assert.False(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_refuse_undo_and_notify_when_other_operator_changed_that_unit()
    {
        // SIM-F121 — 남의 변경을 되돌리기로 지우지 않는다
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("6중대");
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 40, -20));
        await kit.Vm.WhenIdleAsync();
        var version = api.SimulateOtherWrite(moving, 99, 99);
        await kit.Vm.HandleAsync(new Ironwall.Dotnet.Libraries.ViewModel.Models.UnitLayoutChangedMessage(version), default);
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Single(api.Writes);
        Assert.Equal("다른 운영자가 ‘6중대’ 배치를 바꿔 되돌리지 않았습니다.", kit.Vm.BarText);
        Assert.Equal(new Vector(99, 99), api.Current.Deltas[moving]);
    }

    [Fact]
    public async Task should_move_back_to_old_parent_when_reparent_undone()
    {
        var kit = await MapKit.OpenAsync();
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.ConfirmAsync();
        kit.Vm.SetData(Reparented(kit.F, moving, kit.Id("3대대")), kit.Devices);   // 콘솔 재조회

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal($"Move:{moving}->{kit.Id("2대대")}", kit.Commands.Writes.Last());
        Assert.False(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_refuse_reparent_undo_when_parent_changed_elsewhere()
    {
        // ISSUE-21 · SIM-F118 — 그 사이 다른 곳에서 상위가 또 바뀌었으면 되돌리지 않는다
        var kit = await MapKit.OpenAsync();
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.ConfirmAsync();
        kit.Vm.SetData(Reparented(kit.F, moving, kit.Id("4대대")), kit.Devices);

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Single(kit.Commands.Writes);
        Assert.Contains("되돌리지 않았습니다", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_remove_adjacency_once_when_adjoin_undone()
    {
        var kit = await MapKit.OpenAsync();
        var a = kit.Id("6중대");
        var b = kit.Id("9중대");
        kit.Vm.CompleteDrag(Drop(kit, "6중대", "9중대"));
        await kit.Vm.ConfirmAsync();
        kit.Vm.SetData(WithAdjacency(kit.F, a, b, present: true), kit.Devices);

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal($"Adj:{a}+--{b}", kit.Commands.Writes.Last());
    }

    [Fact]
    public async Task should_do_nothing_and_notify_when_adjacency_already_removed_elsewhere()
    {
        // SIM-F120
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit, "6중대", "9중대"));
        await kit.Vm.ConfirmAsync();
        // 재조회된 편제에 그 쌍이 없다(다른 운영자가 끊음)

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Single(kit.Commands.Writes);
        Assert.Contains("이미", kit.Vm.BarText);
        Assert.False(kit.Vm.CanUndo);
    }
    #endregion

    #region - 한 번에 하나 · 막대 · 포커스 -
    [Fact]
    public async Task should_send_second_write_only_after_first_response_when_dropped_quickly()
    {
        // NFR-12 · SIM-F113
        var api = new FakeUnitLayoutApi(version: 3);
        var kit = await MapKit.OpenAsync(api);
        kit.Gate.HoldWrites = true;

        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 10, 0));
        kit.Vm.CompleteDrag(Drop(kit, "7중대", null, 20, 0));
        Assert.Equal(1, kit.Gate.StartedWrites);

        kit.Gate.ReleaseNextWrite();
        await kit.Gate.WhenWritesStartedAsync(2);
        Assert.Equal(2, kit.Gate.StartedWrites);
        kit.Gate.ReleaseNextWrite();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new long[] { 3, 4 }, api.Writes.Select(w => w.IfMatch));
        Assert.All(api.Writes, w => Assert.True(w.Succeeded));
    }

    [Fact]
    public async Task should_keep_bar_until_next_operation()
    {
        // SIM-F127 — 타이머로 사라지지 않고 다음 조작이 교체
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null));
        await kit.Vm.WhenIdleAsync();
        var first = kit.Vm.BarText;
        kit.Delay.ElapseAll();

        Assert.Equal(first, kit.Vm.BarText);

        kit.Vm.CompleteDrag(Drop(kit, "7중대", null));
        await kit.Vm.WhenIdleAsync();
        Assert.Equal("‘7중대’ 위치를 옮겼습니다 — 모든 운영자에게 보입니다.", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_return_focus_to_canvas_when_overlay_closes_or_bar_dismissed()
    {
        // 필수 항목 3 — 오버레이 · 막대가 닫히면 캔버스로 포커스를 되돌려 Ctrl+Z · 화살표가 계속 된다
        var kit = await MapKit.OpenAsync();
        var focus = new List<UnitMapFocusTarget>();
        kit.Vm.FocusRequested += (_, target) => focus.Add(target);

        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        kit.Vm.CancelConfirm();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();
        kit.Vm.DismissBar();

        Assert.Equal(new[] { UnitMapFocusTarget.Canvas, UnitMapFocusTarget.Canvas, UnitMapFocusTarget.Canvas }, focus);
        Assert.Null(kit.Vm.BarText);
    }

    [Fact]
    public async Task should_cancel_drag_without_server_call_when_drop_outside_canvas()
    {
        // 조정자 결정(D-2026-09-27-6615ba) · ISSUE-13 — 캔버스 밖 놓기는 취소(캔버스가 CancelDrag 로 알린다)
        var kit = await MapKit.OpenAsync();
        kit.Vm.BeginDrag(kit.Id("6중대"));

        kit.Vm.CancelDrag(kit.Id("6중대"));
        await kit.Vm.WhenIdleAsync();

        Assert.False(kit.Vm.IsDragging);
        Assert.Empty(kit.Api.Writes);
        Assert.Equal(0, kit.Commands.WriteCalls);
    }
    #endregion

    #region - 조정자 D-2026-09-27-215b6d (#20 · #21 · #22 · #23 · #25 · #49) -
    [Fact]
    public async Task should_keep_overlay_and_explain_when_confirm_pressed_while_console_busy()
    {
        // #49 · #23 · SIM-F111 · F112 — 콘솔이 바쁜 동안 [확정]은 비활성, 눌러도 사라지지 않고 까닭을 말한다
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        kit.Bridge.IsBusy = true;

        Assert.False(kit.Vm.CanConfirm);
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();
        Assert.NotNull(kit.Vm.PendingConfirm);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Contains("앞선 작업", kit.Vm.StatusText);

        kit.Bridge.IsBusy = false;
        Assert.True(kit.Vm.CanConfirm);
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();
        Assert.Single(kit.Commands.Writes);
    }

    [Fact]
    public async Task should_not_send_and_explain_when_fresh_graph_invalidates_pending_move()
    {
        // #25 · SIM-N 계열 — 확인 중 미뤄 둔 편제 변경을 확정 직전에 새 편제로 다시 판정한다
        var kit = await MapKit.OpenAsync();
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        kit.Bridge.HasDeferredReload = true;
        kit.Commands.OnReload = () => kit.Vm.SetData(Reparented(kit.F, moving, kit.Id("3대대")), kit.Devices);   // 다른 운영자가 이미 옮겼다

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(1, kit.Commands.ReloadCalls);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.True(kit.Vm.BarIsError);
        Assert.Contains("편제가 바뀌어", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_send_once_after_fresh_graph_when_pending_move_still_valid()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        kit.Bridge.HasDeferredReload = true;
        kit.Commands.OnReload = () => kit.Vm.SetData(kit.F.Tree, kit.Devices);

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new[] { "Reload:quiet", $"Move:{kit.Id("8중대")}->{kit.Id("3대대")}" }, kit.Commands.Calls.Where(c => !c.StartsWith("Select", System.StringComparison.Ordinal)));
    }

    [Fact]
    public async Task should_offer_bar_undo_only_for_the_operation_the_bar_describes()
    {
        // #22 — 막대 문구와 [되돌리기]가 가리키는 조작이 늘 같다
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("6중대");
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null));
        await kit.Vm.WhenIdleAsync();
        Assert.True(kit.Vm.BarCanUndo);

        var version = api.SimulateOtherWrite(moving, 1, 1);
        await kit.Vm.HandleAsync(new Ironwall.Dotnet.Libraries.ViewModel.Models.UnitLayoutChangedMessage(version), default);
        kit.Delay.ElapseAll();
        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Contains("되돌리지 않았습니다", kit.Vm.BarText);
        Assert.False(kit.Vm.BarCanUndo);
    }

    [Fact]
    public async Task should_read_graph_before_reverse_move_when_reparent_undone()
    {
        // #21 — 상위 되돌리기는 먼저 읽고 판정한다
        var kit = await MapKit.OpenAsync();
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.ConfirmAsync();
        kit.Commands.OnReload = () => kit.Vm.SetData(Reparented(kit.F, moving, kit.Id("3대대")), kit.Devices);

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        var calls = kit.Commands.Calls.Where(c => !c.StartsWith("Select", System.StringComparison.Ordinal)).ToList();
        Assert.Equal(new[] { $"Move:{moving}->{kit.Id("3대대")}", "Reload:quiet", $"Move:{moving}->{kit.Id("2대대")}" }, calls);
    }

    [Fact]
    public async Task should_refuse_reparent_undo_when_new_parent_chain_changed_elsewhere()
    {
        // #21 — 부대 + 조상 값 비교: 새 상위(3대대)가 그 사이 다른 연대로 옮겨졌으면 되돌리지 않는다
        var kit = await MapKit.OpenAsync();
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.ConfirmAsync();
        kit.Vm.SetData(Reparented(kit.F, moving, kit.Id("3대대")), kit.Devices);
        kit.Commands.OnReload = () => kit.Vm.SetData(ReparentedMany(kit.F, (moving, kit.Id("3대대")), (kit.Id("3대대"), kit.Id("2연대"))), kit.Devices);

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Single(kit.Commands.Writes);
        Assert.Contains("되돌리지 않았습니다", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_say_moved_position_stays_auto_when_parent_undo_after_delta()
    {
        // 조정자 결정 — 상위 되돌리기는 Δ 를 되살리지 않는다(막대가 말한다)
        var kit = await MapKit.OpenAsync();
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", null, 30, 0));
        await kit.Vm.WhenIdleAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.ConfirmAsync();
        kit.Vm.SetData(Reparented(kit.F, moving, kit.Id("3대대")), kit.Devices);

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Contains("자동 배치", kit.Vm.BarText);
    }
    #endregion

    #region - 상위 변경 뒤 배치 정리 (FR-08 · ISSUE-4 · 레인 B PlanReparentCleanup) -
    [Fact]
    public async Task should_read_then_clear_moved_units_delta_with_fresh_version_after_reparent()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", null, 30, 0));
        await kit.Vm.WhenIdleAsync();
        var reads = api.ReadCount;
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(reads + 1, api.ReadCount);
        var clear = api.Writes.Last();
        Assert.Equal(new[] { moving }, clear.Change.Clear);
        Assert.Equal(api.Writes[0].VersionAfter, clear.IfMatch);
        Assert.False(api.Current.Deltas.ContainsKey(moving));
        Assert.False(kit.Vm.BarIsError);
    }

    [Fact]
    public async Task should_not_clear_or_report_conflict_when_server_already_removed_row()
    {
        // ISSUE-4 — 서버가 같은 트랜잭션에서 행을 지우고 버전을 올렸다: 충돌이 아니다
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", null, 30, 0));
        await kit.Vm.WhenIdleAsync();
        kit.Commands.OnMoveSucceeded = () => api.SimulateOtherClear(moving, by: "서버");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Single(api.Writes);
        Assert.False(kit.Vm.BarIsError);
        Assert.Equal("‘8중대’를 ‘3대대’ 밑으로 옮겼습니다.", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_reread_and_retry_once_without_conflict_bar_when_cleanup_clear_gets_412()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var moving = kit.Id("8중대");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", null, 30, 0));
        await kit.Vm.WhenIdleAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        api.FailNextWriteWithConflict();

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(3, api.Writes.Count);            // 위치 · 정리(412) · 정리(다시 읽고 한 번 더)
        Assert.False(api.Current.Deltas.ContainsKey(moving));
        Assert.False(kit.Vm.BarIsError);
    }

    [Fact]
    public async Task should_clear_in_memory_after_reparent_when_session_only()
    {
        var api = new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Unsupported };
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.CompleteDrag(Drop(kit, "8중대", null, 30, 0));
        await kit.Vm.WhenIdleAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(0, kit.Gate.StartedWrites);
        Assert.False(kit.Vm.Scene.FactsOf(kit.Id("8중대")).IsMoved);
    }
    #endregion

    #region - 지도 회신 문구 (레인 B MapLocateResult.Hidden · OutsideAnchor) -
    [Fact]
    public async Task should_pass_hidden_and_outside_anchor_to_status_when_map_replies()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("7중대"));
        await kit.Vm.LocateOnMapAsync();
        var request = Assert.IsType<Ironwall.Dotnet.Libraries.ViewModel.Models.MapLocateRequest>(Assert.Single(kit.Published));

        await kit.Vm.HandleAsync(new Ironwall.Dotnet.Libraries.ViewModel.Models.MapLocateResult(request.RequestId, 3, 2, Hidden: 1, OutsideAnchor: 1), default);

        Assert.Equal(UnitMapText.MapLocateResult("7중대", 3, 2, 1, 1), kit.Vm.StatusText);
    }
    #endregion
}
