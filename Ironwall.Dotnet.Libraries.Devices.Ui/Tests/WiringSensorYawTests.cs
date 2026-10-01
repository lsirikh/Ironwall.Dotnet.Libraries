using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 센서 방향(2026-10-01 · 사용자: "90 정면 90 후면 이렇게 돌릴 수 있어야 되고 펜스 내부 외부도 바꿀 수 있어야 된다") — 설치 면(facing) · 보는 방향(yaw 90° 단계)
/// 모델 · 저장 본문 · 옛 값 · 되돌리기 · 모두 적용 · 그림(옆모습 폭 · 렌즈 쪽 · 철망 뒤).
/// </summary>
public class WiringSensorYawTests
{
    private const EnumDeviceType S = EnumDeviceType.SmartSensor2;

    #region - Model · server -
    [Theory]
    [InlineData("""{"wiring":{"line":1,"order":3}}""", WiringYaw.Away)]                                   // v1 — 표지 · 방향 없음
    [InlineData("""{"wiring":{"v":2,"shape":"ring","line":1,"order":3,"facing":"back"}}""", WiringYaw.Away)]
    [InlineData("""{"wiring":{"v":3,"line":1,"order":3,"facing":"front","yaw":90}}""", WiringYaw.Along)]
    [InlineData("""{"wiring":{"v":3,"line":1,"order":3,"yaw":"270"}}""", WiringYaw.Against)]
    [InlineData("""{"wiring":{"v":3,"line":1,"order":3,"yaw":-90}}""", WiringYaw.Against)]
    [InlineData("""{"wiring":{"v":3,"line":1,"order":3,"yaw":450}}""", WiringYaw.Along)]
    [InlineData("""{"wiring":{"v":3,"line":1,"order":3,"yaw":175.5}}""", WiringYaw.Toward)]
    [InlineData("""{"wiring":{"v":3,"line":1,"order":3,"yaw":"sideways"}}""", WiringYaw.Away)]
    [InlineData("""{"wiring":{"v":3,"line":1,"order":3,"yaw":null}}""", WiringYaw.Away)]
    public void should_read_yaw_as_the_nearest_90_degree_step_and_zero_when_missing(string json, WiringYaw expected)
        => Assert.Equal(expected, WiringSpec.Read(JObject.Parse(json))!.Yaw);

    [Fact]
    public void should_keep_reading_the_old_facing_word_as_the_mount_side()
    {
        var back = WiringSpec.Read(JObject.Parse("""{"wiring":{"v":3,"line":1,"order":2,"facing":"back"}}"""))!;

        Assert.Equal((WiringFacing.Back, WiringYaw.Away), (back.Facing, back.Yaw));
        Assert.Equal("내부", WiringYawMath.SideText(back.Facing));
    }

    [Fact]
    public void should_write_facing_and_yaw_together_without_nulls_and_keep_the_format_version()
    {
        var patch = WiringSpec.MergePatch(new WiringPlacement(1, 4, WiringFacing.Back, WiringYaw.Against), WiringShape.Ring, includeFacing: true);
        var fence = WiringSpec.MergePatch(new WiringPlacement(1, 4, WiringFacing.Back, WiringYaw.Against), WiringShape.Ring, includeFacing: false);

        var wiring = (JObject)patch["wiring"]!;
        Assert.Equal(3, (int)wiring["v"]!);
        Assert.Equal("back", (string)wiring["facing"]!);
        Assert.Equal(270, (int)wiring["yaw"]!);
        Assert.DoesNotContain(wiring.Properties(), p => p.Value.Type == JTokenType.Null);
        Assert.Null(fence["wiring"]!["yaw"]);                                                    // 방향이 없는 종류는 싣지 않는다
        var round = WiringSpec.Read(WiringSpec.Apply(null, new WiringPlacement(1, 4, WiringFacing.Back, WiringYaw.Along), WiringShape.Ring, includeFacing: true))!;
        Assert.Equal((WiringFacing.Back, WiringYaw.Along), (round.Facing, round.Yaw));
    }

    [Fact]
    public void should_treat_a_yaw_change_as_a_wiring_change_but_not_as_a_moved_placement()
    {
        var a = new WiringPlacement(1, 2);
        var b = a with { Yaw = WiringYaw.Along };

        Assert.True(WiringSpec.SamePlacement(a, b));
        Assert.False(WiringSpec.SameWiring(a, b));
    }

    [Theory]
    [InlineData(WiringYaw.Away, 1, WiringYaw.Along)]
    [InlineData(WiringYaw.Away, -1, WiringYaw.Against)]
    [InlineData(WiringYaw.Against, 1, WiringYaw.Away)]
    [InlineData(WiringYaw.Along, 2, WiringYaw.Against)]
    [InlineData(WiringYaw.Toward, 4, WiringYaw.Toward)]
    public void should_rotate_in_90_degree_steps_and_wrap(WiringYaw from, int steps, WiringYaw expected)
        => Assert.Equal(expected, WiringYawMath.Rotate(from, steps));

    [Fact]
    public async Task should_patch_facing_and_yaw_when_only_the_yaw_changed()
    {
        // Arrange — 서버 값에는 yaw 가 없다(옛 v3)
        var board = new WiringBoard();
        board.Load(new[] { 1, 2 }.Select(order => (100 + order, (int?)order, new SensorFacts(1100 + order, $"센서 {order}", "SmartSensor2", ""),
            (WiringPlacement?)new WiringPlacement(1, order), (string?)null, (IReadOnlyList<int>?)null)), "SmartController");
        var gateway = new WiringFakeGateway();
        foreach (var order in new[] { 1, 2 })
            gateway.Fetched[100 + order] = new SensorDeviceDto
            {
                Id = 100 + order, NumberDevice = 1100 + order, NameDevice = $"센서 {order}", TypeDevice = "SmartSensor2", Status = "ACTIVATED", IsEnable = true, ControllerId = 10,
                HardwareSpec = new HardwareSpecDto { Spec = JObject.Parse("{\"wiring\":{\"v\":3,\"line\":1,\"order\":" + order + ",\"facing\":\"front\"}}") },
            };

        // Act
        board.SetYaw(new[] { 102 }, WiringYaw.Along);
        var result = await new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()).ApplyAsync(10, board);

        // Assert — 102 만 · facing 도 함께(병합 PATCH)
        Assert.True(result.IsSuccess);
        var (id, dto) = Assert.Single(gateway.Patched);
        Assert.Equal(102, id);
        var wiring = (JObject)JObject.Parse(JsonConvert.SerializeObject(dto, PresetRequestBuilder.WireSettings)).SelectToken("hardware_spec.spec.wiring")!;
        Assert.Equal(("front", 90, 2), ((string)wiring["facing"]!, (int)wiring["yaw"]!, (int)wiring["order"]!));
    }
    #endregion

    #region - View model -
    private static WiringViewModel Smart(int count = 4) => WiringFenceHeightTests.Build(new string('S', count));

    [Fact]
    public void should_rotate_the_selected_sensor_with_the_buttons_and_undo_in_one_step()
    {
        var vm = Smart();
        vm.FenceSelect(102);

        vm.FenceRotateSelected(1);
        var once = (vm.Board.YawOf(102), vm.SelectedYawText, vm.IsYawAlong);
        var status = vm.StatusText;
        vm.FenceRotateSelected(1);
        vm.Undo();

        Assert.Equal((WiringYaw.Along, "정방향 90°", true), once);
        Assert.StartsWith("보는 방향 — ", status);
        Assert.Equal(WiringYaw.Along, vm.Board.YawOf(102));
        vm.Undo();
        Assert.Equal(WiringYaw.Away, vm.Board.YawOf(102));
        Assert.True(vm.HasChanges is false);
    }

    [Fact]
    public void should_set_every_selected_sensor_to_the_picked_direction_and_count_them_as_changed_rows()
    {
        var vm = Smart();
        vm.FenceSelectSensors(new[] { 101, 103 });

        vm.FenceSetYaw(WiringYaw.Against);

        Assert.All(new[] { 101, 103 }, k => Assert.Equal(WiringYaw.Against, vm.Board.YawOf(k)));
        Assert.Equal(WiringYaw.Away, vm.Board.YawOf(102));
        Assert.Contains("2대", vm.StatusText);
        Assert.Equal(2, vm.DraftCountOf());
        Assert.Contains("방향 바뀜 2건", vm.ChangePreview);
    }

    [Fact]
    public void should_refuse_to_rotate_a_fence_sensor()
    {
        var vm = WiringFenceHeightTests.Build("SFS");

        Assert.False(vm.FenceRotate(102, 1));
        Assert.Contains("방향이 없습니다", vm.StatusText);
    }

    [Fact]
    public void should_flip_the_mount_side_and_keep_the_yaw()
    {
        var vm = Smart();
        vm.FenceSelect(101);
        vm.FenceSetYaw(WiringYaw.Along);

        vm.FenceFlipFacing(101);

        Assert.Equal((WiringFacing.Back, WiringYaw.Along), (vm.Board.FacingOf(101), vm.Board.YawOf(101)));
        Assert.Contains("펜스 내부", vm.StatusText);
    }

    [Fact]
    public async Task should_spread_the_side_and_yaw_with_the_mount_style_and_count_them_in_the_confirm()
    {
        // Arrange — 101: 내부 · 역방향
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL", "10.99.7.1", "SmartController"),
            Enumerable.Range(0, 4).Select(i => new WiringSensorSeed(101 + i, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}", "SmartSensor2", "북측"), new WiringPlacement(1, i + 1))).ToList(),
            new[] { "SmartSensor2" }, null, dialogs, fence: new WiringFenceContext(null, new FakeFenceStore(), null));
        vm.Board.SetFacing(new[] { 101 }, WiringFacing.Back);
        vm.Board.SetYaw(new[] { 101 }, WiringYaw.Against);

        // Act
        var ok = await vm.ApplyMountStyleAsync(FenceApplyScope.All, 101);

        // Assert
        Assert.True(ok);
        Assert.All(new[] { 102, 103, 104 }, k => Assert.Equal((WiringFacing.Back, WiringYaw.Against), (vm.Board.FacingOf(k), vm.Board.YawOf(k))));
        var message = dialogs.Confirms.Last().Message;
        Assert.Contains("설치 방식 3대", message);
        Assert.Contains("펜스 내부 · 역방향(270°)", message);
        vm.Undo();
        Assert.All(new[] { 102, 103, 104 }, k => Assert.Equal((WiringFacing.Front, WiringYaw.Away), (vm.Board.FacingOf(k), vm.Board.YawOf(k))));
    }

    [Fact]
    public void should_offer_rotate_and_side_menu_items_for_the_targeted_sensor()
    {
        var vm = Smart();

        var menu = vm.FenceMenu(FenceMenuTargetKind.Sensor, 102);
        menu.Single(e => e.AutomationId.EndsWith(".RotateCw")).Run!().GetAwaiter().GetResult();
        var afterCw = vm.Board.YawOf(102);
        vm.FenceMenu(FenceMenuTargetKind.Sensor, 102).Single(e => e.AutomationId.EndsWith(".SideInside")).Run!().GetAwaiter().GetResult();

        Assert.Equal(WiringYaw.Along, afterCw);
        Assert.Equal(WiringFacing.Back, vm.Board.FacingOf(102));
        Assert.False(vm.FenceMenu(FenceMenuTargetKind.Sensor, 102).Single(e => e.AutomationId.EndsWith(".SideInside")).IsEnabled);   // 이미 내부
        Assert.Equal("R", menu.Single(e => e.AutomationId.EndsWith(".RotateCw")).Gesture);
    }

    [Fact]
    public void should_put_the_side_and_yaw_in_the_concept_node_tooltip_text_only()
    {
        var vm = Smart();
        vm.Board.SetFacing(new[] { 103 }, WiringFacing.Back);
        vm.Board.SetYaw(new[] { 103 }, WiringYaw.Along);

        var node = vm.ConceptNodes().Single(n => n.Key == 103);

        Assert.Equal("내부 · 정방향(90°)", node.OrientationText);
    }
    #endregion

    #region - Rendering -
    private static FenceSensor Sensor(EnumDeviceType type, WiringFacing facing = WiringFacing.Front, WiringYaw yaw = WiringYaw.Away)
        => new(1, 1, 1001, "센서", type, null, 1, 1, null, false, false, false, facing, yaw);

    private static Rect FrontFace(FenceChipPicture picture)
    {
        // 몸 앞면 = 가장 큰 OliveFront 사각형
        var face = picture.Shapes.Where(s => s.Ink == FenceInk.OliveFront).OrderByDescending(s => Span(s.Points).Height).First();
        return Span(face.Points);
    }

    private static Rect Span(IEnumerable<Point> points)
    {
        var list = points.ToList();
        return new Rect(new Point(list.Min(p => p.X), list.Min(p => p.Y)), new Point(list.Max(p => p.X), list.Max(p => p.Y)));
    }

    [Theory]
    [InlineData(EnumDeviceType.SmartSensor2, 0.0)]
    [InlineData(EnumDeviceType.SmartSensor2, 1.0)]
    [InlineData(EnumDeviceType.Multi, 1.0)]
    public void should_draw_a_narrow_side_profile_with_the_lens_on_the_looking_end_at_90_and_270(EnumDeviceType type, double k)
    {
        var p = new FenceProjector(k);

        var front = FenceScene.Sensor(Sensor(type), WiringShape.Ring, p, false);
        var along = FenceScene.Sensor(Sensor(type, yaw: WiringYaw.Along), WiringShape.Ring, p, false);
        var against = FenceScene.Sensor(Sensor(type, yaw: WiringYaw.Against), WiringShape.Ring, p, false);

        var w0 = FrontFace(front).Width;
        Assert.InRange(FrontFace(along).Width / w0, 0.3, 0.6);                                   // 옆모습은 좁다
        Assert.InRange(FrontFace(against).Width / w0, 0.3, 0.6);
        var edgeAlong = Span(along.Shapes.Single(s => s.Ink == FenceInk.LensEdge).Points);
        var edgeAgainst = Span(against.Shapes.Single(s => s.Ink == FenceInk.LensEdge).Points);
        Assert.True(edgeAlong.X > FrontFace(along).X + FrontFace(along).Width / 2, "정방향 렌즈는 오른쪽 끝");
        Assert.True(edgeAgainst.X < FrontFace(against).X + FrontFace(against).Width / 2, "역방향 렌즈는 왼쪽 끝");
        Assert.DoesNotContain(front.Shapes, s => s.Ink == FenceInk.LensEdge);                    // 정면은 지금 모습
    }

    [Fact]
    public void should_hide_the_lens_and_show_a_plain_back_panel_at_180()
    {
        var back = FenceScene.Sensor(Sensor(S, yaw: WiringYaw.Toward), WiringShape.Ring, FenceProjector.Tilt, false);
        var front = FenceScene.Sensor(Sensor(S), WiringShape.Ring, FenceProjector.Tilt, false);

        Assert.DoesNotContain(back.Shapes, s => s.Ink == FenceInk.Pir);
        Assert.Contains(back.Shapes, s => s.Ink == FenceInk.BackSeam);
        Assert.Contains(front.Shapes, s => s.Ink == FenceInk.Pir);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void should_draw_an_inside_sensor_behind_the_mesh_with_mesh_lines_over_it_and_an_inside_tag(double k)
    {
        var p = new FenceProjector(k);

        var outside = FenceScene.Sensor(Sensor(S), WiringShape.Ring, p, false);
        var inside = FenceScene.Sensor(Sensor(S, WiringFacing.Back), WiringShape.Ring, p, false);

        Assert.True(FrontFace(inside).Top < FrontFace(outside).Top - 4, "내부는 철망 너머(위로) 밀린다");
        if (k > 0) Assert.True(FrontFace(inside).Left > FrontFace(outside).Left + 4, "입체에서는 깊이만큼 오른쪽으로");
        Assert.Contains(inside.Shapes, s => s.Ink == FenceInk.MeshOver);
        Assert.DoesNotContain(outside.Shapes, s => s.Ink == FenceInk.MeshOver);
        Assert.Contains(inside.Shapes, s => s.Ink == FenceInk.FacingTagText && s.Text == "내");
        Assert.DoesNotContain(inside.Shapes, s => s.Ink.ToString() is "Facing" or "FacingArrow");    // 화살 · 부채꼴은 되살리지 않는다
    }
    #endregion
}
