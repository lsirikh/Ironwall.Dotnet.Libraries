using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 보드 안에서 펜스 구성과 체인이 맞물리는가(fence-wiring-editor FR-09 · FR-10 · FR-12) — 자리가 바뀌면 체인이, 체인이 바뀌면 자리가 따라가고
/// 번호 대역이 있으면 번호가 다시 매겨진다. 되돌리기 한 걸음이 셋을 함께 되돌린다.
/// </summary>
public class WiringFenceLayoutBoardTests
{
    /// <summary>센서 101…(스마트 · 펜스 섞임 가능) 을 체인 1…N 으로 실은 보드 + 6m 망 위 기둥 0…N−1 자리.</summary>
    private static WiringBoard Board(string types = "SSSSS", NumberBandSet? bands = null)
    {
        var board = new WiringBoard();
        board.Load(types.Select((t, i) => (
            Id: 101 + i,
            Channel: (int?)(i + 1),
            Facts: new SensorFacts(1101 + i, $"북측 {i + 1}", t == 'S' ? "SmartSensor2" : "Fence", "북측"),
            Placement: (WiringPlacement?)new WiringPlacement(1, i + 1),
            Issue: (string?)null,
            Groups: (IReadOnlyList<int>?)null)), "SmartController");
        var panels = Enumerable.Repeat(FencePanelSpec.Default(), types.Length + 1).ToList();
        var mounts = board.Chain.Keys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => new SensorMountSpec(t.i, FenceMountSpot.PostTop));
        board.LoadFenceLayout(WiringFenceLayout.Create(panels, mounts, bands));
        return board;
    }

    [Fact]
    public void should_reorder_the_chain_by_position_when_a_sensor_is_moved_to_a_later_post()
    {
        // Arrange
        var board = Board();

        // Act — 101(기둥 0)을 기둥 3 과 4 사이 망 3 가운데로
        board.PushUndo();
        var changed = board.ApplyFenceEdit(l => l.WithMounts(new Dictionary<int, SensorMountSpec>(l.Mounts) { [101] = new(3, FenceMountSpot.PanelCenter) }));

        // Assert
        Assert.True(changed);
        Assert.Equal(new[] { 102, 103, 104, 101, 105 }, board.Chain.Keys);
        Assert.Equal(new WiringPlacement(1, 4), board.PlacementOf(101));
        Assert.True(board.IsDirty);                                           // 체인 순서 = 서버 저장 대상
    }

    [Fact]
    public void should_change_only_the_layout_when_a_sensor_moves_within_its_own_gap()
    {
        var board = Board();

        var changed = board.ApplyFenceEdit(l => l.WithMounts(new Dictionary<int, SensorMountSpec>(l.Mounts) { [103] = new(2, FenceMountSpot.PostMiddle) }));

        Assert.True(changed);
        Assert.Equal(new[] { 101, 102, 103, 104, 105 }, board.Chain.Keys);
        Assert.False(board.IsDirty);                                          // 서버에 갈 것은 없다
        Assert.True(board.IsFenceDirty);                                      // 로컬 구성만 바뀌었다
    }

    [Fact]
    public void should_let_the_fence_seats_follow_when_the_chain_is_reordered_from_the_table()
    {
        var board = Board();

        board.PushUndo();
        board.MoveBy(101, +1);                                               // Alt+→ — 101 과 102 가 자리를 바꾼다

        Assert.Equal(new[] { 102, 101, 103, 104, 105 }, board.Chain.Keys);
        Assert.Equal(0, board.FenceLayout.MountOf(102)!.Panel);
        Assert.Equal(1, board.FenceLayout.MountOf(101)!.Panel);
    }

    [Fact]
    public void should_renumber_by_band_and_undo_numbers_seats_and_order_in_one_step_when_bands_are_chosen()
    {
        // Arrange — 4차: 스마트 1~ · 펜스 101~ · 섞인 링 S F S F S
        var board = Board("SFSFS");
        board.PushUndo();
        board.SetNumberBands(NumberBandSet.Tier4);
        var afterBands = board.Rows.Select(r => r.Facts.Number).ToList();

        // Act — 101(스마트 1)을 끝 기둥으로 옮긴다
        board.PushUndo();
        board.ApplyFenceEdit(l => l.WithMounts(new Dictionary<int, SensorMountSpec>(l.Mounts) { [101] = new(5, FenceMountSpot.PostTop) }));
        var moved = board.Rows.ToDictionary(r => r.Key, r => r.Facts.Number);
        var movedChain = board.Chain.Keys.ToList();
        board.Undo();

        // Assert
        Assert.Equal(new[] { 1, 101, 2, 102, 3 }, afterBands);
        Assert.Equal(new[] { 102, 103, 104, 105, 101 }, movedChain);
        Assert.Equal(3, moved[101]);                                          // 끝 스마트 = 스마트 3
        Assert.Equal(1, moved[103]);                                          // 103 이 첫 스마트가 됐다
        Assert.Equal(new[] { 101, 102, 103, 104, 105 }, board.Chain.Keys);    // 되돌리기 한 번 = 순서 · 자리 · 번호
        Assert.Equal(afterBands, board.Rows.Select(r => r.Facts.Number));
        Assert.Equal(0, board.FenceLayout.MountOf(101)!.Panel);
    }

    [Fact]
    public void should_list_before_and_after_numbers_only_for_changed_server_sensors()
    {
        var board = Board("SSS");
        board.SetNumberBands(NumberBandSet.Tier3);

        var changes = board.NumberChanges();

        Assert.Equal(new[] { "북측 1 1101 → 1", "북측 2 1102 → 2", "북측 3 1103 → 3" }, changes.Select(c => c.Text));
        Assert.Equal(3, board.Diff().FactChanged.Count);                     // 번호는 number_device 로 저장된다
    }

    [Fact]
    public void should_seat_a_palette_sensor_after_the_last_one_and_grow_the_fence_when_appended()
    {
        var board = Board("SSS");
        board.UnplaceMany(new[] { 103 });

        board.Append(new[] { 103 });

        Assert.Equal(new[] { 101, 102, 103 }, board.Chain.Keys);
        Assert.Equal(new SensorMountSpec(2, FenceMountSpot.PostTop), board.FenceLayout.MountOf(103));   // 앞 이웃(기둥 1) 다음 기둥
    }

    [Fact]
    public void should_block_saving_through_validation_when_a_band_overflows()
    {
        var board = Board("SSSS");
        board.SetNumberBands(new NumberBandSet(NumberBandSet.PRESET_CUSTOM, new[] { new NumberBand(FenceSensorCategory.Smart, 1, 3) }));

        var issues = WiringValidation.Evaluate(board);

        Assert.True(WiringValidation.BlocksSave(issues));
        Assert.Contains(issues, i => i.Code == WiringValidation.CODE_NUMBERING && i.Level == WiringIssueLevel.Critical);
    }

    [Fact]
    public void should_leave_the_fence_alone_when_the_layout_is_not_active()
    {
        var board = WiringDoubles.Board(4, placedOnFirst: 4);

        board.MoveBy(101, +1);

        Assert.False(board.FenceLayout.IsActive);
        Assert.Empty(board.FenceLayout.Mounts);
        Assert.Equal(1101, board.Find(101)!.Facts.Number);                   // 대역 없음 = 번호 그대로
        Assert.False(board.IsFenceDirty);
    }

    [Fact]
    public void should_mark_the_fence_clean_and_drop_the_proposal_flag_when_saved_locally()
    {
        var board = Board();
        board.LoadFenceLayout(WiringFenceLayout.Create(board.FenceLayout.Panels, board.FenceLayout.Mounts, null, isProposed: true));
        board.ApplyFenceEdit(l => l.WithPanels(l.Panels.Select(p => p with { HeightM = 3.0 })));
        var dirty = board.IsFenceDirty;

        board.MarkFenceSaved();

        Assert.True(dirty);
        Assert.False(board.IsFenceDirty);
        Assert.False(board.FenceLayout.IsProposed);
        Assert.All(board.FenceLayout.Panels, p => Assert.Equal(3.0, p.HeightM));
    }

    [Fact]
    public void should_change_the_spot_of_sensors_on_a_panel_turned_into_a_wall()
    {
        var board = Board("SSS");

        board.ApplyFenceEdit(l => l.WithPanels(l.Panels.Select((p, i) => i < 2 ? p with { Style = EnumFenceStyle.Brick } : p)));

        Assert.Equal(new SensorMountSpec(0, FenceMountSpot.WallTop), board.FenceLayout.MountOf(101));   // 기둥 0 은 서지 않는다
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.WallTop), board.FenceLayout.MountOf(102));
        Assert.Equal(FenceMountSpot.PostTop, board.FenceLayout.MountOf(103)!.Spot);                     // 기둥 2 는 펜스 망 옆
        Assert.Equal(new[] { 101, 102, 103 }, board.Chain.Keys);
    }
}
