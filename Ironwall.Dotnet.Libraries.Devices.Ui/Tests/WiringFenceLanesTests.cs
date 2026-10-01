using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>두 줄(레인) 결선 — 뷰모델 길(속성 칸 · 메뉴 · 개념도 키보드가 부르는 같은 길 · fence-wiring-editor v0.3 FR-18 ~ FR-21).</summary>
public class WiringFenceLanesTests
{
    /// <summary>S = 스마트 복합센서2 · F = 펜스센서 · 센서 101…(서버 체인 1…N · 번호 1101…).</summary>
    private static (WiringViewModel Vm, FakeFenceStore Store) Build(string types = "SSSS")
    {
        var gateway = new WiringFakeGateway();
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < types.Length; i++)
        {
            var id = 101 + i;
            var type = types[i] == 'S' ? "SmartSensor2" : "Fence";
            var dto = WiringDoubles.ServerSensor(id, 1101 + i, i + 1, new WiringPlacement(1, i + 1));
            dto.TypeDevice = type;
            gateway.Fetched[id] = dto;
            seeds.Add(new WiringSensorSeed(id, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", type, "북측 7구간"), new WiringPlacement(1, i + 1)));
        }
        var store = new FakeFenceStore();
        var apply = new WiringApplyService(gateway, policy: WiringDoubles.AxisPolicy());
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"), seeds,
            new[] { "SmartSensor2", "Fence" }, apply, new WiringFakeDialogs { Confirm = true }, fence: new WiringFenceContext(null, store, null));
        return (vm, store);
    }

    private static List<int> Numbers(WiringViewModel vm) => vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number).ToList();

    [Fact]
    public void should_reverse_the_chain_and_renumber_from_the_right_when_the_controller_moves_to_the_right_end()
    {
        // Arrange — 3차 대역: 아래 줄 1 · 2 · 3 · 4(왼쪽부터)
        var (vm, _) = Build();
        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);

        // Act
        var moved = vm.ChooseControllerRight();

        // Assert — 사슬은 오른쪽 끝부터, 번호도 오른쪽부터 1
        Assert.True(moved);
        Assert.Equal(new[] { 104, 103, 102, 101 }, vm.FenceChain.Keys);
        Assert.Equal(new[] { 1, 2, 3, 4 }, Numbers(vm));
        Assert.Equal(4, vm.Board.Find(101)!.Facts.Number);
        Assert.Contains("번호 ", vm.StatusText);
        Assert.True(vm.IsControllerRight);

        vm.Undo();
        Assert.Equal(new[] { 101, 102, 103, 104 }, vm.FenceChain.Keys);                     // 되돌리기 한 걸음
    }

    [Fact]
    public void should_move_the_selected_sensor_to_the_upper_lane_and_renumber_when_bands_are_set()
    {
        // Arrange
        var (vm, _) = Build();
        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);
        vm.FenceSelect(102);

        // Act
        var ok = vm.FenceSetLaneUpper();

        // Assert — 사슬 = 아래 줄 101 · 103 · 104 → 위 줄 102. 번호는 줄마다 제어기에서 멀어지며(아래 1 · 2 · 3 → 위 4)
        Assert.True(ok);
        Assert.Equal(FenceLane.Upper, vm.FenceLayout.LaneOf(102));
        Assert.Equal(new[] { 101, 103, 104, 102 }, vm.FenceChain.Keys);
        Assert.Equal(new[] { 1, 2, 3, 4 }, Numbers(vm));
        Assert.True(vm.IsLaneUpper);
    }

    [Fact]
    public void should_change_lane_without_touching_numbers_when_no_band_is_chosen()
    {
        var (vm, _) = Build();
        vm.FenceSelect(101);

        vm.FenceSetLaneUpper();

        Assert.Equal(new[] { 1102, 1103, 1104, 1101 }, Numbers(vm));                      // 대역이 없으면 번호를 매기지 않는다
    }

    [Fact]
    public void should_reorder_within_the_lane_when_a_concept_chip_steps_right_with_alt_arrow()
    {
        var (vm, _) = Build();

        var ok = vm.ConceptLaneStep(102, +1);

        Assert.True(ok);
        Assert.Equal(new[] { 101, 103, 102, 104 }, vm.FenceChain.Keys);
        Assert.Equal(FenceLane.Lower, vm.FenceLayout.LaneOf(102));
    }

    [Fact]
    public void should_put_a_dropped_sensor_between_the_upper_lane_neighbours_when_dragged_across_lanes()
    {
        // Arrange — 103 · 104 를 위 줄로 올려 두고
        var (vm, _) = Build("SSSSSS");
        vm.FenceSetLane(new[] { 104, 105 }, FenceLane.Upper);

        // Act — 101 을 위 줄 왼쪽에서 1번째 틈(104 와 105 사이)에
        var ok = vm.ConceptLaneDrop(new[] { 101 }, FenceLane.Upper, 1);

        // Assert
        Assert.True(ok);
        Assert.Equal(FenceLane.Upper, vm.FenceLayout.LaneOf(101));
        Assert.Equal(new[] { 104, 101, 105 }, vm.LaneKeysLeftToRight(FenceLane.Upper));
        Assert.Equal(new[] { 102, 103, 106, 105, 101, 104 }, vm.FenceChain.Keys);            // 아래 줄 → 먼 끝에서 위 줄 돌아오기
    }

    [Fact]
    public void should_show_a_vbus_marker_between_the_middle_sensors_only_for_smart_composite_rings()
    {
        var (smart, _) = Build("SSSSSS");
        var (fence, _) = Build("FFFF");

        var moved = smart.SetVbusGap(5);

        Assert.True(smart.HasVbus);
        Assert.False(fence.HasVbus);
        Assert.True(moved);
        Assert.Equal(5, smart.VbusGap);
        smart.Undo();
        Assert.Equal(3, smart.VbusGap);                                                    // 기본 = 가운데 두 센서 사이
    }

    [Theory]
    [InlineData("Controller", "SmartSensor2", "Fence")]
    [InlineData("Controller", "Multi", "Multi")]
    [InlineData("SmartController", "SmartSensor2", "Multi")]
    [InlineData("SmartController", "Fence", "Fence")]
    public void should_not_show_a_vbus_marker_on_pids_or_mixed_rings(string controllerType, string typeA, string typeB)
    {
        var seeds = Enumerable.Range(0, 6).Select(i => new WiringSensorSeed(201 + i, i + 1,
            new SensorFacts(1 + i, $"센서 {i + 1}", i % 2 == 0 ? typeA : typeB, "서측"), new WiringPlacement(1, i + 1))).ToList();

        var vm = WiringViewModel.ForController(new WiringControllerInfo(20, 2, "PIDS-서측-02", "10.99.8.2", controllerType), seeds,
            new[] { typeA, typeB }, null, new WiringFakeDialogs(), fence: new WiringFenceContext(null, new FakeFenceStore(), null));

        Assert.False(vm.HasVbus);
    }

    [Fact]
    public void should_put_the_vbus_between_the_two_middle_sensors_of_a_smart_composite_ring_by_default()
    {
        var (vm, _) = Build("SSSSSS");

        var gap = vm.VbusGap;

        Assert.True(vm.HasVbus);
        Assert.Equal(3, gap);                                                              // 3번째 센서(103) 뒤 = 103 과 104 사이
        Assert.Equal(new[] { 103, 104 }, vm.FenceChain.Keys.Skip(gap - 1).Take(2));
    }

    [Fact]
    public async Task should_save_lanes_controller_end_and_vbus_in_the_local_layout_document()
    {
        // Arrange
        var (vm, store) = Build("SSSS");
        vm.FenceSelect(104);
        vm.FenceSetLaneUpper();
        vm.ChooseControllerRight();
        vm.SetVbusGap(1);

        // Act
        await vm.SaveAsync();

        // Assert
        var saved = Assert.Single(store.Saved);
        Assert.Equal(FenceLayoutDocument.SCHEMA, saved.Schema);
        Assert.Equal(FenceControllerEnd.Right, saved.ControllerEnd);
        Assert.Equal(1, saved.VbusGap);
        Assert.Equal(FenceLane.Upper, saved.Mounts[104].Lane);
    }

    [Theory]
    [InlineData(FenceControllerEnd.Left)]
    [InlineData(FenceControllerEnd.Right)]
    public void should_load_a_stored_two_lane_layout_without_reordering_or_renumbering_when_the_server_chain_follows_the_lanes(FenceControllerEnd end)
    {
        // Arrange — 참고 그림 ①: 아래 줄 스마트 1~3 · 위 줄 펜스센서 101~103(같은 기둥) · 4차 대역. 서버 사슬은 두 줄 규칙 그대로.
        var mounts = new Dictionary<int, SensorMountSpec>();
        for (var i = 0; i < 3; i++)
        {
            mounts[11 + i] = new SensorMountSpec(i, FenceMountSpot.PostTop);
            mounts[21 + i] = new SensorMountSpec(i, FenceMountSpot.PostTop, Lane: FenceLane.Upper);
        }
        var order = FenceLayoutMath.ChainOrder(mounts.Select(p => (p.Key, p.Value)), end).ToList();
        var numbering = FenceLayoutMath.NumberingOrder(order, k => mounts[k].Lane);
        var numbers = NumberingMath.Assign(numbering.Select(k => (k, k > 20 ? FenceSensorCategory.Fence : FenceSensorCategory.Smart)), NumberBandSet.Tier4);
        var seeds = order.Select((id, i) => new WiringSensorSeed(id, i + 1, new SensorFacts(numbers[id], $"센서 {id}", id > 20 ? "Fence" : "SmartSensor2", "북측"),
            new WiringPlacement(1, i + 1))).ToList();
        var document = new FenceLayoutDocument
        {
            ControllerId = 10, Panels = Enumerable.Repeat(FencePanelSpec.Default(), 2).ToList(), Mounts = mounts, Bands = NumberBandSet.Tier4,
            ControllerEnd = end, Revision = 1,
        };

        // Act
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "PIDS", "10.99.8.1", "Controller"), seeds, new[] { "SmartSensor2", "Fence" },
            null, new WiringFakeDialogs(), fence: new WiringFenceContext(document, new FakeFenceStore(), null));

        // Assert
        Assert.Equal(order, vm.FenceChain.Keys);
        Assert.Equal(string.Empty, vm.FenceNoticeText);
        Assert.False(vm.HasChanges);
        Assert.Equal(end, vm.FenceControllerEnd);
        Assert.Equal(new[] { 11, 12, 13 }, vm.LaneKeysLeftToRight(FenceLane.Lower));                // 공간 순서는 늘 왼쪽 → 오른쪽
        var leftmostUpper = vm.LaneKeysLeftToRight(FenceLane.Upper)[0];
        Assert.Equal(end == FenceControllerEnd.Left ? 101 : 103, vm.Board.Find(leftmostUpper)!.Facts.Number);   // 제어기 쪽부터 101
    }

    [Theory]
    [InlineData(NumberBandSet.PRESET_TIER4)]
    [InlineData(NumberBandSet.PRESET_TIER3)]
    public void should_say_how_many_fence_sensors_moved_into_the_coil_when_a_band_is_first_chosen_on_razor_panels(string preset)
    {
        // Arrange — 윤형 망(보드에 바로 — 망 모양 단추의 따라 붙이기 없이) · 펜스센서 3대는 아직 아래 줄 망 가운데
        var (vm, _) = Build("SFSFF");
        vm.Board.ApplyFenceEdit(l => l.WithPanels(l.Panels.Select(p => p with { Style = EnumFenceStyle.ChainLinkRazor })));

        // Act
        vm.ChooseBandPreset(preset);

        // Assert — 프리셋과 무관하게 윤형 망의 펜스센서만 위 줄 · 윤형 코일
        Assert.Contains("펜스센서 3대를 위 줄 · 윤형 코일로 옮겼습니다", vm.StatusText);
        var fences = vm.FenceChain.Keys.Where(k => vm.Board.CategoryOf(k) == FenceSensorCategory.Fence).ToList();
        Assert.All(fences, k => Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), (vm.FenceLayout.MountOf(k)!.Spot, vm.FenceLayout.LaneOf(k))));
        vm.Undo();
        Assert.All(vm.FenceChain.Keys, k => Assert.Equal(FenceLane.Lower, vm.FenceLayout.LaneOf(k)));   // 한 번에 취소
    }

    [Fact]
    public void should_leave_fence_sensors_on_the_lower_lane_when_the_tier4_preset_is_chosen_without_razor_panels()
    {
        var (vm, _) = Build("SFSFF");

        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER4);

        Assert.All(vm.FenceChain.Keys, k => Assert.Equal(FenceLane.Lower, vm.FenceLayout.LaneOf(k)));
        Assert.DoesNotContain("윤형 코일", vm.StatusText);
    }

    [Fact]
    public void should_say_that_lanes_followed_the_chain_seats_when_a_table_move_crosses_the_lane_boundary()
    {
        // Arrange — 104 만 위 줄: 사슬 101 · 102 · 103 | 104
        var (vm, _) = Build("SSSS");
        vm.FenceSetLane(new[] { 104 }, FenceLane.Upper);

        // Act — 표에서 101 을 사슬 끝(위 줄 자리)으로
        vm.FencePlace(new[] { 101 }, WiringSpec.LINE_PRIMARY, 4);

        // Assert — 줄은 사슬 자리를 따라 넘겨졌고, 상태 줄이 그렇게 말한다
        Assert.Equal(FenceLane.Upper, vm.FenceLayout.LaneOf(101));
        Assert.Equal(FenceLane.Lower, vm.FenceLayout.LaneOf(104));
        Assert.Contains("줄이 바뀐 센서 2대", vm.StatusText);
    }

    [Fact]
    public void should_offer_upper_and_lower_lane_items_in_the_sensor_menu()
    {
        var (vm, _) = Build();

        var menu = vm.FenceMenu(FenceMenuTargetKind.Sensor, 101).Where(e => !e.IsSeparator).ToList();

        Assert.Contains(menu, e => e.Text == "위 줄로" && e.IsEnabled);
        Assert.Contains(menu, e => e.Text == "아래 줄로" && !e.IsEnabled);                  // 이미 아래 줄
    }
}
