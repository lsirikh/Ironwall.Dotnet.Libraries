using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>PRD FR-12 — 형태 축 상태 머신. 색 축 이벤트(Intrusion/Fault)는 형태를 바꾸지 않는다.</summary>
public class DoorStateMachineTests
{
    [Theory]
    [InlineData(EnumDoorState.Closed, EnumEventType.ContactOn, true, EnumDoorState.Open)]
    [InlineData(EnumDoorState.Open, EnumEventType.ContactOff, true, EnumDoorState.Closed)]
    [InlineData(EnumDoorState.Unknown, EnumEventType.ContactOn, true, EnumDoorState.Open)]
    [InlineData(EnumDoorState.Unknown, EnumEventType.ContactOff, true, EnumDoorState.Closed)]
    public void should_transition_when_contact_event(EnumDoorState cur, EnumEventType evt, bool openOnContactOn, EnumDoorState expected)
        => Assert.Equal(expected, DoorStateMachine.Next(cur, evt, openOnContactOn));

    [Theory]
    [InlineData(EnumEventType.ContactOn, EnumDoorState.Closed)]
    [InlineData(EnumEventType.ContactOff, EnumDoorState.Open)]
    public void should_invert_when_open_on_contact_on_false(EnumEventType evt, EnumDoorState expected)
        => Assert.Equal(expected, DoorStateMachine.Next(EnumDoorState.Unknown, evt, openOnContactOn: false));

    [Theory]
    [InlineData(EnumDoorState.Open, EnumEventType.Intrusion)]
    [InlineData(EnumDoorState.Open, EnumEventType.Fault)]
    [InlineData(EnumDoorState.Closed, EnumEventType.Connection)]
    [InlineData(EnumDoorState.Unknown, EnumEventType.Action)]
    public void should_keep_state_when_non_contact_event(EnumDoorState cur, EnumEventType evt)
        => Assert.Equal(cur, DoorStateMachine.Next(cur, evt));

    [Theory]
    [InlineData("OPEN", EnumDoorState.Open)]
    [InlineData("open", EnumDoorState.Open)]
    [InlineData("CLOSED", EnumDoorState.Closed)]
    [InlineData(null, EnumDoorState.Unknown)]
    [InlineData("", EnumDoorState.Unknown)]
    [InlineData("AJAR", EnumDoorState.Unknown)]
    public void should_parse_server_status_when_booting(string? status, EnumDoorState expected)
        => Assert.Equal(expected, DoorStateMachine.FromServer(status));

    [Theory]
    [InlineData("GATE_OPEN", EnumDoorState.Open)]
    [InlineData("GATE_CLOSED", EnumDoorState.Closed)]
    [InlineData("ENCLOSURE_OPEN", EnumDoorState.Open)]
    [InlineData("ENCLOSURE_CLOSED", EnumDoorState.Closed)]
    [InlineData("UPS_BATTERY_LOW", null)]
    [InlineData(null, null)]
    public void should_map_operation_event_type_when_door_related(string? typeEvent, EnumDoorState? expected)
        => Assert.Equal(expected, DoorStateMachine.FromOperationEvent(typeEvent));

    [Fact]
    public void should_display_closed_when_unknown()
    {
        Assert.Equal(EnumDoorState.Closed, DoorStateMachine.Effective(EnumDoorState.Unknown));
        Assert.Equal(0.0, DoorStateMachine.OpenFraction(EnumDoorState.Unknown));
        Assert.Equal(1.0, DoorStateMachine.OpenFraction(EnumDoorState.Open));
    }

    [Fact]
    public void should_classify_contact_events_and_door_devices()
    {
        Assert.True(DoorStateMachine.IsContactEvent(EnumEventType.ContactOn));
        Assert.True(DoorStateMachine.IsContactEvent(EnumEventType.ContactOff));
        Assert.False(DoorStateMachine.IsContactEvent(EnumEventType.Intrusion));
        Assert.True(DoorStateMachine.HasDoor(EnumDeviceType.Gate));
        Assert.True(DoorStateMachine.HasDoor(EnumDeviceType.Enclosure));
        Assert.False(DoorStateMachine.HasDoor(EnumDeviceType.Contact));   // 접점 센서 자체는 문이 없다
    }

    [Fact]
    public void should_have_gate_enum_value_21_after_smartmultisensor2()
    {
        Assert.Equal(21, (int)EnumDeviceType.Gate);
        Assert.Equal(20, (int)EnumDeviceType.SmartMultisensor2);
        Assert.Equal("Gate", EnumDeviceType.Gate.ToString());   // 서버 계약은 이름 기반 문자열
    }
}
