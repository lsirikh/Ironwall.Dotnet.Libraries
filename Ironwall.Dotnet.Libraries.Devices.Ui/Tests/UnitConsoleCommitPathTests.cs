using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-62 (FR-02 · FR-32 · FR-34 · FR-38 · FR-46 · NFR-12) — 확정 경로 정비.
/// 시나리오: SIM-C031 · C047 · SIM-F001~055 · F111~116 · SIM-O001~022 · SIM-K115~116 · K139~145 · SIM-M019 · SIM-S001~005.
/// </summary>
[Collection("CaliburnIoC")]
public class UnitConsoleCommitPathTests
{
    #region - 선택 불변 · 영향 집합 (ISSUE-20) -
    [Fact]
    public async Task should_keep_unrelated_selection_and_unapplied_edits_when_move_succeeds()
    {
        // ① — 선택(4)이 영향 집합(6 · 새 상위 7 · 옛 상위 3) 밖이면 선택 · 폼 · 미적용 편집 보존, 상세 GET 0
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(4);
        kit.Console.Form.Description = "손댄 설명";
        var details = kit.Units.DetailReads;

        var moved = await kit.Console.MoveAsync(6, 7);

        Assert.True(moved);
        Assert.Equal(4, kit.Console.SelectedRow?.Id);
        Assert.Equal("손댄 설명", kit.Console.Form.Description);
        Assert.Equal(details, kit.Units.DetailReads);
    }

    [Theory]
    [InlineData(6)]     // 옮긴 부대
    [InlineData(7)]     // 새 상위
    [InlineData(3)]     // 옛 상위
    public async Task should_reread_detail_once_and_keep_selection_when_selection_is_affected(int selected)
    {
        // ②
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(selected);
        var details = kit.Units.DetailReads;

        await kit.Console.MoveAsync(6, 7);

        Assert.Equal(details + 1, kit.Units.DetailReads);
        Assert.Equal(selected, kit.Console.SelectedRow?.Id);
    }

    [Fact]
    public async Task should_block_only_the_affected_map_drop_when_detail_is_dirty()
    {
        // ③ — 선택(3 = 옛 상위)이 dirty 면 그 확정만 막힘, 무관한 확정은 통과
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(3);
        kit.Console.Form.Description = "손댄 설명";

        var blocked = kit.Console.Map.Classify(6, 7, ctrl: false);
        var free = kit.Console.Map.Classify(8, 4, ctrl: false);     // 소초 8: 6 → 4 — 영향 {8, 6, 4} 에 3 없음

        Assert.Equal(UnitMapText.DirtyDetailBlocked, blocked.Reason);
        Assert.Equal(UnitMapDropKind.Reparent, free.Kind);
    }
    #endregion

    #region - 인접 오버로드 (ISSUE-24) -
    [Fact]
    public async Task should_patch_source_unit_and_keep_selection_when_adjacency_changed_by_source_id()
    {
        // ⑤ — 선택이 4 여도 PATCH 대상 = 6, 선택 불변, _lastAdjacency = (6, +5)
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(4);

        var ok = await kit.Console.ChangeAdjacencyAsync(6, 5, null);

        Assert.True(ok);
        var patch = Assert.Single(kit.Units.Patches);
        Assert.Equal(6, patch.Id);
        Assert.Equal(new[] { 4, 5 }, patch.Dto.AdjacentUnitIds);
        Assert.Equal(4, kit.Console.SelectedRow?.Id);
        Assert.Equal((6, 5, true), kit.Console.LastAdjacency);
        Assert.True(kit.Console.Tree.Find(6)!.AdjacentIds.Contains(5));
    }

    [Fact]
    public async Task should_keep_old_signature_equivalent_to_selected_source()
    {
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(6);

        await kit.Console.ChangeAdjacencyAsync(add: 5, remove: null);

        Assert.Equal(6, Assert.Single(kit.Units.Patches).Id);
        Assert.Equal(6, kit.Console.SelectedRow?.Id);
    }

    [Fact]
    public async Task should_undo_last_adjacency_with_reverse_operation()
    {
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.ChangeAdjacencyAsync(6, 5, null);

        await kit.Console.UndoAdjacencyAsync();

        Assert.Equal(2, kit.Units.Patches.Count);
        Assert.False(kit.Console.Tree.Find(6)!.AdjacentIds.Contains(5));
        Assert.Null(kit.Console.LastAdjacency);
    }
    #endregion

    #region - Alt+↑/↓ 판정 (ISSUE-34) -
    [Fact]
    public void should_plan_move_up_to_grandparent_and_root_when_top()
    {
        var tree = ConsoleKitTree();

        Assert.Equal(new UnitMovePlan(2, false, null), UnitMovePlanner.PlanMoveUp(tree, 6));     // 6 → b01(3) 의 상위 r01(2)
        Assert.Equal(new UnitMovePlan(null, true, null), UnitMovePlanner.PlanMoveUp(tree, 2));   // r01 → 최상위
        Assert.False(UnitMovePlanner.PlanMoveUp(tree, 1).IsAllowed);                              // 이미 최상위
    }

    [Fact]
    public void should_plan_move_down_to_nearest_preceding_allowed_parent_in_tree_order()
    {
        var tree = ConsoleKitTree();

        Assert.Equal(2, UnitMovePlanner.PlanMoveDown(tree, 6).NewParentId);   // 앞쪽: c04(같은 제대) · b01(현 상위) → r01
        Assert.Equal(3, UnitMovePlanner.PlanMoveDown(tree, 5).NewParentId);   // c05(b02 밑) 앞쪽의 b01
        Assert.False(UnitMovePlanner.PlanMoveDown(tree, 1).IsAllowed);
    }

    [Fact]
    public async Task should_use_full_tree_order_for_alt_down_even_when_tree_rail_is_filtered()
    {
        // 트리 레일에 제대 칩(중대)이 걸려 보이는 행이 중대뿐이어도 후보는 Tree.Ordered 에서
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(6);
        kit.Console.SelectEchelon(kit.Console.EchelonFilters.First(f => f.Echelon == EnumUnitEchelon.Company));

        await kit.Console.MoveSelectedDownAsync();

        Assert.Equal(2, kit.Console.Tree.Find(6)!.ParentId);
    }

    private static UnitTreeModel ConsoleKitTree()
    {
        var api = new FakeConsoleUnitApi();
        return UnitTreeBuilder.Build(api.GetGraphAsync().Result.Data);
    }
    #endregion

    #region - 콘솔 모달 (ISSUE-49) -
    [Fact]
    public async Task should_block_tree_rail_detail_and_toolbar_while_map_confirm_is_pending()
    {
        // ⑦ — 오버레이 대기 동안 트리 선택 · 레일 전환 · [적용] · [이동 되돌리기] · Alt+↑/↓ · [갱신] = 막힘 · 서버 0
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(4);
        await kit.Console.MoveAsync(4, 7);                  // 되돌릴 이동 하나
        var patches = kit.Units.Patches.Count;
        var graphs = kit.Units.GraphReads;
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false));
        Assert.True(kit.Console.IsStructureConfirmPending);
        var rail = kit.Console.SelectedRail;

        await kit.Console.SelectByIdAsync(6);
        kit.Console.SelectedRail = kit.Console.RailEntries.Last();
        await kit.Console.UndoMoveAsync();
        await kit.Console.MoveSelectedUpAsync();
        await kit.Console.ReloadAsync();

        Assert.Equal(4, kit.Console.SelectedRow?.Id);
        Assert.Same(rail, kit.Console.SelectedRail);
        Assert.Equal(patches, kit.Units.Patches.Count);
        Assert.Equal(graphs, kit.Units.GraphReads);
        Assert.False(kit.Console.IsConsoleInteractive);
        Assert.Equal(UnitConsoleViewModel.CONFIRM_PENDING_NOTICE, kit.Console.StatusText);
    }

    [Fact]
    public async Task should_allow_locate_on_map_while_confirm_is_pending()
    {
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(6);
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(4, 0, 0, 7, false));

        await kit.Console.Map.LocateOnMapAsync();

        Assert.Single(kit.Published.OfType<MapLocateRequest>());
    }
    #endregion

    #region - 상태별 사유 (ISSUE-25 · 6) -
    [Theory]
    [InlineData(ApiErrorCodes.Forbidden, "권한이 없습니다(403).")]
    [InlineData(ApiErrorCodes.NotFound, "다른 곳에서 삭제된 부대입니다(404).")]
    [InlineData(ApiErrorCodes.ValidationError, "서버 규칙에 맞지 않아 거절됐습니다.")]
    [InlineData(ApiErrorCodes.ClientTimeout, "결과를 확인하지 못했습니다 — 편제를 다시 읽어 실제 상태로 보입니다.")]
    public async Task should_say_status_specific_reason_and_reload_when_move_fails(string code, string reason)
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Units.FailNextPatchWith = code;
        var graphs = kit.Units.GraphReads;

        var moved = await kit.Console.MoveAsync(6, 7);

        Assert.False(moved);
        Assert.EndsWith(reason, kit.Console.StatusText);
        Assert.Equal(reason, ((IUnitMapConsoleBridge)kit.Console).LastWriteFailureReason);
        Assert.Equal(graphs + 1, kit.Units.GraphReads);
    }
    #endregion

    #region - 지도에서 온 이동 (ISSUE-43) -
    [Fact]
    public async Task should_select_center_and_show_map_rail_when_revealed()
    {
        var kit = await ConsoleKit.OpenAsync();

        var outcome = await kit.Console.TryRevealAsync(6, openMap: true);

        Assert.Equal(OpenUnitConsoleOutcome.Shown, outcome);
        Assert.Equal(6, kit.Console.SelectedRow?.Id);
        Assert.True(kit.Console.IsAdjacencyView);
    }

    [Fact]
    public async Task should_keep_selection_and_say_why_when_reveal_is_blocked_by_unsaved_edit()
    {
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(4);
        kit.Console.Form.Description = "손댄 설명";

        var outcome = await kit.Console.TryRevealAsync(6, openMap: true);

        Assert.Equal(OpenUnitConsoleOutcome.BlockedByUnsavedEdit, outcome);
        Assert.Equal(4, kit.Console.SelectedRow?.Id);
        Assert.Equal(UnitMapText.RevealBlockedBand("c06"), kit.Console.StatusText);
    }

    [Fact]
    public async Task should_report_unavailable_when_revealed_unit_is_not_in_graph()
    {
        var kit = await ConsoleKit.OpenAsync();

        Assert.Equal(OpenUnitConsoleOutcome.Unavailable, await kit.Console.TryRevealAsync(999, openMap: false));
    }
    #endregion

    #region - 상세 GET 폭주 방지 (ISSUE-51) -
    [Fact]
    public async Task should_read_only_first_and_last_detail_when_selection_changes_rapidly()
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Units.HoldDetails = true;
        var details = kit.Units.DetailReads;

        var a = kit.Console.SelectByIdAsync(4);
        var b = kit.Console.SelectByIdAsync(6);
        var c = kit.Console.SelectByIdAsync(3);
        kit.Units.ReleaseDetails();
        await Task.WhenAll(a, b, c).WaitAsync(ConsoleKit.Timeout);
        await kit.Console.WhenDetailSettledAsync().WaitAsync(ConsoleKit.Timeout);

        Assert.Equal(details + 2, kit.Units.DetailReads);
        Assert.Equal(3, kit.Console.SelectedRow?.Id);
        Assert.Equal("b01", kit.Console.Form.Code);
    }
    #endregion

    #region - 되돌리기 결선 (TEST-63 ②) -
    [Fact]
    public async Task should_invalidate_map_parent_undo_when_tree_moves_a_unit()
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false));
        await kit.Console.Map.ConfirmAsync().WaitAsync(ConsoleKit.Timeout);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
        Assert.True(kit.Console.Map.BarCanUndo);

        await kit.Console.MoveAsync(4, 7);                   // 트리 레일의 다른 이동

        Assert.False(kit.Console.Map.BarCanUndo);
    }
    #endregion
}
