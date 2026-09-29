using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>펜스 세계 · 장면(wiring-fence-view F-3 · NFR-01) — 위치 · 드롭 자리 · 삽입 막대 · 묶음 · 투영 · 전체 보기 경계. 헤드리스.</summary>
public class FenceWorldTests
{
    private static FenceSensor S(int key, EnumDeviceType type, int number = 0)
        => new(key, key, number == 0 ? 1000 + key : number, $"센서 {key}", type, key, 1, 1, null, false, false, false);

    private static FenceWorld World(WiringShape shape, EnumDeviceType[] types, int? gap = null)
    {
        var keys = Enumerable.Range(1, types.Length).ToArray();
        var chain = WiringChain.Create(shape, keys, controllerGap: gap);
        var sensors = keys.ToDictionary(k => k, k =>
        {
            var n = chain.NumberOf(k)!;
            return S(k, types[k - 1]) with { Line = n.Line, Order = n.Order, OppositeOrder = n.OppositeOrder };
        });
        return FenceWorld.Build(chain, sensors);
    }

    private const EnumDeviceType Sm = EnumDeviceType.SmartSensor2;
    private const EnumDeviceType M = EnumDeviceType.Multi;
    private const EnumDeviceType F = EnumDeviceType.Fence;
    private const EnumDeviceType U = EnumDeviceType.Underground;

    #region - Positions -
    [Fact]
    public void should_space_ring_sensors_by_six_metres_times_units_per_metre_when_smart()
    {
        var w = World(WiringShape.Ring, new[] { Sm, Sm, Sm, Sm });

        Assert.Equal(new[] { 0.0, 68, 136, 204 }, w.Seq.Select(k => w.X[k]).Select(x => System.Math.Round(x, 6)));
        Assert.Equal(w.GapMid(2), w.ControllerX, 6);                      // 함체 = 가운데 틈
    }

    [Fact]
    public void should_put_the_controller_at_zero_and_branches_on_both_sides_when_two_branch()
    {
        var w = World(WiringShape.TwoBranch, new[] { F, M, M, F }, gap: 2);

        Assert.Equal(0, w.ControllerX);
        Assert.True(w.X[1] < w.X[2] && w.X[2] < 0);                       // 왼쪽 가지는 음수, 제어기 쪽이 오른쪽 끝
        Assert.True(0 < w.X[3] && w.X[3] < w.X[4]);
        Assert.Equal(-5 * w.Upm, w.X[2], 6);                               // 제어기 → 첫 센서 5m
        Assert.Equal(-(5 + 2.5) * w.Upm, w.X[1], 6);                       // 복합 ↔ 펜스 = 작은 쪽 2.5m
    }

    [Fact]
    public void should_start_the_line_after_the_controller_gap_when_line()
    {
        var w = World(WiringShape.Line, new[] { U, U });

        Assert.Equal(12.5 * w.Upm, w.X[1], 6);
        Assert.Equal((12.5 + 25) * w.Upm, w.X[2], 6);
    }
    #endregion

    #region - Drop · insertion -
    [Fact]
    public void should_count_sensors_left_of_the_pointer_when_dropping_on_a_ring()
    {
        var w = World(WiringShape.Ring, new[] { Sm, Sm, Sm, Sm });

        Assert.Equal((1, 0), w.DropAt(-10));
        Assert.Equal((1, 2), w.DropAt(100));
        Assert.Equal((1, 4), w.DropAt(1000));
    }

    [Fact]
    public void should_pick_the_branch_by_side_and_count_from_the_controller_when_dropping_on_two_branches()
    {
        var w = World(WiringShape.TwoBranch, new[] { Sm, Sm, Sm, Sm }, gap: 2);   // 왼쪽 [2, 1] · 오른쪽 [3, 4]

        Assert.Equal((1, 0), w.DropAt((w.X[2] + 0) / 2));                  // 제어기와 L1 사이 = 왼쪽 1번 자리
        Assert.Equal((1, 2), w.DropAt(w.X[1] - 10));                       // 왼쪽 바깥 끝
        Assert.Equal((2, 0), w.DropAt(w.X[3] / 2));                        // 제어기와 R1 사이 = 오른쪽 1번 자리
        Assert.Equal((2, 2), w.DropAt(w.X[4] + 10));
    }

    [Fact]
    public void should_place_the_insertion_bar_between_neighbours_excluding_the_dragged_sensor()
    {
        var w = World(WiringShape.Ring, new[] { Sm, Sm, Sm, Sm });

        Assert.Equal((w.X[1] + w.X[3]) / 2, w.InsertionX(w.X[2] + 5, new[] { 2 }), 6);     // 끄는 센서는 이웃이 아니다
        Assert.Equal((w.X[2] + w.X[3]) / 2, w.InsertionX(w.X[2] + 5, System.Array.Empty<int>()), 6);
        Assert.Equal(w.X[1] - 1.5 * w.Upm, w.InsertionX(-500, System.Array.Empty<int>()), 6);
    }

    [Fact]
    public void should_keep_the_insertion_bar_after_the_controller_when_line()
    {
        var w = World(WiringShape.Line, new[] { U, U });

        Assert.Equal((w.ControllerX + w.X[1]) / 2, w.InsertionX(-1000, System.Array.Empty<int>()), 6);
    }

    [Fact]
    public void should_snap_the_enclosure_to_the_nearest_gap_when_dragged()
    {
        var w = World(WiringShape.Ring, new[] { Sm, Sm, Sm, Sm, Sm, Sm });

        Assert.Equal(0, w.NearestGap(-500));
        Assert.Equal(1, w.NearestGap((w.X[1] + w.X[2]) / 2 + 3));
        Assert.Equal(6, w.NearestGap(5000));
    }
    #endregion

    #region - Units · grouping (FR-18) -
    [Fact]
    public void should_group_fence_runs_between_other_sensors_and_the_controller_when_grouped()
    {
        var w = World(WiringShape.TwoBranch, new[] { F, F, F, M, F, F, F, F }, gap: 4);

        var grouped = w.Units(grouped: true);
        var single = w.Units(grouped: false);

        Assert.Equal(new[] { "G3", "S4", "C", "G4" }, grouped.Select(u => u.IsController ? "C" : u.IsGroup ? $"G{u.Count}" : $"S{u.Key}"));
        Assert.Equal(9, single.Count);                                      // 센서 8 + 제어기
        Assert.True(w.ShouldGroup(0.5));
        Assert.False(w.ShouldGroup(0.8));
    }

    [Fact]
    public void should_never_group_when_no_fence_sensor_is_on_the_chain()
        => Assert.False(World(WiringShape.Ring, new[] { Sm, Sm }).ShouldGroup(0.2));
    #endregion

    #region - Projection · scene -
    [Fact]
    public void should_keep_left_to_right_order_of_chips_when_switching_between_tilt_and_flat()
    {
        var w = World(WiringShape.TwoBranch, new[] { M, F, Sm, F, U }, gap: 2);

        foreach (var p in new[] { FenceProjector.Tilt, FenceProjector.Flat })
        {
            var anchors = w.Seq.Select(k => p.P(w.X[k], 0, FenceProjector.PD * p.K / 2).X).ToList();
            Assert.Equal(anchors.OrderBy(x => x), anchors);
        }
        Assert.NotEqual(FenceProjector.Tilt.P(0, 100, 0).Y, FenceProjector.Flat.P(0, 100, 0).Y);
    }

    [Fact]
    public void should_draw_two_return_cables_and_vbus_markers_when_ring_has_enough_sensors()
    {
        var w = World(WiringShape.Ring, Enumerable.Repeat(Sm, 12).ToArray());

        var shapes = FenceScene.Static(w, FenceProjector.Tilt, false, w.ControllerX, w.Chain.ControllerGap);

        Assert.Equal(2, shapes.Count(s => s.Ink == FenceInk.ReturnOuter));
        Assert.Equal(2, shapes.Count(s => s.Ink == FenceInk.VbusFront));
        Assert.Contains(shapes, s => s.Text?.StartsWith("◀ 리턴케이블 · Sensor A") == true);
        Assert.Equal(12, shapes.Count(s => s.Ink == FenceInk.PillPort));      // A/B 번호 알약마다
    }

    [Fact]
    public void should_show_range_ellipses_and_a_gap_marker_when_range_is_on_and_underground_sensors_are_far_apart()
    {
        var w = World(WiringShape.Line, new[] { U, U, U });                 // 25m 간격 · 반경 15m → 빈틈 없음

        Assert.Empty(w.RangeGaps());
        var measured = FenceWorld.Build(w.Chain, w.Sensors.ToDictionary(p => p.Key, p => p.Value), new Dictionary<int, double> { [3] = 40 });
        var gap = Assert.Single(measured.RangeGaps());
        Assert.Equal(10, gap.Metres, 6);                                    // 40 − 15 − 15

        var shapes = FenceScene.Static(measured, FenceProjector.Tilt, true, measured.ControllerX, 0);
        Assert.Equal(3, shapes.Count(s => s.Ink == FenceInk.Range));
        Assert.Contains(shapes, s => s.Ink == FenceInk.GapText && s.Text == "빈틈 10m");
    }

    [Fact]
    public void should_mark_suggested_and_changed_corners_and_the_selection_when_building_a_sensor_chip()
    {
        var s = new FenceSensor(1, 1, 101, "센서", Sm, 1, 1, 3, 5, IsSuggested: true, IsChanged: true, IsDuplicateNumber: false);

        var picture = FenceScene.Sensor(s, WiringShape.Ring, FenceProjector.Tilt, selected: true);

        Assert.Contains(picture.Shapes, x => x.Ink == FenceInk.Proposal);
        Assert.Contains(picture.Shapes, x => x.Ink == FenceInk.Draft);
        Assert.Contains(picture.Shapes, x => x.Ink == FenceInk.Select);
        Assert.Contains(picture.Shapes, x => x.Ink == FenceInk.Number && x.Text == "3");
        Assert.True(picture.Hit.Width > 20 && picture.Hit.Height > 40);
    }

    [Fact]
    public void should_contain_every_post_and_the_enclosure_when_computing_fit_bounds()
    {
        var w = World(WiringShape.Ring, Enumerable.Repeat(Sm, 10).ToArray());
        var p = FenceProjector.Tilt;

        var bounds = w.FitBounds(p);

        Assert.True(bounds.Contains(p.P(w.MinX, FenceProjector.H, 0)));
        Assert.True(bounds.Contains(p.P(w.MaxX, 0, 0)));
        Assert.True(bounds.Contains(p.P(w.ControllerX, 0, 130)));
    }
    #endregion
}
