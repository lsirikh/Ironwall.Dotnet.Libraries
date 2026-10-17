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
   Purpose      : 장애 이벤트 자동조치보고 독립 설정 — MalfunctionNatsSyncService가
                  장애 전용 필드(MalfunctionTimeDiscardSec / IsMalfunctionAutoEventDiscard)로
                  EventEntry를 스탬프하는지 + 탐지와 독립인지 검증.
   Created By   : GHLee
   Created On   : 2026-07-31
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class MalfunctionNatsSyncServiceTests
{
    private Func<MessageArgsModel, Task>? _capturedHandler;
    private readonly Mock<IEventAggregator> _mockEa = new();

    private MalfunctionNatsSyncService CreateService(
        IEventSetupModel eventSetup,
        out Mock<IEventQueueManager> mockQueue)
    {
        var mockNats = new Mock<INatsService>();
        var mockManager = new Mock<ISymbolEventManager>();
        mockQueue = new Mock<IEventQueueManager>();

        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);

        mockQueue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()))
                 .Returns("test-entry-id");

        // tokenStorage 미전달 → 로그인 게이트 비활성(테스트에서 이벤트 통과)
        return new MalfunctionNatsSyncService(
            null, mockNats.Object, mockManager.Object, mockQueue.Object, eventSetup, _mockEa.Object);
    }

    /// <summary>장애/탐지 필드를 개별 지정한 IEventSetupModel 목 생성.</summary>
    private static IEventSetupModel BuildSetup(bool malfunctionOn, int malfunctionSec,
                                               bool detectionOn = true, int detectionSec = 20)
    {
        var mock = new Mock<IEventSetupModel>();
        mock.Setup(s => s.IsMalfunctionAutoEventDiscard).Returns(malfunctionOn);
        mock.Setup(s => s.MalfunctionTimeDiscardSec).Returns(malfunctionSec);
        mock.Setup(s => s.IsAutoEventDiscard).Returns(detectionOn);
        mock.Setup(s => s.TimeDiscardSec).Returns(detectionSec);
        return mock.Object;
    }

    private static MessageArgsModel MalfunctionMessage()
        => new(subject: "test", subscriptionSubject: null, data: """
        {
          "id": "test-uuid-m",
          "cmd": "MALFUNCTION",
          "body": {
            "id": 55,
            "device_id": 8,
            "device": {
              "id": 8,
              "type_device": "Fence",
              "device_groups": [{ "id": 3, "name": "C" }]
            },
            "reason": "FAULT_FENCE"
          }
        }
        """);

    /// <summary>
    /// 카드 1:1 매칭 신호(EventEntryEnqueuedMessage)가 WPF Application 없는 실행(헤드리스)에서 조용히 버려지던 결함(3e78d574)의 회귀망 —
    /// DetectionNatsSyncService 와 같은 규약: Dispatcher 가 없으면 현재 스레드에서 발행한다.
    /// </summary>
    [Fact]
    public async Task should_publish_entry_enqueued_message_when_malfunction_received_without_wpf_application()
    {
        // Arrange
        var service = CreateService(BuildSetup(malfunctionOn: true, malfunctionSec: 30), out _);
        await service.StartService();

        // Act
        await _capturedHandler!(MalfunctionMessage());

        // Assert
        _mockEa.Verify(ea => ea.PublishAsync(
            It.Is<EventEntryEnqueuedMessage>(m =>
                m.EntryId == "test-entry-id" &&
                m.EventId == 55 &&
                m.DeviceId == 8 &&
                m.EventType == EnumEventType.Fault),
            It.IsAny<Func<Func<Task>, Task>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task should_stamp_entry_with_malfunction_timeout_when_malfunction_received()
    {
        // Arrange — 장애 자동조치보고 ON, 장애 타임아웃 99s
        var service = CreateService(BuildSetup(malfunctionOn: true, malfunctionSec: 99), out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(MalfunctionMessage());

        // Assert — 엔트리가 장애 전용 필드로 스탬프(EventType=Fault, Timeout=99, AutoReport=on)
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.EventType == EnumEventType.Fault &&
            e.TimeoutSeconds == 99 &&
            e.IsAutoReportEnabled == true),
            It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task should_disable_autoreport_when_malfunction_toggle_off()
    {
        // Arrange — 장애 자동조치보고 OFF (탐지는 ON이지만 장애 엔트리에 영향 없어야 함)
        var service = CreateService(BuildSetup(malfunctionOn: false, malfunctionSec: 99), out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(MalfunctionMessage());

        // Assert — IsAutoReportEnabled=false → EQM tick에서 skip → 장애 미보고
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.EventType == EnumEventType.Fault &&
            e.IsAutoReportEnabled == false),
            It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task should_use_malfunction_timeout_not_detection_timeout_when_malfunction_received()
    {
        // Arrange — 장애 77s / 탐지 20s (독립 증명)
        var service = CreateService(
            BuildSetup(malfunctionOn: true, malfunctionSec: 77, detectionOn: true, detectionSec: 20),
            out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(MalfunctionMessage());

        // Assert — 장애 엔트리는 장애 타임아웃(77)만 사용, 탐지 타임아웃(20) 아님
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.TimeoutSeconds == 77),
            It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task should_keep_detection_autoreport_when_only_malfunction_toggle_off()
    {
        // Arrange — 공유 설정: 장애 OFF(99s) / 탐지 ON(20s). 탐지 엔트리는 영향받지 않아야 한다.
        var setup = BuildSetup(malfunctionOn: false, malfunctionSec: 99, detectionOn: true, detectionSec: 20);

        var mockNats = new Mock<INatsService>();
        Func<MessageArgsModel, Task>? detHandler = null;
        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => detHandler = h);

        var mockQueue = new Mock<IEventQueueManager>();
        mockQueue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>())).Returns("det-id");

        var detService = new DetectionNatsSyncService(
            null, mockNats.Object, new Mock<ISymbolEventManager>().Object,
            mockQueue.Object, setup, new Mock<IEventAggregator>().Object);
        await detService.StartService();

        var detJson = """
        {
          "id": "det-uuid",
          "cmd": "DETECT",
          "body": {
            "id": 1,
            "type_event": "Intrusion",
            "device_id": 5,
            "device": { "id": 5, "type_device": "Fence", "device_groups": [{ "id": 1, "name": "A" }] }
          }
        }
        """;

        // Act
        await detHandler!(new MessageArgsModel(subject: "test", subscriptionSubject: null, data: detJson));

        // Assert — 탐지 엔트리는 여전히 자동조치보고 활성 + 탐지 타임아웃(20)
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.EventType == EnumEventType.Intrusion &&
            e.IsAutoReportEnabled == true &&
            e.TimeoutSeconds == 20),
            It.IsAny<string?>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // v7.0+ 본문 — 장비는 참조 {id, category_device}, 그룹 없음, action_reported 는 bool
    // (브로커 명세 v2.0.7 §6.2). 종전엔 type_device 만 읽어 모든 장애가 "DeviceType 파싱 실패" 로 버려졌다.
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>명세 §6.2 Publish Body 그대로(device · reason 만 바꿔 끼울 수 있다).</summary>
    private static MessageArgsModel V7Malfunction(string deviceJson = """{ "id": 346, "category_device": "controller" }""", string reason = "FAULT_CONTROLLER")
        => new(subject: "sensorway.unit001.all.event.malfunction", subscriptionSubject: null, data: $$"""
        {
          "id": "c7b1e4d2-3a55-4f0b-9e77-8d1c2b6f4a30",
          "m_type": "PUB",
          "cmd": "MALFUNCTION",
          "from": "PidsProxy",
          "body": {
            "id": 358,
            "category_event": "malfunction",
            "type_event": "Fault",
            "action_reported": false,
            "reason": "{{reason}}",
            "device": {{deviceJson}},
            "device_description": "[controller:Controller] GOP-CTRL-01 (number: 1, id: 346)",
            "detail": {
              "first_end": 15,
              "second_end": 25,
              "first_start": 10,
              "second_start": 20
            },
            "created_at": "2026-09-12T12:14:30.494175+09:00",
            "updated_at": "2026-09-12T12:14:30.494176+09:00"
          },
          "created": "2026-09-12T12:14:30.511308+09:00"
        }
        """);

    private MalfunctionNatsSyncService CreateServiceWithDevices(
        Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider devices,
        out Mock<IEventQueueManager> mockQueue)
    {
        var mockNats = new Mock<INatsService>();
        mockQueue = new Mock<IEventQueueManager>();
        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);
        mockQueue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>())).Returns("test-entry-id");
        return new MalfunctionNatsSyncService(
            null, mockNats.Object, new Mock<ISymbolEventManager>().Object, mockQueue.Object,
            BuildSetup(malfunctionOn: true, malfunctionSec: 30), _mockEa.Object, deviceProvider: devices);
    }

    [Fact]
    public async Task should_enqueue_controller_blackout_with_cached_groups_when_v7_reference_device_arrives()
    {
        // Arrange — 제어기 346(자기 그룹 7) 아래 센서 349(그룹 2, 5). 무통신이면 센서 그룹까지 검게.
        var devices = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();
        var controller = new Ironwall.Dotnet.Monitoring.Models.Devices.ControllerDeviceModel
        {
            Id = 346, DeviceType = EnumDeviceType.Controller, CategoryDevice = EnumDeviceCategory.Controller, DeviceGroups = new List<int> { 7 }
        };
        devices.Add(controller);
        devices.Add(new Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel
        {
            Id = 349, DeviceType = EnumDeviceType.Fence, Controller = controller, DeviceGroups = new List<int> { 2, 5 }
        });
        var service = CreateServiceWithDevices(devices, out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(V7Malfunction());

        // Assert
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.DeviceId == 346 &&
            e.DeviceType == EnumDeviceType.Controller &&
            e.EventType == EnumEventType.Fault &&
            e.EventId == 358 &&
            e.IsControllerBlackout &&
            e.GroupIds != null && e.GroupIds.OrderBy(g => g).SequenceEqual(new[] { 2, 5, 7 })),
            "c7b1e4d2-3a55-4f0b-9e77-8d1c2b6f4a30"), Times.Once);
    }

    [Fact]
    public async Task should_enqueue_sensor_fault_with_cached_type_and_groups_when_v7_reference_device_arrives()
    {
        // Arrange
        var devices = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider();
        devices.Add(new Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel
        {
            Id = 349, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor, DeviceGroups = new List<int> { 2 }
        });
        var service = CreateServiceWithDevices(devices, out var mockQueue);
        await service.StartService();

        // Act
        await _capturedHandler!(V7Malfunction("""{ "id": 349, "category_device": "sensor" }""", reason: "FAULT_FENCE"));

        // Assert
        mockQueue.Verify(q => q.Enqueue(It.Is<EventEntry>(e =>
            e.DeviceId == 349 && e.DeviceType == EnumDeviceType.Fence && !e.IsControllerBlackout &&
            e.GroupIds != null && e.GroupIds.SequenceEqual(new[] { 2 })),
            It.IsAny<string?>()), Times.Once);
        _mockEa.Verify(ea => ea.PublishAsync(
            It.Is<EventEntryEnqueuedMessage>(m => m.EventId == 358 && m.DeviceType == EnumDeviceType.Fence),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task should_not_enqueue_when_v7_malfunction_device_is_null_because_it_was_deleted()
    {
        var service = CreateServiceWithDevices(new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider(), out var mockQueue);
        await service.StartService();

        await _capturedHandler!(V7Malfunction("null"));

        mockQueue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }
}
