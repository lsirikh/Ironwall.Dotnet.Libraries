using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>펜스 모양 5종 그림 재료(fence-style-art) — 개수 · 범위 · 결정성 · LOD 문턱 · 묶음 기하 캐시(순수 함수).</summary>
public class FenceStyleArtTests
{
    private static int FigureCount(FenceShape s) => s.Figures?.Length ?? 0;

    #region - Razor -
    /// <summary>펜스 높이(세계) 130 = 2.4m · 6m 망 = 세계 68 · 받침 시작 135.</summary>
    private const double H = 130, SPAN = 68, POST_TOP = 135;

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void should_draw_the_coil_as_a_row_of_overlapping_tilted_ellipses_in_front_view_in_tilt_and_flat(double k)
    {
        // Arrange
        var p = new FenceProjector(k);

        // Act
        var shapes = FenceStyleArt.RazorCoil(p, 0, SPAN, POST_TOP, H);
        var layout = FenceStyleArt.CoilLayout(p, 0, SPAN, POST_TOP, H);

        // Assert — 고리마다 화면 타원(가로/세로 0.45~0.6) · 간격은 지름의 0.35~0.45 · 이웃 고리가 겹친다
        var coil = shapes.Single(s => s.Ink == FenceInk.RazorCoil);
        Assert.Equal(layout.Centers.Count, coil.Figures!.Length);
        Assert.True(coil.Closed);
        Assert.InRange(layout.Rx / layout.Ry, 0.45, 0.6);
        var spacing = layout.Centers[1].X - layout.Centers[0].X;
        Assert.InRange(spacing / (2 * layout.Ry), 0.35, 0.45);
        Assert.True(spacing < 2 * layout.Rx);                                                       // 겹친다(용수철)
        foreach (var (loop, i) in Chunks(coil).Select((l, i) => (l, i)))
        {
            var width = loop.Max(q => q.X) - loop.Min(q => q.X);
            var height = loop.Max(q => q.Y) - loop.Min(q => q.Y);
            Assert.InRange(width / height, 0.45, 0.85);                                             // 기울어도 선이 아니라 타원(막대 줄이 아니다)
            Assert.True(width > 4);
        }
        // 번갈아 기운다 — 이웃 고리의 맨 위 점이 서로 반대쪽으로 쏠린다
        var loops = Chunks(coil).ToList();
        var topShift0 = loops[0].OrderBy(q => q.Y).First().X - layout.Centers[0].X;
        var topShift1 = loops[1].OrderBy(q => q.Y).First().X - layout.Centers[1].X;
        Assert.True(topShift0 * topShift1 < 0);
    }

    [Fact]
    public void should_make_the_coil_diameter_about_forty_percent_of_the_fence_height_and_count_loops_per_metre()
    {
        var p = FenceProjector.Flat;

        var layout = FenceStyleArt.CoilLayout(p, 0, SPAN, POST_TOP, H);
        var perMetre = layout.Centers.Count / 6.0;

        Assert.InRange(2 * layout.Radius / H, 0.35, 0.45);
        // 지름의 0.4 간격 · 세계 68 = 6m — 한 칸에 고리 약 3~4개(m 당 0.5~0.7)
        Assert.InRange(perMetre, 0.45, 0.75);
        Assert.Equal(perMetre, FenceStyleArt.CoilLayout(p, 0, SPAN, POST_TOP, H).Centers.Count / 6.0);  // 결정적
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void should_seat_the_coil_inside_the_y_arms_with_arms_thirty_degrees_off_vertical(double k)
    {
        // Arrange
        var p = new FenceProjector(k);

        // Act
        var (arms, tipY, _) = FenceStyleArt.YArms(p, 100, POST_TOP, H);
        var layout = FenceStyleArt.CoilLayout(p, 0, SPAN, POST_TOP, H);

        // Assert — 팔 길이 = 반지름 × 1.2 · 수직에서 30°(평면에서 가로 벌림이 길이 × sin30)
        var arm = Assert.Single(arms);
        Assert.Equal(new[] { 2, 2 }, arm.Figures);
        Assert.Equal(POST_TOP + layout.Radius * 1.2 * System.Math.Cos(System.Math.PI / 6), tipY, 6);
        if (k == 0)
        {
            var spread = arm.Points[3].X - arm.Points[1].X;
            Assert.Equal(2 * layout.Radius * 1.2 * 0.5, spread, 6);
        }
        // 코일은 받침 위 — 바닥은 기둥 꼭대기보다 너무 내려가지 않고, 가운데는 팔 끝 높이 안쪽
        var postTopY = p.P(0, POST_TOP, 0).Y;
        var tipScreenY = p.P(0, tipY, 0).Y;
        Assert.True(layout.BottomY <= postTopY + layout.Ry * 0.1);
        Assert.True(layout.Centers[0].Y < postTopY && layout.Centers[0].Y > tipScreenY - layout.Ry);
    }

    [Fact]
    public void should_add_barbs_and_three_strands_between_the_arm_tips()
    {
        var shapes = FenceStyleArt.RazorCoil(FenceProjector.Tilt, 0, SPAN, POST_TOP, H);

        Assert.True(shapes.Single(s => s.Ink == FenceInk.RazorBarb).Figures!.Length >= 3 * 4);
        Assert.Equal(3, shapes.Single(s => s.Ink == FenceInk.RazorStrand).Figures!.Length);
    }

    [Fact]
    public void should_cap_coil_loops_for_a_very_long_panel()
    {
        var coil = FenceStyleArt.RazorCoil(FenceProjector.Flat, 0, 5000, POST_TOP, H).Single(s => s.Ink == FenceInk.RazorCoil);

        Assert.InRange(coil.Figures!.Length, 1, FenceStyleArt.COIL_LOOP_CAP + 1);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void should_keep_the_upper_lane_number_plate_above_the_coil_when_a_sensor_sits_on_a_razor_fence(double k)
    {
        // Arrange — 윤형 두 칸 · 기둥 1 위 줄 센서
        var p = new FenceProjector(k);
        var layout = WiringFenceLayout.Create(Enumerable.Repeat(FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor), 2),
            new Dictionary<int, SensorMountSpec> { [1] = new(1, FenceMountSpot.PostTop, Lane: FenceLane.Upper) }, null);
        var sensor = new FenceSensor(1, 1, 101, "센서 101", EnumDeviceType.SmartSensor2, null, WiringSpec.LINE_PRIMARY, 1, null, false, false, false);
        var world = FenceWorld.FromLayout(WiringChain.Create(WiringShape.Ring, new[] { 1 }), new Dictionary<int, FenceSensor> { [1] = sensor }, layout);
        var h = world.Geometry!.Panels[0].Spec.HeightM * world.Vpm;

        // Act
        var chip = FenceScene.Sensor(sensor, WiringShape.Ring, p, false, 1, world.LiftOf(1));
        var coil = FenceStyleArt.CoilLayout(p, 0, SPAN, h + 5, h);

        // Assert — 번호판(글자 바탕)의 아래 끝이 코일 꼭대기보다 위(화면 y 가 작다) · 코일 선이 번호를 지나지 않는다
        var plate = chip.Shapes.First(s => s.Ink == FenceInk.Plate);
        var plateBottom = System.Math.Max(plate.Points[0].Y, plate.Points[1].Y);
        Assert.True(plateBottom < coil.TopY, $"번호판 아래 {plateBottom:0.#} · 코일 꼭대기 {coil.TopY:0.#}");
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.0, 0.45)]
    [InlineData(0.0, 2.5)]
    public void should_seat_a_fence_sensor_chip_inside_the_coil_with_its_number_plate_clear_of_every_coil_stroke(double k, double zoom)
    {
        // Arrange — 윤형 두 칸 · 펜스센서가 망 1 윤형 코일(위 줄)
        var p = new FenceProjector(k);
        var layout = WiringFenceLayout.Create(Enumerable.Repeat(FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor), 2),
            new Dictionary<int, SensorMountSpec> { [1] = new(1, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper) }, null);
        var sensor = new FenceSensor(1, 1, 101, "펜스 101", EnumDeviceType.Fence, null, WiringSpec.LINE_PRIMARY, 1, null, false, false, false);
        var world = FenceWorld.FromLayout(WiringChain.Create(WiringShape.Ring, new[] { 1 }), new Dictionary<int, FenceSensor> { [1] = sensor }, layout);
        var panel = world.Geometry!.Panels[1];
        var h = panel.Spec.HeightM * world.Vpm;
        var x = world.X[1];

        // Act
        var chip = FenceScene.Sensor(sensor, WiringShape.Ring, p, false, zoom, world.LiftOf(1), world.CoilOf(1));
        var art = FenceStyleArt.RazorCoil(p, panel.StartM * world.Upm, panel.EndM * world.Upm, h + FenceStyleArt.COIL_SEAT_GAP, h);
        var coil = FenceStyleArt.CoilLayout(p, panel.StartM * world.Upm, panel.EndM * world.Upm, h + FenceStyleArt.COIL_SEAT_GAP, h);

        // Assert — 몸(앞면)의 가운데가 코일 띠 안 · 가로는 망 가운데(기둥 사이)
        Assert.True(world.CoilOf(1) > 0);
        Assert.Equal(panel.CenterM * world.Upm, x, 6);
        var body = chip.Shapes.First(s => s.Ink == FenceInk.OliveFront).Points;
        var bodyY = body.Average(q => q.Y);
        Assert.InRange(bodyY, coil.TopY + coil.Ry * 0.4, coil.BottomY - coil.Ry * 0.4);

        // 번호판은 코일 위 — 아래 끝이 코일 꼭대기보다 위이고, 코일 · 가시 선의 어떤 점도 판 안에 없다(글자가 코일 선을 지나지 않는다)
        var plate = chip.Shapes.Single(s => s.Ink == FenceInk.Plate);
        var rect = new System.Windows.Rect(plate.Points[0], plate.Points[1]);
        rect.Offset(x, 0);
        Assert.True(rect.Bottom < coil.TopY, $"번호판 아래 {rect.Bottom:0.#} · 코일 꼭대기 {coil.TopY:0.#}");
        var strokes = art.Where(s => s.Ink is FenceInk.RazorCoil or FenceInk.RazorBarb).SelectMany(s => s.Points);
        Assert.DoesNotContain(strokes, q => rect.Contains(q));
        Assert.Contains(chip.Shapes, s => s.Kind == FenceShapeKind.Text && s.Text == "101" && s.Points[0].Y < coil.TopY);
    }

    [Fact]
    public void should_keep_an_ordinary_fence_sensor_chip_unchanged_when_it_is_not_in_a_coil()
    {
        var sensor = new FenceSensor(1, 1, 101, "펜스 101", EnumDeviceType.Fence, null, WiringSpec.LINE_PRIMARY, 1, null, false, false, false);

        var plain = FenceScene.Sensor(sensor, WiringShape.Ring, FenceProjector.Tilt, false, 1, 20);

        Assert.DoesNotContain(plain.Shapes, s => s.Ink == FenceInk.Plate);                  // 번호는 몸 아래 글자 그대로
        Assert.Contains(plain.Shapes, s => s.Ink == FenceInk.FenceLabel);
    }

    private static IEnumerable<System.Windows.Point[]> Chunks(FenceShape shape)
    {
        var at = 0;
        foreach (var n in shape.Figures!)
        {
            yield return shape.Points.Skip(at).Take(n).ToArray();
            at += n;
        }
    }
    #endregion

    #region - Design -
    [Theory]
    [InlineData(70.0, 2)]
    [InlineData(130.0, 3)]
    public void should_fold_the_design_mesh_two_or_three_times_and_bulge_forward_only_in_tilt(double h, int folds)
    {
        var tilt = FenceStyleArt.DesignMesh(FenceProjector.Tilt, 0, 64, h);
        var flat = FenceStyleArt.DesignMesh(FenceProjector.Flat, 0, 64, h);

        var fold = tilt.Single(s => s.Ink == FenceInk.DesignFold);
        Assert.Equal(folds, FigureCount(fold));
        var wires = tilt.Single(s => s.Ink == FenceInk.DesignWire);
        var verticals = (int)System.Math.Round(64 / FenceStyleArt.DESIGN_WIRE_STEP) + 1;
        Assert.True(FigureCount(wires) >= verticals);
        // 세로 철선은 접힘마다 세 점(앞으로 꺾인다) — 바닥 + 3 × 접힘 + 꼭대기
        Assert.Equal(2 + 3 * folds, wires.Figures![0]);
        Assert.NotEqual(fold.Points, flat.Single(s => s.Ink == FenceInk.DesignFold).Points);
    }

    [Fact]
    public void should_put_a_clamp_on_the_design_post_at_each_fold()
    {
        var clamps = FenceStyleArt.DesignClamps(FenceProjector.Tilt, 50, 130, 10);

        Assert.Equal(3, clamps.Count);
        Assert.All(clamps, c => Assert.Equal(FenceInk.DesignClamp, c.Ink));
    }
    #endregion

    #region - Brick -
    [Fact]
    public void should_lay_staggered_bricks_in_three_deterministic_tones_inside_the_front_face_when_zoomed_in()
    {
        var p = FenceProjector.Tilt;

        var shapes = FenceStyleArt.BrickCourses(p, 0, 51, 108, 5, zoom: 1, panelIndex: 7);
        var again = FenceStyleArt.BrickCourses(p, 0, 51, 108, 5, zoom: 1, panelIndex: 7);
        var other = FenceStyleArt.BrickCourses(p, 0, 51, 108, 5, zoom: 1, panelIndex: 8);

        var tones = shapes.Where(s => s.Kind == FenceShapeKind.Patches).ToList();
        Assert.Equal(3, tones.Count);
        var bricks = tones.Sum(FigureCount);
        Assert.InRange(bricks, FenceStyleArt.BRICK_ROWS * 2, FenceStyleArt.BRICK_ROWS * 6);
        Assert.All(tones, t => Assert.True(FigureCount(t) > 0));                                    // 색 셋이 모두 쓰인다
        Assert.Equal(tones.Select(t => t.Points), again.Where(s => s.Kind == FenceShapeKind.Patches).Select(t => t.Points));
        Assert.NotEqual(tones.Select(FigureCount), other.Where(s => s.Kind == FenceShapeKind.Patches).Select(FigureCount));
        var face = shapes.Single(s => s.Ink == FenceInk.BrickMortarFace).Points;
        var (minX, maxX, minY, maxY) = (face.Min(q => q.X), face.Max(q => q.X), face.Min(q => q.Y), face.Max(q => q.Y));
        Assert.All(tones.SelectMany(t => t.Points), q => Assert.True(q.X >= minX - 1e-6 && q.X <= maxX + 1e-6 && q.Y >= minY - 1e-6 && q.Y <= maxY + 1e-6));
    }

    [Fact]
    public void should_skip_bricks_when_rows_are_smaller_than_the_pixel_threshold()
    {
        var zoom = FenceStyleArt.BRICK_MIN_ROW_PX / (108.0 / FenceStyleArt.BRICK_ROWS) * 0.9;

        Assert.Empty(FenceStyleArt.BrickCourses(FenceProjector.Tilt, 0, 51, 108, 5, zoom, 0));
        Assert.NotEmpty(FenceStyleArt.BrickCourses(FenceProjector.Tilt, 0, 51, 108, 5, zoom / 0.8, 0));
    }
    #endregion

    #region - Concrete -
    [Fact]
    public void should_scatter_capped_deterministic_speckle_seeded_by_panel_and_skip_it_when_too_small()
    {
        var p = FenceProjector.Flat;

        var small = FenceStyleArt.ConcreteStucco(p, 0, 51, 108, 5, 1, 3);
        var again = FenceStyleArt.ConcreteStucco(p, 0, 51, 108, 5, 1, 3);
        var other = FenceStyleArt.ConcreteStucco(p, 0, 51, 108, 5, 1, 4);
        var huge = FenceStyleArt.ConcreteStucco(p, 0, 5000, 300, 5, 1, 3);
        var tiny = FenceStyleArt.ConcreteStucco(p, 0, 51, 108, 5, FenceStyleArt.SPECKLE_MIN_PX / FenceStyleArt.SPECKLE_SIZE * 0.9, 3);

        Assert.Equal((int)(51 * 108 / FenceStyleArt.SPECKLE_AREA), small.Sum(FigureCount));
        Assert.Equal(small.SelectMany(s => s.Points), again.SelectMany(s => s.Points));
        Assert.NotEqual(small.SelectMany(s => s.Points), other.SelectMany(s => s.Points));
        Assert.Equal(FenceStyleArt.SPECKLE_CAP, huge.Sum(FigureCount));
        Assert.Empty(tiny);
    }
    #endregion

    #region - Renderer cache · scale -
    [Fact]
    public void should_build_one_frozen_geometry_per_figure_shape_and_reuse_it()
    {
        var coil = FenceStyleArt.RazorCoil(FenceProjector.Tilt, 0, 68, 135, 130).Single(s => s.Ink == FenceInk.RazorCoil);

        var first = FenceRenderer.FiguresGeometry(coil);
        var second = FenceRenderer.FiguresGeometry(coil);

        Assert.Same(first, second);
        Assert.True(first.IsFrozen);
    }

    [Theory]
    [InlineData(EnumFenceStyle.ChainLinkRazor)]
    [InlineData(EnumFenceStyle.Brick)]
    [InlineData(EnumFenceStyle.Concrete)]
    [InlineData(EnumFenceStyle.DesignFence)]
    [InlineData(EnumFenceStyle.ChainLink)]
    public void should_keep_the_static_layer_to_a_few_shapes_per_panel_when_two_hundred_panels_are_drawn(EnumFenceStyle style)
    {
        // Arrange — 망 200칸(무늬가 다 나오는 배율 1)
        var layout = WiringFenceLayout.Create(Enumerable.Repeat(FencePanelSpec.Default(style), 200), new Dictionary<int, SensorMountSpec>(), null);
        var world = FenceWorld.FromLayout(WiringChain.Create(WiringShape.Ring, new int[0]), new Dictionary<int, FenceSensor>(), layout);

        // Act
        var shapes = FenceScene.StaticLayout(world, FenceProjector.Tilt, false, false, 0, 0, zoom: 1);

        // Assert — 조각은 묶음 그림 안에 들어가 칸마다 그림 수가 적다(렌더러가 칸마다 기하 몇 개만 만든다)
        Assert.InRange(shapes.Count, 200, 200 * 14 + 50);                                          // + 땅 · 글자 · 끝 기둥 몫
    }
    #endregion
}
