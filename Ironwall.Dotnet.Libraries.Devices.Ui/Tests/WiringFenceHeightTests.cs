using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 높이 단계(위 · 아래로 올리고 내리기) · 윤형 코일 설치(펜스센서는 윤형과 같이) — 결선 창 뷰모델 경로(▲▼ · 끌기 · 키보드가 부르는 같은 길).
/// </summary>
public class WiringFenceHeightTests
{
    /// <summary>S = 스마트(기둥 위) · F = 펜스센서(망 가운데) — 키 101 부터. 저장된 펜스가 없어 제안 구성(철조망)으로 연다.</summary>
    internal static WiringViewModel Build(string types)
    {
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i] == 'S' ? "SmartSensor2" : "Fence";
            seeds.Add(new WiringSensorSeed(101 + i, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간", type, "북측 7구간"), new WiringPlacement(1, i + 1)));
        }
        return WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"), seeds,
            new[] { "SmartSensor2", "Fence" }, null, new WiringFakeDialogs { Confirm = true }, fence: new WiringFenceContext(null, new FakeFenceStore(), null));
    }

    /// <summary>망을 모두 윤형으로(보드에 바로 — 센서 자리는 그대로 아래 줄 · 망 가운데).</summary>
    internal static WiringViewModel Razor(WiringViewModel vm)
    {
        vm.Board.ApplyFenceEdit(l => l.WithPanels(l.Panels.Select(p => p with { Style = EnumFenceStyle.ChainLinkRazor })));
        return vm;
    }

    private static (FenceMountSpot Spot, FenceLane Lane) SpotOf(WiringViewModel vm, int key) => (vm.FenceLayout.MountOf(key)!.Spot, vm.FenceLayout.LaneOf(key));

    #region - ▲ ▼ · steps -
    [Fact]
    public void should_climb_a_fence_sensor_into_the_coil_with_the_raise_button_without_moving_it_sideways_and_say_the_lane_changed()
    {
        // Arrange
        var vm = Razor(Build("SFSFF"));
        vm.FenceSelect(102);
        var before = vm.FenceLayout.MountOf(102)!;
        var x = FenceLayoutMath.PointOf(before, vm.FenceLayout.Geometry).XM;

        // Act
        var raised = vm.FenceRaiseSelected();

        // Assert — 망 가운데 → (같은 망) 윤형 코일(위 줄) · 사슬 끝(위 줄)으로 · 가로 자리 그대로
        Assert.True(raised);
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 102));
        Assert.Equal(before.Panel, vm.FenceLayout.MountOf(102)!.Panel);
        Assert.Equal(x, FenceLayoutMath.PointOf(vm.FenceLayout.MountOf(102)!, vm.FenceLayout.Geometry).XM, 9);
        Assert.Equal(102, vm.FenceChain.Keys.Last());
        Assert.Contains("윤형 코일", vm.StatusText);
        Assert.Contains("줄이 바뀐 센서 1대", vm.StatusText);
    }

    [Fact]
    public void should_keep_a_post_sensor_on_its_post_and_stop_at_the_post_top_beside_razor()
    {
        var vm = Razor(Build("SFSFF"));
        vm.FenceSelect(101);
        var post = vm.FenceLayout.MountOf(101)!.Panel;

        Assert.False(vm.FenceRaiseSelected());                                           // 기둥 위가 맨 위(기둥에는 코일 자리가 없다)
        Assert.Contains("맨 위", vm.StatusText);
        Assert.True(vm.FenceLowerSelected());
        Assert.Equal((FenceMountSpot.PostMiddle, FenceLane.Lower), SpotOf(vm, 101));
        Assert.Equal(post, vm.FenceLayout.MountOf(101)!.Panel);
    }

    [Fact]
    public void should_undo_each_step_in_one_go_and_stop_at_the_ends_with_a_message()
    {
        var vm = Razor(Build("SFSFF"));
        vm.FenceSelect(102);
        vm.FenceRaiseSelected();

        Assert.False(vm.FenceRaiseSelected());                                           // 맨 위
        Assert.Contains("맨 위", vm.StatusText);
        vm.Undo();
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), SpotOf(vm, 102));
        Assert.True(vm.FenceLowerSelected());
        Assert.Equal(FenceMountSpot.PanelBottom, SpotOf(vm, 102).Spot);
        Assert.False(vm.FenceLowerSelected());
        Assert.Contains("맨 아래", vm.StatusText);
    }

    [Fact]
    public void should_move_every_selected_sensor_one_stop_together_as_one_undo_step()
    {
        var vm = Razor(Build("SFSFF"));
        vm.FenceSelectSensors(new[] { 102, 104 });

        vm.FenceStepStop(vm.FenceSelectedKeys, 1);

        Assert.All(new[] { 102, 104 }, k => Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, k)));
        Assert.Contains("2대", vm.StatusText);
        vm.Undo();
        Assert.All(new[] { 102, 104 }, k => Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), SpotOf(vm, k)));
    }

    [Fact]
    public void should_snap_a_drop_to_the_chosen_stop_level_and_say_nothing_moved_on_the_same_level()
    {
        var vm = Razor(Build("SFSFF"));

        Assert.Equal(new[] { FenceMountSpot.PanelBottom, FenceMountSpot.PanelCenter, FenceMountSpot.RazorCoil }, vm.FenceHeightStops(102));
        Assert.Equal(new[] { FenceMountSpot.PostMiddle, FenceMountSpot.PostTop }, vm.FenceHeightStops(101));
        Assert.Equal(1, vm.FenceStopLevel(102));
        Assert.Equal("높이: 윤형 코일 · 위 줄로", vm.FenceStopLabel(new[] { 102 }, 102, 2));
        Assert.False(vm.FenceSetStopLevel(new[] { 102 }, 102, 1));
        Assert.Contains("제자리", vm.StatusText);
        Assert.True(vm.FenceSetStopLevel(new[] { 102 }, 102, 2));
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 102));
    }

    [Fact]
    public void should_nudge_the_fine_height_by_a_tenth_without_changing_the_stop()
    {
        var vm = Razor(Build("SFSFF"));
        vm.FenceSelect(102);

        vm.FenceNudgeHeight(new[] { 102 }, FenceLayoutMath.NUDGE_M);
        vm.FenceNudgeHeight(new[] { 102 }, FenceLayoutMath.NUDGE_M);

        Assert.Equal(0.2, vm.FenceLayout.MountOf(102)!.HeightOffsetM, 6);
        Assert.Equal(FenceMountSpot.PanelCenter, SpotOf(vm, 102).Spot);
        Assert.Equal("0.2", vm.MountOffsetText);
    }
    #endregion

    #region - Lanes · spot buttons -
    [Fact]
    public void should_seat_a_fence_sensor_in_the_coil_when_the_concept_moves_it_to_the_upper_lane_and_back_to_the_panel_center()
    {
        var vm = Razor(Build("SFSFF"));

        vm.ConceptLaneChange(102, FenceLane.Upper);
        var up = SpotOf(vm, 102);
        vm.ConceptLaneChange(102, FenceLane.Lower);

        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), up);
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), SpotOf(vm, 102));
    }

    [Fact]
    public void should_seat_a_fence_sensor_dropped_onto_the_upper_lane_in_the_concept_in_the_coil()
    {
        var vm = Razor(Build("SFSFF"));

        vm.ConceptLaneDrop(new[] { 104 }, FenceLane.Upper, 0);

        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 104));
    }

    [Fact]
    public void should_offer_the_coil_button_only_on_razor_and_put_the_sensor_on_the_upper_lane_when_chosen()
    {
        var plain = Build("SFSFF");
        plain.FenceSelect(102);
        Assert.False(plain.IsMountOnRazor);

        var vm = Razor(Build("SFSFF"));
        vm.FenceSelect(102);
        Assert.True(vm.IsMountOnRazor);
        Assert.True(vm.ChooseMountSpot(FenceMountSpot.RazorCoil));

        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 102));
        Assert.True(vm.IsSpotRazorCoil);
        Assert.Equal("망 2 · 윤형 코일", vm.MountPlaceText);
    }
    #endregion

    #region - Several sensors on one wall panel (헤디드 r21) -
    /// <summary>다섯 자리 번호(70066…) 스마트 센서 넷 · 망 1 을 벽돌로 바꾸고 101 · 102 · 103 을 그 담 위에(기둥이 없어 한 점에 모였다).</summary>
    private static WiringViewModel ThreeOnBrick()
    {
        var seeds = Enumerable.Range(0, 4).Select(i => new WiringSensorSeed(101 + i, i + 1, new SensorFacts(70066 + i, $"LRT-{70066 + i}", "SmartSensor2", "북측"),
            new WiringPlacement(1, i + 1))).ToList();
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL", "10.99.7.1", "SmartController"), seeds, new[] { "SmartSensor2" },
            null, new WiringFakeDialogs(), fence: new WiringFenceContext(null, new FakeFenceStore(), null));
        vm.Board.ApplyFenceEdit(l =>
        {
            var panels = l.Panels.Select((p, i) => i == 1 ? FencePanelSpec.Default(EnumFenceStyle.Brick, p.SpanM) : p).ToList();
            var mounts = l.Mounts.ToDictionary(p => p.Key, p => p.Key <= 103 ? new SensorMountSpec(1, FenceMountSpot.WallTop) : p.Value);
            return l.With(panels, mounts);
        });
        return vm;
    }

    [Fact]
    public void should_spread_several_sensors_on_one_wall_panel_evenly_along_the_panel()
    {
        var vm = ThreeOnBrick();
        var layout = vm.FenceLayout;
        var panel = layout.Geometry.Panels[1];

        var xs = FenceLayoutMath.SpreadXs(vm.FenceChain.Keys.Select(k => (k, layout.MountOf(k)!)).ToList(), layout.Geometry);

        Assert.Equal(new[] { 1.0 / 6, 3.0 / 6, 5.0 / 6 }.Select(f => panel.StartM + panel.SpanM * f), new[] { 101, 102, 103 }.Select(k => xs[k]), new ToleranceComparer(1e-9));
    }

    [Fact]
    public void should_keep_the_number_plates_of_three_sensors_on_one_brick_panel_apart_in_the_fence_view()
    {
        var vm = ThreeOnBrick();

        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout);
        var plates = new[] { 101, 102, 103 }.Select(k =>
        {
            var chip = FenceScene.Sensor(world.Sensors[k], world.Shape, FenceProjector.Tilt, false, 1, world.LiftOf(k));
            var plate = chip.Shapes.Single(s => s.Ink == FenceInk.Plate);
            var rect = new System.Windows.Rect(plate.Points[0], plate.Points[1]);
            rect.Offset(world.X[k], 0);
            return rect;
        }).OrderBy(r => r.X).ToList();

        Assert.True(world.X[101] < world.X[102] && world.X[102] < world.X[103]);           // 사슬 차례 그대로 왼쪽 → 오른쪽
        for (var i = 1; i < plates.Count; i++) Assert.False(plates[i - 1].IntersectsWith(plates[i]), $"번호판 {i} · {i + 1} 이 겹친다");
        Assert.True(FenceWorld.ChipGap(world.Sensors.Values) > FenceWorld.MIN_CHIP_DX);     // 다섯 자리 번호판은 40 보다 넓게
    }

    [Fact]
    public void should_keep_concept_chips_and_their_numbers_apart_for_three_sensors_on_one_brick_panel()
    {
        var vm = ThreeOnBrick();

        var g = Consoles.Wiring.Concept.ConceptLayout.Build(vm.ConceptItems(), vm.ConceptPostsM(), vm.ConceptLengthM, vm.FenceControllerEnd, new System.Windows.Size(1100, 150));
        var nodes = g.Nodes.Where(n => n.Lane == FenceLane.Lower).OrderBy(n => n.Center.X).ToList();
        var labels = nodes.Where(n => n.ShowLabel).ToList();

        for (var i = 1; i < nodes.Count; i++) Assert.True(nodes[i].Center.X - nodes[i - 1].Center.X >= g.Chip.Width, "개념도 칩이 겹친다");
        var width = Consoles.Wiring.Concept.ConceptLayout.LabelWidth("70066");
        for (var i = 1; i < labels.Count; i++) Assert.True(labels[i].Center.X - labels[i - 1].Center.X >= width - 1e-6, "개념도 번호가 겹친다");
        Assert.Equal(4, nodes.Count);
    }

    private sealed class ToleranceComparer : IEqualityComparer<double>
    {
        private readonly double _tolerance;
        public ToleranceComparer(double tolerance) => _tolerance = tolerance;
        public bool Equals(double a, double b) => System.Math.Abs(a - b) <= _tolerance;
        public int GetHashCode(double value) => 0;
    }
    #endregion

    #region - Defaults (펜스센서는 윤형과 같이) -
    [Fact]
    public void should_follow_the_panel_style_into_and_out_of_the_coil_only_for_untouched_fence_seats()
    {
        // Arrange — 104 는 손으로 높이 조정(기본 자리가 아니다)
        var vm = Build("SFSFF");
        vm.FenceNudgeHeight(new[] { 104 }, FenceLayoutMath.NUDGE_M);

        // Act — 망을 모두 윤형으로
        vm.FenceSelectAllPanels();
        vm.ChoosePanelStyle(EnumFenceStyle.ChainLinkRazor);
        vm.ApplyPanelEdit();
        var raisedText = vm.StatusText;

        // Assert
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 102));
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 105));
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), SpotOf(vm, 104));       // 손댄 자리는 그대로
        Assert.Equal((FenceMountSpot.PostTop, FenceLane.Lower), SpotOf(vm, 101));           // 스마트는 그대로
        Assert.Contains("펜스센서 2대를 윤형 코일(위 줄)로", raisedText);

        // Act — 다시 철조망으로: 코일 센서는 망 가운데 · 아래 줄
        vm.FenceSelectAllPanels();
        vm.ChoosePanelStyle(EnumFenceStyle.ChainLink);
        vm.ApplyPanelEdit();
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), SpotOf(vm, 102));
        Assert.Contains("코일 센서 2대를 망 가운데(아래 줄)로", vm.StatusText);
    }

    [Fact]
    public void should_never_overwrite_saved_mounts_when_a_stored_razor_layout_is_loaded()
    {
        // Arrange — 저장 문서: 윤형 망 · 펜스센서 102 는 아래 줄 망 가운데로 저장됨(사용자 값) · 104 는 코일(위 줄)
        var panels = Enumerable.Repeat(FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor), 3).ToList();
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [101] = new(0, FenceMountSpot.PostTop),
            [102] = new(0, FenceMountSpot.PanelCenter),
            [103] = new(1, FenceMountSpot.PostTop),
            [104] = new(2, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper),
        };
        var order = FenceLayoutMath.ChainOrder(mounts.Select(p => (p.Key, p.Value)), FenceControllerEnd.Left).ToList();
        var types = new Dictionary<int, string> { [101] = "SmartSensor2", [102] = "Fence", [103] = "SmartSensor2", [104] = "Fence" };
        var seeds = order.Select((id, i) => new WiringSensorSeed(id, i + 1, new SensorFacts(1000 + id, $"센서 {id}", types[id], "북측"), new WiringPlacement(1, i + 1))).ToList();
        var document = new FenceLayoutDocument { ControllerId = 10, Panels = panels, Mounts = mounts, Revision = 1 };

        // Act
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL", "10.99.7.1", "SmartController"), seeds, new[] { "SmartSensor2", "Fence" },
            null, new WiringFakeDialogs(), fence: new WiringFenceContext(document, new FakeFenceStore(), null));

        // Assert — 저장값 그대로 · 바뀐 것 없음
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), SpotOf(vm, 102));
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 104));
        Assert.False(vm.HasChanges);
        Assert.False(vm.HasLocalChanges);
        Assert.Equal(order, vm.FenceChain.Keys);
    }

    [Fact]
    public void should_seat_an_unsaved_fence_sensor_that_joins_the_upper_lane_on_razor_in_the_coil_when_loading()
    {
        // Arrange — 저장 문서에 없는 펜스센서 105 가 서버 사슬에서 위 줄 코일 센서 104 뒤(위 줄)에 있다
        var panels = Enumerable.Repeat(FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor), 3).ToList();
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [101] = new(0, FenceMountSpot.PostTop),
            [104] = new(2, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper),
        };
        var chain = new[] { 101, 104, 105 };
        var types = new Dictionary<int, string> { [101] = "SmartSensor2", [104] = "Fence", [105] = "Fence" };
        var seeds = chain.Select((id, i) => new WiringSensorSeed(id, i + 1, new SensorFacts(1000 + id, $"센서 {id}", types[id], "북측"), new WiringPlacement(1, i + 1))).ToList();
        var document = new FenceLayoutDocument { ControllerId = 10, Panels = panels, Mounts = mounts, Revision = 1 };

        // Act
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL", "10.99.7.1", "SmartController"), seeds, new[] { "SmartSensor2", "Fence" },
            null, new WiringFakeDialogs(), fence: new WiringFenceContext(document, new FakeFenceStore(), null));

        // Assert — 새 센서만 기본값(위 줄 윤형 → 코일) · 저장된 둘은 그대로 · 서버 사슬 순서 그대로
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), SpotOf(vm, 105));
        Assert.Equal(mounts[104], vm.FenceLayout.MountOf(104));
        Assert.Equal(mounts[101], vm.FenceLayout.MountOf(101));
        Assert.Equal(chain, vm.FenceChain.Keys);
    }
    #endregion
}
