using Xunit;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : EventEntryEnqueuedMessage 단위 테스트
   Created By   : GHLee
   Created On   : 2026-03-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventEntryEnqueuedMessageTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange & Act
        var msg = new EventEntryEnqueuedMessage(
            entryId: "uuid-abc-123",
            eventId: 29651,
            deviceId: 42,
            deviceType: EnumDeviceType.Fence,
            eventType: EnumEventType.Intrusion);

        // Assert
        Assert.Equal("uuid-abc-123", msg.EntryId);
        Assert.Equal(29651, msg.EventId);
        Assert.Equal(42, msg.DeviceId);
        Assert.Equal(EnumDeviceType.Fence, msg.DeviceType);
        Assert.Equal(EnumEventType.Intrusion, msg.EventType);
    }

    [Fact]
    public void Constructor_DifferentEventTypes_ShouldBeDistinguishable()
    {
        // Act
        var detection = new EventEntryEnqueuedMessage("e1", 100, 1, EnumDeviceType.Fence, EnumEventType.Intrusion);
        var malfunction = new EventEntryEnqueuedMessage("e2", 200, 2, EnumDeviceType.Fence, EnumEventType.Fault);

        // Assert
        Assert.Equal(EnumEventType.Intrusion, detection.EventType);
        Assert.Equal(EnumEventType.Fault, malfunction.EventType);
        Assert.NotEqual(detection.EntryId, malfunction.EntryId);
    }
}
