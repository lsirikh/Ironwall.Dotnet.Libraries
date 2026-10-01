using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>펜스 뷰 겹침 · 작은 배율 가독성(fence-wiring-editor 검토 V2 · V3) — 순수 그림 함수.</summary>
public class FenceLegibilityReviewTests
{
    private static FenceSensor Sensor(int key, EnumDeviceType type, int order)
        => new(key, key, key, $"센서 {key}", type, null, WiringSpec.LINE_PRIMARY, order, null, false, false, false);

    [Fact]
    public void should_spread_only_the_crowded_chips_and_keep_their_order_when_values_are_too_close()
    {
        var spread = FenceWorld.Separate(new[] { 0.0, 10, 20, 100 }, 30);

        Assert.Equal(new[] { -20.0, 10, 40, 100 }, spread);                  // 겹친 셋은 평균(0)을 가운데로 30 씩 · 넷째는 그대로
    }

    [Fact]
    public void should_keep_neighbouring_chips_apart_when_sensors_sit_at_a_wall_and_fence_style_boundary()
    {
        // Arrange — 디자인펜스 1m 다음 시멘트 담 1m: 기둥 1 위 센서와 담 위 센서가 거의 같은 x
        var panels = new[] { FencePanelSpec.Default(EnumFenceStyle.DesignFence, 1), FencePanelSpec.Default(EnumFenceStyle.Concrete, 1) };
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [106] = new(1, FenceMountSpot.PostTop),
            [107] = new(1, FenceMountSpot.WallTop),
        };
        var layout = WiringFenceLayout.Create(panels, mounts, null);
        var chain = WiringChain.Create(WiringShape.Ring, new[] { 106, 107 });
        var sensors = new Dictionary<int, FenceSensor> { [106] = Sensor(106, EnumDeviceType.SmartSensor2, 1), [107] = Sensor(107, EnumDeviceType.SmartSensor2, 2) };

        // Act
        var world = FenceWorld.FromLayout(chain, sensors, layout);

        // Assert
        Assert.True(world.X[107] - world.X[106] >= FenceWorld.MIN_CHIP_DX - 1e-9);
    }

    [Fact]
    public void should_shorten_the_group_card_when_it_would_cover_a_neighbour_number_at_small_zoom()
    {
        var members = Enumerable.Range(0, 5).Select(i => Sensor(201 + i, EnumDeviceType.Fence, 17 + i)).ToList();

        var roomy = FenceScene.Group(members, WiringShape.Ring, FenceProjector.Tilt, false, 0.34);
        var tight = FenceScene.Group(members, WiringShape.Ring, FenceProjector.Tilt, false, 0.34, maxWidth: 120);

        Assert.Contains(roomy.Shapes, s => s.Text == "펜스센서 ×5");
        Assert.Contains(tight.Shapes, s => s.Text == "×5");
        Assert.DoesNotContain(tight.Shapes, s => s.Ink == FenceInk.GroupSub);
        Assert.True(tight.Hit.Width < roomy.Hit.Width);
    }

    [Theory]
    [InlineData(0.34, true)]
    [InlineData(1.0, false)]
    public void should_draw_a_razor_band_instead_of_thin_coils_when_zoomed_out(double zoom, bool band)
    {
        // Arrange
        var layout = WiringFenceLayout.Create(Enumerable.Repeat(FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor), 3), new Dictionary<int, SensorMountSpec>(), null);
        var world = FenceWorld.FromLayout(WiringChain.Create(WiringShape.Ring, new int[0]), new Dictionary<int, FenceSensor>(), layout);

        // Act
        var shapes = FenceScene.StaticLayout(world, FenceProjector.Tilt, false, false, 0, 0, zoom);

        // Assert
        Assert.Equal(band, shapes.Any(s => s.Ink == FenceInk.RazorBand));
        Assert.Equal(!band, shapes.Any(s => s.Ink == FenceInk.Razor));
    }
}
