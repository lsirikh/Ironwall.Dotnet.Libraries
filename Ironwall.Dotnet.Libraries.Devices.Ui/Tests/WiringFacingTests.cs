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
/// 센서 설치 위치 · 방향(wiring-fence-view FR-20) — 펜스센서는 기둥 사이 철망 가운데, 스마트 복합 · 복합은 기둥에 앞(외부) · 뒤(내부).
/// 좌표(기둥 · 철망 가운데 · 뒤 센서 밀림) · 모델(뒤집기 · 되돌리기 · 바뀐 줄 · 여럿) · 저장 본문 · 불러오기 기본값.
/// </summary>
public class WiringFacingTests
{
    private const EnumDeviceType M = EnumDeviceType.Multi;
    private const EnumDeviceType F = EnumDeviceType.Fence;
    private const EnumDeviceType S = EnumDeviceType.SmartSensor2;
    private const EnumDeviceType U = EnumDeviceType.Underground;

    #region - Layout (설치 위치) -
    [Fact]
    public void should_hang_each_fence_sensor_in_the_middle_of_a_mesh_panel_between_two_posts_when_built()
    {
        var types = new[] { S, F, F, F, S };
        var chain = WiringChain.Create(WiringShape.Ring, Enumerable.Range(1, types.Length).ToArray());
        var layout = FenceSlotLayout.Build(chain, k => types[k - 1], new FenceLayoutOptions { Projection = FenceProjection.Flat });
        var posts = layout.PostXs.OrderBy(x => x).ToList();

        foreach (var slot in layout.Slots.Where(s => s.Type == F))
        {
            Assert.DoesNotContain(posts, x => System.Math.Abs(x - slot.X) < 0.01);               // 기둥 위가 아니다
            var left = posts.Last(x => x < slot.X);
            var right = posts.First(x => x > slot.X);
            Assert.Equal((left + right) / 2, slot.X, 3);                                          // 자기 칸 가운데
            Assert.Equal((layout.GroundY + layout.PostTopY) / 2, slot.Anchor.Y, 3);               // 높이 중간
        }
        foreach (var slot in layout.Slots.Where(s => s.Type == S))
            Assert.Contains(posts, x => System.Math.Abs(x - slot.X) < 0.01);                     // 기둥 센서는 자기 기둥
    }

    [Fact]
    public void should_share_posts_between_adjacent_fence_panels_and_keep_sensor_x_when_built()
    {
        var types = new[] { M, F, F, U };
        var chain = WiringChain.Create(WiringShape.TwoBranch, Enumerable.Range(1, types.Length).ToArray());
        var layout = FenceSlotLayout.Build(chain, k => types[k - 1]);
        var half = FenceSlotLayout.FENCE_HALF_PANEL_M * FenceSlotLayout.DEFAULT_PIXELS_PER_METRE;

        // 복합 기둥 1 + 펜스 두 칸이 가운데 기둥을 나눠 써서 3 = 4개 · 지진동은 기둥이 없다.
        Assert.Equal(new[] { layout.SensorXs[0], layout.SensorXs[1] - half, layout.SensorXs[1] + half, layout.SensorXs[2] + half }, layout.PostXs);
        Assert.Equal(FenceSlotLayout.GapMetres(M, F) * FenceSlotLayout.DEFAULT_PIXELS_PER_METRE, layout.SensorXs[1] - layout.SensorXs[0], 6);  // 간격 규칙 그대로
    }

    [Fact]
    public void should_fill_background_posts_but_never_split_a_fence_panel_when_filling()
    {
        var posts = FenceSlotLayout.MountPosts(new[] { (0.0, M), (100.0, F) }, halfPanel: 10, fillSpacing: 30, from: -50, to: 200);

        Assert.Contains(0.0, posts);
        Assert.Contains(90.0, posts);
        Assert.Contains(110.0, posts);
        Assert.DoesNotContain(posts, x => x > 90 && x < 110);                                     // 펜스센서 칸은 그대로
        Assert.Contains(posts, x => x > 0 && x < 90);                                             // 빈 구간은 채운다
        Assert.True(posts.Min() >= -50 - 0.1 && posts.Max() <= 200 + 0.1);
    }

    [Fact]
    public void should_draw_fence_scene_posts_around_fence_sensors_when_ring_mixes_types()
    {
        var sensors = new Dictionary<int, FenceSensor>
        {
            [1] = Sensor(1, S), [2] = Sensor(2, F), [3] = Sensor(3, S),
        };
        var world = FenceWorld.Build(WiringChain.Create(WiringShape.Ring, new[] { 1, 2, 3 }), sensors);
        var posts = FenceScene.Posts(world, world.MinX, world.MaxX);

        Assert.Contains(posts, x => System.Math.Abs(x - world.X[1]) < 0.01);
        Assert.DoesNotContain(posts, x => System.Math.Abs(x - world.X[2]) < 0.01);
        var left = posts.Last(x => x < world.X[2]);
        var right = posts.First(x => x > world.X[2]);
        Assert.Equal((left + right) / 2, world.X[2], 3);
    }
    #endregion

    #region - Drawing (방향 표지) -
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.0)]
    public void should_draw_a_back_facing_sensor_offset_behind_its_post_with_a_back_tag_when_facing_back(double k)
    {
        var p = new FenceProjector(k);
        var front = FenceScene.Sensor(Sensor(1, S), WiringShape.Ring, p, selected: false);
        var back = FenceScene.Sensor(Sensor(1, S, WiringFacing.Back), WiringShape.Ring, p, selected: false);

        Assert.True(back.Hit.Top < front.Hit.Top - 4, $"뒤 센서는 위로(너머로) 밀린다: {front.Hit} → {back.Hit}");
        if (k > 0) Assert.True(back.Hit.Right > front.Hit.Right + 4, "입체에서는 오른쪽으로도 밀린다");   // 왼쪽은 "뒤" 표지가 넓힌다 — 오른쪽 끝으로 잰다
        Assert.Contains(back.Shapes, s => s.Ink == FenceInk.FacingTagText && s.Text == "뒤");
        Assert.DoesNotContain(front.Shapes, s => s.Ink == FenceInk.FacingTagText);
    }

    [Fact]
    public void should_point_the_detection_fan_to_the_facing_side_and_skip_it_for_fence_sensors()
    {
        var sensors = new Dictionary<int, FenceSensor> { [1] = Sensor(1, M), [2] = Sensor(2, F), [3] = Sensor(3, M, WiringFacing.Back) };
        var world = FenceWorld.Build(WiringChain.Create(WiringShape.Line, new[] { 1, 2, 3 }), sensors);
        foreach (var p in new[] { FenceProjector.Tilt, FenceProjector.Flat })
        {
            var arrows = FenceScene.Static(world, p, showRange: false, world.ControllerX, world.Chain.ControllerGap)
                                   .Where(s => s.Ink == FenceInk.FacingArrow).ToList();
            var ground = p.P(0, 0, 0).Y;

            Assert.Equal(2, arrows.Count);                                                        // 펜스센서는 부채꼴이 없다
            var front = arrows.Single(a => a.Points.Max(q => q.Y) < ground);                   // 앞 = 펜스 너머(외부 · 위)
            var back = arrows.Single(a => a.Points.Min(q => q.Y) > ground);                    // 뒤 = 보는 쪽(내부 · 아래)
            Assert.NotSame(front, back);
        }
    }

    [Fact]
    public void should_label_outside_beyond_the_fence_and_inside_toward_the_viewer_when_static_layer_drawn()
    {
        var world = FenceWorld.Build(WiringChain.Create(WiringShape.Ring, new[] { 1, 2 }), new Dictionary<int, FenceSensor> { [1] = Sensor(1, S), [2] = Sensor(2, S) });
        foreach (var p in new[] { FenceProjector.Tilt, FenceProjector.Flat })
        {
            var shapes = FenceScene.Static(world, p, showRange: false, world.ControllerX, world.Chain.ControllerGap);
            var outside = shapes.Single(s => s.Text == "펜스 외부");
            var inside = shapes.Single(s => s.Text == "펜스 내부");
            Assert.True(outside.Points[0].Y < inside.Points[0].Y);
            Assert.True(world.FitBounds(p).Contains(new Point(outside.Points[0].X - 1, outside.Points[0].Y)));
        }
    }
    #endregion

    #region - Model (뒤집기 · 되돌리기 · 바뀐 줄) -
    [Fact]
    public void should_count_a_changed_row_and_undo_in_one_step_when_facing_flipped()
    {
        var board = Board((S, 1), (S, 2), (F, 3));
        Assert.False(board.IsDirty);

        board.PushUndo();
        Assert.Equal(1, board.SetFacing(new[] { 101 }, WiringFacing.Back));

        Assert.Equal(1, board.UnsavedChangeCount);
        Assert.Equal(new WiringPlacement(1, 1, WiringFacing.Back), board.PlacementOf(101));
        Assert.True(board.FacingChanged(101));

        Assert.True(board.Undo());
        Assert.Equal(WiringFacing.Front, board.FacingOf(101));
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_refuse_facing_on_fence_and_underground_sensors_when_set()
    {
        var board = Board((F, 1), (U, 2));

        Assert.Equal(0, board.SetFacing(new[] { 101, 102 }, WiringFacing.Back));
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_flip_all_selected_to_the_opposite_of_the_first_and_skip_fence_sensors_when_f_pressed_on_a_multi_selection()
    {
        var vm = Ring(new[] { "SmartSensor2", "SmartSensor2", "Fence" }, back: new[] { 102 });
        vm.FenceSelect(101);
        vm.FenceToggleSelect(102);
        vm.FenceToggleSelect(103);

        Assert.True(vm.FenceFlipFacing(101));

        var sensors = vm.FenceSensors();
        Assert.Equal(WiringFacing.Back, sensors[101].Facing);                // 첫 센서의 반대쪽으로 모두 맞춘다
        Assert.Equal(WiringFacing.Back, sensors[102].Facing);
        Assert.False(sensors[103].HasFacing);
        Assert.Equal(1, vm.DraftCountOf());                                   // 102 는 원래 뒤 — 바뀐 줄은 101 하나

        vm.Undo();
        Assert.Equal(WiringFacing.Front, vm.FenceSensors()[101].Facing);
        Assert.Equal(WiringFacing.Back, vm.FenceSensors()[102].Facing);
    }

    [Fact]
    public void should_disable_facing_buttons_with_a_reason_when_a_fence_sensor_is_selected()
    {
        var vm = Ring(new[] { "SmartSensor2", "Fence" });
        vm.FenceSelect(102);

        Assert.True(vm.HasFacingRow);
        Assert.False(vm.CanChooseFacing);
        Assert.Contains("방향이 없습니다", vm.FacingNote);

        vm.FenceSelect(101);
        Assert.True(vm.CanChooseFacing);
        Assert.True(vm.IsSelectedFront);
        Assert.True(vm.FenceSetFacingBack());
        Assert.True(vm.IsSelectedBack);
        Assert.Contains("방향 바뀜 1건", vm.ChangePreview);
    }
    #endregion

    #region - Load · save -
    [Fact]
    public void should_read_missing_facing_as_front_and_back_only_from_the_back_word()
    {
        Assert.Equal(WiringFacing.Front, WiringSpec.Read(JObject.Parse("""{"wiring":{"v":2,"shape":"ring","line":1,"order":3}}"""))!.Facing);
        Assert.Equal(WiringFacing.Back, WiringSpec.Read(JObject.Parse("""{"wiring":{"v":2,"shape":"ring","line":1,"order":3,"facing":"back"}}"""))!.Facing);
        Assert.Equal(WiringFacing.Front, WiringSpec.Read(JObject.Parse("""{"wiring":{"line":1,"order":3,"facing":"sideways"}}"""))!.Facing);
    }

    [Fact]
    public void should_not_change_an_untouched_sensor_whose_saved_value_has_no_facing_when_loaded()
    {
        var saved = WiringSpec.Read(JObject.Parse("""{"wiring":{"v":2,"shape":"ring","line":1,"order":1}}"""));
        var board = new WiringBoard();
        board.Load(new[] { (101, (int?)1, new SensorFacts(1101, "스마트 1", "SmartSensor2", ""), saved, (string?)null, (IReadOnlyList<int>?)null) }, "SmartController");

        Assert.Equal(WiringFacing.Front, board.FacingOf(101));
        Assert.False(board.IsDirty);
        Assert.Empty(board.Diff().ToSend);
    }

    [Fact]
    public async Task should_patch_the_full_v2_wiring_object_with_unchanged_line_and_order_when_only_facing_changed()
    {
        var board = Board((S, 1), (S, 2));
        var gateway = new WiringFakeGateway();
        foreach (var (id, order) in new[] { (101, 1), (102, 2) })
            gateway.Fetched[id] = new SensorDeviceDto
            {
                Id = id, NumberDevice = 1000 + id, NameDevice = $"센서 {order}", TypeDevice = "SmartSensor2", Status = "ACTIVATED", IsEnable = true, ControllerId = 10,
                HardwareSpec = new HardwareSpecDto { Spec = WiringSpec.Apply(null, new WiringPlacement(1, order), WiringShape.Ring, includeFacing: true) },
            };

        board.SetFacing(new[] { 102 }, WiringFacing.Back);
        var result = await new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        var (patchedId, dto) = Assert.Single(gateway.Patched);
        Assert.Equal(102, patchedId);
        var wiring = (JObject)JObject.Parse(JsonConvert.SerializeObject(dto, PresetRequestBuilder.WireSettings)).SelectToken("hardware_spec.spec.wiring")!;
        Assert.Equal(3, (int)wiring["v"]!);
        Assert.Null(wiring["shape"]);
        Assert.Equal(1, (int)wiring["line"]!);
        Assert.Equal(2, (int)wiring["order"]!);
        Assert.Equal("back", (string)wiring["facing"]!);

        // 저장 뒤 기준이 옮겨져 다시 저장해도 나가지 않는다.
        board.MarkBaseline(result.OkKeys, includeGroups: false);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_write_facing_only_for_post_mounted_sensors_when_building_the_spec()
    {
        var smart = WiringSpec.Apply(null, new WiringPlacement(1, 4), WiringShape.Ring, includeFacing: true);
        var fence = WiringSpec.Apply(null, new WiringPlacement(1, 4, WiringFacing.Back), WiringShape.TwoBranch, includeFacing: false);

        Assert.Equal("front", (string)smart["wiring"]!["facing"]!);          // 기본값도 명시 — 병합 PATCH 가 옛 "back" 을 남기지 않게
        Assert.Null(fence["wiring"]!["facing"]);
    }
    #endregion

    #region - Fixtures -
    private static FenceSensor Sensor(int key, EnumDeviceType type, WiringFacing facing = WiringFacing.Front)
        => new(key, key, 1000 + key, $"센서 {key}", type, null, 1, key, null, false, false, false, facing);

    private static WiringBoard Board(params (EnumDeviceType Type, int Order)[] rows)
    {
        var board = new WiringBoard();
        board.Load(rows.Select((r, i) => (
            100 + r.Order,
            (int?)r.Order,
            new SensorFacts(1100 + r.Order, $"센서 {r.Order}", r.Type.ToString(), ""),
            (WiringPlacement?)new WiringPlacement(1, r.Order),
            (string?)null,
            (IReadOnlyList<int>?)null)), "SmartController");
        return board;
    }

    private static WiringViewModel Ring(IReadOnlyList<string> types, IReadOnlyCollection<int>? back = null)
    {
        var seeds = types.Select((t, i) => new WiringSensorSeed(
            101 + i, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}", t, "북측"),
            new WiringPlacement(1, i + 1, back?.Contains(101 + i) == true ? WiringFacing.Back : WiringFacing.Front)));
        return WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"),
            seeds, types.Distinct().ToList(), null, new WiringFakeDialogs());
    }
    #endregion
}

internal static class WiringFacingTestExtensions
{
    public static int DraftCountOf(this WiringViewModel vm) => int.Parse(vm.DraftText.Split(' ').Last());
}
