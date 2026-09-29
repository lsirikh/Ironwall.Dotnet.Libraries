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
              "type_device": "Fence",
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
              "type_device": "Fence",
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
              "type_device": "Fence",
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

    [Fact]
    public async Task OnNatsDetection_InvalidDeviceType_ShouldDropEvent()
    {
        // Arrange — 파싱 불가능한 type_device가 오면 이벤트 드롭 (Fence 오분류 금지)
        var service = CreateService(out var mockEa, out var mockQueue);
        await service.StartService();

        var json = """
        {
          "cmd": "DETECT",
          "body": {
            "id": 99,
            "type_event": "Intrusion",
            "device": {
              "id": 10,
              "type_device": "UnknownType",
              "device_groups": []
            }
          }
        }
        """;

        var args = new MessageArgsModel(
            subject: "test", subscriptionSubject: null, data: json);

        // Act
        await _capturedHandler!(args);

        // Assert — Enqueue 호출 없음 (이벤트 드롭)
        mockQueue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
        mockEa.Verify(ea => ea.PublishAsync(
            It.IsAny<EventEntryEnqueuedMessage>(),
            It.IsAny<Func<Func<Task>, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // v7.0+ 본문 — 장비는 참조 {id, category_device} 두 키뿐, 그룹 없음, action_reported 는 bool
    // (브로커 명세 Gop_Message_Broker_연동설계.md v2.0.7 §6.1). 종전엔 type_device 만 읽어
    // "DeviceType 파싱 실패 — 이벤트 무시" 로 모든 탐지가 버려졌다(2026-09-30).
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>명세 §6.1 Publish Body 그대로(device 만 바꿔 끼울 수 있다).</summary>
    private static MessageArgsModel V7Detect(string deviceJson = """{ "id": 349, "category_device": "sensor" }""", string typeEvent = "Intrusion")
        => new(subject: "sensorway.unit001.all.event.detect", subscriptionSubject: null, data: $$"""
        {
          "id": "5a3c1c0e-0f4a-4a1e-9a2a-2c3f5f9d1b77",
          "m_type": "PUB",
          "cmd": "DETECT",
          "from": "PidsProxy",
          "body": {
            "id": 357,
            "category_event": "detection",
            "type_event": "{{typeEvent}}",
            "action_reported": false,
            "result": "PIR_SENSOR",
            "device": {{deviceJson}},
            "device_description": "[sensor:Fence] GOP-SNS-01-01 (number: 101, id: 349)",
            "detail": {
              "signal": 2000,
              "thumbnail": "http://192.168.10.60:8080/events/2001/thumb.jpg",
              "frame_width": 1280,
              "frame_height": 720
            },
            "created_at": "2026-09-12T12:14:30.441457+09:00",
            "updated_at": "2026-09-12T12:14:30.441459+09:00"
          },
          "created": "2026-09-12T12:14:30.448601+09:00"
        }
        """);

    private DetectionNatsSyncService CreateServiceWithDevices(
        Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider devices,
        out Mock<IEventAggregator> mockEa,
        out Mock<IEventQueueManager> mockQueue)
    {
        var mockNats = new Mock<INatsService>();
        mockEa = new Mock<IEventAggregator>();
        mockQueue = new Mock<IEventQueueManager>();
        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);
        mockQueue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>())).Returns("test-entry-id");
        return new DetectionNatsSyncService(
            null, mockNats.Object, new Mock<ISymbolEventManager>().Object, mockQueue.Object,
            new Mock<IEventSetupModel>().Object, mockEa.Object, deviceProvider: devices);
    }

    [Fact]
    public async Task should_enqueue_with_cached_type_and_groups_when_v7_reference_device_arrives()
    {
        // Arrange — 장비 캐시에 349 가 Fence · 그룹 [2, 5] 로 있다(7.0+ 는 캐시의 group_ids 가 정본)
        var devices = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();
        devices.Add(new Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel
        {
            Id = 349, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor, DeviceGroups = new List<int> { 2, 5 }
        });
        var service = CreateServiceWithDevices(devices, out var mockEa, out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(V7Detect());

        // Assert
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.DeviceId == 349 &&
            e.DeviceType == EnumDeviceType.Fence &&
            e.EventType == EnumEventType.Intrusion &&
            e.EventId == 357 &&
            e.GroupIds != null && e.GroupIds.OrderBy(g => g).SequenceEqual(new[] { 2, 5 })),
            "5a3c1c0e-0f4a-4a1e-9a2a-2c3f5f9d1b77"), Times.Once);
        mockEa.Verify(ea => ea.PublishAsync(
            It.Is<EventEntryEnqueuedMessage>(m => m.EventId == 357 && m.DeviceId == 349 && m.DeviceType == EnumDeviceType.Fence),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task should_resolve_type_from_category_when_v7_reference_is_not_cached_and_category_is_one_to_one()
    {
        // Arrange — 제어기 접점 탐지(명세 §6.1: 제어기 · 함체 · 통문 접점도 DETECT 로 온다), 캐시 비어 있음
        var service = CreateServiceWithDevices(new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider(), out _, out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(V7Detect("""{ "id": 346, "category_device": "controller" }"""));

        // Assert — controller 는 종류가 하나라 카테고리만으로 확정된다
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.DeviceId == 346 && e.DeviceType == EnumDeviceType.Controller), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task should_not_guess_sensor_kind_when_v7_sensor_reference_is_not_cached()
    {
        // Arrange — sensor 카테고리 안에는 Fence · PIR · Multi … 가 다 있다. 캐시에 없으면 종류를 단정하지 않는다.
        var service = CreateServiceWithDevices(new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider(), out _, out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(V7Detect());

        // Assert
        mockQueue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task should_not_enqueue_when_v7_device_is_null_because_it_was_deleted()
    {
        // Arrange — 장비가 지워졌으면 device: null(명세 §6.1). 깜빡일 심볼이 없다 — 예외 없이 건너뛴다.
        var service = CreateServiceWithDevices(new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider(), out _, out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(V7Detect("null"));

        // Assert
        mockQueue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task should_prefer_legacy_type_device_over_cache_when_old_server_sends_full_device()
    {
        // Arrange — 옛 서버(6.3) 전문: type_device · device_groups 가 본문에 있다 → 본문이 이긴다(무회귀)
        var devices = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();
        devices.Add(new Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel
        {
            Id = 349, DeviceType = EnumDeviceType.PIR, DeviceGroups = new List<int> { 9 }
        });
        var service = CreateServiceWithDevices(devices, out _, out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(V7Detect("""{ "id": 349, "type_device": "Fence", "device_groups": [{ "id": 2, "name": "B" }] }"""));

        // Assert
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.DeviceType == EnumDeviceType.Fence && e.GroupIds != null && e.GroupIds.SequenceEqual(new[] { 2 })),
            It.IsAny<string?>()), Times.Once);
    }
}
