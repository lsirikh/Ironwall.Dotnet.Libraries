using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>펜스 좌표(wiring-fence-view FR-04 · FR-05 · FR-09 · FR-17 · FR-18) — 종류별 간격 · 투영 · 함체 · 리턴케이블 · 묶음.</summary>
public class FenceSlotLayoutTests
{
    private const EnumDeviceType M = EnumDeviceType.Multi;
    private const EnumDeviceType F = EnumDeviceType.Fence;
    private const EnumDeviceType S = EnumDeviceType.SmartSensor2;
    private const EnumDeviceType U = EnumDeviceType.Underground;

    /// <summary>키 1…N 에 종류를 차례로 붙인 체인과 배치.</summary>
    private static FenceSlotLayout Layout(WiringShape shape, EnumDeviceType[] types, FenceLayoutOptions? options = null, int? gap = null)
    {
        var keys = Enumerable.Range(1, types.Length).ToArray();
        var chain = WiringChain.Create(shape, keys, controllerGap: gap);
        return FenceSlotLayout.Build(chain, k => types[k - 1], options);
    }

    [Theory]
    [InlineData(EnumDeviceType.Fence, 2.5)]
    [InlineData(EnumDeviceType.SmartSensor, 6)]
    [InlineData(EnumDeviceType.SmartSensor2, 6)]
    [InlineData(EnumDeviceType.SmartCompound, 6)]
    [InlineData(EnumDeviceType.SmartMultisensor2, 6)]
    [InlineData(EnumDeviceType.Multi, 20)]
    [InlineData(EnumDeviceType.Underground, 25)]
    [InlineData(EnumDeviceType.NONE, 6)]
    public void should_use_catalogue_spacing_when_type_given(EnumDeviceType type, double metres)
        => Assert.Equal(metres, FenceSlotLayout.SpacingMetres(type));

    [Fact]
    public void should_take_smaller_spacing_between_neighbours_when_multi_and_fence_mix()
    {
        var layout = Layout(WiringShape.TwoBranch, new[] { M, F, F, F, M, M, S, S });

        Assert.Equal(new[] { 0, 2.5, 5, 7.5, 10, 30, 36, 42 }, layout.Slots.Select(s => s.Metres));
    }

    [Fact]
    public void should_use_measured_distance_when_given_for_sensor()
    {
        var options = new FenceLayoutOptions
        {
            MeasuredGapMetres = new Dictionary<int, double> { [2] = 4.0, [3] = double.NaN, [1] = 99 },   // 1 은 맨 왼쪽이라 쓸 곳이 없다
        };

        var layout = Layout(WiringShape.Ring, new[] { S, S, S }, options);

        Assert.Equal(new[] { 0, 4.0, 10.0 }, layout.Slots.Select(s => s.Metres));
    }

    [Fact]
    public void should_place_x_by_metres_times_scale_when_built()
    {
        var layout = Layout(WiringShape.Ring, new[] { S, S }, new FenceLayoutOptions { PixelsPerMetre = 10 });

        Assert.Equal(60, layout.SensorXs[1] - layout.SensorXs[0], 6);
        Assert.Equal(layout.SensorXs, layout.Slots.Select(s => s.X));
    }

    [Fact]
    public void should_keep_x_and_order_but_change_height_when_projection_switches()
    {
        var types = new[] { M, F, S, U, F };
        var tilt = Layout(WiringShape.TwoBranch, types, new FenceLayoutOptions { Projection = FenceProjection.Tilt35 });
        var flat = Layout(WiringShape.TwoBranch, types, new FenceLayoutOptions { Projection = FenceProjection.Flat });

        Assert.Equal(tilt.SensorXs, flat.SensorXs);
        Assert.Equal(tilt.SensorXs.OrderBy(x => x), tilt.SensorXs);          // 왼쪽→오른쪽 순서 그대로
        Assert.Equal(tilt.Slots.Select(s => s.Anchor.X), flat.Slots.Select(s => s.Anchor.X));
        Assert.NotEqual(tilt.Slots[0].Anchor.Y, flat.Slots[0].Anchor.Y);    // 35° 는 높이를 줄여 보인다
        Assert.True(tilt.Slots[3].Anchor.Y > tilt.GroundY);                  // 지진동은 땅속
        Assert.True(flat.Slots[3].Anchor.Y > flat.GroundY);
    }

    [Fact]
    public void should_draw_enclosure_at_middle_gap_with_two_return_cables_and_vbus_when_ring()
    {
        var layout = Layout(WiringShape.Ring, Enumerable.Repeat(S, 34).ToArray());

        Assert.Equal(17, layout.Chain.ControllerGap);
        Assert.Equal(layout.GapX(17), layout.EnclosureRect.Left + layout.EnclosureRect.Width / 2, 6);
        Assert.Equal(2, layout.ReturnCables.Count);
        Assert.Equal(layout.PortA, layout.ReturnCables[0][0]);
        Assert.Equal(layout.Slots[0].Anchor, layout.ReturnCables[0][^1]);
        Assert.Equal(layout.PortB, layout.ReturnCables[1][0]);
        Assert.Equal(layout.Slots[^1].Anchor, layout.ReturnCables[1][^1]);
        Assert.True(layout.ReturnCables[0].Max(p => p.Y) > layout.GroundY);   // 땅속으로 지난다
        Assert.Equal(new[] { 12, 22 }, layout.VbusMarkers.Select(v => v.Gap));
        Assert.Equal(34, layout.ChainPolyline.Count);
        Assert.Empty(layout.BranchPaths);
    }

    [Fact]
    public void should_move_enclosure_cables_and_vbus_but_not_sensors_when_enclosure_moves()
    {
        var types = Enumerable.Repeat(S, 20).ToArray();
        var before = Layout(WiringShape.Ring, types);
        var after = Layout(WiringShape.Ring, types, gap: 3);

        Assert.Equal(before.SensorXs, after.SensorXs);
        Assert.Equal(after.GapX(3), after.EnclosureRect.Left + after.EnclosureRect.Width / 2, 6);
        Assert.Equal(new[] { 0, 8 }, after.VbusMarkers.Select(v => v.Gap));
        Assert.Equal(after.PortA, after.ReturnCables[0][0]);
    }

    [Fact]
    public void should_draw_two_branch_paths_from_controller_outward_when_two_branch()
    {
        var layout = Layout(WiringShape.TwoBranch, new[] { M, F, F, M }, gap: 2);

        Assert.Empty(layout.ReturnCables);
        Assert.Empty(layout.ChainPolyline);
        Assert.Empty(layout.VbusMarkers);
        Assert.Equal(2, layout.BranchPaths.Count);
        Assert.Equal(new[] { layout.PortA, layout.Slots[1].Anchor, layout.Slots[0].Anchor }, layout.BranchPaths[0]);
        Assert.Equal(new[] { layout.PortB, layout.Slots[2].Anchor, layout.Slots[3].Anchor }, layout.BranchPaths[1]);
    }

    [Fact]
    public void should_start_chain_at_controller_on_left_when_line()
    {
        var layout = Layout(WiringShape.Line, new[] { U, U, U });

        Assert.Equal(layout.PortB, layout.ChainPolyline[0]);
        Assert.True(layout.EnclosureRect.Right < layout.SensorXs[0]);
        Assert.Empty(layout.PostXs);                                          // 지진동은 기둥이 없다
    }

    [Fact]
    public void should_put_posts_under_non_underground_sensors_when_built()
    {
        var layout = Layout(WiringShape.TwoBranch, new[] { M, F, U });

        Assert.Equal(new[] { layout.SensorXs[0], layout.SensorXs[1] }, layout.PostXs);
        Assert.True(layout.PostTopY < layout.GroundY);
    }

    [Fact]
    public void should_contain_every_drawn_part_when_bounds_computed()
    {
        var layout = Layout(WiringShape.Ring, new[] { S, M, F, F, S });

        foreach (var s in layout.Slots) Assert.True(layout.Bounds.Contains(s.Rect));
        Assert.True(layout.Bounds.Contains(layout.EnclosureRect));
        foreach (var p in layout.ReturnCables.SelectMany(c => c)) Assert.True(layout.Bounds.Contains(p));
    }

    [Fact]
    public void should_not_throw_and_keep_enclosure_when_chain_is_empty()
    {
        var layout = Layout(WiringShape.Ring, System.Array.Empty<EnumDeviceType>());

        Assert.Empty(layout.Slots);
        Assert.Empty(layout.ReturnCables);
        Assert.Empty(layout.VbusMarkers);
        Assert.False(layout.Bounds.IsEmpty);
    }

    #region - Grouping (FR-18) -
    [Fact]
    public void should_fold_consecutive_fence_runs_when_zoomed_out()
    {
        var layout = Layout(WiringShape.Line, new[] { M, F, F, F, F, M, F, S });

        var folded = layout.Group(0.5);

        Assert.Equal(5, folded.Count);                                        // M · [F×4] · M · F · S
        Assert.True(folded[1].IsGroup);
        Assert.Equal("펜스센서 ×4", folded[1].Label);
        Assert.Equal(new[] { 2, 3, 4, 5 }, folded[1].Keys);
        Assert.Equal(1, folded[1].FirstIndex);
        Assert.Equal(4, folded[1].LastIndex);
        Assert.False(folded[3].IsGroup);                                      // 혼자인 펜스센서는 접지 않는다
        Assert.True(folded[1].Rect.Contains(layout.Slots[3].Rect));
    }

    [Fact]
    public void should_show_every_sensor_when_zoom_is_at_or_above_threshold()
    {
        var layout = Layout(WiringShape.Line, new[] { M, F, F, F, F, M, F, S });

        Assert.Equal(8, layout.Group(1.0).Count);
        Assert.Equal(8, layout.Group(FenceSlotLayout.GROUP_ZOOM_THRESHOLD).Count);
        Assert.All(layout.Group(1.0), i => Assert.False(i.IsGroup));
    }

    [Fact]
    public void should_split_fence_group_at_controller_when_two_branch()
    {
        var layout = Layout(WiringShape.TwoBranch, new[] { F, F, F, F, F }, gap: 2);

        var folded = layout.Group(0.5);

        Assert.Equal(2, folded.Count);
        Assert.Equal(new[] { 1, 2 }, folded[0].Keys);
        Assert.Equal(new[] { 3, 4, 5 }, folded[1].Keys);
    }
    #endregion
}
