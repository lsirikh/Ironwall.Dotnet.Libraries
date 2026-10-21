using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 지도 아이콘의 문 위치 찾는 순서(GOP API 8.0.15 대조 G34) — 함체는 문 센서 → 구동부, 통문은 구동부 → 문 센서.
/// </summary>
public class ComponentDoorOrderTests
{
    private static Ironwall.Dotnet.Monitoring.Models.Devices.DeviceAxesModel BothDoorParts() =>
        ComponentHealthSummaryTests.Axes(true,
            ("door", "DOOR_SENSOR", null, null, "OPEN", "OK", null, true),
            ("actuator", "DOOR_ACTUATOR", null, null, "CLOSED", "OK", null, true));

    [Fact]
    public void should_read_door_sensor_first_for_enclosure()
        => Assert.Equal(DoorMotionState.Open, ComponentHealthSummary.Build(BothDoorParts(), deviceType: EnumDeviceType.Enclosure).DoorMotion);

    [Fact]
    public void should_read_actuator_first_for_gate()
        => Assert.Equal(DoorMotionState.Closed, ComponentHealthSummary.Build(BothDoorParts(), deviceType: EnumDeviceType.Gate).DoorMotion);
}
