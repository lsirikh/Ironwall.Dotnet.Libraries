using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 문 위치 부품 찾는 순서(GOP API 8.0.15 대조 G34) — 함체는 문 센서 → 구동부 → 관례 key, 통문은 구동부 → 문 센서 → 관례 key.
/// </summary>
public class DoorPositionOrderTests
{
    private static DeviceAxesModel Axes(bool declare, params (string Key, string Type, string? State)[] parts)
    {
        var spec = new HardwareSpecModel();
        var status = new DeviceStatusModel();
        foreach (var p in parts)
        {
            if (declare) spec.Components.Add(new ComponentDefinitionModel { Key = p.Key, Type = p.Type });
            if (p.State != null) status.Components[p.Key] = new ComponentStatusModel { State = p.State };
        }
        return new DeviceAxesModel { HardwareSpec = spec, DeviceStatus = status };
    }

    [Fact]
    public void should_prefer_door_sensor_over_actuator_on_enclosure()
    {
        var enclosure = new EnclosureDeviceModel { Axes = Axes(true, ("door", "DOOR_SENSOR", "OPEN"), ("actuator", "DOOR_ACTUATOR", "CLOSED")) };

        Assert.Equal("OPEN", DoorPosition.Of(enclosure));
    }

    [Fact]
    public void should_prefer_actuator_over_door_sensor_on_gate()
    {
        var gate = new GateDeviceModel { Axes = Axes(true, ("door", "DOOR_SENSOR", "CLOSED"), ("actuator", "DOOR_ACTUATOR", "RUNNING")) };

        Assert.Equal("RUNNING", DoorPosition.Of(gate));
    }

    [Fact]
    public void should_use_actuator_on_enclosure_when_it_has_no_door_sensor()
    {
        var enclosure = new EnclosureDeviceModel { Axes = Axes(true, ("motor", "DOOR_ACTUATOR", "RUNNING")) };

        Assert.Equal("RUNNING", DoorPosition.Of(enclosure));
    }

    [Fact]
    public void should_fall_back_to_conventional_key_when_shape_is_not_declared()
    {
        var enclosure = new EnclosureDeviceModel { Axes = Axes(false, ("door", "DOOR_SENSOR", "OPEN"), ("actuator", "DOOR_ACTUATOR", "CLOSED")) };
        var gate = new GateDeviceModel { Axes = Axes(false, ("door", "DOOR_SENSOR", "OPEN"), ("actuator", "DOOR_ACTUATOR", "CLOSED")) };

        Assert.Equal("OPEN", DoorPosition.Of(enclosure));     // 함체 key 순서: door → actuator
        Assert.Equal("CLOSED", DoorPosition.Of(gate));        // 통문 key 순서: actuator → door
    }

    [Fact]
    public void should_not_read_conventional_key_when_it_is_declared_as_another_type()
    {
        // 'door' 라는 key 가 잠금(DOOR_LOCK)으로 선언돼 있으면 문 위치로 읽지 않는다 — 옛 스칼라로 내려간다.
        var enclosure = new EnclosureDeviceModel { DoorStatus = "CLOSED", Axes = Axes(true, ("door", "DOOR_LOCK", "LOCKED")) };

        Assert.Equal("CLOSED", DoorPosition.Of(enclosure));
    }

    [Fact]
    public void should_expose_enclosure_and_gate_orders()
    {
        Assert.Equal("DOOR_SENSOR", DoorPosition.OrderFor(new EnclosureDeviceModel())[0].Type);
        Assert.Equal("DOOR_ACTUATOR", DoorPosition.OrderFor(new GateDeviceModel())[0].Type);
    }
}
