using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 통문·함체 개폐 형태 배선 (PRD FR-12/13) —
                  접점 이벤트는 큐에 들어가지 않고 DoorState 만 바꾼다 · OPERATION_EVENT → SetDoorState ·
                  RegisterDeviceSymbol 함체 door_status 초기화 · 색 축(CompositeStatus)과 독립.
   Created By   : Claude
   Created On   : 2026-09-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class DoorStateWiringTests
{
    // ─────────────── helpers ───────────────
    private static SymbolEventManager CreateManager()
    {
        var eqm = new Mock<IEventQueueManager>();
        eqm.Setup(q => q.GetDeviceState(It.IsAny<int>(), It.IsAny<EnumDeviceType>())).Returns(EnumCompositeEventStatus.Normal);
        return new SymbolEventManager(new Mock<IEventAggregator>().Object, new Mock<ILogService>().Object,
            new EventSetupModel(new Mock<IEventSetupModel>().Object), eqm.Object);
    }

    private static Mock<IBaseDeviceModel> Device(int id, EnumDeviceType type)
    {
        var m = new Mock<IBaseDeviceModel>();
        m.Setup(d => d.Id).Returns(id);
        m.Setup(d => d.DeviceType).Returns(type);
        m.Setup(d => d.Status).Returns(EnumDeviceStatus.ACTIVATED);
        m.Setup(d => d.DeviceGroups).Returns(new List<int>());
        return m;
    }

    private static string DetectJson(int deviceId, string typeDevice, string typeEvent) => $$"""
        { "id": "uuid-{{deviceId}}", "m_type": "REQ", "cmd": "DETECT", "from": "pids-proxy",
          "body": { "id": 77, "type_event": "{{typeEvent}}", "device_id": {{deviceId}},
                    "device": { "id": {{deviceId}}, "type_device": "{{typeDevice}}", "device_groups": [] } } }
        """;

    private static (DetectionNatsSyncService svc, Mock<ISymbolEventManager> manager, Mock<IEventQueueManager> queue, Func<MessageArgsModel, Task> handler)
        CreateDetection(IDoorContactPolicy? policy)
    {
        Func<MessageArgsModel, Task>? captured = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>()).Callback<Func<MessageArgsModel, Task>>(h => captured = h);
        var manager = new Mock<ISymbolEventManager>();
        var queue = new Mock<IEventQueueManager>();
        queue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>())).Returns("entry");
        var svc = new DetectionNatsSyncService(null, nats.Object, manager.Object, queue.Object, new Mock<IEventSetupModel>().Object,
            new Mock<IEventAggregator>().Object, null, null, policy);
        svc.StartService().GetAwaiter().GetResult();
        return (svc, manager, queue, captured!);
    }

    private static (OperationEventNatsSyncService svc, Mock<ISymbolEventManager> manager, Func<MessageArgsModel, Task> handler)
        CreateOperation(ITokenStorageService? token = null)
    {
        Func<MessageArgsModel, Task>? captured = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>()).Callback<Func<MessageArgsModel, Task>>(h => captured = h);
        var manager = new Mock<ISymbolEventManager>();
        var svc = new OperationEventNatsSyncService(null, nats.Object, manager.Object, token);
        svc.StartService().GetAwaiter().GetResult();
        return (svc, manager, captured!);
    }

    private static MessageArgsModel Args(string json) => new(subject: "sensorway.unit001.all.event.operation", subscriptionSubject: null, data: json);

    // ─────────────── DeviceSymbolLookupModel / SymbolEventManager ───────────────
    [Theory]
    [InlineData(true,  EnumEventType.ContactOn,  EnumDoorState.Open)]
    [InlineData(true,  EnumEventType.ContactOff, EnumDoorState.Closed)]
    [InlineData(false, EnumEventType.ContactOn,  EnumDoorState.Closed)]   // 현장 배선 반전(OpenOnContactOn=false)
    [InlineData(false, EnumEventType.ContactOff, EnumDoorState.Open)]
    public void should_transition_door_state_when_contact_event_applied_to_gate_symbol(bool openOnContactOn, EnumEventType evt, EnumDoorState expected)
    {
        var manager = CreateManager();
        var symbol = new PidsSymbolModel { DeviceType = EnumDeviceType.Gate, OpenOnContactOn = openOnContactOn, Title = "통문-1" };
        manager.RegisterDeviceSymbol(Device(7, EnumDeviceType.Gate).Object, symbol);
        int updates = 0; symbol.Update += (_, _) => updates++;

        manager.ApplyDoorEvent(7, EnumDeviceType.Gate, evt);

        Assert.Equal(expected, symbol.DoorState);
        Assert.Equal(EnumCompositeEventStatus.Normal, symbol.CompositeStatus);   // 색 축은 그대로
        Assert.True(updates >= 1, "형태 변경은 SetUpdate 로 렌더에 통지되어야 한다");
    }

    [Fact]
    public void should_keep_door_state_when_non_contact_event_applied()
    {
        var manager = CreateManager();
        var symbol = new PidsSymbolModel { DeviceType = EnumDeviceType.Gate, DoorState = EnumDoorState.Open };
        manager.RegisterDeviceSymbol(Device(7, EnumDeviceType.Gate).Object, symbol);

        manager.ApplyDoorEvent(7, EnumDeviceType.Gate, EnumEventType.Intrusion);
        manager.ApplyDoorEvent(7, EnumDeviceType.Gate, EnumEventType.Fault);

        Assert.Equal(EnumDoorState.Open, symbol.DoorState);
    }

    [Fact]
    public void should_set_door_state_directly_and_notify_only_on_change()
    {
        var manager = CreateManager();
        var symbol = new PidsSymbolModel { DeviceType = EnumDeviceType.Enclosure };
        manager.RegisterDeviceSymbol(Device(9, EnumDeviceType.Enclosure).Object, symbol);
        int updates = 0; symbol.Update += (_, _) => updates++;

        manager.SetDoorState(9, EnumDeviceType.Enclosure, EnumDoorState.Open);
        Assert.Equal(EnumDoorState.Open, symbol.DoorState); Assert.Equal(1, updates);
        manager.SetDoorState(9, EnumDeviceType.Enclosure, EnumDoorState.Open);   // 동일 상태 → 무통지
        Assert.Equal(1, updates);
        manager.SetDoorState(9, EnumDeviceType.Gate, EnumDoorState.Closed);        // 타입 불일치 → Id 폴백으로 해석
        Assert.Equal(EnumDoorState.Closed, symbol.DoorState);
        manager.SetDoorState(4444, EnumDeviceType.Gate, EnumDoorState.Open);       // 미등록 → 무해
    }

    [Fact]
    public void should_init_enclosure_door_state_from_device_model_when_registered()
    {
        var manager = CreateManager();
        var enclosure = new Mock<IEnclosureDeviceModel>();
        enclosure.Setup(d => d.Id).Returns(12);
        enclosure.Setup(d => d.DeviceType).Returns(EnumDeviceType.Enclosure);
        enclosure.Setup(d => d.Status).Returns(EnumDeviceStatus.ACTIVATED);
        enclosure.Setup(d => d.DeviceGroups).Returns(new List<int>());
        enclosure.Setup(d => d.DoorStatus).Returns("open");
        var symbol = new PidsSymbolModel { DeviceType = EnumDeviceType.Enclosure };

        manager.RegisterDeviceSymbol(enclosure.Object, symbol);

        Assert.Equal(EnumDoorState.Open, symbol.DoorState);
    }

    [Fact]
    public void should_ignore_door_state_when_symbol_has_no_door()
    {
        var lookup = new DeviceSymbolLookupModel(new Mock<ILogService>().Object) { Id = 1, SymbolModel = new PidsGroupSymbolModel() };
        lookup.ApplyDoorState(EnumDoorState.Open);
        lookup.ApplyDoorEvent(EnumEventType.ContactOn);   // 그룹 심볼: 예외 없이 무시
        Assert.True(true);
    }

    // ─────────────── DetectionNatsSyncService 접점 분기 ───────────────
    [Theory]
    [InlineData("Gate")]
    [InlineData("Enclosure")]
    public async Task should_route_door_contact_to_door_state_and_skip_queue_when_fallback_enabled(string typeDevice)
    {
        var (_, manager, queue, handler) = CreateDetection(new DefaultDoorContactPolicy());
        var type = Enum.Parse<EnumDeviceType>(typeDevice);

        await handler(Args(DetectJson(5, typeDevice, "ContactOn")));
        await handler(Args(DetectJson(5, typeDevice, "ContactOff")));

        manager.Verify(m => m.ApplyDoorEvent(5, type, EnumEventType.ContactOn), Times.Once);
        manager.Verify(m => m.ApplyDoorEvent(5, type, EnumEventType.ContactOff), Times.Once);
        queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);   // 카드·탐지음·조치보고 큐 제외
    }

    [Fact]
    public async Task should_drop_door_contact_entirely_when_fallback_disabled()
    {
        var policy = new Mock<IDoorContactPolicy>(); policy.Setup(p => p.FallbackEnabled).Returns(false);
        var (_, manager, queue, handler) = CreateDetection(policy.Object);

        await handler(Args(DetectJson(5, "Gate", "ContactOn")));

        manager.Verify(m => m.ApplyDoorEvent(It.IsAny<int>(), It.IsAny<EnumDeviceType>(), It.IsAny<EnumEventType>()), Times.Never);
        queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task should_enqueue_contact_when_device_is_contact_sensor()
    {
        var (_, manager, queue, handler) = CreateDetection(null);   // 정책 미주입 = 기본 true

        await handler(Args(DetectJson(6, "Contact", "ContactOn")));

        queue.Verify(q => q.Enqueue(It.Is<EventEntry>(e => e.DeviceId == 6 && e.EventType == EnumEventType.ContactOn), It.IsAny<string?>()), Times.Once);
        manager.Verify(m => m.ApplyDoorEvent(It.IsAny<int>(), It.IsAny<EnumDeviceType>(), It.IsAny<EnumEventType>()), Times.Never);
    }

    [Fact]
    public async Task should_enqueue_intrusion_on_gate_as_before()
    {
        var (_, manager, queue, handler) = CreateDetection(new DefaultDoorContactPolicy());

        await handler(Args(DetectJson(5, "Gate", "Intrusion")));

        queue.Verify(q => q.Enqueue(It.Is<EventEntry>(e => e.DeviceId == 5 && e.EventType == EnumEventType.Intrusion), It.IsAny<string?>()), Times.Once);
        manager.Verify(m => m.ApplyDoorEvent(It.IsAny<int>(), It.IsAny<EnumDeviceType>(), It.IsAny<EnumEventType>()), Times.Never);
    }

    // ─────────────── OperationEventNatsSyncService ───────────────
    private const string EnclosureOpenJson = """
        { "id": "op-1", "m_type": "REQ", "cmd": "OPERATION_EVENT", "from": "gop-server",
          "body": { "id": 20431, "category_event": "operation", "type_event": "Operation", "reason": "ENCLOSURE_DOOR_OPEN", "severity": "WARNING",
                    "device": { "id": 1351, "name_device": "함체-A-01", "type_device": "Enclosure", "device_groups": [] },
                    "detail": { "door_status": "OPEN", "device_status": "ACTIVATED" } } }
        """;

    [Fact]
    public async Task should_set_door_open_when_operation_event_reports_enclosure_door_open()
    {
        var (_, manager, handler) = CreateOperation();
        await handler(Args(EnclosureOpenJson));
        manager.Verify(m => m.SetDoorState(1351, EnumDeviceType.Enclosure, EnumDoorState.Open), Times.Once);
    }

    [Fact]
    public async Task should_use_reason_suffix_when_detail_missing()
    {
        var (_, manager, handler) = CreateOperation();
        await handler(Args("""{ "cmd": "OPERATION_EVENT", "body": { "reason": "GATE_CLOSED", "device": { "id": 21, "type_device": "Gate" } } }"""));
        manager.Verify(m => m.SetDoorState(21, EnumDeviceType.Gate, EnumDoorState.Closed), Times.Once);
    }

    [Theory]
    [InlineData("""{ "cmd": "OPERATION_EVENT", "body": { "reason": "ENCLOSURE_TEMP_HIGH", "device": { "id": 1351, "type_device": "Enclosure" }, "detail": { "field": "temperature", "value": 52.4 } } }""")]   // 임계치 경보
    [InlineData("""{ "cmd": "OPERATION_EVENT", "body": { "reason": "GATE_OPEN", "device": { "id": 5, "type_device": "Fence" } } }""")]                       // 문 없는 타입
    [InlineData("""{ "cmd": "DETECT", "body": { "reason": "GATE_OPEN", "device": { "id": 5, "type_device": "Gate" } } }""")]                                   // 다른 cmd
    [InlineData("""{ "cmd": "OPERATION_EVENT", "body": { "reason": "GATE_OPEN", "device": { "id": 0, "type_device": "Gate" } } }""")]                          // id 0
    [InlineData("not json")]
    public async Task should_ignore_operation_event_when_not_a_door_change(string json)
    {
        var (_, manager, handler) = CreateOperation();
        await handler(Args(json));
        manager.Verify(m => m.SetDoorState(It.IsAny<int>(), It.IsAny<EnumDeviceType>(), It.IsAny<EnumDoorState>()), Times.Never);
    }

    [Fact]
    public async Task should_gate_operation_event_when_not_authenticated()
    {
        var token = new Mock<ITokenStorageService>(); token.Setup(t => t.IsAuthenticated).Returns(false);
        var (_, manager, handler) = CreateOperation(token.Object);
        await handler(Args(EnclosureOpenJson));
        manager.Verify(m => m.SetDoorState(It.IsAny<int>(), It.IsAny<EnumDeviceType>(), It.IsAny<EnumDoorState>()), Times.Never);
    }

    [Theory]
    [InlineData("OPEN", "ENCLOSURE_TEMP_HIGH", EnumDoorState.Open)]     // detail 이 권위
    [InlineData("closed", "GATE_OPEN", EnumDoorState.Closed)]
    [InlineData(null, "GATE_OPEN", EnumDoorState.Open)]
    [InlineData(null, "ENCLOSURE_DOOR_CLOSED", EnumDoorState.Closed)]
    [InlineData(null, "ENCLOSURE_TEMP_HIGH", null)]
    [InlineData(null, null, null)]
    public void should_resolve_door_state_from_detail_then_reason(string? detail, string? reason, EnumDoorState? expected)
        => Assert.Equal(expected, OperationEventNatsSyncService.Resolve(detail, reason));
}
