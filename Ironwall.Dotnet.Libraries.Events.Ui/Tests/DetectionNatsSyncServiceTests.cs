using Xunit;
using Moq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : DetectionNatsSyncService entryId 발행 테스트
   Created By   : GHLee
   Created On   : 2026-03-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionNatsSyncServiceTests
{
    private Func<MessageArgsModel, Task>? _capturedHandler;

    private DetectionNatsSyncService CreateService(
        out Mock<IEventAggregator> mockEa,
        out Mock<IEventQueueManager> mockQueue)
    {
        var mockNats = new Mock<INatsService>();
        var mockManager = new Mock<ISymbolEventManager>();
        mockEa = new Mock<IEventAggregator>();
        mockQueue = new Mock<IEventQueueManager>();
        var mockEventSetup = new Mock<IEventSetupModel>();

        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);

        // Enqueue 호출 시 entryId 반환
        mockQueue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()))
                 .Returns("test-entry-id");

        return new DetectionNatsSyncService(
            null, mockNats.Object, mockManager.Object, mockQueue.Object, mockEventSetup.Object, mockEa.Object);
    }

    [Fact]
    public async Task OnNatsDetection_ShouldPublishEventEntryEnqueuedMessage()
    {
        // Arrange
        var service = CreateService(out var mockEa, out var mockQueue);
        await service.StartService();

        var json = """
        {
          "id": "test-uuid",
          "m_type": "REQ",
          "cmd": "DETECT",
          "from": "pids-proxy",
          "body": {
            "id": 1,
            "type_event": "Intrusion",
            "device_id": 5,
            "device": {
              "id": 5,
              "type_device": "Sensor",
              "device_groups": [{ "id": 1, "name": "A구역" }]
            },
            "result": "PIR_SENSOR"
          }
        }
        """;

        var args = new MessageArgsModel(
            subject: "sensorway.unit001.gis.event.detect",
            subscriptionSubject: null,
            data: json);

        // Act
        await _capturedHandler!(args);

        // Assert — IEventAggregator.PublishAsync 호출 확인
        // (PublishOnBackgroundThreadAsync는 extension method → PublishAsync를 내부 호출)
        mockEa.Verify(ea => ea.PublishAsync(
            It.Is<EventEntryEnqueuedMessage>(m =>
                m.EntryId == "test-entry-id" &&
                m.EventId == 1 &&
                m.DeviceId == 5 &&
                m.DeviceType == EnumDeviceType.Fence &&
                m.EventType == EnumEventType.Intrusion),
            It.IsAny<Func<Func<Task>, Task>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnNatsDetection_NonDetectionCmd_ShouldNotPublish()
    {
        // Arrange
        var service = CreateService(out var mockEa, out var mockQueue);
        await service.StartService();

        var json = """
        {
          "cmd": "SYNC_DEVICE",
          "body": { "action": "UPDATED" }
        }
        """;

        var args = new MessageArgsModel(
            subject: "sensorway.unit001.gis.event.detect",
            subscriptionSubject: null,
            data: json);

        // Act
        await _capturedHandler!(args);

        // Assert — 발행 없음
        mockEa.Verify(ea => ea.PublishAsync(
            It.IsAny<EventEntryEnqueuedMessage>(),
            It.IsAny<Func<Func<Task>, Task>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnNatsDetection_ShouldCallEnqueueWithCorrectEntry()
    {
        // Arrange
        var service = CreateService(out var mockEa, out var mockQueue);
        await service.StartService();

        var json = """
        {
          "cmd": "DETECT",
          "body": {
            "id": 1,
            "type_event": "Intrusion",
            "device_id": 7,
            "device": {
              "id": 7,
              "type_device": "Sensor",
              "device_groups": [{ "id": 2, "name": "B구역" }]
            }
          }
        }
        """;

        var args = new MessageArgsModel(
            subject: "test", subscriptionSubject: null, data: json);

        // Act
        await _capturedHandler!(args);

        // Assert — Enqueue가 올바른 파라미터로 호출됨
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.DeviceId == 7 &&
            e.DeviceType == EnumDeviceType.Fence &&
            e.EventType == EnumEventType.Intrusion &&
            e.GroupIds != null && e.GroupIds.Contains(2)),
            It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task OnNatsDetection_DeviceIdFromNestedDevice_WhenTopLevelDeviceIdMissing()
    {
        // Arrange — device_id 필드 없이 device.id만 있는 실제 NATS 페이로드 시나리오
        var service = CreateService(out var mockEa, out var mockQueue);
        await service.StartService();

        var json = """
        {
          "cmd": "DETECT",
          "body": {
            "id": 29637,
            "type_event": "Intrusion",
            "device": {
              "id": 16,
              "type_device": "Sensor",
              "device_groups": [{ "id": 2, "name": "B구역" }]
            }
          }
        }
        """;

        var args = new MessageArgsModel(
            subject: "test", subscriptionSubject: null, data: json);

        // Act
        await _capturedHandler!(args);

        // Assert — device.id=16이 사용되어야 함 (device_id=0이 아님)
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.DeviceId == 16 &&
            e.DeviceType == EnumDeviceType.Fence),
            It.IsAny<string?>()),
            Times.Once);
    }
}
