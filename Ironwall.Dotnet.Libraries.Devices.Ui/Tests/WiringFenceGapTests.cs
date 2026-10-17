using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 헤드리스 판(2026-10-08 · 시나리오 카탈로그 §6) — 헤디드에서 GAP 이던 항목을 뷰모델 길로 잡는다. 창은 띄우지 않는다.
/// 이름 주석의 <c>WIR-nnn</c> 은 카탈로그 항목 id 다.
/// </summary>
internal static class WiringGapRig
{
    /// <summary>
    /// 센서 101…(서버 체인 1…N · 번호 1101…). 종류 글자: S = 스마트2(IP) · F = 펜스(RS485) · M = 복합 · U = 지진동.
    /// </summary>
    public static (WiringViewModel Vm, WiringFakeDialogs Dialogs, FakeFenceStore Store, WiringFakeGateway Gateway) Build(
        string types = "SSSS", FenceLayoutDocument? document = null, bool withStore = true, FakePing? ping = null, string address = "10.99.7.1",
        string controllerType = "SmartController")
    {
        var gateway = new WiringFakeGateway();
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < types.Length; i++)
        {
            var id = 101 + i;
            var type = types[i] switch { 'F' => "Fence", 'M' => "Multi", 'U' => "Underground", _ => "SmartSensor2" };
            var ip = types[i] == 'S';
            var dto = WiringDoubles.ServerSensor(id, 1101 + i, i + 1, new WiringPlacement(1, i + 1));
            dto.TypeDevice = type;
            gateway.Fetched[id] = dto;
            seeds.Add(new WiringSensorSeed(id, ip ? null : i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", type, "북측 7구간"),
                new WiringPlacement(1, i + 1),
                ConnectionType: ip ? "IP_DIRECT" : "RS485",
                IpAddress: ip ? $"192.168.10.{i + 1}" : null));
        }
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var store = new FakeFenceStore { Stored = document };
        var apply = new WiringApplyService(gateway, policy: WiringDoubles.AxisPolicy());
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", address, controllerType), seeds,
            new[] { "SmartSensor2", "Fence", "Multi", "Underground" }, apply, dialogs, fence: new WiringFenceContext(document, withStore ? store : null, ping));
        return (vm, dialogs, store, gateway);
    }

    /// <summary>망 하나를 골라 모양을 바꿔 적용한다.</summary>
    public static void Style(WiringViewModel vm, int panel, EnumFenceStyle style)
    {
        vm.FenceSelectPanel(panel);
        vm.ChoosePanelStyle(style);
        Assert.True(vm.ApplyPanelEdit());
    }
}

public class WiringFenceGapTests
{
    #region - 망 속성 (WIR-105 · 107 · 109 · 111) -
    [Fact]
    [Trait("Scenario", "WIR-105")]
    public async Task should_refuse_a_malformed_direct_colour_and_take_a_well_formed_one_when_the_colour_is_typed_WIR105()
    {
        var (vm, dialogs, _, _) = WiringGapRig.Build();
        vm.FenceSelectPanel(0);

        dialogs.TextAnswer = "#12G";
        await vm.AskPanelColorAsync();
        var refused = (vm.StatusText, vm.PanelColorText, vm.HasPanelEdit);

        dialogs.TextAnswer = "#4e565e";
        await vm.AskPanelColorAsync();
        var taken = vm.PanelColorText;
        var applied = vm.ApplyPanelEdit();

        Assert.StartsWith("색은 #RRGGBB", refused.StatusText);
        Assert.Equal("기본색", refused.PanelColorText);                                   // 잘못된 값은 칸에 들어가지 않는다
        Assert.False(refused.HasPanelEdit);
        Assert.Equal("#4E565E", taken, ignoreCase: true);
        Assert.True(applied);
        Assert.Equal("#4E565E", vm.FenceLayout.Panels[0].Color, ignoreCase: true);
        Assert.Null(vm.FenceLayout.Panels[1].Color);                                       // 고른 망만
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [Trait("Scenario", "WIR-107")]
    public void should_light_the_chosen_span_chip_and_change_the_total_length_when_a_span_preset_is_applied_WIR107(int metres)
    {
        var (vm, _, _, _) = WiringGapRig.Build();
        vm.FenceSelectPanel(0);
        var before = vm.FenceLayout.Geometry.LengthM;

        vm.ChoosePanelSpan(metres);
        var lit = new[] { vm.IsPanelSpan1, vm.IsPanelSpan2, vm.IsPanelSpan3, vm.IsPanelSpan4, vm.IsPanelSpan5, vm.IsPanelSpan6 };
        var applied = vm.ApplyPanelEdit();

        Assert.Equal(Enumerable.Range(1, 6).Select(i => i == metres), lit);
        Assert.Equal(metres == 6 ? false : true, applied);                                 // 기본 6m 이면 바뀐 것이 없다
        Assert.Equal(before - 6 + metres, vm.FenceLayout.Geometry.LengthM, 3);
        Assert.Contains($"길이 {vm.FenceLayout.Geometry.LengthM:0.#}m", vm.PanelTotalText);
    }

    [Theory]
    [InlineData("25", false)]
    [InlineData("0.4", false)]
    [InlineData("abc", false)]
    [InlineData("0.5", true)]
    [InlineData("20", true)]
    [InlineData("7.5", true)]
    [Trait("Scenario", "WIR-107")]
    public void should_accept_a_typed_span_only_inside_half_to_twenty_metres_WIR107(string text, bool ok)
    {
        var (vm, _, _, _) = WiringGapRig.Build();
        vm.FenceSelectPanel(1);

        vm.PanelSpanText = text;

        Assert.Equal(!ok, vm.HasPanelEditError);
        Assert.Equal(ok, vm.CanApplyPanelEdit);
        if (!ok) Assert.Equal("망 간 거리는 0.5~20m 로 적어 주세요.", vm.PanelEditError);
        Assert.Equal(ok, vm.ApplyPanelEdit());
    }

    [Fact]
    [Trait("Scenario", "WIR-109")]
    public void should_title_the_pane_with_the_panel_count_when_panels_are_ctrl_clicked_WIR109()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSSS");

        vm.FenceTogglePanel(1);
        vm.FenceTogglePanel(3);
        var two = (vm.PanelSelectionTitle, vm.ApplyPanelEditText, vm.FencePaneKind);
        vm.FenceTogglePanel(1);                                                             // Ctrl+클릭 다시 = 빼기

        Assert.Equal(("선택한 망 2칸", "선택한 망에 적용 (2칸)", FenceSelectionKind.Panels), two);
        Assert.Equal(new[] { 3 }, vm.FenceSelectedPanels);
        Assert.Equal("선택한 망 1칸", vm.PanelSelectionTitle);
    }

    [Fact]
    [Trait("Scenario", "WIR-111")]
    public void should_extend_toggle_and_select_all_panels_the_way_the_keyboard_does_WIR111()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSSSSS");                                 // 망 6칸
        var count = vm.FenceLayout.Panels.Count;

        vm.FenceSelectPanel(2);
        vm.FenceExtendPanelSelection(4);                                                    // Shift+→ ×2
        var range = vm.FenceSelectedPanels.OrderBy(i => i).ToList();
        vm.FenceExtendPanelSelection(0);                                                    // Shift+Home — 기준(2)에서 반대쪽 끝까지
        var back = vm.FenceSelectedPanels.OrderBy(i => i).ToList();
        vm.FenceTogglePanel(1);                                                             // Ctrl+Space — 빼기
        var toggled = vm.FenceSelectedPanels.OrderBy(i => i).ToList();
        vm.FenceSelectAllPanels();                                                          // Ctrl+A(망 칸)

        Assert.Equal(new[] { 2, 3, 4 }, range);
        Assert.Equal(new[] { 0, 1, 2 }, back);
        Assert.Equal(new[] { 0, 2 }, toggled);
        Assert.Equal(Enumerable.Range(0, count), vm.FenceSelectedPanels.OrderBy(i => i));
        Assert.Equal($"망 {count}칸 선택", vm.StatusText);
    }
    #endregion

    #region - 센서 선택 · 빼기 (WIR-116 · 127) -
    [Fact]
    [Trait("Scenario", "WIR-116")]
    public void should_grow_and_shrink_the_sensor_selection_with_ctrl_toggle_and_shift_range_WIR116()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSSS");

        vm.FenceSelect(102);
        vm.FenceToggleSelect(104);
        var two = vm.FenceSelectedKeys.ToList();
        vm.FenceExtendSensorSelection(105);                                                 // 기준 102 → 105
        var range = vm.FenceSelectedKeys.OrderBy(k => k).ToList();
        vm.FenceToggleSelect(103);

        Assert.Equal(new[] { 102, 104 }, two);
        Assert.Equal(new[] { 102, 103, 104, 105 }, range);
        Assert.Equal(new[] { 102, 104, 105 }, vm.FenceSelectedKeys.OrderBy(k => k));
        Assert.Equal("센서 3대", vm.MountPlaceText);
    }

    [Fact]
    [Trait("Scenario", "WIR-127")]
    public void should_say_the_bin_hint_while_dragging_and_send_sensors_to_the_palette_when_dropped_on_the_bin_WIR127()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");

        var idle = vm.BinHintText;
        vm.NotifySensorDrag(true);
        var dragging = vm.BinHintText;
        var removed = vm.FenceUnplace(new[] { 102, 103 });
        vm.NotifySensorDrag(false);

        Assert.Equal("빼는 곳", idle);
        Assert.Equal("여기 놓으면 결선에서 빠집니다", dragging);
        Assert.True(removed);
        Assert.Equal(new[] { 101, 104 }, vm.FenceChain.Keys);
        Assert.Equal(2, vm.UnplacedCount);
        Assert.Equal("미배치 2", vm.UnplacedStripText);
        Assert.True(vm.IsUnplacedStripExpanded);
        Assert.Equal("빼는 곳", vm.BinHintText);
        vm.Undo();
        Assert.Equal(new[] { 101, 102, 103, 104 }, vm.FenceChain.Keys);
        Assert.Equal("미배치 0 · 전부 결선에 붙었습니다 ✓", vm.UnplacedStripText);
    }
    #endregion

    #region - 3×3 고르개 · 오프셋 · 자리 (WIR-131 · 136 · 137) -
    public static IEnumerable<object[]> NineCells()
        => from c in new[] { FenceColumn.Left, FenceColumn.Center, FenceColumn.Right } from r in new[] { 0, 1, 2 } select new object[] { c, r };

    [Theory]
    [MemberData(nameof(NineCells))]
    [Trait("Scenario", "WIR-131")]
    public void should_seat_a_panel_sensor_on_each_of_the_nine_grid_points_WIR131(FenceColumn column, int row)
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelect(102);
        vm.ChooseMountSpot(FenceMountSpot.PanelCenter);
        var panel = vm.FenceLayout.MountOf(102)!.Panel;
        var start = vm.FenceGridCellOf(102);

        var ok = vm.FenceSetGridPoint(column, row);
        var mount = vm.FenceLayout.MountOf(102)!;

        Assert.Equal(panel, mount.Panel);                                                   // 같은 망 안에서만
        Assert.Equal(column, mount.Column);
        var moved = vm.FenceGridCellOf(102);
        Assert.NotNull(moved);
        if (ok) Assert.NotEqual(start, moved);
        else Assert.Equal(start, moved);                                                    // 이미 그 점이면 제자리
    }

    [Fact]
    [Trait("Scenario", "WIR-131")]
    public void should_put_nine_different_grid_points_inside_one_panel_WIR131()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelect(102);
        vm.ChooseMountSpot(FenceMountSpot.PanelCenter);

        var cells = NineCells().Select(a =>
        {
            vm.FenceSetGridPoint((FenceColumn)a[0], (int)a[1]);
            return vm.FenceGridCellOf(102);
        }).ToList();

        Assert.Equal(9, cells.Distinct().Count());                                          // 9점이 서로 다른 격자 칸
        Assert.Equal(3, cells.Select(c => c!.Value.Gx).Distinct().Count());
        Assert.Equal(3, cells.Select(c => c!.Value.Gy).Distinct().Count());
    }

    [Theory]
    [InlineData("0.5", true, 0.5)]
    [InlineData("-3", true, -3.0)]
    [InlineData("3", true, 3.0)]
    [InlineData("5", false, 0.0)]
    [InlineData("-3.5", false, 0.0)]
    [InlineData("높이", false, 0.0)]
    [Trait("Scenario", "WIR-136")]
    public void should_apply_an_offset_only_between_minus_three_and_plus_three_metres_WIR136(string text, bool ok, double expected)
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelect(103);

        vm.MountOffsetText = text;
        var applied = vm.ApplyMountOffset();

        Assert.Equal(ok, applied);
        Assert.Equal(expected, vm.FenceLayout.MountOf(103)!.HeightOffsetM, 3);
        if (!ok) Assert.Equal("높이 조정은 -3~+3m 로 적어 주세요.", vm.StatusText);
        Assert.Equal(expected.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), vm.MountOffsetText);   // 거부되면 칸이 원래 값으로
    }

    [Fact]
    [Trait("Scenario", "WIR-137")]
    public void should_seat_every_mount_spot_button_and_name_it_in_the_place_text_WIR137()
    {
        // 망 0 철조망 · 망 1 윤형 · 망 2 벽돌 — 펜스센서로 철조망 자리 · 윤형 코일 · 담 자리를 모두 지난다
        var (vm, _, _, _) = WiringGapRig.Build("FFFF");
        WiringGapRig.Style(vm, 1, EnumFenceStyle.ChainLinkRazor);
        WiringGapRig.Style(vm, 2, EnumFenceStyle.Brick);
        var onPanel0 = vm.FenceLayout.Mounts.First(p => p.Value.Panel == 0 && !p.Value.IsPostSpot).Key;

        vm.FenceSelect(onPanel0);
        var fenceSpots = new[] { FenceMountSpot.PostTop, FenceMountSpot.PostMiddle, FenceMountSpot.PanelCenter, FenceMountSpot.PanelBottom }
            .Select(s => (Spot: s, Ok: vm.ChooseMountSpot(s), Now: vm.FenceLayout.MountOf(onPanel0)!.Spot, vm.MountPlaceText)).ToList();

        Assert.All(fenceSpots, r => Assert.Equal(r.Spot, r.Now));
        Assert.All(fenceSpots, r => Assert.EndsWith(SensorMountSpec.SpotText(r.Spot), r.MountPlaceText));
        Assert.Contains(fenceSpots, r => r.MountPlaceText.StartsWith("기둥"));
        Assert.Contains(fenceSpots, r => r.MountPlaceText.StartsWith("망"));

        // 윤형 망 위 펜스센서 — [윤형 코일] 단추가 보이고 누르면 위 줄
        var onRazor = vm.FenceLayout.Mounts.First(p => p.Value.Panel == 1 && !p.Value.IsPostSpot).Key;
        vm.FenceSelect(onRazor);
        Assert.True(vm.IsMountOnRazor);
        vm.ChooseMountSpot(FenceMountSpot.RazorCoil);
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), (vm.FenceLayout.MountOf(onRazor)!.Spot, vm.FenceLayout.LaneOf(onRazor)));

        // 벽돌 담 위 — 단추가 담 위 · 담 앞면으로
        var onWall = vm.FenceLayout.Mounts.First(p => p.Value.Panel == 2 && !p.Value.IsPostSpot).Key;
        vm.FenceSelect(onWall);
        vm.ChooseMountSpot(FenceMountSpot.WallTop);
        var top = (vm.FenceLayout.MountOf(onWall)!.Spot, vm.IsMountOnWall, vm.IsSpotWallTop);
        vm.ChooseMountSpot(FenceMountSpot.WallFace);
        Assert.Equal((FenceMountSpot.WallTop, true, true), top);
        Assert.Equal(FenceMountSpot.WallFace, vm.FenceLayout.MountOf(onWall)!.Spot);
        Assert.True(vm.IsSpotWallFace);
    }
    #endregion

    #region - 방향 (WIR-139 · 142) -
    [Fact]
    [Trait("Scenario", "WIR-139")]
    public void should_turn_counter_clockwise_and_pick_each_of_the_four_yaws_WIR139()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSS");
        vm.FenceSelect(102);
        var start = vm.Board.YawOf(102);

        vm.FenceRotateSelected(-1);                                                         // ⟲ = Shift+R
        var ccw = vm.Board.YawOf(102);
        vm.FenceRotateSelected(1);
        var back = vm.Board.YawOf(102);
        var picks = new[] { WiringYaw.Away, WiringYaw.Along, WiringYaw.Toward, WiringYaw.Against }
            .Select(y => { vm.FenceSetYaw(y); return (y, vm.SelectedYaw, vm.SelectedYawText); }).ToList();

        Assert.Equal(WiringYawMath.Rotate(start, -1), ccw);
        Assert.NotEqual(start, ccw);
        Assert.Equal(start, back);
        Assert.All(picks, p => Assert.Equal(p.y, p.SelectedYaw));
        Assert.All(picks, p => Assert.EndsWith($"{(int)p.y}°", p.SelectedYawText));
    }

    [Fact]
    [Trait("Scenario", "WIR-142")]
    public void should_leave_fence_sensors_unturned_and_disable_the_turn_menu_when_the_kind_has_no_facing_WIR142()
    {
        var (vm, _, _, _) = WiringGapRig.Build("FFS");
        vm.FenceSelect(101);
        var facing = vm.Board.FacingOf(101);

        var flipped = vm.FenceFlipFacing(101);                                              // F
        var rotated = vm.FenceRotate(101, 1);                                               // R
        var menu = vm.FenceMenu(FenceMenuTargetKind.Sensor, 101).ToDictionary(e => e.AutomationId[(e.AutomationId.LastIndexOf('.') + 1)..]);
        var smartMenu = vm.FenceMenu(FenceMenuTargetKind.Sensor, 103).ToDictionary(e => e.AutomationId[(e.AutomationId.LastIndexOf('.') + 1)..]);

        Assert.False(flipped);
        Assert.False(rotated);
        Assert.Equal(facing, vm.Board.FacingOf(101));
        Assert.False(vm.HasChanges);
        Assert.False(menu["RotateCw"].IsEnabled);
        Assert.False(menu["RotateCcw"].IsEnabled);
        Assert.False(menu["SideOutside"].IsEnabled);
        Assert.False(menu["SideInside"].IsEnabled);
        Assert.True(smartMenu["RotateCw"].IsEnabled);                                       // 스마트(방향 있음)는 켜짐
    }
    #endregion

    #region - 메뉴 (WIR-145 ~ 149) -
    private static IReadOnlyDictionary<string, FenceMenuEntry> Menu(WiringViewModel vm, FenceMenuTargetKind kind, int target)
        => vm.FenceMenu(kind, target).Where(e => !e.IsSeparator).ToDictionary(e => e.AutomationId[(e.AutomationId.LastIndexOf('.') + 1)..]);

    [Fact]
    [Trait("Scenario", "WIR-145")]
    public async Task should_spread_the_mount_style_only_to_the_same_kind_when_apply_same_type_is_run_WIR145()
    {
        var (vm, dialogs, _, _) = WiringGapRig.Build("SFSFS");
        vm.FenceSelect(101);
        vm.ChooseMountSpot(FenceMountSpot.PostMiddle);

        var menu = Menu(vm, FenceMenuTargetKind.Sensor, 101);
        await menu["ApplySameType"].Run!();

        Assert.Contains("3대", menu["ApplySameType"].Text);
        Assert.Contains(dialogs.Confirms, c => c.Title == "설치 방식 적용" && c.Message.StartsWith("3대 중 설치 방식 2대"));
        Assert.All(new[] { 101, 103, 105 }, k => Assert.Equal(FenceMountSpot.PostMiddle, vm.FenceLayout.MountOf(k)!.Spot));
        Assert.All(new[] { 102, 104 }, k => Assert.NotEqual(FenceMountSpot.PostMiddle, vm.FenceLayout.MountOf(k)!.Spot));
    }

    [Fact]
    [Trait("Scenario", "WIR-145")]
    public async Task should_spread_the_mount_style_only_to_the_selection_and_count_it_in_the_menu_text_WIR145()
    {
        var (vm, dialogs, _, _) = WiringGapRig.Build("SSSSS");
        vm.FenceSelect(101);
        vm.ChooseMountSpot(FenceMountSpot.PostMiddle);
        vm.FenceSelectSensors(new[] { 101, 102, 104 });

        var menu = Menu(vm, FenceMenuTargetKind.Sensor, 101);
        await menu["ApplySelected"].Run!();

        Assert.Equal("선택한 센서에 적용 (3대)", menu["ApplySelected"].Text);
        Assert.True(menu["ApplySelected"].IsEnabled);
        Assert.Contains(dialogs.Confirms, c => c.Message.StartsWith("3대 중 설치 방식 2대"));
        Assert.All(new[] { 101, 102, 104 }, k => Assert.Equal(FenceMountSpot.PostMiddle, vm.FenceLayout.MountOf(k)!.Spot));
        Assert.All(new[] { 103, 105 }, k => Assert.Equal(FenceMountSpot.PostTop, vm.FenceLayout.MountOf(k)!.Spot));
    }

    [Fact]
    [Trait("Scenario", "WIR-146")]
    public async Task should_turn_and_flip_through_the_menu_and_confirm_the_all_facing_entry_WIR146()
    {
        var (vm, dialogs, _, _) = WiringGapRig.Build("SSS");
        var yaw = vm.Board.YawOf(102);

        await Menu(vm, FenceMenuTargetKind.Sensor, 102)["RotateCw"].Run!();
        var turned = vm.Board.YawOf(102);
        await Menu(vm, FenceMenuTargetKind.Sensor, 102)["SideInside"].Run!();
        var inside = vm.Board.FacingOf(102);
        await Menu(vm, FenceMenuTargetKind.Sensor, 102)["FacingBack"].Run!();

        Assert.Equal(WiringYawMath.Rotate(yaw, 1), turned);
        Assert.Equal(WiringFacing.Back, inside);
        Assert.Contains(dialogs.Confirms, c => c.Title == "방향 적용" && c.Message.StartsWith("3대 중 2대가 바뀝니다"));
        Assert.All(vm.FenceChain.Keys, k => Assert.Equal(WiringFacing.Back, vm.Board.FacingOf(k)));
        Assert.Contains("내부로", Menu(vm, FenceMenuTargetKind.Sensor, 101).Values.Select(e => e.Text));
        Assert.False(Menu(vm, FenceMenuTargetKind.Sensor, 101)["SideInside"].IsEnabled);    // 이미 안쪽이면 꺼짐
    }

    [Fact]
    [Trait("Scenario", "WIR-147")]
    public async Task should_move_lanes_and_unplace_through_the_sensor_menu_WIR147()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        var lowerOnly = Menu(vm, FenceMenuTargetKind.Sensor, 103);

        await lowerOnly["LaneUpper"].Run!();
        var upper = vm.FenceLayout.LaneOf(103);
        var title = vm.ConceptTitle;
        var afterMenu = Menu(vm, FenceMenuTargetKind.Sensor, 103);
        await afterMenu["LaneLower"].Run!();
        var lower = vm.FenceLayout.LaneOf(103);
        await Menu(vm, FenceMenuTargetKind.Sensor, 103)["Unplace"].Run!();

        Assert.False(lowerOnly["LaneLower"].IsEnabled);                                     // 이미 아래 줄
        Assert.Equal(FenceLane.Upper, upper);
        Assert.Contains("위 줄 1", title);
        Assert.False(afterMenu["LaneUpper"].IsEnabled);
        Assert.Equal(FenceLane.Lower, lower);
        Assert.DoesNotContain(103, vm.FenceChain.Keys);
        Assert.Equal(1, vm.UnplacedCount);
        Assert.Equal("Delete", lowerOnly["Unplace"].Gesture);
    }

    [Fact]
    [Trait("Scenario", "WIR-148")]
    public async Task should_copy_a_panel_and_spread_it_to_all_panels_through_the_panel_menu_WIR148()
    {
        var (vm, dialogs, _, _) = WiringGapRig.Build("SSSSS");
        WiringGapRig.Style(vm, 2, EnumFenceStyle.Concrete);

        var menu = Menu(vm, FenceMenuTargetKind.Panel, 2);
        await menu["CopyPanel"].Run!();
        var copied = vm.StatusText;
        await menu["PanelToAll"].Run!();
        var all = vm.FenceLayout.Panels.Select(p => p.Style).Distinct().ToList();
        vm.Undo();

        Assert.StartsWith("망 3 속성을 복사했습니다 — 시멘트", copied);
        Assert.False(menu["PastePanel"].IsEnabled);                                         // 복사 전 · 고른 망 없음
        Assert.Contains(dialogs.Confirms, c => c.Title == "모든 망에 적용" && c.Message.StartsWith($"{vm.FenceLayout.Panels.Count}칸 중 {vm.FenceLayout.Panels.Count - 1}칸이 바뀝니다"));
        Assert.Equal(new[] { EnumFenceStyle.Concrete }, all);
        Assert.Single(vm.FenceLayout.Panels, p => p.Style == EnumFenceStyle.Concrete);     // 되돌리기 한 번으로 통째
    }

    [Fact]
    [Trait("Scenario", "WIR-149")]
    public async Task should_select_all_sensors_or_all_panels_from_the_empty_ground_menu_WIR149()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        var menu = Menu(vm, FenceMenuTargetKind.Empty, -1);

        await menu["SelectAllSensors"].Run!();
        var sensors = vm.FenceSelectedKeys.ToList();
        await menu["SelectAllPanels"].Run!();

        Assert.Equal(new[] { "SelectAllSensors", "SelectAllPanels" }, menu.Keys);
        Assert.Equal("Ctrl+A", menu["SelectAllSensors"].Gesture);
        Assert.Equal(new[] { 101, 102, 103, 104 }, sensors);
        Assert.Equal(Enumerable.Range(0, vm.FenceLayout.Panels.Count), vm.FenceSelectedPanels.OrderBy(i => i));
        Assert.Equal(FenceSelectionKind.Panels, vm.FencePaneKind);
    }
    #endregion

    #region - 도구줄 · 함체 · 사슬 끝 · 대수 (WIR-155 · 157 · 164 · 165 · 166) -
    [Fact]
    [Trait("Scenario", "WIR-155")]
    public void should_keep_the_range_toggle_off_without_range_sensors_and_on_with_a_multi_sensor_WIR155()
    {
        var (smart, _, _, _) = WiringGapRig.Build("SSS");
        smart.ToggleRange();
        var (multi, _, _, _) = WiringGapRig.Build("MSF", controllerType: "PidsController");
        multi.ToggleRange();
        var on = multi.ShowRange;
        multi.ToggleRange();

        Assert.False(smart.HasRangeSensors);
        Assert.False(smart.ShowRange);                                                      // 켜지지 않는다
        Assert.True(multi.HasRangeSensors);
        Assert.True(on);
        Assert.False(multi.ShowRange);
    }

    [Fact]
    [Trait("Scenario", "WIR-157")]
    public void should_move_the_enclosure_only_as_a_marker_and_undo_it_WIR157()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSSS");
        var chain = vm.FenceChain.Keys.ToList();
        var numbers = chain.Select(k => vm.Board.Find(k)!.Facts.Number).ToList();
        var gap = vm.Board.Chain.ControllerGap;

        vm.MoveEnclosureForward();                                                          // Alt+→ (함체 선택)
        var moved = (vm.Board.Chain.ControllerGap, vm.StatusText, vm.EnclosureGapText);
        vm.Undo();

        Assert.Equal(gap + 1, moved.ControllerGap);
        Assert.StartsWith("함체 이동 — ", moved.StatusText);
        Assert.Contains("체인 순서 · A/B 번호 그대로", moved.StatusText);
        Assert.Equal(chain, vm.FenceChain.Keys);                                            // 사슬 · 번호는 그대로
        Assert.Equal(numbers, chain.Select(k => vm.Board.Find(k)!.Facts.Number));
        Assert.Equal(gap, vm.Board.Chain.ControllerGap);
    }

    [Fact]
    [Trait("Scenario", "WIR-164")]
    public void should_jump_to_either_end_of_the_chain_with_alt_home_and_alt_end_WIR164()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSSS");

        var end = vm.FenceToEnd(102, end: true);
        var atEnd = vm.FenceChain.Keys.ToList();
        var home = vm.FenceToEnd(104, end: false);

        Assert.True(end);
        Assert.Equal(new[] { 101, 103, 104, 105, 102 }, atEnd);
        Assert.True(home);
        Assert.Equal(new[] { 104, 101, 103, 105, 102 }, vm.FenceChain.Keys);
    }

    [Fact]
    [Trait("Scenario", "WIR-165")]
    public void should_offer_the_spacing_combo_only_with_fence_sensors_and_not_count_it_as_a_server_change_WIR165()
    {
        var (smart, _, _, _) = WiringGapRig.Build("SSS");
        var (mixed, _, _, gateway) = WiringGapRig.Build("FFFS");

        mixed.FenceSpacingMetres = 2.5;

        Assert.False(smart.HasFenceSensors);
        Assert.True(mixed.HasFenceSensors);
        Assert.Equal(new[] { 2, 2.5, 3, 3.5, 4 }, mixed.FenceSpacingChoices);
        Assert.Equal(2.5, mixed.FenceSpacingMetres);
        Assert.Equal("바뀐 줄 0 · 펜스 구성 바뀜", mixed.DraftText);                         // 서버 바뀐 줄이 아니다
        Assert.True(mixed.HasLocalChanges);                                                 // 이 PC 의 펜스 구성에만
        Assert.Contains("2.5m 로 다시 놓았습니다", mixed.StatusText);
        Assert.Equal(0, gateway.PatchCount);
    }

    [Fact]
    [Trait("Scenario", "WIR-166")]
    public void should_show_the_kind_counts_only_in_the_fence_view_of_the_wiring_step_WIR166()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SFF");
        vm.GoWiring();

        var fence = vm.IsFenceCountsShown;
        vm.ShowTableView();
        var table = vm.IsFenceCountsShown;
        vm.ShowFenceView();
        vm.GoSensors();

        Assert.True(fence);
        Assert.False(table);
        Assert.False(vm.IsFenceCountsShown);
        Assert.StartsWith("스마트 1 · 펜스 2 · 센서 3 · 체인 3 · 미배치 0", vm.FenceCountsText);
    }
    #endregion

    #region - 나눔 · 알림 (WIR-159 · 161) -
    [Theory]
    [InlineData(0.10, double.NaN, 0.25)]
    [InlineData(0.95, double.NaN, 0.85)]
    [InlineData(double.NaN, double.NaN, 0.58)]
    [InlineData(0.30, 400, 0.40)]                                                           // 160px / 400 — 3D 최소
    [InlineData(0.85, 400, 0.625)]                                                          // 1 − 150/400 — 개념도 최소
    [InlineData(0.70, 250, 0.5)]                                                            // 둘 다 못 지키면 가운데
    [Trait("Scenario", "WIR-159")]
    public void should_clamp_the_split_ratio_to_the_range_and_the_minimum_heights_WIR159(double ratio, double total, double expected)
        => Assert.Equal(expected, WiringLayoutMath.ClampSplit(ratio, total), 3);

    [Fact]
    [Trait("Scenario", "WIR-159")]
    public void should_start_from_the_remembered_split_and_save_each_move_WIR159()
    {
        var (vm, _, _, _) = WiringGapRig.Build();
        var saved = new List<(double, bool)>();
        vm.UseLayoutPrefs(0.7, false, (r, open) => saved.Add((r, open)));

        var start = (vm.FenceSplitRatio, vm.IsDetailPaneOpen);
        vm.SetFenceSplitRatio(0.5);
        vm.SetFenceSplitRatio(0.5);                                                         // 같은 값은 다시 남기지 않는다

        Assert.Equal((0.7, false), start);
        Assert.Equal(new[] { (0.5, false) }, saved);
    }

    [Fact]
    [Trait("Scenario", "WIR-161")]
    public void should_order_notices_action_first_then_warnings_then_information_and_drop_blank_ones_WIR161()
    {
        var notices = new[]
        {
            new WiringNotice(WiringNoticeKind.Fence, "펜스 안내", false),
            new WiringNotice(WiringNoticeKind.Legacy, "변환 경고", true),
            new WiringNotice(WiringNoticeKind.Topology, " ", false),
            new WiringNotice(WiringNoticeKind.Bands, "대역", false),
            new WiringNotice(WiringNoticeKind.Applied, "함께 적용", true),
            new WiringNotice(WiringNoticeKind.Suggestion, "제안", false),
        };

        var ordered = WiringLayoutMath.OrderNotices(notices);

        Assert.Equal(new[] { WiringNoticeKind.Suggestion, WiringNoticeKind.Bands, WiringNoticeKind.Applied, WiringNoticeKind.Legacy, WiringNoticeKind.Fence },
                     ordered.Select(n => n.Kind));
        Assert.Equal(new[] { "이대로 적용", "대역 고르기", "", "", "" }, ordered.Select(n => n.ActionText));
        Assert.Equal("알림 5", WiringLayoutMath.NoticeChipText(ordered.Count));
        Assert.Equal(string.Empty, WiringLayoutMath.NoticeChipText(0));
    }
    #endregion

    #region - 신호등 (WIR-221 · 222) -
    [Fact]
    [Trait("Scenario", "WIR-221")]
    public void should_say_unknown_without_an_address_and_without_a_ping_probe_WIR221()
    {
        var (noAddress, _, _, _) = WiringGapRig.Build("S", ping: new FakePing(), address: " ");
        var (noPing, _, _, _) = WiringGapRig.Build("S", ping: null);

        Assert.Equal("모름 — 제어기 주소가 없습니다", noAddress.ControllerSignalText);
        Assert.Equal(SignalLevel.Unknown, noAddress.ControllerSignal);
        Assert.Equal("모름 — 이 창에서는 ping 을 쓰지 않습니다", noPing.ControllerSignalText);
    }
    #endregion
}
