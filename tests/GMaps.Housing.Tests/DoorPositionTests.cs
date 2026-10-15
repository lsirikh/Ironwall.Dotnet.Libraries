using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>장비의 문 위치 읽기(<see cref="DoorPosition"/>) — 통문 · 함체 공용.</summary>
public class DoorPositionTests
{
    // ── 문 위치(이슈 #7 Q-4b) — 관측 축 구동부 → 문 센서 → 옛 스칼라 ──
    private static DeviceAxesModel DoorAxes(params (string Key, string Type, string? State)[] parts)
    {
        var spec = new HardwareSpecModel();
        var status = new DeviceStatusModel();
        foreach (var p in parts)
        {
            spec.Components.Add(new ComponentDefinitionModel { Key = p.Key, Type = p.Type });
            if (p.State != null) status.Components[p.Key] = new ComponentStatusModel { State = p.State };
        }
        return new DeviceAxesModel { HardwareSpec = spec, DeviceStatus = status, Meta = new ResponseMeta("full", new[] { "hardware_spec", "device_status" }) };
    }

    [Fact]
    public void should_read_the_door_sensor_when_a_gate_has_no_actuator()
    {
        var gate = new GateDeviceModel { Axes = DoorAxes(("door", "DOOR_SENSOR", "OPEN"), ("lock", "DOOR_LOCK", "LOCKED")) };
        Assert.Equal("OPEN", DoorPosition.Of(gate));
    }

    [Fact]
    public void should_prefer_the_actuator_over_the_sensor()
    {
        var gate = new GateDeviceModel { Axes = DoorAxes(("door", "DOOR_SENSOR", "CLOSED"), ("actuator", "DOOR_ACTUATOR", "RUNNING")) };
        Assert.Equal("RUNNING", DoorPosition.Of(gate));
    }

    [Fact]
    public void should_fall_back_to_the_legacy_scalar_when_axes_have_no_door_state()
    {
        Assert.Equal("CLOSED", DoorPosition.Of(new GateDeviceModel { GateStatus = "CLOSED" }));
        Assert.Equal("OPEN", DoorPosition.Of(new EnclosureDeviceModel { DoorStatus = "OPEN", Axes = DoorAxes(("door", "DOOR_SENSOR", null)) }));
        Assert.Null(DoorPosition.Of(new GateDeviceModel { GateStatus = "" }));
        Assert.Null(DoorPosition.Of(null));
    }
}
