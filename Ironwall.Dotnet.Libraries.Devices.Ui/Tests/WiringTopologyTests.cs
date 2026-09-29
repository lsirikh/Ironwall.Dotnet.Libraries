using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>결선 모양 판정(wiring-fence-view FR-16 · FR-14 ④ · O-6 ~ O-8) — 제어기 종류 → 링 · 양쪽 가지 · 한 줄.</summary>
public class WiringTopologyTests
{
    private static readonly EnumDeviceType[] Smart = { EnumDeviceType.SmartSensor2, EnumDeviceType.SmartCompound };

    [Fact]
    public void should_be_ring_with_limit_34_when_smart_controller()
    {
        var t = WiringTopology.For("SmartController", Smart);

        Assert.Equal(WiringShape.Ring, t.Shape);
        Assert.Equal(WiringControllerKind.Smart, t.ControllerKind);
        Assert.Equal(34, t.MaxSensors);
        Assert.Equal(2, t.ReturnCableCount);
        Assert.True(t.HasOppositeNumber);
        Assert.True(t.HasVbus);
        Assert.False(t.IsProvisional);
        Assert.False(t.IsInferred);
        Assert.Null(t.MixWarning);
    }

    [Fact]
    public void should_be_provisional_two_branch_without_limit_when_pids_controller_has_fence_sensors()
    {
        var t = WiringTopology.For("Controller", new[] { EnumDeviceType.Multi, EnumDeviceType.Fence, EnumDeviceType.Fence });

        Assert.Equal(WiringShape.TwoBranch, t.Shape);
        Assert.True(t.IsProvisional);
        Assert.Null(t.MaxSensors);                                  // O-7 — 모른다
        Assert.Equal(0, t.ReturnCableCount);
        Assert.Null(t.LimitWarning(400));
    }

    [Fact]
    public void should_be_line_when_pids_controller_has_only_underground_sensors()
    {
        var t = WiringTopology.For("Controller", new[] { EnumDeviceType.Underground, EnumDeviceType.Underground });

        Assert.Equal(WiringShape.Line, t.Shape);
        Assert.False(t.IsProvisional);
    }

    [Fact]
    public void should_be_two_branch_when_pids_controller_has_no_sensor_yet()
        => Assert.Equal(WiringShape.TwoBranch, WiringTopology.For("Controller", System.Array.Empty<EnumDeviceType>()).Shape);

    [Fact]
    public void should_be_line_when_io_controller()
        => Assert.Equal(WiringShape.Line, WiringTopology.For("IoController", new[] { EnumDeviceType.Contact }).Shape);

    [Theory]
    [InlineData(new[] { EnumDeviceType.SmartSensor }, WiringShape.Ring)]
    [InlineData(new[] { EnumDeviceType.Underground }, WiringShape.Line)]
    [InlineData(new[] { EnumDeviceType.Fence, EnumDeviceType.Underground }, WiringShape.TwoBranch)]
    [InlineData(new EnumDeviceType[0], WiringShape.Ring)]
    public void should_infer_shape_from_sensors_when_controller_type_is_unknown(EnumDeviceType[] sensors, WiringShape expected)
    {
        var t = WiringTopology.For(null, sensors);

        Assert.Equal(expected, t.Shape);
        Assert.True(t.IsInferred);
    }

    [Fact]
    public void should_warn_over_product_limit_only_above_34_when_ring()
    {
        var t = WiringTopology.For("SmartController", Smart);

        Assert.Null(t.LimitWarning(34));
        Assert.Contains("34", t.LimitWarning(35));
    }

    [Fact]
    public void should_warn_mix_when_smart_sensors_hang_on_pids_controller()
    {
        var t = WiringTopology.For("Controller", new[] { EnumDeviceType.SmartSensor2 });
        Assert.NotNull(t.MixWarning);
    }

    [Fact]
    public void should_warn_mix_when_smart_and_pids_sensors_share_a_controller()
    {
        var t = WiringTopology.For("SmartController", new[] { EnumDeviceType.SmartSensor2, EnumDeviceType.Fence });
        Assert.NotNull(t.MixWarning);
        Assert.NotNull(WiringTopology.For("SmartController", new[] { EnumDeviceType.Multi }).MixWarning);
    }

    [Theory]
    [InlineData("SmartController", WiringControllerKind.Smart)]
    [InlineData(" smartcontroller ", WiringControllerKind.Smart)]
    [InlineData("Controller", WiringControllerKind.Pids)]
    [InlineData("IoController", WiringControllerKind.Io)]
    [InlineData("Radar", WiringControllerKind.Unknown)]
    [InlineData("", WiringControllerKind.Unknown)]
    [InlineData(null, WiringControllerKind.Unknown)]
    public void should_parse_server_controller_type_when_text_given(string? text, WiringControllerKind expected)
        => Assert.Equal(expected, WiringTopology.ParseControllerKind(text));

    [Theory]
    [InlineData("SmartSensor2", EnumDeviceType.SmartSensor2)]
    [InlineData("fence", EnumDeviceType.Fence)]
    [InlineData("12", EnumDeviceType.NONE)]
    [InlineData("SPEED_DOME", EnumDeviceType.NONE)]
    [InlineData(null, EnumDeviceType.NONE)]
    public void should_parse_sensor_type_text_when_text_given(string? text, EnumDeviceType expected)
        => Assert.Equal(expected, WiringTopology.ParseSensorType(text));

    [Fact]
    public void should_decide_from_type_texts_when_string_overload_used()
        => Assert.Equal(WiringShape.Line, WiringTopology.For("Controller", new[] { "Underground", "underground" }).Shape);

    [Theory]
    [InlineData(17, 34, new[] { 12, 22 })]
    [InlineData(1, 3, new[] { 0, 3 })]        // 체인 끝으로 눌러 붙인다
    [InlineData(0, 1, new[] { 0, 1 })]
    [InlineData(99, 10, new[] { 5, 10 })]     // 틈이 범위 밖이면 끝 틈으로
    public void should_place_vbus_five_slots_from_enclosure_when_ring(int gap, int count, int[] expected)
        => Assert.Equal(expected, WiringTopology.VbusGaps(gap, count).ToArray());

    [Fact]
    public void should_have_no_vbus_when_chain_is_empty()
        => Assert.Empty(WiringTopology.VbusGaps(0, 0));
}
