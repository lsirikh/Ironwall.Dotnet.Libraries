using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-61 (FR-47 · FR-48 · NFR-12) — 부대 콘솔 재조회 정비: 편제만 재조회 · 연기 게이트 · 최대 대기 · 더러운 상세 · 선택 소실 한 곳.
/// 시나리오: SIM-N001~069 · SIM-N070~073 · SIM-C011~012 · SIM-C025~027 · SIM-C041~043 · SIM-O007 · SIM-O019 · SIM-E005.
/// </summary>
[Collection("CaliburnIoC")]
public class UnitConsoleGraphReloadTests
{
    private static async Task NoticeAsync(ConsoleKit kit, int resourceId = 4)
        => await kit.Console.HandleAsync(new UnitTopologyChangedMessage("UPDATED", resourceId), CancellationToken.None);

    private static async Task SettleAsync(ConsoleKit kit)
    {
        kit.Delay.ElapseAll();
        await kit.Console.ExternalChangeTask.WaitAsync(ConsoleKit.Timeout);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
    }

    [Fact]
    public async Task should_read_graph_once_and_no_devices_when_remote_changes_settle()
    {
        // ① ISSUE-28 — 합친 알림 뒤 /graph 1회 · 장비 GET 0
        var kit = await ConsoleKit.OpenAsync();
        var (graphs, loads) = (kit.Units.GraphReads, kit.Devices.Loads);

        for (var i = 0; i < 5; i++) await NoticeAsync(kit);
        await SettleAsync(kit);

        Assert.Equal(graphs + 1, kit.Units.GraphReads);
        Assert.Equal(loads, kit.Devices.Loads);
    }

    [Theory]
    [InlineData(41)]      // 인접 대리키
    [InlineData(12)]      // 부대 id
    [InlineData(0)]
    public async Task should_behave_the_same_whatever_resource_id_says(int resourceId)
    {
        // ⑦ ISSUE-31 — ResourceId 는 판단에 쓰지 않는다
        var kit = await ConsoleKit.OpenAsync();
        var graphs = kit.Units.GraphReads;

        await NoticeAsync(kit, resourceId);
        await SettleAsync(kit);

        Assert.Equal(graphs + 1, kit.Units.GraphReads);
    }

    [Theory]
    [InlineData("drag")]
    [InlineData("move-mode")]
    [InlineData("confirm")]
    public async Task should_defer_while_map_hands_are_busy_then_read_exactly_once(string busy)
    {
        // ② ISSUE-9 · 49 — 끄는 중 · M 모드 · 확인 오버레이 동안 0회, 끝나면 1회
        var kit = await ConsoleKit.OpenAsync();
        var map = kit.Console.Map;
        switch (busy)
        {
            case "drag": map.BeginDrag(6); break;
            case "move-mode": map.RequestSelect(6); await kit.Console.WhenDetailSettledAsync().WaitAsync(ConsoleKit.Timeout); map.HandleKey(UnitMapKeyCommand.MoveMode, false); break;
            default: map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false)); break;
        }
        Assert.True(map.DefersReload);
        var graphs = kit.Units.GraphReads;

        await NoticeAsync(kit);
        await SettleAsync(kit);
        Assert.Equal(graphs, kit.Units.GraphReads);

        switch (busy)
        {
            case "drag": map.CancelDrag(6); break;
            case "move-mode": map.HandleKey(UnitMapKeyCommand.Escape, false); break;
            default: map.CancelConfirm(); break;
        }
        await SettleAsync(kit);
        await SettleAsync(kit);

        Assert.Equal(graphs + 1, kit.Units.GraphReads);
    }

    [Fact]
    public async Task should_read_within_two_seconds_when_notices_keep_coming_every_400ms()
    {
        // ③ ISSUE-30 — 뒤끝 디바운스의 굶주림: 첫 알림 2초 안에 1회(가짜 시계)
        var kit = await ConsoleKit.OpenAsync();
        var graphs = kit.Units.GraphReads;

        for (var i = 0; i < 6; i++)          // 0 · 0.4 · … · 2.0 초 — 창은 한 번도 닫히지 않는다
        {
            await NoticeAsync(kit);
            kit.Clock.Advance(TimeSpan.FromMilliseconds(400));
        }
        await kit.Console.ExternalChangeTask.WaitAsync(ConsoleKit.Timeout);

        Assert.Equal(graphs + 1, kit.Units.GraphReads);
    }

    [Fact]
    public async Task should_read_graph_only_and_keep_edits_when_person_presses_read_again()
    {
        // ④ 결정 D-2026-09-27-215b6d — dirty 면 그림 불변 + 띠, [다시 읽기] = 편제 1회 + 그림 갱신(편집은 그대로)
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(6);
        kit.Console.Form.Description = "손댄 설명";
        kit.Units.AddNode(9, "c09", "Company", 7);
        await NoticeAsync(kit);
        await SettleAsync(kit);
        Assert.True(kit.Console.IsExternallyChanged);
        Assert.Equal(8, kit.Console.Map.Scene.Positions.Count);
        var (graphs, loads) = (kit.Units.GraphReads, kit.Devices.Loads);

        await kit.Console.ReadExternalChangeAsync();

        Assert.Equal(graphs + 1, kit.Units.GraphReads);
        Assert.Equal(loads, kit.Devices.Loads);
        Assert.Equal(9, kit.Console.Map.Scene.Positions.Count);
        Assert.True(kit.Console.Detail.IsDirty);
        Assert.Equal("손댄 설명", kit.Console.Form.Description);
        Assert.False(kit.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_drop_selection_clear_form_and_say_so_when_selected_unit_vanishes_on_any_reload()
    {
        // ⑤ ISSUE-29 — 어느 재조회 뒤든 RestoreSelection 한 곳
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(8);
        kit.Units.Remove(8);

        await kit.Console.ReloadAsync(CancellationToken.None, quiet: true, bypassGuard: true);

        Assert.Null(kit.Console.SelectedRow);
        Assert.Null(kit.Console.Map.SelectedUnitId);
        Assert.Equal(UnitConsoleViewModel.SELECTED_UNIT_GONE, kit.Console.StatusText);
    }

    [Fact]
    public async Task should_reread_graph_before_confirmed_move_when_notice_was_deferred()
    {
        // ⑧ ISSUE-25 — 확인 중 미뤄 둔 알림이 있으면 확정 직전 /graph 재조회 후 재판정
        var kit = await ConsoleKit.OpenAsync();
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false));
        await NoticeAsync(kit);
        await SettleAsync(kit);
        var graphs = kit.Units.GraphReads;

        await kit.Console.Map.ConfirmAsync().WaitAsync(ConsoleKit.Timeout);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);

        Assert.True(kit.Units.GraphReads >= graphs + 1);
        Assert.Single(kit.Units.Patches);
        Assert.Equal(7, kit.Console.Tree.Find(6)!.ParentId);
    }

    [Fact]
    public async Task should_not_send_when_fresh_graph_shows_target_deleted_elsewhere()
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false));
        kit.Units.Remove(5);
        kit.Units.Remove(7);
        await NoticeAsync(kit, 7);
        await SettleAsync(kit);

        await kit.Console.Map.ConfirmAsync().WaitAsync(ConsoleKit.Timeout);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);

        Assert.Empty(kit.Units.Patches);
        Assert.Null(kit.Console.Map.PendingConfirm);
    }
}
