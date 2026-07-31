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

    private MalfunctionNatsSyncService CreateService(
        IEventSetupModel eventSetup,
        out Mock<IEventQueueManager> mockQueue)
    {
        var mockNats = new Mock<INatsService>();
        var mockManager = new Mock<ISymbolEventManager>();
        var mockEa = new Mock<IEventAggregator>();
        mockQueue = new Mock<IEventQueueManager>();

        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);

        mockQueue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()))
                 .Returns("test-entry-id");

        // tokenStorage 미전달 → 로그인 게이트 비활성(테스트에서 이벤트 통과)
        return new MalfunctionNatsSyncService(
            null, mockNats.Object, mockManager.Object, mockQueue.Object, eventSetup, mockEa.Object);
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
}
