using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 높이 단계(위 · 아래로 올리고 내리기) · 윤형 코일 자리 · 펜스센서 기본 자리(윤형과 같이 배치) — 순수 함수(<see cref="FenceLayoutMath"/>).
/// </summary>
public class FenceHeightStopsTests
{
    private static IReadOnlyList<FencePanelSpec> Panels(params EnumFenceStyle[] styles) => styles.Select(s => FencePanelSpec.Default(s)).ToList();

    private static readonly IReadOnlyList<FencePanelSpec> Razor3 = Panels(EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.ChainLinkRazor);

    #region - Stops per position kind -
    [Theory]
    [InlineData(EnumFenceStyle.ChainLink, new[] { FenceMountSpot.PanelBottom, FenceMountSpot.PanelCenter, FenceMountSpot.PanelTop })]
    [InlineData(EnumFenceStyle.DesignFence, new[] { FenceMountSpot.PanelBottom, FenceMountSpot.PanelCenter, FenceMountSpot.PanelTop })]
    [InlineData(EnumFenceStyle.ChainLinkRazor, new[] { FenceMountSpot.PanelBottom, FenceMountSpot.PanelCenter, FenceMountSpot.PanelTop, FenceMountSpot.RazorCoil })]
    [InlineData(EnumFenceStyle.Brick, new[] { FenceMountSpot.WallBottom, FenceMountSpot.WallFace, FenceMountSpot.WallTop })]
    [InlineData(EnumFenceStyle.Concrete, new[] { FenceMountSpot.WallBottom, FenceMountSpot.WallFace, FenceMountSpot.WallTop })]
    public void should_give_a_mid_panel_sensor_only_mid_panel_stops_bottom_to_top_for_each_style(EnumFenceStyle style, FenceMountSpot[] expected)
    {
        var panels = Panels(style, style);
        var spot = FencePanelSpec.IsWallStyle(style) ? FenceMountSpot.WallFace : FenceMountSpot.PanelCenter;

        Assert.Equal(expected, FenceLayoutMath.HeightStops(new SensorMountSpec(1, spot), panels));
    }

    [Theory]
    [InlineData(EnumFenceStyle.ChainLink)]
    [InlineData(EnumFenceStyle.ChainLinkRazor)]
    [InlineData(EnumFenceStyle.DesignFence)]
    public void should_give_a_post_sensor_only_post_stops_and_no_coil_even_beside_razor(EnumFenceStyle style)
    {
        var panels = Panels(style, style);

        Assert.Equal(new[] { FenceMountSpot.PostMiddle, FenceMountSpot.PostTop }, FenceLayoutMath.HeightStops(new SensorMountSpec(1, FenceMountSpot.PostTop), panels));
        Assert.Equal(new[] { FenceMountSpot.PostMiddle, FenceMountSpot.PostTop }, FenceLayoutMath.HeightStops(new SensorMountSpec(1, FenceMountSpot.PostMiddle), panels));
    }

    [Fact]
    public void should_stop_a_post_sensor_beside_razor_at_the_post_top_below_the_coil()
    {
        var top = new SensorMountSpec(1, FenceMountSpot.PostTop);

        Assert.Same(top, FenceLayoutMath.StepStop(top, 1, Razor3));                         // 기둥에는 코일 자리가 없다 — 기둥 위에서 멈춘다
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PostMiddle), FenceLayoutMath.StepStop(top, -1, Razor3));
    }

    [Fact]
    public void should_read_an_upper_lane_sensor_off_the_coil_as_above_every_stop()
    {
        Assert.Equal(2, FenceLayoutMath.StopLevel(new SensorMountSpec(1, FenceMountSpot.PostTop, Lane: FenceLane.Upper), Razor3));
        Assert.Equal(3, FenceLayoutMath.StopLevel(new SensorMountSpec(1, FenceMountSpot.PanelCenter, Lane: FenceLane.Upper), Panels(EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLink)));
        Assert.Equal(3, FenceLayoutMath.StopLevel(new SensorMountSpec(1, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper), Razor3));
        Assert.DoesNotContain(FenceMountSpot.RazorCoil, FenceLayoutMath.HeightStops(new SensorMountSpec(1, FenceMountSpot.PanelCenter), Razor3, FenceSensorCategory.Smart));   // 코일은 펜스센서만
    }
    #endregion

    #region - Stepping · snapping (가로 자리는 그대로) -
    [Fact]
    public void should_climb_a_mid_panel_sensor_into_the_coil_and_stop_at_the_top()
    {
        // Arrange — 윤형 망 1 아래
        var m = new SensorMountSpec(1, FenceMountSpot.PanelBottom);

        // Act
        var center = FenceLayoutMath.StepStop(m, 1, Razor3);
        var top = FenceLayoutMath.StepStop(center, 1, Razor3);
        var coil = FenceLayoutMath.StepStop(top, 1, Razor3);
        var over = FenceLayoutMath.StepStop(coil, 1, Razor3);

        // Assert — 망 1 아래 → 가운데 → 위 → 코일(위 줄) · 맨 위에서는 그대로 · 맨 아래에서 내리기도 그대로
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelCenter), center);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelTop), top);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper), coil);
        Assert.Same(coil, over);
        Assert.Same(m, FenceLayoutMath.StepStop(m, -1, Razor3));
    }

    [Fact]
    public void should_switch_to_the_upper_lane_entering_the_coil_and_back_to_the_lower_lane_leaving_it_without_moving_sideways()
    {
        var geometry = FenceLayoutMath.Geometry(Razor3);
        var center = new SensorMountSpec(2, FenceMountSpot.PanelTop, Column: FenceColumn.Left);

        var coil = FenceLayoutMath.StepStop(center, 1, Razor3);
        var back = FenceLayoutMath.StepStop(coil, -1, Razor3);

        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper, 2, FenceColumn.Left), (coil.Spot, coil.Lane, coil.Panel, coil.Column));
        Assert.Equal((FenceMountSpot.PanelTop, FenceLane.Lower, 2, FenceColumn.Left), (back.Spot, back.Lane, back.Panel, back.Column));
        Assert.Equal(FenceLayoutMath.PointOf(center, geometry).XM, FenceLayoutMath.PointOf(coil, geometry).XM, 9);     // 코일로 들어가도 x 그대로
    }

    private static readonly EnumFenceStyle[] Mixed =
    {
        EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.Brick, EnumFenceStyle.Concrete, EnumFenceStyle.DesignFence, EnumFenceStyle.ChainLinkRazor,
    };

    public static IEnumerable<object[]> EveryPositionKind()
    {
        for (var i = 0; i < Mixed.Length; i++)
        {
            var wall = FencePanelSpec.IsWallStyle(Mixed[i]);
            foreach (var spot in wall ? new[] { FenceMountSpot.WallBottom, FenceMountSpot.WallFace, FenceMountSpot.WallTop }
                                      : new[] { FenceMountSpot.PanelBottom, FenceMountSpot.PanelCenter, FenceMountSpot.PanelTop, FenceMountSpot.PostTop, FenceMountSpot.PostMiddle })
                yield return new object[] { i, spot, FenceLane.Lower };
            if (!wall) yield return new object[] { i, FenceMountSpot.PostTop, FenceLane.Upper };
            if (!wall) yield return new object[] { i, FenceMountSpot.PanelCenter, FenceLane.Upper };
            if (Mixed[i] == EnumFenceStyle.ChainLinkRazor) yield return new object[] { i, FenceMountSpot.RazorCoil, FenceLane.Upper };
        }
    }

    [Theory]
    [MemberData(nameof(EveryPositionKind))]
    public void should_never_move_a_sensor_sideways_on_any_height_step(int panel, FenceMountSpot spot, FenceLane lane)
    {
        // Arrange — 모양이 섞인 펜스(철조망 · 윤형 · 벽돌 · 시멘트 · 디자인 · 윤형)
        var panels = Panels(Mixed);
        var geometry = FenceLayoutMath.Geometry(panels);
        var start = FenceLayoutMath.Normalize(new SensorMountSpec(panel, spot, Lane: lane), panels);
        var x = FenceLayoutMath.PointOf(start, geometry).XM;

        // Act + Assert — 맨 위까지 한 칸씩, 다시 맨 아래까지 한 칸씩 · 한 번에 여러 칸도
        var m = start;
        for (var i = 0; i < 8; i++)
        {
            m = FenceLayoutMath.StepStop(m, i < 4 ? 1 : -1, panels);
            Assert.Equal(x, FenceLayoutMath.PointOf(m, geometry).XM, 9);
            Assert.Equal((start.IsPostSpot, start.Panel), (m.IsPostSpot, m.Panel));
        }
        foreach (var delta in new[] { 10, -10, 2, -2 })
            Assert.Equal(x, FenceLayoutMath.PointOf(FenceLayoutMath.StepStop(start, delta, panels), geometry).XM, 9);
    }

    [Fact]
    public void should_snap_several_stops_at_once_and_clamp_at_the_end()
    {
        var m = new SensorMountSpec(0, FenceMountSpot.PanelBottom, HeightOffsetM: 0.4);

        var top = FenceLayoutMath.StepStop(m, 10, Razor3);
        var bottom = FenceLayoutMath.StepStop(top, -10, Razor3);

        Assert.Equal(FenceMountSpot.RazorCoil, top.Spot);
        Assert.Equal(0, top.HeightOffsetM);                                               // 단계를 바꾸면 미세 높이는 새로
        Assert.Equal((FenceMountSpot.PanelBottom, FenceLane.Lower), (bottom.Spot, bottom.Lane));
    }

    [Fact]
    public void should_step_an_upper_lane_sensor_off_the_coil_down_to_its_top_stop_on_the_lower_lane()
    {
        var chainLink = Panels(EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLink);
        var upper = new SensorMountSpec(1, FenceMountSpot.PostTop, Lane: FenceLane.Upper);

        var down = FenceLayoutMath.StepStop(upper, -1, chainLink);
        var up = FenceLayoutMath.StepStop(upper, 1, chainLink);

        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PostTop), down);              // 예전 Alt+↓ "아래 줄로"
        Assert.Same(upper, up);                                                           // 이미 모든 단계 위
    }

    [Fact]
    public void should_step_walls_between_face_and_top()
    {
        var walls = Panels(EnumFenceStyle.Brick, EnumFenceStyle.Concrete);

        var top = FenceLayoutMath.StepStop(new SensorMountSpec(1, FenceMountSpot.WallFace), 1, walls);

        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.WallTop), top);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.WallFace), FenceLayoutMath.StepStop(top, -1, walls));
    }

    [Theory]
    [InlineData(0.0, 0.1, 0.1)]
    [InlineData(2.95, 0.1, 3.0)]
    [InlineData(-3.0, -0.1, -3.0)]
    public void should_nudge_the_fine_height_by_a_tenth_within_the_offset_range(double start, double delta, double expected)
        => Assert.Equal(expected, FenceLayoutMath.Nudge(new SensorMountSpec(0, FenceMountSpot.PanelCenter, start), delta).HeightOffsetM, 6);
    #endregion

    #region - Coil spot · normalize · heights -
    [Fact]
    public void should_keep_the_coil_spot_only_on_the_upper_lane_of_a_razor_panel()
    {
        var mixed = Panels(EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.ChainLink, EnumFenceStyle.Brick);

        Assert.Equal(FenceMountSpot.RazorCoil, FenceLayoutMath.Normalize(new SensorMountSpec(0, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper), mixed).Spot);
        Assert.Equal(FenceMountSpot.PanelCenter, FenceLayoutMath.Normalize(new SensorMountSpec(0, FenceMountSpot.RazorCoil), mixed).Spot);            // 아래 줄
        Assert.Equal(FenceMountSpot.PanelCenter, FenceLayoutMath.Normalize(new SensorMountSpec(1, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper), mixed).Spot);
        Assert.Equal(FenceMountSpot.WallTop, FenceLayoutMath.Normalize(new SensorMountSpec(2, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper), mixed).Spot);
        Assert.Equal(FenceMountSpot.WallBottom, FenceLayoutMath.Normalize(new SensorMountSpec(2, FenceMountSpot.PanelBottom), mixed).Spot);       // 9점 격자 — 같은 줄의 담 자리
    }

    [Fact]
    public void should_put_panel_bottom_low_and_the_coil_above_the_fence_top()
    {
        var geometry = FenceLayoutMath.Geometry(Razor3);
        var h = Razor3[0].HeightM;

        var bottom = FenceLayoutMath.PointOf(new SensorMountSpec(0, FenceMountSpot.PanelBottom), geometry);
        var coil = FenceLayoutMath.LaneHeightM(new SensorMountSpec(0, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper), geometry, twoLanes: true);
        var upperOff = FenceLayoutMath.LaneHeightM(new SensorMountSpec(0, FenceMountSpot.PostTop, Lane: FenceLane.Upper), geometry, twoLanes: true);

        Assert.Equal(h * FenceLayoutMath.PANEL_BOTTOM_RATIO, bottom.HeightM, 6);
        Assert.Equal(FenceLayoutMath.RazorCoilCenterM(h), coil, 6);
        Assert.InRange(coil, h + h * 0.15, h + h * 0.25);                                // 코일 가운데(지름 = 높이 40%)
        Assert.True(upperOff > coil);                                                      // 코일 밖 위 줄은 코일 위
    }
    #endregion

    #region - Defaults (펜스센서는 윤형과 같이) -
    [Fact]
    public void should_default_a_fence_sensor_to_the_coil_on_razor_and_to_the_panel_center_lower_lane_elsewhere()
    {
        var mixed = Panels(EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.ChainLink);

        var onRazor = FenceLayoutMath.DefaultMount(new SensorMountSpec(0, FenceMountSpot.PanelCenter), FenceSensorCategory.Fence, mixed);
        var plain = FenceLayoutMath.DefaultMount(new SensorMountSpec(1, FenceMountSpot.PanelCenter, Lane: FenceLane.Upper), FenceSensorCategory.Fence, mixed);
        var smart = FenceLayoutMath.DefaultMount(new SensorMountSpec(0, FenceMountSpot.PanelCenter), FenceSensorCategory.Smart, mixed);

        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), (onRazor.Spot, onRazor.Lane));
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), (plain.Spot, plain.Lane));
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), (smart.Spot, smart.Lane));
    }

    [Fact]
    public void should_seat_a_fence_sensor_in_the_coil_when_its_lane_becomes_upper_on_a_razor_panel_and_back_when_lower()
    {
        var up = FenceLayoutMath.WithLane(new SensorMountSpec(1, FenceMountSpot.PanelCenter), FenceLane.Upper, FenceSensorCategory.Fence, Razor3);
        var down = FenceLayoutMath.WithLane(up, FenceLane.Lower, FenceSensorCategory.Fence, Razor3);
        var smart = FenceLayoutMath.WithLane(new SensorMountSpec(1, FenceMountSpot.PanelCenter), FenceLane.Upper, FenceSensorCategory.Smart, Razor3);

        Assert.Equal(FenceMountSpot.RazorCoil, up.Spot);
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), (down.Spot, down.Lane));
        Assert.Equal(FenceMountSpot.PanelCenter, smart.Spot);                             // 펜스센서만 코일에
    }

    [Fact]
    public void should_seat_a_new_fence_sensor_joining_the_upper_lane_on_razor_in_the_coil_without_overriding_the_chain_lane()
    {
        // Arrange — 위 줄 코일 센서 1 · 2(사슬 1 → 2) · 새 펜스센서 3 이 그 사이 · 새 펜스센서 4 는 아래 줄 센서 5 앞
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [5] = new(0, FenceMountSpot.PanelCenter),
            [1] = new(2, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper),
            [2] = new(0, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper),
        };
        var oldOrder = new[] { 5, 1, 2 };

        // Act
        var (result, _) = FenceLayoutMath.Reconcile(oldOrder, new[] { 4, 5, 1, 3, 2 }, mounts, Razor3, _ => FenceSensorCategory.Fence);

        // Assert — 3 은 위 줄 이웃 사이라 코일, 4 는 사슬 자리대로 아래 줄(서버 순서를 바꾸지 않는다)
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), (result[3].Spot, result[3].Lane));
        Assert.Equal(FenceLane.Lower, result[4].Lane);
        Assert.NotEqual(FenceMountSpot.RazorCoil, result[4].Spot);
        Assert.Equal(mounts[1], result[1]);                                                // 저장된 자리는 그대로
    }

    [Fact]
    public void should_keep_new_spot_names_through_the_local_document_json()
    {
        var document = new FenceLayoutDocument
        {
            ControllerId = 3,
            Panels = Razor3.ToList(),
            Mounts = new Dictionary<int, SensorMountSpec>
            {
                [7] = new(1, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper),
                [8] = new(2, FenceMountSpot.PanelBottom, 0.3),
            },
        };

        var json = FenceLayoutJson.Serialize(document);
        var back = FenceLayoutJson.Deserialize(json)!;

        Assert.Contains("\"RazorCoil\"", json);
        Assert.Contains("\"PanelBottom\"", json);
        Assert.Equal(document.Mounts[7], back.Mounts[7]);
        Assert.Equal(document.Mounts[8], back.Mounts[8]);
    }
    #endregion
}
