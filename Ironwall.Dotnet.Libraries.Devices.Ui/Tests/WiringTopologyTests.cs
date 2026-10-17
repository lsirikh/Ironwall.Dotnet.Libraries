using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>결선 모양 판정(wiring-fence-view FR-16 · v0.4 §1-C) — 모든 제어기는 링.</summary>
public class WiringTopologyTests
{
    private static readonly EnumDeviceType[] Smart = { EnumDeviceType.SmartSensor2, EnumDeviceType.SmartCompound };

    [Theory]
    [InlineData("SmartController", WiringControllerKind.Smart)]
    [InlineData("Controller", WiringControllerKind.Pids)]
    [InlineData("IoController", WiringControllerKind.Io)]
    [InlineData(null, WiringControllerKind.Unknown)]
    public void should_be_ring_for_every_controller_when_deciding_the_shape(string? controller, WiringControllerKind kind)
    {
        // Arrange
        var sensors = new[] { EnumDeviceType.Multi, EnumDeviceType.Fence, EnumDeviceType.Underground };

        // Act
        var t = WiringTopology.For(controller, sensors);

        // Assert — v0.4 §1-C: 모든 제어기는 링 · 한도는 한도 표가 맡는다
        Assert.Equal(WiringShape.Ring, t.Shape);
        Assert.Equal(kind, t.ControllerKind);
        Assert.Null(t.MaxSensors);
        Assert.Equal(2, t.ReturnCableCount);
        Assert.True(t.HasOppositeNumber);
        Assert.True(t.HasVbus);
        Assert.False(t.IsProvisional);
        Assert.False(t.IsInferred);
        Assert.Null(t.MixWarning);
    }

    [Theory]
    [InlineData(new[] { EnumDeviceType.SmartSensor })]
    [InlineData(new[] { EnumDeviceType.Underground })]
    [InlineData(new[] { EnumDeviceType.Fence, EnumDeviceType.Underground })]
    [InlineData(new EnumDeviceType[0])]
    public void should_be_ring_without_inference_when_controller_type_is_unknown(EnumDeviceType[] sensors)
    {
        // Act
        var t = WiringTopology.For(null, sensors);

        // Assert
        Assert.Equal(WiringShape.Ring, t.Shape);
        Assert.False(t.IsInferred);
    }

    [Fact]
    public void should_not_warn_mix_when_smart_and_pids_sensors_share_a_controller()
    {
        // Act
        var smartOnPids = WiringTopology.For("Controller", new[] { EnumDeviceType.SmartSensor2 });
        var mixed = WiringTopology.For("SmartController", new[] { EnumDeviceType.SmartSensor2, EnumDeviceType.Fence });

        // Assert — 섞어 쓰기는 정상(옛 O-8 경고 폐기) · 알림은 검증의 Info 가 맡는다
        Assert.Null(smartOnPids.MixWarning);
        Assert.Null(mixed.MixWarning);
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
    public void should_decide_ring_from_type_texts_when_string_overload_used()
        => Assert.Equal(WiringShape.Ring, WiringTopology.For("Controller", new[] { "Underground", "underground" }).Shape);

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
