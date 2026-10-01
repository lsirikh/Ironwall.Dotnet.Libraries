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
    [Fact]
    public void should_build_overlapping_coil_loops_barbs_and_three_strands_inside_the_y_arms_when_a_razor_panel_is_drawn()
    {
        // Arrange — 6m 망(세계 68) · 꼭대기 135
        var p = FenceProjector.Tilt;

        // Act
        var shapes = FenceStyleArt.RazorCoil(p, 0, 68, 135);
        var again = FenceStyleArt.RazorCoil(p, 0, 68, 135);

        // Assert
        var coil = shapes.Single(s => s.Ink == FenceInk.RazorCoil);
        var loops = FigureCount(coil);
        Assert.InRange(loops, 8, FenceStyleArt.COIL_LOOP_CAP);
        Assert.Equal(loops * FenceStyleArt.COIL_LOOP_POINTS, coil.Points.Length);
        Assert.True(coil.Closed);
        Assert.Equal(FenceShapeKind.Strokes, coil.Kind);
        Assert.True(FigureCount(shapes.Single(s => s.Ink == FenceInk.RazorBarb)) > 0);
        Assert.Equal(3, FigureCount(shapes.Single(s => s.Ink == FenceInk.RazorStrand)));
        Assert.Equal(coil.Points, again.Single(s => s.Ink == FenceInk.RazorCoil).Points);            // 결정적
        // 코일은 기둥 꼭대기 위(화면 위쪽 = y 작은 쪽)에 있다
        var top = p.P(0, 135, 0).Y;
        Assert.True(coil.Points.Average(q => q.Y) < top);
    }

    [Fact]
    public void should_cap_coil_loops_for_a_very_long_panel()
    {
        var coil = FenceStyleArt.RazorCoil(FenceProjector.Flat, 0, 5000, 135).Single(s => s.Ink == FenceInk.RazorCoil);

        Assert.InRange(FigureCount(coil), 1, FenceStyleArt.COIL_LOOP_CAP + 1);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void should_spread_the_y_arms_outward_above_the_post_top_in_tilt_and_flat(double k)
    {
        var p = new FenceProjector(k);

        var (shapes, tipY, _) = FenceStyleArt.YArms(p, 100, 135);

        var arms = Assert.Single(shapes);
        Assert.Equal(new[] { 2, 2 }, arms.Figures);
        Assert.True(tipY > 135);
        Assert.NotEqual(arms.Points[1], arms.Points[3]);                                            // 두 팔이 갈라진다(Y)
        Assert.Equal(p.P(100, 135, 0), arms.Points[0]);                                             // 기둥 꼭대기에서
        Assert.Equal(p.P(100, 135, 0), arms.Points[2]);
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
        var coil = FenceStyleArt.RazorCoil(FenceProjector.Tilt, 0, 68, 135).Single(s => s.Ink == FenceInk.RazorCoil);

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
