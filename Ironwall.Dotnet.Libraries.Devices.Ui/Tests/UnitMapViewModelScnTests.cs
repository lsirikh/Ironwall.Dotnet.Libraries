using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// SCN-01(v1.3) 개정분 중 관계도 뷰모델 몫 — TEST-26 · 27 · 25 · 63 의 새 케이스.
/// FR-37 M 모드 경계 표(ISSUE-57 · SIM-E001~009) · 알림 최대 대기 2초(ISSUE-30) · 연기 30초 알림(ISSUE-9) ·
/// 인접선 꺼짐 막대(ISSUE-55) · 계층선 꺼짐 확인 문구 · 트리 이동이 관계도 되돌리기를 무효로 함(ISSUE-22 · TEST-63 ②).
/// </summary>
public class UnitMapViewModelScnTests
{
    private static UnitMapDropRequest Drop(MapKit kit, string moving, string? hover, double dx = 40, double dy = 0)
        => new(kit.Id(moving), dx, dy, hover is null ? null : kit.Id(hover), false);

    #region - M 모드 경계 표 (FR-37 · SIM-E001~009) -
    [Fact]
    public async Task should_refuse_move_mode_with_reason_when_nothing_selected()
    {
        var kit = await MapKit.OpenAsync();

        Assert.True(kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false));

        Assert.False(kit.Vm.IsMoveMode);
        Assert.Equal(UnitMapText.MoveModeNeedsSelection, kit.Vm.StatusText);
    }

    [Fact]
    public async Task should_enter_move_mode_at_l0_and_keep_it_through_window_deactivation()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.AttachSurface(kit.Surface);
        kit.Surface.Level = UnitMapLevel.L0;
        kit.Vm.RequestSelect(kit.Id("6중대"));

        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        kit.Vm.OnWindowDeactivated();

        Assert.True(kit.Vm.IsMoveMode);
    }

    [Fact]
    public async Task should_cancel_move_mode_without_server_then_select_when_other_node_clicked()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.RequestSelect(kit.Id("6중대"));
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        kit.Vm.HandleKey(UnitMapKeyCommand.Right, false);

        kit.Vm.RequestSelect(kit.Id("9중대"));
        await kit.Vm.WhenIdleAsync();

        Assert.False(kit.Vm.IsMoveMode);
        Assert.Empty(api.Writes);
        Assert.Equal(kit.Id("9중대"), kit.Vm.SelectedUnitId);
        Assert.False(kit.Vm.Scene.FactsOf(kit.Id("6중대")).IsMoved);
    }

    [Fact]
    public async Task should_cancel_move_mode_and_say_deleted_when_unit_is_removed_elsewhere()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var eight = kit.Id("81소초");
        kit.Vm.RequestSelect(eight);
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);

        var nodes = kit.F.Graph.Nodes.Where(n => n.Id != eight).ToList();
        kit.Vm.SetData(UnitMapTestData.Tree(UnitMapTestData.Graph(nodes, adjacency: kit.F.Graph.Edges.AdjacencyPairs.Where(p => p.Low != eight && p.High != eight).ToList())), kit.Devices);

        Assert.False(kit.Vm.IsMoveMode);
        Assert.Equal(UnitMapText.MoveModeUnitDeleted, kit.Vm.StatusText);
        Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task should_pan_to_moving_node_when_it_leaves_the_view()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.AttachSurface(kit.Surface);
        var six = kit.Id("6중대");
        kit.Vm.RequestSelect(six);
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        kit.Surface.Calls.Clear();
        kit.Surface.InView = _ => false;

        kit.Vm.HandleKey(UnitMapKeyCommand.Down, false);

        Assert.Equal(new[] { $"CenterOn:{six}" }, kit.Surface.Calls);
    }

    [Theory]
    [InlineData(UnitMapKeyCommand.Undo)]
    [InlineData(UnitMapKeyCommand.ParentUp)]
    [InlineData(UnitMapKeyCommand.ParentDown)]
    public async Task should_ignore_undo_and_alt_arrows_in_move_mode(UnitMapKeyCommand key)
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.CompleteDrag(Drop(kit, "7중대", null));
        await kit.Vm.WhenIdleAsync();
        kit.Vm.RequestSelect(kit.Id("8중대"));
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);

        Assert.True(kit.Vm.HandleKey(key, false));
        await kit.Vm.WhenIdleAsync();

        Assert.True(kit.Vm.IsMoveMode);
        Assert.Null(kit.Vm.PendingConfirm);
        Assert.Single(api.Writes);
    }
    #endregion

    #region - 알림 최대 대기 · 연기 알림 (ISSUE-30 · ISSUE-9) -
    [Fact]
    public async Task should_refetch_within_two_seconds_even_when_notices_keep_arriving_every_400ms()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var reads = api.ReadCount;

        for (var i = 0; i < 6; i++)          // 0 · 0.4 · 0.8 · 1.2 · 1.6 · 2.0 초
        {
            var version = api.SimulateOtherWrite(kit.Id("30중대"), i, 0);
            await kit.Vm.HandleAsync(new UnitLayoutChangedMessage(version), default);
            await kit.Vm.WhenIdleAsync();
            kit.Clock.Advance(TimeSpan.FromMilliseconds(400));
        }

        Assert.Equal(reads + 1, api.ReadCount);    // 창이 한 번도 닫히지 않았지만 2초째에 한 번
    }

    [Fact]
    public async Task should_say_once_that_change_waits_when_deferred_longer_than_30_seconds()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.RequestSelect(kit.Id("6중대"));
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);

        await kit.Vm.HandleAsync(new UnitLayoutChangedMessage(api.SimulateOtherWrite(kit.Id("30중대"), 1, 0)), default);
        kit.Clock.Advance(TimeSpan.FromSeconds(31));
        kit.Delay.ElapseAll();
        await Task.Yield();

        Assert.Equal(UnitMapText.LayoutNoticeDeferredStatus, kit.Vm.StatusText);
        Assert.True(kit.Vm.IsMoveMode);                     // 강제 반영 · 모드 해제 없음

        kit.Vm.HandleKey(UnitMapKeyCommand.Escape, false);
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();
        Assert.NotEqual(UnitMapText.LayoutNoticeDeferredStatus, kit.Vm.StatusText);
    }
    #endregion

    #region - 레이어 꺼짐 (ISSUE-55) -
    [Fact]
    public async Task should_offer_show_adjacency_action_without_turning_it_on_when_adjoined_while_hidden()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.SetLayers(new UnitMapLayers(Hierarchy: true, Adjacency: false, DeviceBadges: true));
        kit.Vm.CompleteDrag(Drop(kit, "6중대", "9중대"));
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.EndsWith(UnitMapText.AdjacencyHiddenNote, kit.Vm.BarText);
        Assert.Equal(UnitMapText.ShowAdjacencyAction, kit.Vm.BarActionText);
        Assert.False(kit.Vm.Layers.Adjacency);              // 결정 — 자동으로 켜지 않는다

        kit.Vm.RunBarAction();

        Assert.True(kit.Vm.Layers.Adjacency);
        Assert.Null(kit.Vm.BarActionText);
    }

    [Fact]
    public async Task should_show_new_parent_path_in_confirm_when_hierarchy_layer_hidden()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.SetLayers(new UnitMapLayers(Hierarchy: false, Adjacency: true, DeviceBadges: true));

        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        Assert.Contains("새 상위 경로: 3대대 › 1연대 › 제○○사단", kit.Vm.PendingConfirm!.Text.Lines);
    }
    #endregion

    #region - 되돌리기 (TEST-63 ② · ⑦) -
    [Fact]
    public async Task should_invalidate_map_parent_undo_when_tree_moves_another_unit()
    {
        // ISSUE-22 — 트리 레일에서 다른 이동이 일어나면 관계도 막대의 [되돌리기]가 그 이동을 가리키지 않게 무효
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();
        Assert.True(kit.Vm.BarCanUndo);

        kit.Vm.OnConsoleStructureChanged();
        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.False(kit.Vm.BarCanUndo);
        Assert.Single(kit.Commands.Writes);                 // 되돌리기 호출 0
    }

    [Fact]
    public async Task should_keep_position_undo_when_tree_moves_another_unit()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null));
        await kit.Vm.WhenIdleAsync();

        kit.Vm.OnConsoleStructureChanged();

        Assert.True(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_undo_in_reverse_time_order_across_kinds()
    {
        // ISSUE-10 · SIM-F123 — 위치 A → 상위 B → 위치 C 뒤 Ctrl+Z ×3 = C · B · A
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 10, 0));                  // A
        await kit.Vm.WhenIdleAsync();
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));                      // B
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();
        kit.Vm.SetData(UnitMapViewModelDropTests.Reparented(kit.F, kit.Id("8중대"), kit.Id("3대대")), kit.Devices);
        kit.Commands.OnReload = () => kit.Vm.SetData(UnitMapViewModelDropTests.Reparented(kit.F, kit.Id("8중대"), kit.Id("3대대")), kit.Devices);
        kit.Vm.CompleteDrag(Drop(kit, "7중대", null, 20, 0));                  // C
        await kit.Vm.WhenIdleAsync();
        var writesBefore = api.Writes.Count;

        await kit.Vm.UndoAsync(); await kit.Vm.WhenIdleAsync();
        Assert.Equal(new[] { kit.Id("7중대") }, api.Writes.Last().Change.Clear);     // C
        await kit.Vm.UndoAsync(); await kit.Vm.WhenIdleAsync();
        Assert.Equal($"Move:{kit.Id("8중대")}->{kit.Id("2대대")}", kit.Commands.Writes.Last());   // B
        await kit.Vm.UndoAsync(); await kit.Vm.WhenIdleAsync();
        Assert.Equal(new[] { kit.Id("6중대") }, api.Writes.Last().Change.Clear);     // A
        Assert.Equal(writesBefore + 2, api.Writes.Count);
    }
    #endregion
}
