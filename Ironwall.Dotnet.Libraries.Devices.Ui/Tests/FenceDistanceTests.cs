using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 거리(m) 표시(2026-10-01 · 사용자: "개념도나 3D 화면에는 센서간 거리 m 표시, 펜스나 담의 m 표시 해줘, on/off 되게") — 거리 계산 · 글자 · 솎기 ·
/// 펜스 뷰 치수 줄 자리 · 개념도 글자 자리 · [거리 표시] 기억.
/// </summary>
public class FenceDistanceTests
{
    private static FenceGeometry Geometry(params (EnumFenceStyle Style, double Span)[] panels)
        => FenceLayoutMath.Geometry(panels.Select(p => FencePanelSpec.Default(p.Style, p.Span)).ToList());

    #region - Math -
    [Fact]
    public void should_measure_same_lane_neighbours_over_mixed_panel_spans_and_never_join_the_two_lanes()
    {
        var geometry = Geometry((EnumFenceStyle.ChainLink, 6), (EnumFenceStyle.ChainLink, 3), (EnumFenceStyle.ChainLink, 4.5));
        var mounts = new[]
        {
            (1, new SensorMountSpec(0, FenceMountSpot.PostTop)),
            (2, new SensorMountSpec(1, FenceMountSpot.PostTop)),
            (3, new SensorMountSpec(2, FenceMountSpot.PostTop)),
            (4, new SensorMountSpec(3, FenceMountSpot.PostTop)),
            (9, new SensorMountSpec(1, FenceMountSpot.PostTop, Lane: FenceLane.Upper)),           // 위 줄 혼자 — 이웃 없음
        };

        var gaps = FenceLayoutMath.LaneGaps(mounts, geometry);

        Assert.Equal(new[] { 6.0, 3.0, 4.5 }, gaps.Select(g => g.Metres));
        Assert.All(gaps, g => Assert.Equal(FenceLane.Lower, g.Lane));
        Assert.Equal(new[] { (1, 2), (2, 3), (3, 4) }, gaps.Select(g => (g.LeftKey, g.RightKey)));
    }

    [Fact]
    public void should_use_column_offsets_on_wall_and_mesh_panels_and_skip_sensors_on_the_same_point()
    {
        var geometry = Geometry((EnumFenceStyle.Brick, 5), (EnumFenceStyle.ChainLink, 6));
        var mounts = new[]
        {
            (1, new SensorMountSpec(0, FenceMountSpot.WallFace, Column: FenceColumn.Left)),         // 1.0m
            (2, new SensorMountSpec(0, FenceMountSpot.WallTop, Column: FenceColumn.Right)),         // 4.0m
            (3, new SensorMountSpec(1, FenceMountSpot.PanelCenter, Column: FenceColumn.Left)),      // 5 + 1.2 = 6.2m
            (4, new SensorMountSpec(1, FenceMountSpot.PanelBottom, Column: FenceColumn.Left)),      // 같은 x — 빼기
        };

        var gaps = FenceLayoutMath.LaneGaps(mounts, geometry);

        Assert.Equal(new[] { 3.0, 2.2 }, gaps.Select(g => System.Math.Round(g.Metres, 6)));
    }

    [Fact]
    public void should_measure_razor_coil_fence_sensors_on_the_upper_lane()
    {
        var geometry = Geometry((EnumFenceStyle.ChainLinkRazor, 6), (EnumFenceStyle.ChainLinkRazor, 6));
        var mounts = new[]
        {
            (1, new SensorMountSpec(0, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper)),
            (2, new SensorMountSpec(1, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper, Column: FenceColumn.Right)),
            (3, new SensorMountSpec(1, FenceMountSpot.PostTop)),
        };

        var gap = Assert.Single(FenceLayoutMath.LaneGaps(mounts, geometry));

        Assert.Equal((FenceLane.Upper, 1, 2), (gap.Lane, gap.LeftKey, gap.RightKey));
        Assert.Equal(3 + 6 * 0.8, gap.Metres, 6);                                                 // 3m → 10.8m
    }

    [Theory]
    [InlineData(6.0, "6m")]
    [InlineData(2.45, "2.5m")]
    [InlineData(3.04, "3m")]
    [InlineData(0.15, "0.2m")]
    [InlineData(12.25, "12.3m")]
    public void should_round_to_a_tenth_and_drop_a_trailing_zero(double metres, string expected)
        => Assert.Equal(expected, FenceLayoutMath.MetresText(metres));
    #endregion

    #region - Thinning -
    [Fact]
    public void should_keep_every_label_when_none_overlap()
        => Assert.All(FenceDimensions.Thin(new[] { (0.0, 20.0), (40.0, 20.0), (80.0, 20.0) }, 6), Assert.True);

    [Fact]
    public void should_hide_overlapping_labels_but_keep_the_first_and_the_last()
    {
        var keep = FenceDimensions.Thin(new[] { (0.0, 20.0), (10.0, 20.0), (30.0, 20.0), (60.0, 20.0), (70.0, 20.0) }, 6);

        Assert.True(keep[0]);
        Assert.True(keep[4]);
        Assert.False(keep[1]);
        Assert.False(keep[3]);                                                                    // 끝과 겹쳐 빠진다
        Assert.True(keep[2]);
    }

    [Fact]
    public void should_keep_only_the_first_when_the_first_and_the_last_overlap()
        => Assert.Equal(new[] { true, false }, FenceDimensions.Thin(new[] { (0.0, 20.0), (5.0, 20.0) }, 6));
    #endregion

    #region - Fence view · concept -
    [Fact]
    public void should_draw_panel_and_sensor_rows_below_the_ground_number_row_only_when_switched_on()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout);

        var off = FenceScene.StaticLayout(world, FenceProjector.Tilt, false, false, world.ControllerX, 0, 1);
        var on = FenceScene.StaticLayout(world, FenceProjector.Tilt, false, false, world.ControllerX, 0, 1, showDistances: true);

        Assert.DoesNotContain(off, s => s.Ink is FenceInk.DimText or FenceInk.DimLine);
        var dims = on.Where(s => s.Ink == FenceInk.DimText).ToList();
        Assert.Equal(3 + 3, dims.Count);                                                          // 망 3칸 + 센서 사이 3
        Assert.All(dims, d => Assert.Equal("6m", d.Text));
        var numbers = on.Where(s => s.Ink == FenceInk.PostNumber).Max(s => s.Points[0].Y);
        Assert.True(dims.Min(d => d.Points[0].Y) > numbers + 8, "거리 줄은 땅 번호 줄 아래");
    }

    [Fact]
    public void should_put_concept_distances_between_chips_below_the_lower_lane_and_above_the_upper_lane()
    {
        var items = new[]
        {
            new ConceptLaneItem(1, FenceLane.Lower, 0), new ConceptLaneItem(2, FenceLane.Lower, 6),
            new ConceptLaneItem(3, FenceLane.Upper, 0), new ConceptLaneItem(4, FenceLane.Upper, 6),
        };
        var g = ConceptLayout.Build(items, new[] { 0.0, 6.0 }, 6, FenceControllerEnd.Left, new System.Windows.Size(600, 200));
        var gaps = new[] { new FenceLaneGap(1, 2, FenceLane.Lower, 0, 6), new FenceLaneGap(3, 4, FenceLane.Upper, 0, 6) };

        var labels = ConceptLayout.DistanceLabels(g, gaps);

        Assert.Equal(2, labels.Count);
        var lower = labels.Single(l => l.Points[0].Y > g.LowerY);
        var upper = labels.Single(l => l.Points[0].Y < g.UpperY);
        var mid = (g.Nodes.Single(n => n.Key == 1).Center.X + g.Nodes.Single(n => n.Key == 2).Center.X) / 2;
        Assert.Equal(mid, lower.Points[0].X, 6);
        Assert.Equal("6m", upper.Text);
    }
    #endregion

    #region - Toggle -
    [Fact]
    public void should_start_off_remember_the_toggle_and_not_save_while_applying_the_stored_value()
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        var saved = new List<bool>();
        var changes = 0;
        vm.FenceChanged += (_, _) => changes++;

        var initial = vm.ShowDistances;
        vm.UseDistancePrefs(true, saved.Add);
        var applied = (vm.ShowDistances, saved.Count);
        vm.ToggleDistances();

        Assert.False(initial);
        Assert.Equal((true, 0), applied);
        Assert.Equal(new[] { false }, saved);
        Assert.Equal(2, changes);                                                                 // 켤 때 · 끌 때 다시 그린다
    }

    [Fact]
    public void should_offer_a_styled_distance_toggle_in_the_fence_toolbar()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "Fence", "FenceView.xaml"));
        var button = System.Text.RegularExpressions.Regex.Match(xaml, "<Button[^>]*AutomationId=\"Devices.Wiring.Fence.ShowDistances\"[^>]*/>", System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.True(button.Success);
        Assert.Contains("Style=\"{StaticResource F.Tool}\"", button.Value);
        Assert.Contains("Tag=\"{Binding ShowDistances}\"", button.Value);
        Assert.Contains("Click=\"OnDistances\"", button.Value);
    }

    private static string Root([CallerFilePath] string? file = null) => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));
    #endregion
}
