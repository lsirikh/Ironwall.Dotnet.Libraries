using Xunit;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : EventEntry 모델 단위 테스트
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventEntryTests
{
    [Fact]
    public void EventEntry_ShouldHaveAllRequiredProperties()
    {
        // Arrange
        var sourceEvent = new Mock<IExEventModel>().Object;
        var groupIds = new List<int> { 1, 2, 3 };

        // Act
        var entry = new EventEntry
        {
            EntryId = "test-uuid",
            DeviceId = 42,
            DeviceType = EnumDeviceType.Fence,
            GroupIds = groupIds,
            EventType = EnumEventType.Intrusion,
            EnqueuedAt = new DateTime(2026, 3, 8, 12, 0, 0),
            TimeoutSeconds = 30,
            SourceEvent = sourceEvent,
            IsAutoReportEnabled = true
        };

        // Assert
        Assert.Equal("test-uuid", entry.EntryId);
        Assert.Equal(42, entry.DeviceId);
        Assert.Equal(EnumDeviceType.Fence, entry.DeviceType);
        Assert.Equal(groupIds, entry.GroupIds);
        Assert.Equal(EnumEventType.Intrusion, entry.EventType);
        Assert.Equal(new DateTime(2026, 3, 8, 12, 0, 0), entry.EnqueuedAt);
        Assert.Equal(30, entry.TimeoutSeconds);
        Assert.Same(sourceEvent, entry.SourceEvent);
        Assert.True(entry.IsAutoReportEnabled);
    }

    [Fact]
    public void EventEntry_MultipleInstances_ShouldHaveUniqueUuids()
    {
        // Arrange & Act
        var entries = Enumerable.Range(0, 100)
            .Select(_ => new EventEntry())
            .ToList();

        // UUID를 외부에서 할당하는 패턴이므로,
        // Guid.NewGuid()로 생성한 EntryId가 모두 고유한지 검증
        foreach (var entry in entries)
        {
            entry.EntryId = Guid.NewGuid().ToString();
        }

        var uniqueIds = entries.Select(e => e.EntryId).Distinct().Count();

        // Assert
        Assert.Equal(100, uniqueIds);
    }

    [Fact]
    public void EventEntry_DefaultValues_ShouldBeReasonable()
    {
        // Act
        var entry = new EventEntry();

        // Assert
        Assert.Null(entry.EntryId);
        Assert.Equal(0, entry.DeviceId);
        Assert.Equal(EnumDeviceType.NONE, entry.DeviceType);
        Assert.Null(entry.GroupIds);
        Assert.Equal(EnumEventType.None, entry.EventType);
        Assert.Equal(default(DateTime), entry.EnqueuedAt);
        Assert.Equal(0, entry.TimeoutSeconds);
        Assert.Null(entry.SourceEvent);
        Assert.False(entry.IsAutoReportEnabled);
    }
}
