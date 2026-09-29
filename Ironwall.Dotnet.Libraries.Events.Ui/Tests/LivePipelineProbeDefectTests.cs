using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 헤드리스 라이브 NATS 파이프라인 프로브(WP-5) 결함 D1~D8 회귀 시험.
                  D1 버퍼 속 장애 카드의 자동복구 · D2/D3 캐시 미스 단건 GET(브로커 §2.4 N-5) ·
                  D4 삭제된 장비의 카드는 엔트리 없이도 조치 가능 · D5 m_type=RSP 무시 · D6 cmd 대문자 ·
                  D7 OPERATION_EVENT device:null · D8 SYNC_DETECTION 배열 봉투
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// D1 — 장애 카드가 아직 150 ms 묶음 버퍼에 있을 때 같은 장비의 탐지가 자동복구를 부르면
/// 조치보고(API)는 간격과 무관하게 나가고, 조치된 카드는 목록에 오르지 않는다(WP-1 ④ 와 같은 규칙).
/// </summary>
[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적
public class AutoRecoveryBufferedCardTests : IDisposable
{
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IEventApiService> _api = new();
    private readonly Mock<IAccountModel> _account = new();
    private readonly Mock<ISymbolEventManager> _symbols = new();
    private readonly Mock<IEventQueueManager> _queue = new();
    private readonly ActionReportGuard _guard = new();
    private readonly List<object> _published = new();
    private readonly EventSetupModel _setup = new(new Mock<IEventSetupModel>().Object);
    private readonly Func<Type, string, object> _previousGetInstance = IoC.GetInstance;

    public AutoRecoveryBufferedCardTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return _ea.Object;
            if (type == typeof(ILogService)) return _log.Object;
            if (type == typeof(EventSetupModel)) return _setup;
            if (type == typeof(IAccountModel)) return _account.Object;
            if (type == typeof(IEventApiService)) return _api.Object;
            if (type == typeof(IActionReportGuard)) return _guard;
            return null!;
        };
        _ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
           .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => _published.Add(m))
           .Returns(Task.CompletedTask);
        _account.SetupGet(a => a.Username).Returns("kim");
        _account.SetupGet(a => a.EmployeeNumber).Returns("1234");
    }

    public void Dispose() => IoC.GetInstance = _previousGetInstance;

    private EventCardListPanelViewModel CreatePanel()
        => new(_ea.Object, _log.Object, null!, _account.Object, _api.Object, _symbols.Object, _queue.Object, _guard);

    private static MalfunctionEventCardViewModel Malfunction(int id)
    {
        var model = new Mock<IMalfunctionEventModel>();
        model.SetupGet(m => m.Id).Returns(id);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Fault);
        model.SetupProperty(m => m.Status);
        return new MalfunctionEventCardViewModel(model.Object);
    }

    private void ApiReturns(bool success)
        => _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = success, Data = success ? new ActionEventDto { Id = 77 } : null, Message = success ? null : "boom" });

    [Fact]
    public async Task should_report_and_never_raise_the_card_when_auto_recovery_fires_while_the_fault_card_is_buffered()
    {
        // Arrange — 장애 카드는 아직 묶음 버퍼, 큐 엔트리는 보류 표에만 있다(장애 → 30 ms 뒤 탐지)
        var panel = CreatePanel();
        var card = Malfunction(9);
        panel.EnqueueCard(card);
        await panel.HandleAsync(new EventEntryEnqueuedMessage("fault-9", 9, 2114, EnumDeviceType.PIR, EnumEventType.Fault), CancellationToken.None);
        ApiReturns(success: true);

        // Act
        await panel.HandleAutoRecoveryAsync("fault-9");
        await panel.FlushPendingCardsNowAsync();

        // Assert — 조치 POST 1회 · ACTION_REPORT 발행 1회 · 카드는 끝내 목록에 오르지 않는다
        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == 9), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(_published.OfType<SendActionRequestMessage>());
        Assert.Empty(panel.ViewModelProvider);
        Assert.Equal(0, panel.PendingCardCount);
    }

    [Fact]
    public async Task should_keep_the_card_when_auto_recovery_report_fails_while_the_fault_card_is_buffered()
    {
        // Arrange
        var panel = CreatePanel();
        var card = Malfunction(9);
        panel.EnqueueCard(card);
        await panel.HandleAsync(new EventEntryEnqueuedMessage("fault-9", 9, 2114, EnumDeviceType.PIR, EnumEventType.Fault), CancellationToken.None);
        ApiReturns(success: false);

        // Act
        await panel.HandleAutoRecoveryAsync("fault-9");
        await panel.FlushPendingCardsNowAsync();

        // Assert — 시도는 했고(간격 무관), 서버에 조치가 없으니 카드는 남고 NATS 도 없다(표시된 카드의 실패와 같은 규칙)
        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == 9), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(card, panel.ViewModelProvider);
        Assert.DoesNotContain(_published, m => m is SendActionRequestMessage);
    }

    [Fact]
    public async Task should_close_the_card_by_remote_report_when_its_queue_entry_was_removed_by_device_deletion()
    {
        // Arrange — D4: 장비 삭제(SYNC_DEVICE DELETED)로 큐 엔트리만 빠지고 카드는 남는다(EVT-E2E-022 · 브로커 N-5 스냅숏)
        var queue = new EventQueueManager();
        var panel = new EventCardListPanelViewModel(_ea.Object, _log.Object, null!, _account.Object, _api.Object, _symbols.Object, queue, _guard);
        var entryId = queue.Enqueue(new EventEntry { DeviceId = 2300, DeviceType = EnumDeviceType.Fence, GroupIds = new List<int> { 13 }, EventType = EnumEventType.Intrusion, EventId = 7, TimeoutSeconds = 3600 });
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(7);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        model.SetupProperty(m => m.Status);
        var card = new DetectionEventCardViewModel(model.Object) { EntryId = entryId };
        panel.ViewModelProvider.Add(card);
        queue.RemoveByDevice(2300, EnumDeviceType.Fence);

        // Act — 다른 GIS 가 조치했다
        var outcome = panel.CloseByRemoteActionReport(ActionReportKind.Detection, 7, EnumEventType.Intrusion);

        // Assert
        Assert.Equal(RemoteActionReportOutcome.CardClosed, outcome);
        Assert.Empty(panel.ViewModelProvider);
        Assert.Equal(EnumCompositeEventStatus.Normal, queue.GetGroupState(13));
    }
}

/// <summary>
/// D2 · D3 — 캐시에 없는 장비 참조는 단건 GET 1회로 채운다(브로커 §2.4 N-5 · §6.1). 404 면 큐에 넣지 않는다(스냅숏 카드만).
/// 카테고리(sensor · controller)와 무관하게 같은 규칙.
/// </summary>
public class NatsEventCacheMissLookupTests
{
    private Func<MessageArgsModel, Task>? _handler;
    private readonly Mock<IEventQueueManager> _queue = new();
    private readonly Mock<IDeviceProviderService> _lookup = new();
    private readonly DeviceProvider _devices = new();

    public NatsEventCacheMissLookupTests()
    {
        _queue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>())).Returns("entry");
    }

    private Mock<INatsService> Nats()
    {
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handler = h);
        return nats;
    }

    private async Task<DetectionNatsSyncService> StartDetectionAsync()
    {
        var service = new DetectionNatsSyncService(null, Nats().Object, new Mock<ISymbolEventManager>().Object, _queue.Object,
            new Mock<IEventSetupModel>().Object, null, null, _devices, null, _lookup.Object);
        await service.StartService();
        return service;
    }

    private async Task<MalfunctionNatsSyncService> StartMalfunctionAsync()
    {
        var service = new MalfunctionNatsSyncService(null, Nats().Object, new Mock<ISymbolEventManager>().Object, _queue.Object,
            new Mock<IEventSetupModel>().Object, null, null, _devices, _lookup.Object);
        await service.StartService();
        return service;
    }

    private static MessageArgsModel Envelope(string cmd, int eventId, int deviceId, string category, string extra = "")
        => new("sensorway.unit999.gis.event.detect", null, $$"""
        {
          "id": "env-{{eventId}}",
          "m_type": "PUB",
          "cmd": "{{cmd}}",
          "from": "DBApi",
          "body": { "id": {{eventId}}, "type_event": "{{(cmd == "DETECT" ? "Intrusion" : "Fault")}}", {{extra}}
                    "device": { "id": {{deviceId}}, "category_device": "{{category}}" } }
        }
        """);

    private void ServerHas(IBaseDeviceModel device, string category)
        => _lookup.Setup(l => l.FetchDeviceByIdAsync(category, device.Id, It.IsAny<CancellationToken>()))
                  .Callback(() => _devices.Add(device))
                  .ReturnsAsync(device);

    private void ServerLacks(int id, string category)
        => _lookup.Setup(l => l.FetchDeviceByIdAsync(category, id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((IBaseDeviceModel?)null);

    [Fact]
    public async Task should_fetch_once_and_enqueue_with_fetched_type_and_groups_when_the_detected_sensor_is_not_cached()
    {
        // Arrange — DETECT 가 SYNC_DEVICE CREATED 보다 먼저 왔다
        ServerHas(new SensorDeviceModel { Id = 2301, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor, DeviceGroups = new List<int> { 13 } }, "sensor");
        await StartDetectionAsync();

        // Act
        await _handler!(Envelope("DETECT", 7791644, 2301, "sensor"));

        // Assert
        _lookup.Verify(l => l.FetchDeviceByIdAsync("sensor", 2301, It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.Enqueue(It.Is<EventEntry>(e => e.DeviceId == 2301 && e.DeviceType == EnumDeviceType.Fence
                                                            && e.GroupIds != null && e.GroupIds.SequenceEqual(new[] { 13 })), "env-7791644"), Times.Once);
    }

    [Fact]
    public async Task should_not_enqueue_when_the_server_does_not_know_the_detected_controller()
    {
        // Arrange — 어디에도 없는 제어기(404)
        ServerLacks(2399, "controller");
        await StartDetectionAsync();

        // Act
        await _handler!(Envelope("DETECT", 7791645, 2399, "controller"));

        // Assert — 조회는 했고, 큐 엔트리는 없다(카드는 호스트가 스냅숏으로)
        _lookup.Verify(l => l.FetchDeviceByIdAsync("controller", 2399, It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task should_not_enqueue_when_the_server_does_not_know_the_detected_sensor()
    {
        // Arrange
        ServerLacks(2302, "sensor");
        await StartDetectionAsync();

        // Act
        await _handler!(Envelope("DETECT", 7791646, 2302, "sensor"));

        // Assert
        _lookup.Verify(l => l.FetchDeviceByIdAsync("sensor", 2302, It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task should_not_fetch_when_the_detected_device_is_cached()
    {
        // Arrange
        _devices.Add(new SensorDeviceModel { Id = 2131, DeviceType = EnumDeviceType.SmartSensor2, CategoryDevice = EnumDeviceCategory.Sensor, DeviceGroups = new List<int> { 11 } });
        await StartDetectionAsync();

        // Act
        await _handler!(Envelope("DETECT", 7791647, 2131, "sensor"));

        // Assert
        _lookup.Verify(l => l.FetchDeviceByIdAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _queue.Verify(q => q.Enqueue(It.Is<EventEntry>(e => e.DeviceType == EnumDeviceType.SmartSensor2), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task should_fetch_once_and_enqueue_with_fetched_type_when_the_malfunctioning_sensor_is_not_cached()
    {
        // Arrange
        ServerHas(new SensorDeviceModel { Id = 2303, DeviceType = EnumDeviceType.PIR, CategoryDevice = EnumDeviceCategory.Sensor, DeviceGroups = new List<int> { 12 } }, "sensor");
        await StartMalfunctionAsync();

        // Act
        await _handler!(Envelope("MALFUNCTION", 7791648, 2303, "sensor", "\"reason\": \"FAULT_ETC\","));

        // Assert
        _lookup.Verify(l => l.FetchDeviceByIdAsync("sensor", 2303, It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.Enqueue(It.Is<EventEntry>(e => e.DeviceId == 2303 && e.DeviceType == EnumDeviceType.PIR
                                                            && e.GroupIds != null && e.GroupIds.SequenceEqual(new[] { 12 })), "env-7791648"), Times.Once);
    }
}

/// <summary>
/// D5 · D6 — 봉투 규칙은 호스트 라우터와 같다: 응답(<c>m_type=RSP</c>)은 이벤트가 아니고, <c>cmd</c> 는 명세의 대문자 토큰만 받는다.
/// D7 · D8 — OPERATION_EVENT 의 <c>device: null</c> 은 조용히 넘기고, SYNC_DETECTION 도 배열 봉투를 항목마다 처리한다.
/// </summary>
public class NatsEnvelopeRuleTests
{
    private Func<MessageArgsModel, Task>? _handler;
    private readonly Mock<IEventQueueManager> _queue = new();
    private readonly Mock<ILogService> _log = new();
    private readonly DeviceProvider _devices = new();

    public NatsEnvelopeRuleTests()
    {
        _queue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>())).Returns("entry");
        _devices.Add(new SensorDeviceModel { Id = 2131, DeviceType = EnumDeviceType.SmartSensor2, CategoryDevice = EnumDeviceCategory.Sensor, DeviceGroups = new List<int> { 11 } });
    }

    private Mock<INatsService> Nats()
    {
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handler = h);
        return nats;
    }

    private static MessageArgsModel Message(string json) => new("sensorway.unit999.gis.event.detect", null, json);

    private static string Event(string cmd, string mType, int eventId, string typeEvent = "Intrusion")
        => $$"""
        { "id": "env-{{eventId}}", "m_type": "{{mType}}", "cmd": "{{cmd}}", "from": "DBApi", "success": true, "req_id": "r-{{eventId}}",
          "body": { "id": {{eventId}}, "type_event": "{{typeEvent}}", "reason": "FAULT_ETC", "device": { "id": 2131, "category_device": "sensor" } } }
        """;

    private void VerifyNoError()
        => _log.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);

    [Fact]
    public async Task should_not_enqueue_when_the_detect_envelope_is_a_response()
    {
        // Arrange
        await new DetectionNatsSyncService(_log.Object, Nats().Object, new Mock<ISymbolEventManager>().Object, _queue.Object,
            new Mock<IEventSetupModel>().Object, null, null, _devices).StartService();

        // Act
        await _handler!(Message(Event("DETECT", "RSP", 1)));

        // Assert
        _queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task should_not_enqueue_when_the_malfunction_envelope_is_a_response()
    {
        // Arrange
        await new MalfunctionNatsSyncService(_log.Object, Nats().Object, new Mock<ISymbolEventManager>().Object, _queue.Object,
            new Mock<IEventSetupModel>().Object, null, null, _devices).StartService();

        // Act
        await _handler!(Message(Event("MALFUNCTION", "RSP", 2, "Fault")));

        // Assert
        _queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [InlineData("PUB")]
    [InlineData("REQ")]
    [InlineData("")]
    public async Task should_enqueue_when_the_detect_envelope_is_not_a_response(string mType)
    {
        // Arrange
        await new DetectionNatsSyncService(_log.Object, Nats().Object, new Mock<ISymbolEventManager>().Object, _queue.Object,
            new Mock<IEventSetupModel>().Object, null, null, _devices).StartService();

        // Act
        await _handler!(Message(Event("DETECT", mType, 3)));

        // Assert
        _queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Once);
    }

    [Theory]
    [InlineData("detect")]
    [InlineData("Detect")]
    public async Task should_not_enqueue_when_the_detect_cmd_is_not_uppercase(string cmd)
    {
        // Arrange
        await new DetectionNatsSyncService(_log.Object, Nats().Object, new Mock<ISymbolEventManager>().Object, _queue.Object,
            new Mock<IEventSetupModel>().Object, null, null, _devices).StartService();

        // Act
        await _handler!(Message(Event(cmd, "PUB", 4)));

        // Assert
        _queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task should_ignore_quietly_when_the_operation_event_device_is_null()
    {
        // Arrange — 지워진 함체의 운영 이벤트(브로커 §6.x device: null)
        var symbols = new Mock<ISymbolEventManager>();
        await new OperationEventNatsSyncService(_log.Object, Nats().Object, symbols.Object).StartService();

        // Act
        await _handler!(Message("""
        { "id": "op-1", "m_type": "PUB", "cmd": "OPERATION_EVENT", "from": "DBApi",
          "body": { "id": 11, "device": null, "reason": "ENCLOSURE_DOOR_OPEN", "detail": { "door_status": "OPEN" } } }
        """));

        // Assert
        VerifyNoError();
        symbols.Verify(s => s.SetDoorState(It.IsAny<int>(), It.IsAny<EnumDeviceType>(), It.IsAny<EnumDoorState>()), Times.Never);
    }

    [Fact]
    public async Task should_process_every_item_when_the_sync_detection_envelope_is_an_array()
    {
        // Arrange — 활성 탐지 42 · 43
        var api = new Mock<IEventApiService>();
        api.Setup(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<DetectionEventDto> { Success = false });
        _queue.Setup(q => q.FindEntryByEventId(It.IsAny<int>(), EnumEventType.Intrusion)).Returns(new EventEntry());
        var service = new DetectionSyncNatsService(_log.Object, Nats().Object, _queue.Object, api.Object);
        await service.StartService();

        // Act
        await _handler!(Message("""
        [ { "id": "s-1", "m_type": "PUB", "cmd": "SYNC_DETECTION", "from": "DBApi", "body": { "action": "UPDATED", "resource_id": 42 } },
          { "id": "s-2", "m_type": "PUB", "cmd": "SYNC_DETECTION", "from": "DBApi", "body": { "action": "UPDATED", "resource_id": 43 } } ]
        """));
        if (service.LastProcessingTask is { } last) await last;
        await Task.Yield();

        // Assert
        VerifyNoError();
        api.Verify(a => a.GetDetectionEventByIdAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.GetDetectionEventByIdAsync(43, It.IsAny<CancellationToken>()), Times.Once);
    }
}
