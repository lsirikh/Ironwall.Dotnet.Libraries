using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>2.5D 펜스 뷰의 줄 높이(fence-wiring-editor v0.3 FR-18) — 위 줄은 펜스 꼭대기(윗 레일 · 윤형) 위, 아래 줄은 망 위.</summary>
public class FenceLaneHeightTests
{
    private static FenceSensor Smart(int key) => new(key, key, key, $"센서 {key}", EnumDeviceType.SmartSensor2, null, WiringSpec.LINE_PRIMARY, 1, null, false, false, false);

    [Fact]
    public void should_put_the_upper_lane_above_the_razor_top_and_the_lower_post_top_on_the_mesh_when_both_lanes_have_sensors()
    {
        var g = FenceLayoutMath.Geometry(new[] { FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor, 6), FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor, 6) });
        var top = g.Posts[1].HeightM;

        var upper = FenceLayoutMath.LaneHeightM(new SensorMountSpec(1, FenceMountSpot.PostTop, Lane: FenceLane.Upper), g, twoLanes: true);
        var lower = FenceLayoutMath.LaneHeightM(new SensorMountSpec(1, FenceMountSpot.PostTop), g, twoLanes: true);
        var single = FenceLayoutMath.LaneHeightM(new SensorMountSpec(1, FenceMountSpot.PostTop), g, twoLanes: false);

        Assert.True(upper > top + FenceLayoutMath.UPPER_LANE_RAZOR_RISE_M - 1e-9);                 // 코일 위
        Assert.True(lower < top);                                                                  // 망 위
        Assert.Equal(top, single, 6);                                                              // 한 줄 현장은 그대로(기둥 위)
    }

    [Fact]
    public void should_keep_the_spot_adjusting_height_within_the_upper_lane()
    {
        var g = FenceLayoutMath.Geometry(new[] { FencePanelSpec.Default(EnumFenceStyle.ChainLink, 6), FencePanelSpec.Default(EnumFenceStyle.ChainLink, 6) });

        var postTop = FenceLayoutMath.LaneHeightM(new SensorMountSpec(1, FenceMountSpot.PostTop, Lane: FenceLane.Upper), g, true);
        var postMiddle = FenceLayoutMath.LaneHeightM(new SensorMountSpec(1, FenceMountSpot.PostMiddle, Lane: FenceLane.Upper), g, true);

        Assert.True(postTop > postMiddle);
        Assert.True(postMiddle > g.Posts[1].HeightM);                                              // 그래도 꼭대기 위
    }

    [Fact]
    public void should_lift_upper_lane_chips_above_lower_lane_chips_in_the_fence_world()
    {
        // Arrange — 같은 기둥 1 에 아래 · 위 줄 하나씩
        var layout = WiringFenceLayout.Create(Enumerable.Repeat(FencePanelSpec.Default(), 2),
            new Dictionary<int, SensorMountSpec> { [1] = new(1, FenceMountSpot.PostTop), [2] = new(1, FenceMountSpot.PostTop, Lane: FenceLane.Upper) }, null);
        var chain = WiringChain.Create(WiringShape.Ring, new[] { 1, 2 });

        // Act
        var world = FenceWorld.FromLayout(chain, new Dictionary<int, FenceSensor> { [1] = Smart(1), [2] = Smart(2) }, layout);

        // Assert — 줄이 다르면 같은 기둥이어도 옆으로 벌리지 않고 높이로 가른다
        Assert.True(world.LiftOf(2) > world.LiftOf(1) + 40);
        Assert.Equal(world.X[1], world.X[2], 6);
    }
}
