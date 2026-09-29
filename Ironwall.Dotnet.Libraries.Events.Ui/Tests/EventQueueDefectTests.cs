using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 이벤트 큐(EQM) · NATS 수신 결함(WP-1, 2026-09-30 감사) 회귀 시험.
                  ⑥ 자동 조치보고 스위치는 종류마다 따로다 — 탐지 스위치를 끄면 장애 자동 조치보고까지 멈췄다
                  ⑦ 같은 봉투(id)가 두 번 오면 큐에 한 번만 넣는다
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventQueueDefectTests
{
    #region - ⑥ 종류별 자동 조치보고 스위치 -

    private static EventEntry Expired(EnumEventType type, int eventId) => new()
    {
        DeviceId = eventId,
        DeviceType = EnumDeviceType.Fence,
        EventType = type,
        EventId = eventId,
        TimeoutSeconds = 0,
        IsAutoReportEnabled = true,
    };

    [Fact]
    public void should_auto_report_faults_when_only_the_detection_switch_is_off()
    {
        var setup = new Mock<IEventSetupModel>();
        setup.SetupGet(s => s.IsAutoEventDiscard).Returns(false);             // 탐지 이벤트 해제 끔
        setup.SetupGet(s => s.IsMalfunctionAutoEventDiscard).Returns(true);   // 장애 이벤트 해제 켬
        var queue = new EventQueueManager(null, setup.Object);
        var fired = new List<EventEntry>();
        queue.OnAutoReport += fired.Add;
        queue.Enqueue(Expired(EnumEventType.Intrusion, 1));
        queue.Enqueue(Expired(EnumEventType.Fault, 2));

        queue.OnSharedTimerTick();

        var entry = Assert.Single(fired);
        Assert.Equal(EnumEventType.Fault, entry.EventType);
    }

    [Fact]
    public void should_auto_report_detections_when_only_the_malfunction_switch_is_off()
    {
        var setup = new Mock<IEventSetupModel>();
        setup.SetupGet(s => s.IsAutoEventDiscard).Returns(true);
        setup.SetupGet(s => s.IsMalfunctionAutoEventDiscard).Returns(false);
        var queue = new EventQueueManager(null, setup.Object);
        var fired = new List<EventEntry>();
        queue.OnAutoReport += fired.Add;
        queue.Enqueue(Expired(EnumEventType.Intrusion, 1));
        queue.Enqueue(Expired(EnumEventType.Fault, 2));

        queue.OnSharedTimerTick();

        Assert.Equal(EnumEventType.Intrusion, Assert.Single(fired).EventType);
    }

    #endregion

    #region - ⑦ 같은 봉투 두 번 → 큐에 한 번 -

    private const string DetectEnvelope = """
    {"id":"env-dup-1","m_type":"PUB","cmd":"DETECT","from":"PidsProxy",
     "body":{"id":41,"type_event":"Intrusion","device_id":5,"device":{"id":5,"type_device":"Fence","device_groups":[{"id":1}]},"result":"PIR_SENSOR"}}
    """;

    private const string MalfunctionEnvelope = """
    {"id":"env-dup-2","m_type":"PUB","cmd":"MALFUNCTION","from":"PidsProxy",
     "body":{"id":42,"type_event":"Fault","reason":"FAULT_CABLE_CUTTING","device_id":5,"device":{"id":5,"type_device":"Fence","device_groups":[{"id":1}]}}}
    """;

    private static (Func<MessageArgsModel, Task> Handler, Mock<IEventQueueManager> Queue) Subscribe(Func<INatsService, ISymbolEventManager, IEventQueueManager, IService> create)
    {
        Func<MessageArgsModel, Task>? handler = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handler = h);
        var queue = new Mock<IEventQueueManager>();
        queue.Setup(q => q.Enqueue(It.IsAny<EventEntry>(), It.IsAny<string?>())).Returns<EventEntry, string?>((_, id) => id ?? "x");
        var service = create(nats.Object, new Mock<ISymbolEventManager>().Object, queue.Object);
        service.ExecuteAsync().GetAwaiter().GetResult();
        return (handler!, queue);
    }

    [Fact]
    public async Task should_enqueue_a_detection_once_when_the_same_envelope_arrives_twice()
    {
        var (handler, queue) = Subscribe((n, s, q) => new DetectionNatsSyncService(null, n, s, q, new Mock<IEventSetupModel>().Object));
        var args = new MessageArgsModel("sensorway.unit001.all.event.detect", null, DetectEnvelope);

        await handler(args);
        await handler(args);

        queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), "env-dup-1"), Times.Once);
    }

    [Fact]
    public async Task should_enqueue_a_malfunction_once_when_the_same_envelope_arrives_twice()
    {
        var (handler, queue) = Subscribe((n, s, q) => new MalfunctionNatsSyncService(null, n, s, q, new Mock<IEventSetupModel>().Object));
        var args = new MessageArgsModel("sensorway.unit001.all.event.malfunction", null, MalfunctionEnvelope);

        await handler(args);
        await handler(args);

        queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), "env-dup-2"), Times.Once);
    }

    #endregion

    #region - ⑰ 배열 봉투 · ⑱ 캐시 미스 센서 -

    [Fact]
    public async Task should_enqueue_every_item_when_detections_arrive_as_an_array_envelope()
    {
        var (handler, queue) = Subscribe((n, s, q) => new DetectionNatsSyncService(null, n, s, q, new Mock<IEventSetupModel>().Object));
        var second = DetectEnvelope.Replace("env-dup-1", "env-arr-2").Replace("\"id\":41", "\"id\":43");
        var array = $"[{DetectEnvelope.Replace("env-dup-1", "env-arr-1")}, 17, {second}]";   // 객체가 아닌 항목은 건너뛴다

        await handler(new MessageArgsModel("sensorway.unit001.all.event.detect", null, array));

        queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), "env-arr-1"), Times.Once);
        queue.Verify(q => q.Enqueue(It.IsAny<EventEntry>(), "env-arr-2"), Times.Once);
    }

    [Fact]
    public async Task should_enqueue_a_sensor_detection_as_type_none_when_the_device_cache_misses()
    {
        var (handler, queue) = Subscribe((n, s, q) => new DetectionNatsSyncService(null, n, s, q, new Mock<IEventSetupModel>().Object));
        const string v7 = """
        {"id":"env-miss","m_type":"PUB","cmd":"DETECT","from":"PidsProxy",
         "body":{"id":57,"category_event":"detection","type_event":"Intrusion","device":{"id":777,"category_device":"sensor"}}}
        """;

        await handler(new MessageArgsModel("sensorway.unit001.all.event.detect", null, v7));

        queue.Verify(q => q.Enqueue(It.Is<EventEntry>(e => e.DeviceId == 777 && e.DeviceType == EnumDeviceType.NONE && e.EventId == 57), "env-miss"), Times.Once);
    }

    [Fact]
    public async Task should_enqueue_a_sensor_malfunction_as_type_none_when_the_device_cache_misses()
    {
        var (handler, queue) = Subscribe((n, s, q) => new MalfunctionNatsSyncService(null, n, s, q, new Mock<IEventSetupModel>().Object));
        const string v7 = """
        {"id":"env-miss-m","m_type":"PUB","cmd":"MALFUNCTION","from":"PidsProxy",
         "body":{"id":58,"category_event":"malfunction","type_event":"Fault","reason":"FAULT_CABLE_CUTTING","device":{"id":778,"category_device":"sensor"}}}
        """;

        await handler(new MessageArgsModel("sensorway.unit001.all.event.malfunction", null, v7));

        queue.Verify(q => q.Enqueue(It.Is<EventEntry>(e => e.DeviceId == 778 && e.DeviceType == EnumDeviceType.NONE && e.EventType == EnumEventType.Fault), "env-miss-m"), Times.Once);
    }

    #endregion

    #region - ⑨ 알람 소리 — 한 주인(SoundAlarmController), 조치보고가 그 소리를 끈다 -

    private sealed class SoundProbe
    {
        public List<EnumEventType> Plays { get; } = new();
        public int Stops { get; private set; }
        public SoundAlarmController Create() => new(t => Plays.Add(t), durationSeconds: 20, typeSwitchThrottleMs: 0, stopAll: () => Stops++);
    }

    [Fact]
    public void should_stop_the_alarm_when_the_last_active_event_is_reported()
    {
        var probe = new SoundProbe();
        var sac = probe.Create();
        sac.OnEventArrived(EnumEventType.Intrusion);

        sac.OnActiveCountsChanged(detection: 0, fault: 0);

        Assert.Equal(1, probe.Stops);
        Assert.Equal(SoundAlarmState.Idle, sac.State);
    }

    [Fact]
    public void should_switch_to_the_fault_sound_when_detections_are_cleared_but_faults_remain()
    {
        var probe = new SoundProbe();
        var sac = probe.Create();
        sac.OnEventArrived(EnumEventType.Intrusion);

        sac.OnActiveCountsChanged(detection: 0, fault: 2);

        Assert.Equal(new[] { EnumEventType.Intrusion, EnumEventType.Fault }, probe.Plays);
        Assert.Equal(0, probe.Stops);
    }

    [Fact]
    public void should_keep_playing_while_events_of_the_playing_kind_remain()
    {
        var probe = new SoundProbe();
        var sac = probe.Create();
        sac.OnEventArrived(EnumEventType.Fault);

        sac.OnActiveCountsChanged(detection: 3, fault: 1);

        Assert.Single(probe.Plays);
        Assert.Equal(0, probe.Stops);
    }

    [Fact]
    public void should_play_once_per_event_and_stop_on_report_when_wired_to_the_queue()
    {
        // 모듈 배선 그대로 — 큐에 한 건 → 소리 한 번(주인 하나), 그 한 건 조치 → 멈춤.
        var probe = new SoundProbe();
        var sac = probe.Create();
        var sound = new Mock<Ironwall.Dotnet.Libraries.Sounds.Services.ISoundService>();
        var queue = new EventQueueManager();
        Ironwall.Dotnet.Libraries.Events.Ui.Modules.EventUiModule.WireSoundAlarm(queue, sac, sound.Object);

        var entryId = queue.Enqueue(new EventEntry { DeviceId = 1, DeviceType = EnumDeviceType.Fence, EventType = EnumEventType.ContactOn, EventId = 1 });
        Assert.Equal(new[] { EnumEventType.Intrusion }, probe.Plays);          // 접점도 탐지 소리

        queue.Dequeue(entryId);
        Assert.Equal(1, probe.Stops);
    }

    #endregion

    #region - ⑤ 자동복구 로그인 게이트 -

    /// <summary>
    /// 자동복구 배선 — 제어기 블랙아웃 장애 카드를 <b>목록에 실제로 걸어 둔</b> 상태(엔트리 ↔ 카드 매칭까지)에서 자동복구를 일으킨다.
    /// 카드가 없으면 HandleAutoRecoveryAsync 가 게이트와 상관없이 "카드 없음" 으로 먼저 돌아가 시험이 늘 통과한다(Loop A, WP-7).
    /// </summary>
    private static (EventQueueManager Queue, Mock<Ironwall.Dotnet.Libraries.Events.Api.Services.IEventApiService> Api) WireRecoveryWithRegisteredCard(bool isAuthenticated)
    {
        var api = new Mock<Ironwall.Dotnet.Libraries.Events.Api.Services.IEventApiService>();
        api.Setup(a => a.CreateActionEventAsync(It.IsAny<Ironwall.Dotnet.Libraries.Messages.Dto.Events.ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiResponse<Ironwall.Dotnet.Libraries.Messages.Dto.Events.ActionEventDto> { Success = false, Message = "시험 — 보내지 않는다" });
        var panel = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels.EventCardListPanelViewModel(
            new Mock<Caliburn.Micro.IEventAggregator>().Object, new Mock<ILogService>().Object, null!,
            new Mock<Ironwall.Dotnet.Monitoring.Models.Accounts.IAccountModel>().Object, api.Object,
            new Mock<ISymbolEventManager>().Object, new Mock<IEventQueueManager>().Object, new ActionReportGuard());
        var token = new Mock<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>();
        token.SetupGet(t => t.IsAuthenticated).Returns(isAuthenticated);
        var queue = new EventQueueManager();
        Ironwall.Dotnet.Libraries.Events.Ui.Modules.EventUiModule.WireAutoActions(queue, panel, token.Object, null);

        var entryId = queue.Enqueue(new EventEntry { DeviceId = 9, DeviceType = EnumDeviceType.Controller, EventType = EnumEventType.Fault, IsControllerBlackout = true, EventId = 90 });
        var model = new Mock<Ironwall.Dotnet.Monitoring.Models.Events.IMalfunctionEventModel>();
        model.SetupGet(m => m.Id).Returns(90);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Fault);
        var card = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events.MalfunctionEventCardViewModel(model.Object);
        panel.ViewModelProvider.Add(card);
        panel.HandleAsync(new EventEntryEnqueuedMessage(entryId, 90, 9, EnumDeviceType.Controller, EnumEventType.Fault), CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(entryId, card.EntryId);   // 카드가 엔트리에 걸렸다 — 이제 게이트만이 발송을 막는다
        return (queue, api);
    }

    [Fact]
    public void should_not_send_the_auto_recovery_report_when_logged_out()
    {
        var (queue, api) = WireRecoveryWithRegisteredCard(isAuthenticated: false);

        Assert.True(queue.TryAutoRecoverController(9));

        api.Verify(a => a.CreateActionEventAsync(It.IsAny<Ironwall.Dotnet.Libraries.Messages.Dto.Events.ActionEventCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void should_send_the_auto_recovery_report_when_logged_in_and_the_card_is_registered()
    {
        // 위 시험의 대조군 — 같은 배선에서 로그인 상태면 조치보고 API 가 실제로 불린다(시험이 게이트를 재고 있다는 증거).
        var (queue, api) = WireRecoveryWithRegisteredCard(isAuthenticated: true);

        Assert.True(queue.TryAutoRecoverController(9));

        api.Verify(a => a.CreateActionEventAsync(It.Is<Ironwall.Dotnet.Libraries.Messages.Dto.Events.ActionEventCreateDto>(d => d.FromEventId == 90), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region - 도우미 규칙 -

    [Theory]
    [InlineData("detection", null, "detection")]
    [InlineData("MALFUNCTION", "Intrusion", "malfunction")]
    [InlineData(null, "Fault", "malfunction")]
    [InlineData(null, "ContactOn", "detection")]
    [InlineData(null, "Action", null)]
    [InlineData(null, null, null)]
    public void should_read_the_origin_kind_from_category_event_then_type_event(string? category, string? type, string? expected)
        => Assert.Equal(expected, Ironwall.Dotnet.Libraries.Events.Ui.Helpers.EventCardKind.FromActionReport(category, type));

    [Fact]
    public void should_format_the_reporter_as_username_and_employee_number()
    {
        var account = new Mock<Ironwall.Dotnet.Monitoring.Models.Accounts.IAccountModel>();
        account.SetupGet(a => a.Username).Returns("kim");
        account.SetupGet(a => a.EmployeeNumber).Returns("1234");
        account.SetupGet(a => a.Name).Returns("홍길동");
        Assert.Equal("kim(1234)", Ironwall.Dotnet.Libraries.Events.Ui.Helpers.ActionReportRules.FormatActor(account.Object));

        account.SetupGet(a => a.EmployeeNumber).Returns((string?)null);
        Assert.Equal("kim", Ironwall.Dotnet.Libraries.Events.Ui.Helpers.ActionReportRules.FormatActor(account.Object));
        Assert.Equal("SYSTEM", Ironwall.Dotnet.Libraries.Events.Ui.Helpers.ActionReportRules.FormatActor(null));
    }

    #endregion
}
