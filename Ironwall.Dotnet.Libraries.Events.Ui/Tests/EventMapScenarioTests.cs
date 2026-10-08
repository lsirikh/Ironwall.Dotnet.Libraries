using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
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
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using System.IO;
using Xunit;
using DeviceProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;
using IDeviceProviderService = Ironwall.Dotnet.Libraries.Devices.Ui.Services.IDeviceProviderService;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 이벤트 → 지도 심볼 · 구역선 · 신호등 · 소리 · 카드 반영 헤드리스 시나리오
                  (카탈로그 gis-map-event-scenario-catalog §2 EVM-*, 2026-10-08 r27 헤드리스 이관 1순위)
                  - 실제 큐(EventQueueManager) + 실제 심볼 관리자(SymbolEventManager) + 실제 NATS 수신 서비스
                    (탐지 · 장애 · 운영 · PTZ) + 실제 카드 패널(EventCardListPanelViewModel)을 호스트 배선(EventUiModule)과 같게 잇는다.
                  - 실제 흐름은 "API 기록 → NATS 알림" — 시험도 가짜 서버(FakeGop)에 먼저 기록한 뒤 같은 번호로 NATS 봉투를 보낸다.
                  - 탐지 · 장애는 같은 대상에 조치보고 전 중복 송신하지 않는다(운영 전제). 그 전제를 깨는 입력은 "견딤" 시험에서만.
                  - 창을 띄우지 않는다(Window.Show 없음). 그림(마커 색 픽셀)은 tests/GMaps.Housing.Tests/EventMarkerPaintScenarioTests.
   Created By   : Claude
   Created On   : 2026-10-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 시험 장치 — 가짜 서버(장비 표 · 이벤트 · 조치) + 실제 라이브러리 수신 경로. 한 시험에 하나.
/// </summary>
internal sealed class EventMapRig : IDisposable
{
    private readonly List<Func<MessageArgsModel, Task>> _handlers = new();
    private readonly Func<Type, string, object> _previousIoC = IoC.GetInstance;
    private int _nextDetectionId = 9000;
    private int _nextMalfunctionId = 7000;
    private int _envelopeSeq;

    public EventQueueManager Queue { get; } = new();
    public SymbolEventManager Symbols { get; }
    public EventCardListPanelViewModel Panel { get; }
    /// <summary>GIS 의 장비 캐시(부팅 때 서버에서 읽은 것).</summary>
    public DeviceProvider Devices { get; } = new();
    /// <summary>가짜 서버의 장비 표 — 단건 GET(캐시 미스) 응답.</summary>
    public Dictionary<int, IBaseDeviceModel> ServerDevices { get; } = new();
    /// <summary>가짜 서버에 기록된 이벤트 — (종류, 번호) → (장비, 유형 · 사유).</summary>
    public Dictionary<(string Kind, int Id), (int DeviceId, string What)> ServerEvents { get; } = new();
    /// <summary>가짜 서버에 들어간 조치(from_event id).</summary>
    public List<int> ServerActions { get; } = new();
    public Mock<IEventSetupModel> Setup { get; } = new();
    public Mock<IEventApiService> EventApi { get; } = new();
    public Mock<IDeviceProviderService> DeviceLookup { get; } = new();
    public Mock<IDoorContactPolicy> DoorPolicy { get; } = new();
    public Mock<ILogService> Log { get; } = new();
    public List<object> Published { get; } = new();
    public List<(string Kind, int EventId)> ActionReported { get; } = new();
    public List<EnumEventType> SoundPlays { get; } = new();
    public int SoundStops { get; private set; }
    public (int Detection, int Fault) Counts { get; private set; } = (-1, -1);
    public SoundAlarmController Sound { get; }

    public EventMapRig(bool doorContactFallback = true, bool withDeviceLookup = true)
    {
        Setup.SetupAllProperties();
        Setup.Object.TimeDiscardSec = 3600;
        Setup.Object.IsAutoEventDiscard = true;
        Setup.Object.MalfunctionTimeDiscardSec = 3600;
        Setup.Object.IsMalfunctionAutoEventDiscard = true;
        DoorPolicy.SetupGet(p => p.FallbackEnabled).Returns(doorContactFallback);

        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Returns((object message, Func<Func<Task>, Task> _, CancellationToken _) =>
          {
              lock (Published) Published.Add(message);
              // 카드 패널의 구독 — EntryId 매칭 · "이미 조치됨" 이면 엔트리 즉시 해제(B5)
              return message is EventEntryEnqueuedMessage enqueued ? Panel!.HandleAsync(enqueued, CancellationToken.None) : Task.CompletedTask;
          });

        var setupModel = new EventSetupModel(Setup.Object);
        var account = new Mock<IAccountModel>().Object;
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return ea.Object;
            if (type == typeof(ILogService)) return Log.Object;
            if (type == typeof(EventSetupModel)) return setupModel;
            if (type == typeof(IAccountModel)) return account;
            return null!;
        };

        Symbols = new SymbolEventManager(ea.Object, Log.Object, setupModel, Queue);
        // 호스트 배선(EventUiModule RegisterBuildCallback)과 같다
        Queue.OnDeviceStateChanged += Symbols.HandleDeviceStateChanged;
        Queue.OnGroupStateChanged += Symbols.HandleGroupStateChanged;
        Queue.OnActiveCountChanged += (d, f) => Counts = (d, f);

        Sound = new SoundAlarmController(t => SoundPlays.Add(t), durationSeconds: 20, typeSwitchThrottleMs: 0, stopAll: () => SoundStops++);
        Ironwall.Dotnet.Libraries.Events.Ui.Modules.EventUiModule.WireSoundAlarm(Queue, Sound, new Mock<Ironwall.Dotnet.Libraries.Sounds.Services.ISoundService>().Object);

        EventApi.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActionEventCreateDto dto, CancellationToken _) =>
                {
                    lock (ServerActions) ServerActions.Add(dto.FromEventId);
                    return new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 50_000 + ServerActions.Count } };
                });
        DeviceLookup.Setup(l => l.FetchDeviceByIdAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string _, int id, CancellationToken _) => ServerDevices.TryGetValue(id, out var d) ? d : null);

        Panel = new EventCardListPanelViewModel(ea.Object, Log.Object, null!, account, EventApi.Object, Symbols, Queue, new ActionReportGuard());
        Panel.DispatchToUi = (action, _) => { action(); return Task.CompletedTask; };
        Panel.ActionReported += (kind, id) => ActionReported.Add((kind, id));
        Ironwall.Dotnet.Libraries.Events.Ui.Modules.EventUiModule.WireAutoActions(Queue, Panel, null, Log.Object);

        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handlers.Add(h));
        nats.SetupRemove(n => n.NatsSubscribeEventAsync -= It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handlers.Remove(h));
        var lookup = withDeviceLookup ? DeviceLookup.Object : null;   // 없으면 캐시 미스 단건 GET 을 하지 않는 옛 규칙
        new DetectionNatsSyncService(Log.Object, nats.Object, Symbols, Queue, Setup.Object, ea.Object, null, Devices, DoorPolicy.Object, lookup).StartService();
        new MalfunctionNatsSyncService(Log.Object, nats.Object, Symbols, Queue, Setup.Object, ea.Object, null, Devices, lookup).StartService();
        new OperationEventNatsSyncService(Log.Object, nats.Object, Symbols).StartService();
        new CameraPtzNatsSyncService(Log.Object, nats.Object, Symbols).StartService();
    }

    public void Dispose()
    {
        Queue.Dispose();
        IoC.GetInstance = _previousIoC;
    }

    // ───────────────────────── 장비 · 심볼 ─────────────────────────

    /// <summary>지도에 놓인 센서(서버 장비 + 캐시 + 심볼). <paramref name="controller"/> 가 있으면 그 제어기 소속.</summary>
    public PidsSymbolModel Sensor(int id, EnumDeviceType type = EnumDeviceType.Fence, ControllerDeviceModel? controller = null,
                                  EnumDeviceStatus status = EnumDeviceStatus.ACTIVATED, params int[] groups)
    {
        var device = new SensorDeviceModel
        {
            Id = id, DeviceType = type, CategoryDevice = EnumDeviceCategory.Sensor, Status = status,
            DeviceGroups = groups.ToList(), Controller = controller!,
        };
        return Place(device);
    }

    public (ControllerDeviceModel Device, PidsSymbolModel Symbol) Controller(int id, EnumDeviceStatus status = EnumDeviceStatus.ACTIVATED)
    {
        var device = new ControllerDeviceModel { Id = id, DeviceType = EnumDeviceType.Controller, CategoryDevice = EnumDeviceCategory.Controller, Status = status };
        return (device, Place(device));
    }

    /// <summary>지도에 놓인 장비(종류 아무거나).</summary>
    public PidsSymbolModel Device(int id, EnumDeviceType type, EnumDeviceStatus status = EnumDeviceStatus.ACTIVATED, params int[] groups)
        => Place(new BaseDeviceModel { Id = id, DeviceType = type, CategoryDevice = DeviceTypeResolver.CategoryOf(type), Status = status, DeviceGroups = groups.ToList() });

    /// <summary>캐시 · 서버에만 있고 지도에 심볼이 없는 장비.</summary>
    public void Unplaced(IBaseDeviceModel device)
    {
        Devices.Add(device);
        ServerDevices[device.Id] = device;
    }

    private PidsSymbolModel Place(IBaseDeviceModel device)
    {
        Unplaced(device);
        var symbol = new PidsSymbolModel { Title = $"{device.DeviceType} {device.Id}", DeviceType = device.DeviceType };
        Symbols.RegisterDeviceSymbol(device, symbol);
        return symbol;
    }

    /// <summary>구역선(그룹 심볼) — 속성창에서 그룹을 이은 PIDS 그룹 선.</summary>
    public PidsGroupSymbolModel Zone(int groupId)
    {
        var line = new PidsGroupSymbolModel { Title = $"구역 {groupId}", LinkedDeviceGroup = groupId };
        Symbols.RegisterGroupSymbol(groupId, new BaseDeviceModel { Id = 0 }, line);
        return line;
    }

    // ───────────────────────── 가짜 서버 → NATS ─────────────────────────

    /// <summary>탐지를 서버에 기록하고(POST events/detections) 그 번호로 DETECT 를 보낸다.</summary>
    public async Task<int> DetectAsync(int deviceId, string typeEvent = "Intrusion")
    {
        var id = RecordDetection(deviceId, typeEvent);
        await PublishDetectAsync(id);
        return id;
    }

    public int RecordDetection(int deviceId, string typeEvent = "Intrusion")
    {
        var id = Interlocked.Increment(ref _nextDetectionId);
        ServerEvents[(ActionReportKind.Detection, id)] = (deviceId, typeEvent);
        return id;
    }

    /// <summary>장애를 서버에 기록하고 그 번호로 MALFUNCTION 을 보낸다.</summary>
    public async Task<int> MalfunctionAsync(int deviceId, string reason = "FAULT_FENCE")
    {
        var id = RecordMalfunction(deviceId, reason);
        await PublishMalfunctionAsync(id);
        return id;
    }

    public int RecordMalfunction(int deviceId, string reason)
    {
        var id = Interlocked.Increment(ref _nextMalfunctionId);
        ServerEvents[(ActionReportKind.Malfunction, id)] = (deviceId, reason);
        return id;
    }

    public string NewEnvelopeId() => $"env-{Interlocked.Increment(ref _envelopeSeq):0000}";

    public Task PublishDetectAsync(int eventId, string? envelopeId = null, string cmd = "DETECT", string mType = "PUB")
        => PublishRawAsync(DetectJson(eventId, envelopeId, cmd, mType));

    public string DetectJson(int eventId, string? envelopeId = null, string cmd = "DETECT", string mType = "PUB")
    {
        var (deviceId, typeEvent) = ServerEvents[(ActionReportKind.Detection, eventId)];
        return $$"""
        { "id": "{{envelopeId ?? NewEnvelopeId()}}", "m_type": "{{mType}}", "cmd": "{{cmd}}", "from": "PidsProxy",
          "body": { "id": {{eventId}}, "category_event": "detection", "type_event": "{{typeEvent}}", "action_reported": false,
                    "device": {{DeviceRef(deviceId)}}, "result": "SENSOR" } }
        """;
    }

    public Task PublishMalfunctionAsync(int eventId, string? envelopeId = null)
    {
        var (deviceId, reason) = ServerEvents[(ActionReportKind.Malfunction, eventId)];
        return PublishRawAsync($$"""
        { "id": "{{envelopeId ?? NewEnvelopeId()}}", "m_type": "PUB", "cmd": "MALFUNCTION", "from": "PidsProxy",
          "body": { "id": {{eventId}}, "category_event": "malfunction", "type_event": "Fault", "action_reported": false,
                    "reason": "{{reason}}", "device": {{DeviceRef(deviceId)}},
                    "detail": { "first_start": 10, "first_end": 15, "second_start": 20, "second_end": 25 } } }
        """);
    }

    /// <summary>서버 OPERATION_EVENT(component-status PATCH 뒤) — 함체 · 통문 개폐.</summary>
    public Task PublishOperationAsync(int deviceId, string reason, string? doorStatus = null)
        => PublishRawAsync($$"""
        { "id": "{{NewEnvelopeId()}}", "m_type": "PUB", "cmd": "OPERATION_EVENT", "from": "server",
          "body": { "id": 1, "reason": "{{reason}}", "device": {{DeviceRef(deviceId)}},
                    "detail": {{(doorStatus is null ? "null" : $$"""{ "door_status": "{{doorStatus}}" }""")}} } }
        """);

    /// <summary>v7.0+ 장비 참조 {id, category_device} — 종류 · 그룹은 캐시(없으면 단건 GET)에서.</summary>
    private string DeviceRef(int deviceId)
    {
        var category = ServerDevices.TryGetValue(deviceId, out var d) ? DeviceTypeResolver.CategoryOf(d.DeviceType).ToString().ToLowerInvariant() : "sensor";
        return $$"""{ "id": {{deviceId}}, "category_device": "{{category}}" }""";
    }

    public async Task PublishRawAsync(string json, string subject = "sensorway.unit001.all.event")
    {
        var args = new MessageArgsModel(subject: subject, subscriptionSubject: null, data: json);
        foreach (var handler in _handlers.ToList()) await handler(args);
    }

    // ───────────────────────── 조치보고 ─────────────────────────

    /// <summary>다른 GIS · 서브시스템의 조치(서버 POST events/actions → ACTION_REPORT) — 호스트 라우터가 패널로 옮긴다.</summary>
    public RemoteActionReportOutcome RemoteReport(string kind, int eventId, IBaseDeviceModel? device = null)
    {
        lock (ServerActions) ServerActions.Add(eventId);
        var type = kind == ActionReportKind.Malfunction ? EnumEventType.Fault : TypeOf(eventId);
        return Panel.CloseByRemoteActionReport(kind, eventId, type, device);
    }

    private EnumEventType TypeOf(int detectionId)
        => ServerEvents.TryGetValue((ActionReportKind.Detection, detectionId), out var e) && Enum.TryParse<EnumEventType>(e.What, true, out var t)
            ? t : EnumEventType.Intrusion;

    // ───────────────────────── 카드(호스트가 띄우는 것) ─────────────────────────

    private IBaseDeviceModel? ServerDevice(int deviceId) => ServerDevices.TryGetValue(deviceId, out var d) ? d : null;

    public async Task<DetectionEventCardViewModel> ShowDetectionCardAsync(int eventId)
    {
        var (deviceId, _) = ServerEvents[(ActionReportKind.Detection, eventId)];
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(eventId);
        model.SetupGet(m => m.MessageType).Returns(TypeOf(eventId));
        model.SetupGet(m => m.Device).Returns(ServerDevice(deviceId)!);
        model.SetupProperty(m => m.Status);
        var card = new DetectionEventCardViewModel(model.Object);
        Panel.EnqueueCard(card);
        await Panel.FlushPendingCardsNowAsync();
        return card;
    }

    public async Task<MalfunctionEventCardViewModel> ShowMalfunctionCardAsync(int eventId)
    {
        var (deviceId, _) = ServerEvents[(ActionReportKind.Malfunction, eventId)];
        var model = new Mock<IMalfunctionEventModel>();
        model.SetupGet(m => m.Id).Returns(eventId);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Fault);
        model.SetupGet(m => m.Device).Returns(ServerDevice(deviceId)!);
        model.SetupProperty(m => m.Status);
        var card = new MalfunctionEventCardViewModel(model.Object);
        Panel.EnqueueCard(card);
        await Panel.FlushPendingCardsNowAsync();
        return card;
    }

    /// <summary>운영자가 카드의 조치보고 창에서 [확인] — 서버 POST 뒤 패널로(DetectionReportedMessageModel).</summary>
    public Task ReportLocallyAsync(DetectionEventCardViewModel card)
    {
        lock (ServerActions) ServerActions.Add(card.Model!.Id);
        return Panel.HandleAsync(new DetectionReportedMessageModel(card, "조치 완료", "operator"), CancellationToken.None);
    }

    public Task ReportLocallyAsync(MalfunctionEventCardViewModel card)
    {
        lock (ServerActions) ServerActions.Add(card.Model!.Id);
        return Panel.HandleAsync(new MalfunctionReportedMessageModel(card, "조치 완료", "operator"), CancellationToken.None);
    }
}

/// <summary>
/// §2 A · B · C · D · E · F · G — NATS 수신 → 큐 → 심볼(색 축 CompositeStatus/EventStatus · 운영 축 OperationState · 개폐 축 DoorState)
/// · 구역선 · 신호등 건수 · 소리. 렌더(픽셀)는 GMaps.Housing.Tests/EventMarkerPaintScenarioTests.
/// </summary>
[Collection("IoC-Dependent")]   // 카드 패널이 IoC.GetInstance(전역 정적)를 읽는다
public class EventMapScenarioTests
{
    private static EventMapRig Rig(bool doorContactFallback = true) => new(doorContactFallback);

    // ───────────────────────── A. 센서 탐지 ─────────────────────────

    [Fact]
    public async Task should_paint_symbol_and_zone_detecting_and_count_one_when_detect_arrives_after_api_record_evm005_009()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(101, groups: 7);
        var zone = rig.Zone(7);

        await rig.DetectAsync(101);

        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);   // EVM-007 구역선
        Assert.Equal((1, 0), rig.Counts);                                           // EVM-009 신호등 탐지 +1
        Assert.Equal(new[] { EnumEventType.Intrusion }, rig.SoundPlays);           // EVM-011 탐지 소리
    }

    [Fact]
    public async Task should_paint_both_zone_lines_when_sensor_in_two_groups_detects_evm019()
    {
        using var rig = Rig();
        rig.Sensor(102, groups: new[] { 3, 4 });
        var a = rig.Zone(3);
        var b = rig.Zone(4);

        await rig.DetectAsync(102);

        Assert.Equal(EnumCompositeEventStatus.Detecting, a.CompositeStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, b.CompositeStatus);
    }

    [Theory]
    [InlineData("Alert", EnumDeviceType.Fence)]        // EVM-021 사전 경보
    [InlineData("ContactOn", EnumDeviceType.Contact)]  // EVM-051 접점 센서
    [InlineData("ContactOff", EnumDeviceType.Contact)]
    public async Task should_paint_symbol_and_zone_detecting_when_alert_or_contact_sensor_detects_evm021_051(string typeEvent, EnumDeviceType type)
    {
        // 결함(2026-10-08 수정): 큐가 Intrusion 만 탐지로 쳐서, 카드 · 소리 · 신호등은 올라가는데 심볼 · 구역선은 초록 그대로였다.
        using var rig = Rig();
        var symbol = rig.Sensor(103, type, groups: 8);
        var zone = rig.Zone(8);

        var id = await rig.DetectAsync(103, typeEvent);

        Assert.Equal((1, 0), rig.Counts);
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);

        rig.RemoteReport(ActionReportKind.Detection, id);   // 모든 시나리오는 조치로 닫는다

        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
    }

    [Fact]
    public async Task should_fetch_device_once_and_paint_when_v7_reference_misses_cache_evm005_n5()
    {
        // 장비가 캐시에 없고 서버에만 있다(부팅 뒤 추가) — 단건 GET 으로 종류 · 그룹을 채워 칠한다. 심볼은 시작 때 놓인 것.
        using var rig = Rig();
        var device = new SensorDeviceModel { Id = 104, DeviceType = EnumDeviceType.PIR, CategoryDevice = EnumDeviceCategory.Sensor, DeviceGroups = new() { 9 } };
        rig.ServerDevices[104] = device;
        var symbol = new PidsSymbolModel { DeviceType = EnumDeviceType.PIR };
        rig.Symbols.RegisterDeviceSymbol(device, symbol);
        var zone = rig.Zone(9);

        await rig.DetectAsync(104);

        rig.DeviceLookup.Verify(l => l.FetchDeviceByIdAsync("sensor", 104, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);
    }

    [Fact]
    public async Task should_skip_queue_and_symbols_when_detected_device_was_deleted_on_server_evm016()
    {
        using var rig = Rig();
        var sibling = rig.Sensor(110);
        rig.ServerEvents[(ActionReportKind.Detection, 1)] = (999, "Intrusion");   // 서버 기록은 있으나 장비는 지워짐(단건 GET 404)

        await rig.PublishDetectAsync(1);

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, sibling.EventStatus);
        Assert.Equal(-1, rig.Counts.Detection);   // 건수 통지 자체가 없다
    }

    [Fact]
    public async Task should_queue_as_none_and_paint_nothing_when_sensor_type_cannot_be_resolved_evm017()
    {
        // 캐시 미스 + 단건 GET 없음(조회 불가) + 카테고리 sensor — 종류를 추측하지 않고 NONE 으로 큐에(자동조치 · 원격 해제용), 심볼은 칠하지 않는다.
        //   지도에는 같은 번호로 이어진 옛 심볼(DB 심볼이 아는 다른 종류)이 있다 — 그것을 칠하면 안 된다(WP-8 H2).
        using var rig = new EventMapRig(withDeviceLookup: false);
        var staleLinked = new PidsSymbolModel { DeviceType = EnumDeviceType.IpCamera };
        rig.Symbols.RegisterDeviceSymbol(new BaseDeviceModel { Id = 120, DeviceType = EnumDeviceType.IpCamera }, staleLinked);
        rig.ServerEvents[(ActionReportKind.Detection, 2)] = (120, "Intrusion");

        await rig.PublishDetectAsync(2);

        Assert.NotNull(rig.Queue.FindEntryByDevice(120, EnumDeviceType.NONE));
        Assert.Equal(EnumEventStatus.Normal, staleLinked.EventStatus);
        Assert.Equal((1, 0), rig.Counts);

        rig.RemoteReport(ActionReportKind.Detection, 2);
        Assert.Equal((0, 0), rig.Counts);
    }

    [Fact]
    public async Task should_queue_once_when_same_envelope_is_published_three_times_evm032()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(130);
        var id = rig.RecordDetection(130);

        for (int i = 0; i < 3; i++) await rig.PublishDetectAsync(id, envelopeId: "env-same");

        Assert.Equal(1, rig.Queue.GetTotalQueueCount());
        Assert.Single(rig.SoundPlays);
        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);   // 하나만 들어갔으므로 조치 하나로 원상복귀
    }

    [Fact]
    public async Task should_queue_once_when_same_event_is_resent_with_new_envelope_id_evm033()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(131);
        var id = rig.RecordDetection(131);

        await rig.PublishDetectAsync(id);
        await rig.PublishDetectAsync(id);   // 매니저 재전송 — 봉투 id 만 새것

        Assert.Equal(1, rig.Queue.GetTotalQueueCount());
        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    public async Task should_end_normal_without_alarm_when_action_report_arrives_before_detection_evm034()
    {
        // 순서 뒤집힘 — 서버는 탐지를 기록하고 곧 조치됐는데, 이 GIS 엔 ACTION_REPORT 가 DETECT 보다 먼저 닿았다.
        using var rig = Rig();
        var symbol = rig.Sensor(140, groups: 5);
        var zone = rig.Zone(5);
        var id = rig.RecordDetection(140);

        rig.RemoteReport(ActionReportKind.Detection, id);
        await rig.PublishDetectAsync(id);

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(1, rig.SoundStops);   // 잠깐 울렸다가 곧바로 멈춘다(0 건)
    }

    [Fact]
    public async Task should_not_repaint_when_same_detection_envelope_is_redelivered_after_report_evm036()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(141);
        var id = rig.RecordDetection(141);
        await rig.PublishDetectAsync(id, envelopeId: "env-redeliver");
        rig.RemoteReport(ActionReportKind.Detection, id);

        await rig.PublishDetectAsync(id, envelopeId: "env-redeliver");   // 브로커 재배달

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    public async Task should_process_each_item_when_array_envelope_carries_two_detections_evm035()
    {
        using var rig = Rig();
        var a = rig.Sensor(142);
        var b = rig.Sensor(143);
        var first = rig.RecordDetection(142);
        var second = rig.RecordDetection(143);

        await rig.PublishRawAsync($"[{rig.DetectJson(first)}, {rig.DetectJson(second)}]");

        Assert.Equal(EnumEventStatus.Detecting, a.EventStatus);
        Assert.Equal(EnumEventStatus.Detecting, b.EventStatus);
        rig.RemoteReport(ActionReportKind.Detection, first);
        rig.RemoteReport(ActionReportKind.Detection, second);
        Assert.Equal((0, 0), rig.Counts);
    }

    // ───────────────────────── B. AI · 접점 ─────────────────────────

    [Fact]
    public async Task should_paint_camera_detecting_and_release_on_report_when_ai_detection_arrives_evm041_042()
    {
        using var rig = Rig();
        var camera = rig.Device(150, EnumDeviceType.IpCamera, groups: 6);
        var zone = rig.Zone(6);

        var id = await rig.DetectAsync(150);

        Assert.Equal(EnumEventStatus.Detecting, camera.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);   // EVM-046 매핑 그룹 — 창은 EventWindowTriggerTests
        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, camera.EventStatus);
    }

    [Fact]
    public async Task should_ignore_legacy_ai_detection_cmd_in_library_because_host_normalizes_it_evm044()
    {
        // 옛 cmd AI_DETECTION 은 호스트(NatsBrokerService :309)가 DETECT 로 정규화해 큐에 넣는다 — 라이브러리 수신 서비스까지 받으면 두 번 들어간다.
        using var rig = Rig();
        var camera = rig.Device(151, EnumDeviceType.IpCamera);
        var id = rig.RecordDetection(151);

        await rig.PublishDetectAsync(id, cmd: "AI_DETECTION");

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, camera.EventStatus);
    }

    [Fact]
    public async Task should_skip_queue_when_ai_camera_was_deleted_evm045()
    {
        using var rig = Rig();
        rig.ServerEvents[(ActionReportKind.Detection, 3)] = (152, "Intrusion");
        var json = rig.DetectJson(3).Replace("\"category_device\": \"sensor\"", "\"category_device\": \"camera\"");   // 서버에 없는 카메라(404)

        await rig.PublishRawAsync(json);

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
    }

    [Theory]
    [InlineData("ContactOn", EnumDoorState.Open)]
    [InlineData("ContactOff", EnumDoorState.Closed)]
    public async Task should_move_door_without_card_queue_or_color_when_enclosure_contact_arrives_evm047(string typeEvent, EnumDoorState expected)
    {
        using var rig = Rig();
        var enclosure = rig.Device(160, EnumDeviceType.Enclosure, groups: 2);
        var zone = rig.Zone(2);

        await rig.DetectAsync(160, typeEvent);

        Assert.Equal(expected, enclosure.DoorState);
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, enclosure.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Empty(rig.SoundPlays);
    }

    [Fact]
    public async Task should_invert_door_when_open_on_contact_on_is_off_evm049()
    {
        using var rig = Rig();
        var gate = rig.Device(161, EnumDeviceType.Gate);
        gate.OpenOnContactOn = false;   // 현장 배선 반전

        await rig.DetectAsync(161, "ContactOn");

        Assert.Equal(EnumDoorState.Closed, gate.DoorState);
    }

    [Fact]
    public async Task should_leave_door_untouched_when_door_contact_fallback_is_off_evm050()
    {
        using var rig = Rig(doorContactFallback: false);
        var gate = rig.Device(162, EnumDeviceType.Gate);

        await rig.DetectAsync(162, "ContactOn");

        Assert.Equal(EnumDoorState.Unknown, gate.DoorState);
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
    }

    // ───────────────────────── C. 장애 · 블랙아웃 ─────────────────────────

    [Fact]
    public async Task should_paint_fault_and_count_fault_and_play_fault_sound_when_malfunction_arrives_evm054_055_057_058()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(201, groups: 11);
        var zone = rig.Zone(11);

        var id = await rig.MalfunctionAsync(201);

        Assert.Equal(EnumEventStatus.Fault, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Faulted, zone.CompositeStatus);
        Assert.Equal((0, 1), rig.Counts);
        Assert.Equal(new[] { EnumEventType.Fault }, rig.SoundPlays);

        rig.RemoteReport(ActionReportKind.Malfunction, id);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
    }

    [Fact]
    public async Task should_show_fault_color_and_error_axis_together_when_server_marks_device_error_after_fault_evm056()
    {
        // 장애 → 서버가 장비를 ERROR 로 바꾸고 SYNC_DEVICE(호스트가 SyncDeviceStatus 로 옮김) — 색 축(장애)과 운영 축(오류)이 같이 선다.
        using var rig = Rig();
        var symbol = rig.Sensor(202);
        var id = await rig.MalfunctionAsync(202);

        rig.Symbols.SyncDeviceStatus(202, EnumDeviceType.Fence, EnumDeviceStatus.ERROR);

        Assert.Equal(EnumEventStatus.Fault, symbol.EventStatus);
        Assert.Equal(EnumOperationState.ERROR, symbol.OperationState);

        rig.RemoteReport(ActionReportKind.Malfunction, id);
        rig.Symbols.SyncDeviceStatus(202, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);   // EVM-093 조치 → 서버 ACTIVATED
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumOperationState.ACTIVATED, symbol.OperationState);
    }

    [Fact]
    public async Task should_skip_queue_when_faulted_device_was_deleted_evm061()
    {
        using var rig = Rig();
        rig.ServerEvents[(ActionReportKind.Malfunction, 1)] = (998, "FAULT_FENCE");

        await rig.PublishMalfunctionAsync(1);

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
    }

    [Fact]
    public async Task should_paint_again_when_new_malfunction_arrives_after_report_evm062()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(203);
        var first = await rig.MalfunctionAsync(203);
        rig.RemoteReport(ActionReportKind.Malfunction, first);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);

        var second = await rig.MalfunctionAsync(203);

        Assert.Equal(EnumEventStatus.Fault, symbol.EventStatus);
        rig.RemoteReport(ActionReportKind.Malfunction, second);
    }

    [Fact]
    public async Task should_black_out_controller_and_member_zones_but_keep_member_sensor_color_when_controller_faults_evm064_065_066()
    {
        using var rig = Rig();
        var (ctrl, ctrlSymbol) = rig.Controller(300);
        var member = rig.Sensor(301, controller: ctrl, groups: 21);
        var memberZone = rig.Zone(21);
        var otherZone = rig.Zone(22);
        rig.Sensor(302, groups: 22);   // 다른 제어기 소속

        var id = await rig.MalfunctionAsync(300, "FAULT_CONTROLLER");

        Assert.Equal(EnumEventStatus.Blackout, ctrlSymbol.EventStatus);                 // EVM-066 제어기 심볼 검정
        Assert.Equal(EnumCompositeEventStatus.Blackout, memberZone.CompositeStatus);   // EVM-065 소속 구역선 검정
        Assert.Equal(EnumCompositeEventStatus.Normal, otherZone.CompositeStatus);
        Assert.Equal(EnumEventStatus.Normal, member.EventStatus);                       // EVM-064 소속 센서 이벤트 축은 그대로(설계)

        rig.RemoteReport(ActionReportKind.Malfunction, id);
        Assert.Equal(EnumEventStatus.Normal, ctrlSymbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, memberZone.CompositeStatus);
    }

    [Fact]
    public async Task should_auto_recover_blackout_when_controller_becomes_activated_evm070()
    {
        using var rig = Rig();
        var (ctrl, ctrlSymbol) = rig.Controller(310);
        rig.Sensor(311, controller: ctrl, groups: 23);
        var zone = rig.Zone(23);
        var id = await rig.MalfunctionAsync(310, "FAULT_CONTROLLER");
        await rig.ShowMalfunctionCardAsync(id);

        // 호스트 SYNC_DEVICE ACTIVATED 처리와 같은 순서: 자동복구 → 상태 동기
        Assert.True(rig.Queue.TryAutoRecoverController(310));
        rig.Symbols.SyncDeviceStatus(310, EnumDeviceType.Controller, EnumDeviceStatus.ACTIVATED);
        await Task.Delay(50);   // 자동복구 조치보고는 배경 작업(WireAutoActions)

        Assert.Equal(EnumEventStatus.Normal, ctrlSymbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Contains(id, rig.ServerActions);                                            // 서버에 자동 조치
        Assert.Empty(rig.Panel.ViewModelProvider);                                          // 카드 닫힘
    }

    [Fact]
    public async Task should_turn_zone_from_black_to_red_when_member_sensor_detects_during_blackout_evm071()
    {
        using var rig = Rig();
        var (ctrl, ctrlSymbol) = rig.Controller(320);
        var member = rig.Sensor(321, controller: ctrl, groups: 24);
        var zone = rig.Zone(24);
        var fault = await rig.MalfunctionAsync(320, "FAULT_CONTROLLER");
        await rig.ShowMalfunctionCardAsync(fault);
        Assert.Equal(EnumCompositeEventStatus.Blackout, zone.CompositeStatus);

        var detection = await rig.DetectAsync(321);   // 소속 센서가 살아 있다 = 제어기 통신 복구
        await Task.Delay(50);

        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);
        Assert.Equal(EnumEventStatus.Normal, ctrlSymbol.EventStatus);   // 제어기 자기 심볼도 풀린다
        Assert.Equal(EnumEventStatus.Detecting, member.EventStatus);
        Assert.Contains(fault, rig.ServerActions);                       // 제어기 장애 자동 조치

        rig.RemoteReport(ActionReportKind.Detection, detection);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
    }

    [Fact]
    public async Task should_not_expand_groups_when_lonely_controller_faults_evm072()
    {
        using var rig = Rig();
        var (_, ctrlSymbol) = rig.Controller(330);
        var unrelatedZone = rig.Zone(25);
        rig.Sensor(331, groups: 25);   // 소속 제어기 없음

        var id = await rig.MalfunctionAsync(330, "FAULT_CONTROLLER");

        Assert.Equal(EnumEventStatus.Blackout, ctrlSymbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, unrelatedZone.CompositeStatus);
        Assert.DoesNotContain(25, rig.Queue.FindEntryByEventId(id, EnumEventType.Fault)!.GroupIds ?? new List<int>());
        rig.RemoteReport(ActionReportKind.Malfunction, id);
    }

    // ───────────────────────── D. 복합 상태 ─────────────────────────

    [Fact]
    public async Task should_step_down_to_fault_when_detection_of_faulted_detecting_sensor_is_reported_evm074_075()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(401, groups: 31);
        var zone = rig.Zone(31);
        var detection = await rig.DetectAsync(401);
        var fault = await rig.MalfunctionAsync(401);

        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting, symbol.CompositeStatus);
        Assert.Equal(EnumEventStatus.Fault, symbol.EventStatus);                       // 마커 점은 장애(주황) 우선 — 설계(PidsSymbolModel)
        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting, zone.CompositeStatus); // 구역선은 빨강/주황 번갈아(EVM-078 픽셀은 Housing 시험)

        rig.RemoteReport(ActionReportKind.Detection, detection);
        Assert.Equal(EnumCompositeEventStatus.Faulted, symbol.CompositeStatus);
        Assert.Equal(EnumCompositeEventStatus.Faulted, zone.CompositeStatus);

        rig.RemoteReport(ActionReportKind.Malfunction, fault);
        Assert.Equal(EnumCompositeEventStatus.Normal, symbol.CompositeStatus);
    }

    [Fact]
    public async Task should_keep_detecting_when_fault_is_reported_first_evm076()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(402, groups: 32);
        var zone = rig.Zone(32);
        var detection = await rig.DetectAsync(402);
        var fault = await rig.MalfunctionAsync(402);

        rig.RemoteReport(ActionReportKind.Malfunction, fault);

        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);
        rig.RemoteReport(ActionReportKind.Detection, detection);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    public async Task should_auto_recover_fault_and_show_detecting_when_same_sensor_detects_after_fault_evm077()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(403);
        var fault = await rig.MalfunctionAsync(403);
        await rig.ShowMalfunctionCardAsync(fault);

        var detection = await rig.DetectAsync(403);
        await Task.Delay(50);

        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.CompositeStatus);   // 장애는 자동복구로 빠졌다
        Assert.Contains(fault, rig.ServerActions);
        Assert.Equal((1, 0), rig.Counts);
        rig.RemoteReport(ActionReportKind.Detection, detection);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    // ───────────────────────── E. 연결 ─────────────────────────

    [Fact]
    public async Task should_change_nothing_on_map_when_connection_envelope_arrives_evm079_081_082()
    {
        // CONNECTION 은 라이브러리 수신 서비스가 받지 않는다(카드 없음 · 청록 Connection 상태는 만들어지지 않음 — 카탈로그 정적 판독 4).
        using var rig = Rig();
        var symbol = rig.Sensor(501);

        await rig.PublishRawAsync("""{ "id": "c1", "m_type": "PUB", "cmd": "CONNECTION", "body": { "id": 1, "device": { "id": 501, "category_device": "sensor" } } }""");
        var after = await rig.DetectAsync(501);   // 뒤 이벤트는 계속 처리

        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal((1, 0), rig.Counts);
        rig.RemoteReport(ActionReportKind.Detection, after);
    }

    // ───────────────────────── F. 조치보고와 원상복귀 ─────────────────────────

    [Fact]
    public async Task should_restore_symbol_zone_counts_and_stop_sound_when_local_report_ok_evm091_095()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(601, groups: 41);
        var zone = rig.Zone(41);
        var id = await rig.DetectAsync(601);
        var card = await rig.ShowDetectionCardAsync(id);

        await rig.ReportLocallyAsync(card);

        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(1, rig.SoundStops);
        Assert.Contains((ActionReportKind.Detection, id), rig.ActionReported);   // 이벤트 창 닫기 신호(EVM-088)
    }

    [Fact]
    public async Task should_restore_symbol_when_local_malfunction_report_ok_evm093()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(602, groups: 42);
        var zone = rig.Zone(42);
        var id = await rig.MalfunctionAsync(602);
        var card = await rig.ShowMalfunctionCardAsync(id);

        await rig.ReportLocallyAsync(card);

        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
    }

    [Fact]
    public async Task should_restore_symbol_and_close_card_when_auto_report_timer_fires_evm094()
    {
        using var rig = Rig();
        rig.Setup.Object.TimeDiscardSec = 0;   // 곧바로 만료
        var symbol = rig.Sensor(603);
        var id = await rig.DetectAsync(603);
        await rig.ShowDetectionCardAsync(id);

        rig.Queue.OnSharedTimerTick();
        await Task.Delay(100);   // 자동 조치보고는 배경 작업

        Assert.Contains(id, rig.ServerActions);
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal((0, 0), rig.Counts);
    }

    [Fact]
    public async Task should_leave_own_symbols_when_unknown_event_is_reported_elsewhere_evm096()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(604);
        var id = await rig.DetectAsync(604);

        var outcome = rig.RemoteReport(ActionReportKind.Detection, 424242);

        Assert.Equal(RemoteActionReportOutcome.Nothing, outcome);
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal((1, 0), rig.Counts);
        rig.RemoteReport(ActionReportKind.Detection, id);
    }

    [Fact]
    public async Task should_close_only_detection_when_detection_and_malfunction_share_number_evm083()
    {
        // 서버는 탐지 · 장애를 따로 번호 매긴다 — 같은 번호라도 종류로 가른다.
        using var rig = Rig();
        var symbol = rig.Sensor(605);
        rig.ServerEvents[(ActionReportKind.Detection, 77)] = (605, "Intrusion");
        rig.ServerEvents[(ActionReportKind.Malfunction, 77)] = (605, "FAULT_FENCE");
        await rig.PublishDetectAsync(77);
        await rig.PublishMalfunctionAsync(77);

        rig.RemoteReport(ActionReportKind.Detection, 77);

        Assert.Equal(EnumCompositeEventStatus.Faulted, symbol.CompositeStatus);
        Assert.Equal((0, 1), rig.Counts);
        rig.RemoteReport(ActionReportKind.Malfunction, 77);
        Assert.Equal(EnumCompositeEventStatus.Normal, symbol.CompositeStatus);
    }

    [Fact]
    public async Task should_clear_every_symbol_when_burst_on_distinct_sensors_is_reported_one_by_one_evm101()
    {
        // 운영 전제(같은 대상 중복 없음)를 지키는 버스트 — 서로 다른 센서 20개.
        using var rig = Rig();
        var symbols = Enumerable.Range(0, 20).Select(i => rig.Sensor(700 + i, groups: 50 + i % 3)).ToList();
        var zones = new[] { rig.Zone(50), rig.Zone(51), rig.Zone(52) };
        var ids = new List<int>();
        foreach (var i in Enumerable.Range(0, 20)) ids.Add(await rig.DetectAsync(700 + i));
        Assert.Equal((20, 0), rig.Counts);
        Assert.All(zones, z => Assert.Equal(EnumCompositeEventStatus.Detecting, z.CompositeStatus));

        foreach (var id in ids) rig.RemoteReport(ActionReportKind.Detection, id);

        Assert.All(symbols, s => Assert.Equal(EnumEventStatus.Normal, s.EventStatus));
        Assert.All(zones, z => Assert.Equal(EnumCompositeEventStatus.Normal, z.CompositeStatus));
        Assert.Equal((0, 0), rig.Counts);
    }

    [Fact]
    public async Task should_do_nothing_when_action_report_has_no_from_event_evm103()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(606);
        var id = await rig.DetectAsync(606);

        var outcome = rig.Panel.CloseByRemoteActionReport(null, 0);

        Assert.Equal(RemoteActionReportOutcome.Invalid, outcome);
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        rig.RemoteReport(ActionReportKind.Detection, id);
    }

    [Fact]
    public async Task should_recompute_symbol_from_queue_when_report_arrives_for_event_this_gis_never_queued_evm003_fr03()
    {
        // 부팅 전에 난 장애(큐에 없음) — 원격 조치보고가 장비를 알려 주면 심볼을 큐 실제 상태로 다시 계산한다(맹목 Normal 아님).
        using var rig = Rig();
        var symbol = rig.Sensor(607, status: EnumDeviceStatus.ERROR);
        symbol.CompositeStatus = EnumCompositeEventStatus.Faulted;   // DB 에서 실려 온 옛 색
        var live = await rig.DetectAsync(607);                         // 지금 살아 있는 탐지

        var outcome = rig.RemoteReport(ActionReportKind.Malfunction, 5555, rig.ServerDevices[607]);

        Assert.Equal(RemoteActionReportOutcome.SymbolRefreshed, outcome);
        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.CompositeStatus);
        rig.RemoteReport(ActionReportKind.Detection, live);
    }

    // ───────────────────────── G. 문 · SYNC_DEVICE ─────────────────────────

    [Theory]
    [InlineData(EnumDeviceType.Enclosure, "DOOR_OPEN", "OPEN", EnumDoorState.Open)]
    [InlineData(EnumDeviceType.Enclosure, "DOOR_CLOSED", "CLOSED", EnumDoorState.Closed)]
    [InlineData(EnumDeviceType.Gate, "GATE_OPEN", null, EnumDoorState.Open)]
    public async Task should_set_door_state_without_color_or_count_when_operation_event_arrives_evm106_107_109(
        EnumDeviceType type, string reason, string? doorStatus, EnumDoorState expected)
    {
        using var rig = Rig();
        var symbol = rig.Device(801, type);

        await rig.PublishOperationAsync(801, reason, doorStatus);

        Assert.Equal(expected, symbol.DoorState);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(-1, rig.Counts.Detection);   // 신호등 · 카드 영향 없음
    }

    [Fact]
    public async Task should_keep_door_when_operation_event_is_threshold_reason_evm110()
    {
        using var rig = Rig();
        var symbol = rig.Device(802, EnumDeviceType.Enclosure);
        await rig.PublishOperationAsync(802, "DOOR_OPEN", "OPEN");

        await rig.PublishOperationAsync(802, "TEMPERATURE_HIGH");

        Assert.Equal(EnumDoorState.Open, symbol.DoorState);
    }

    [Fact]
    public async Task should_skip_operation_event_for_deleted_device_and_keep_processing_evm111()
    {
        using var rig = Rig();
        var symbol = rig.Device(803, EnumDeviceType.Enclosure);

        await rig.PublishRawAsync("""{ "id": "op-null", "m_type": "PUB", "cmd": "OPERATION_EVENT", "body": { "id": 1, "reason": "DOOR_OPEN", "device": null, "detail": null } }""");
        await rig.PublishOperationAsync(803, "DOOR_OPEN", "OPEN");

        Assert.Equal(EnumDoorState.Open, symbol.DoorState);
    }

    [Theory]
    [InlineData(EnumDeviceStatus.ERROR, EnumOperationState.ERROR)]
    [InlineData(EnumDeviceStatus.DEACTIVATED, EnumOperationState.DEACTIVATED)]
    [InlineData(EnumDeviceStatus.ACTIVATED, EnumOperationState.ACTIVATED)]
    public void should_follow_sync_device_status_on_operation_axis_without_touching_event_axis_evm104_114_115(EnumDeviceStatus status, EnumOperationState expected)
    {
        using var rig = Rig();
        var symbol = rig.Sensor(804);

        rig.Symbols.SyncDeviceStatus(804, EnumDeviceType.Fence, status);

        Assert.Equal(expected, symbol.OperationState);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    public async Task should_recompute_from_queue_when_activated_arrives_with_stale_blackout_evm116()
    {
        using var rig = Rig();
        var (_, ctrlSymbol) = rig.Controller(805, EnumDeviceStatus.ERROR);
        ctrlSymbol.CompositeStatus = EnumCompositeEventStatus.Blackout;   // 남은 옛 값(큐엔 없음)
        var sensor = rig.Sensor(806);
        var live = await rig.DetectAsync(806);

        rig.Symbols.SyncDeviceStatus(805, EnumDeviceType.Controller, EnumDeviceStatus.ACTIVATED);
        rig.Symbols.SyncDeviceStatus(806, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);

        Assert.Equal(EnumEventStatus.Normal, ctrlSymbol.EventStatus);
        Assert.Equal(EnumEventStatus.Detecting, sensor.EventStatus);   // 큐에 살아 있는 것은 지우지 않는다
        rig.RemoteReport(ActionReportKind.Detection, live);
    }

    [Fact]
    public async Task should_keep_card_reportable_and_lower_counts_and_normalize_symbol_when_device_deleted_evm117()
    {
        // 호스트 SYNC_DEVICE{DELETED}: RemoveByDevice(큐) + 지도 SymbolLifecycleCoordinator.UnregisterDevice(심볼)
        using var rig = Rig();
        var symbol = rig.Sensor(807, groups: 61);
        var zone = rig.Zone(61);
        var id = await rig.DetectAsync(807);
        var card = await rig.ShowDetectionCardAsync(id);

        rig.Queue.RemoveByDevice(807, EnumDeviceType.Fence);
        rig.Symbols.UnregisterDeviceSymbol(807, EnumDeviceType.Fence);

        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Single(rig.Panel.ViewModelProvider);   // 카드는 남아 조치할 수 있다

        await rig.ReportLocallyAsync(card);
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);   // 지워진 장비는 다시 칠하지 않는다
    }

    [Fact]
    public async Task should_paint_new_zone_when_cached_device_moves_to_another_group_evm120()
    {
        // 호스트 SYNC_DEVICE_GROUP/SYNC_DEVICE 가 캐시 장비의 그룹을 바꾼 뒤 — 다음 탐지는 새 그룹 선을 칠한다.
        using var rig = Rig();
        rig.Sensor(808, groups: 71);
        var oldZone = rig.Zone(71);
        var newZone = rig.Zone(72);
        ((SensorDeviceModel)rig.ServerDevices[808]).DeviceGroups = new List<int> { 72 };

        var id = await rig.DetectAsync(808);

        Assert.Equal(EnumCompositeEventStatus.Normal, oldZone.CompositeStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, newZone.CompositeStatus);
        rig.RemoteReport(ActionReportKind.Detection, id);
    }

    [Fact]
    public async Task should_rotate_fov_when_ptz_status_arrives_evm139()
    {
        using var rig = Rig();
        var camera = rig.Device(809, EnumDeviceType.IpCamera);
        camera.BaseBearing = 10;

        await rig.PublishRawAsync("""{ "id": "ptz-1", "m_type": "PUB", "cmd": "PTZ_STATUS", "body": { "camera_id": 809, "pan": 90, "tilt": 0, "zoom": 100 } }""",
                                  subject: "sensorway.unit001.gis.ptz-status");

        Assert.Equal(100, camera.DetectionBearing, 3);   // 설치 방향 10° + pan 90°
        Assert.Equal(5, camera.DetectionAngle, 3);
    }

    // ───────────────────────── H. SYNC_DETECTION · 억제 ─────────────────────────

    [Fact]
    public async Task should_close_card_restore_symbol_and_signal_window_when_detection_deleted_on_server_evm125()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(901, groups: 81);
        var zone = rig.Zone(81);
        var id = await rig.DetectAsync(901);
        await rig.ShowDetectionCardAsync(id);

        await rig.Panel.HandleAsync(new DetectionDeletedOnServerMessage(id, EnumEventType.Intrusion), CancellationToken.None);

        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Contains((ActionReportKind.Detection, id), rig.ActionReported);   // 이벤트 창 닫기
    }

    [Fact]
    public async Task should_still_paint_when_suppressed_device_event_bypasses_rest_gate_evm130()
    {
        // 억제는 서버 REST 게이트 하나(클라이언트 게이트 없음) — 직접 NATS 로 온 이벤트는 그대로 칠한다(설계 확인).
        using var rig = Rig();
        var symbol = rig.Sensor(902);

        var id = await rig.DetectAsync(902);

        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        rig.RemoteReport(ActionReportKind.Detection, id);
    }

    // ───────────────────────── C. 고장 구간 글(조치 창) ─────────────────────────

    [Theory]
    [InlineData(1, 3, "1 ~ 3")]
    [InlineData(10, 15, "10 ~ 15")]
    public void should_show_first_and_second_section_as_start_tilde_end_in_malfunction_report_dialog_evm067(int start, int end, string expected)
    {
        // 블랙아웃 · 울타리 장애의 고장 구간은 지도에 그리지 않고(EVM-068/069 구현 없음) 조치 창 글로만 나온다 —
        //   두 칸의 바인딩(FirstStart~FirstEnd · SecondStart~SecondEnd, " ~ ")과 글 만드는 규칙을 본다.
        Assert.Equal(expected, Ironwall.Dotnet.Libraries.Events.Ui.Converters.ReportValueTextConverter.Pair(start, end, " ~ "));

        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Events.Ui", "Views", "Dialogs", "MalfunctionReportDialogView.xaml"));
        foreach (var (id, first, second) in new[] { ("FirstSection", "FirstStart", "FirstEnd"), ("SecondSection", "SecondStart", "SecondEnd") })
        {
            var at = xaml.IndexOf($"Events.MalfunctionActionReport.Field.{id}", StringComparison.Ordinal);
            Assert.True(at > 0, $"{id} 칸 없음");
            var block = xaml.Substring(at, Math.Min(600, xaml.Length - at));
            Assert.Contains($"Path=\"{first}\"", block);
            Assert.Contains($"Path=\"{second}\"", block);
            Assert.Contains("ConverterParameter=\" ~ \"", block);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("저장소 루트(sln)를 찾지 못함");
    }

    // ───────────────────────── I. 견딤 ─────────────────────────

    [Fact]
    public async Task should_keep_map_unchanged_and_keep_processing_when_junk_messages_arrive_evm140()
    {
        using var rig = Rig();
        var symbol = rig.Sensor(1001, groups: 91);
        var zone = rig.Zone(91);
        var junk = new[]
        {
            """{ "id": "s1", "m_type": "PUB", "cmd": "SYSTEM_EVENT", "body": { "id": 1 } }""",
            """{ "id": "s2", "m_type": "PUB", "cmd": "detect", "body": { "id": 2, "type_event": "Intrusion", "device": { "id": 1001, "category_device": "sensor" } } }""",
            """{ "id": "s3", "m_type": "RSP", "cmd": "DETECT", "body": { "id": 3, "type_event": "Intrusion", "device": { "id": 1001, "category_device": "sensor" } } }""",
            """{ "id": "s4", "m_type": "PUB", "cmd": "DETECT", "body": { "id": 4, "type_event": "Nonsense", "device": { "id": 1001, "category_device": "sensor" } } }""",
            """{ "id": "s5", "m_type": "PUB", "cmd": "MALFUNCTION", "body": "not-an-object" }""",
            """{ "id": "s6", "cmd": "DETECT", "body": { "id": """,
            "[1, 2, \"x\"]",
            "",
            "{" + new string(' ', 512 * 1024) + "}",
            """{ "id": "s7", "m_type": "PUB", "cmd": "UNKNOWN_CMD", "body": {} }""",
        };

        foreach (var json in junk) await rig.PublishRawAsync(json);

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);

        var id = await rig.DetectAsync(1001);   // 뒤 이벤트는 처리된다
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    public async Task should_end_with_queue_state_when_detects_and_reports_interleave_across_threads_resilience()
    {
        // 견딤(운영 전제 밖이 아닌 동시성): NATS 스레드의 탐지와 UI 스레드의 조치가 섞여도 마지막 색 = 큐의 실제 상태.
        using var rig = Rig();
        var symbols = Enumerable.Range(0, 10).Select(i => rig.Sensor(1100 + i, groups: 95)).ToList();
        var zone = rig.Zone(95);
        var ids = Enumerable.Range(0, 10).Select(i => rig.RecordDetection(1100 + i)).ToList();

        var publish = Task.Run(async () => { foreach (var id in ids) await rig.PublishDetectAsync(id); });
        var report = Task.Run(async () =>
        {
            foreach (var id in ids)
            {
                while (rig.Queue.FindEntryByEventId(id, EnumEventType.Intrusion) is null) await Task.Yield();
                rig.RemoteReport(ActionReportKind.Detection, id);
            }
        });
        await Task.WhenAll(publish, report).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.All(symbols, s => Assert.Equal(EnumEventStatus.Normal, s.EventStatus));
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
    }
}
