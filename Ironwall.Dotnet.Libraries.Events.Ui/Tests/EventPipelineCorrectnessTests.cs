using Xunit;
using Moq;
using Caliburn.Micro;
using System.Windows.Threading;
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
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : WP-8 — 라이브러리 이벤트 파이프라인 정합성(Loop A CORRECTNESS) 결함 H1 · H2 · M3 · M4 · M6 · L7 재현 · 회귀
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// H1 · M4 — 활성 건수(소리 · 신호등의 근거)가 큐에 들어간 이벤트와 어긋나지 않는다.
/// </summary>
public class EventQueueActiveCountCorrectnessTests
{
    private sealed class SoundProbe
    {
        public readonly List<EnumEventType> Plays = new();
        public int Stops;
        public SoundAlarmController Create()
            => new(t => Plays.Add(t), durationSeconds: 20, typeSwitchThrottleMs: 0, stopAll: () => Stops++);
    }

    private static EventEntry Entry(int deviceId, EnumDeviceType type, EnumEventType evt, int eventId)
        => new() { DeviceId = deviceId, DeviceType = type, EventType = evt, EventId = eventId, TimeoutSeconds = 3600 };

    private static (EventQueueManager Queue, SoundProbe Probe, SoundAlarmController Sac) WiredQueue()
    {
        var probe = new SoundProbe();
        var sac = probe.Create();
        var queue = new EventQueueManager();
        Ironwall.Dotnet.Libraries.Events.Ui.Modules.EventUiModule.WireSoundAlarm(
            queue, sac, new Mock<Ironwall.Dotnet.Libraries.Sounds.Services.ISoundService>().Object);
        return (queue, probe, sac);
    }

    [Theory]
    [InlineData(EnumEventType.ContactOn)]
    [InlineData(EnumEventType.ContactOff)]
    [InlineData(EnumEventType.Alert)]
    public void should_count_every_sounding_detection_kind_when_enqueued(EnumEventType kind)
    {
        var queue = new EventQueueManager();
        queue.Enqueue(Entry(6, EnumDeviceType.Contact, kind, 1));

        Assert.Equal((1, 0), queue.GetActiveCounts());
    }

    [Theory]
    [InlineData(EnumEventType.ContactOn)]
    [InlineData(EnumEventType.Alert)]
    public void should_keep_detection_sound_playing_when_contact_or_alert_enqueued(EnumEventType kind)
    {
        var (queue, probe, sac) = WiredQueue();

        queue.Enqueue(Entry(6, EnumDeviceType.Contact, kind, 1));

        Assert.Equal(new[] { EnumEventType.Intrusion }, probe.Plays);   // 탐지 소리로 울리고
        Assert.Equal(0, probe.Stops);                                     // 곧바로 꺼지지 않는다
        Assert.Equal(SoundAlarmState.Playing, sac.State);
    }

    [Fact]
    public void should_keep_sound_when_intrusion_reported_while_contact_still_active()
    {
        var (queue, probe, sac) = WiredQueue();
        queue.Enqueue(Entry(6, EnumDeviceType.Contact, EnumEventType.ContactOn, 1));
        var intrusion = queue.Enqueue(Entry(3, EnumDeviceType.Fence, EnumEventType.Intrusion, 2));

        queue.Dequeue(intrusion);   // 침입만 조치 — 접점은 아직 미조치

        Assert.Equal(0, probe.Stops);
        Assert.Equal(SoundAlarmState.Playing, sac.State);
    }

    [Fact]
    public void should_stop_sound_when_last_contact_reported()
    {
        var (queue, probe, _) = WiredQueue();
        var contact = queue.Enqueue(Entry(6, EnumDeviceType.Contact, EnumEventType.ContactOn, 1));

        queue.Dequeue(contact);

        Assert.Equal(1, probe.Stops);
    }

    [Fact]
    public void should_deliver_latest_counts_last_when_another_thread_mutates_during_delivery()
    {
        // M4: A(UI 스레드) Dequeue 가 (0,0) 을 계산하고 전달하는 사이 B(NATS) 가 장애를 넣고 (0,1) 을 전달한다.
        //     종전엔 A 의 늦은 (0,0) 이 마지막에 도착해 활성 장애가 있는데 소리가 꺼졌다.
        var queue = new EventQueueManager();
        var first = queue.Enqueue(Entry(3, EnumDeviceType.Fence, EnumEventType.Intrusion, 1));

        var interleaved = false;
        var last = (-1, -1);
        queue.OnActiveCountChanged += (d, f) =>
        {
            if (interleaved || (d, f) != (0, 0)) return;
            interleaved = true;
            var nats = new Thread(() => queue.Enqueue(Entry(9, EnumDeviceType.Controller, EnumEventType.Fault, 7)));
            nats.Start();
            Assert.True(nats.Join(TimeSpan.FromSeconds(5)), "다른 스레드의 Enqueue 가 전달 중에 막히면 안 된다(교착)");
        };
        queue.OnActiveCountChanged += (d, f) => last = (d, f);

        queue.Dequeue(first);

        Assert.True(interleaved);
        Assert.Equal(queue.GetActiveCounts(), last);   // 마지막으로 받은 값 = 큐의 실제 값
        Assert.Equal((0, 1), last);
    }
}

/// <summary>
/// H2 · L7 — 심볼 색은 (Id, 종류) 로 정해지고, 큐 읽기와 칠하기 사이에 끼어든 전이가 옛 값으로 덮이지 않는다.
/// </summary>
public class SymbolPaintCorrectnessTests
{
    private readonly Mock<ILogService> _log = new();

    private SymbolEventManager CreateManager(IEventQueueManager? queue)
        => new(new Mock<IEventAggregator>().Object, _log.Object, new EventSetupModel(new Mock<IEventSetupModel>().Object), queue);

    private static BaseDeviceModel Device(int id, EnumDeviceType type)
        => new() { Id = id, DeviceType = type, Status = EnumDeviceStatus.ACTIVATED };

    private static PidsSymbolModel Symbol(EnumDeviceType type) => new() { Title = $"{type}", DeviceType = type };

    private static EventEntry Entry(int deviceId, EnumDeviceType type, EnumEventType evt)
        => new() { DeviceId = deviceId, DeviceType = type, EventType = evt, TimeoutSeconds = 3600 };

    [Fact]
    public void should_not_paint_same_id_symbol_of_other_type_when_none_type_entry_transitions()
    {
        // H2: 캐시 미스 센서(7, NONE) 가 카메라 7 을 칠하고, 그 조치(Dequeue)가 카메라의 진짜 장애색을 지웠다.
        var queue = new EventQueueManager();
        var sem = CreateManager(queue);
        queue.OnDeviceStateChanged += sem.HandleDeviceStateChanged;
        var camera = Symbol(EnumDeviceType.IpCamera);
        sem.RegisterDeviceSymbol(Device(7, EnumDeviceType.IpCamera), camera);
        queue.Enqueue(Entry(7, EnumDeviceType.IpCamera, EnumEventType.Fault));
        Assert.Equal(EnumCompositeEventStatus.Faulted, camera.CompositeStatus);

        var none = queue.Enqueue(Entry(7, EnumDeviceType.NONE, EnumEventType.Intrusion));
        Assert.Equal(EnumCompositeEventStatus.Faulted, camera.CompositeStatus);

        queue.Dequeue(none);
        Assert.Equal(EnumCompositeEventStatus.Faulted, camera.CompositeStatus);
    }

    [Theory]
    [InlineData(EnumDeviceType.IpCamera)]
    [InlineData(EnumDeviceType.Underground)]
    public void should_not_paint_sibling_when_unregistered_device_leftover_entry_transitions(EnumDeviceType siblingType)
    {
        // H2: 해제된 장비의 Id 보조 색인이 같은 Id 의 다른 장비로 옮겨가, 지워진 장비의 남은 엔트리가 그 장비를 칠했다.
        var queue = new EventQueueManager();
        var sem = CreateManager(queue);
        queue.OnDeviceStateChanged += sem.HandleDeviceStateChanged;
        var sibling = Symbol(siblingType);
        sem.RegisterDeviceSymbol(Device(7, siblingType), sibling);
        sem.RegisterDeviceSymbol(Device(7, EnumDeviceType.Fence), Symbol(EnumDeviceType.Fence));
        queue.Enqueue(Entry(7, siblingType, EnumEventType.Fault));
        var leftover = queue.Enqueue(Entry(7, EnumDeviceType.Fence, EnumEventType.Intrusion));

        Assert.True(sem.UnregisterDeviceSymbol(7, EnumDeviceType.Fence));
        queue.Dequeue(leftover);                                                // 지워진 장비의 늦은 전이
        queue.Enqueue(Entry(7, EnumDeviceType.Fence, EnumEventType.Intrusion)); // 지워진 장비로 온 새 이벤트

        Assert.Equal(EnumCompositeEventStatus.Faulted, sibling.CompositeStatus);
    }

    [Fact]
    public void should_still_follow_single_same_category_symbol_when_type_drifted()
    {
        // 남기는 폴백(FR-B2): 같은 Id 가 하나뿐이고 같은 계열(센서) 안에서 종류만 바뀐 재등록은 따라간다.
        var queue = new EventQueueManager();
        var sem = CreateManager(queue);
        queue.OnDeviceStateChanged += sem.HandleDeviceStateChanged;
        var symbol = Symbol(EnumDeviceType.SmartSensor);
        sem.RegisterDeviceSymbol(Device(12, EnumDeviceType.SmartSensor), symbol);

        queue.Enqueue(Entry(12, EnumDeviceType.SmartSensor2, EnumEventType.Intrusion));

        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.CompositeStatus);
    }

    [Fact]
    public void should_not_follow_id_when_two_symbols_share_it_and_type_misses()
    {
        var queue = new EventQueueManager();
        var sem = CreateManager(queue);
        queue.OnDeviceStateChanged += sem.HandleDeviceStateChanged;
        var fence = Symbol(EnumDeviceType.Fence);
        var pir = Symbol(EnumDeviceType.PIR);
        sem.RegisterDeviceSymbol(Device(4, EnumDeviceType.Fence), fence);
        sem.RegisterDeviceSymbol(Device(4, EnumDeviceType.PIR), pir);

        queue.Enqueue(Entry(4, EnumDeviceType.Multi, EnumEventType.Intrusion));   // 어느 쪽인지 정할 수 없다

        Assert.Equal(EnumCompositeEventStatus.Normal, fence.CompositeStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, pir.CompositeStatus);
    }

    [Fact]
    public void should_end_with_queue_state_when_transition_lands_between_refresh_read_and_paint()
    {
        // L7: Refresh 가 큐를 읽은(Normal) 뒤 칠하기 전에 NATS 전이(Detecting)가 먼저 칠하면, Refresh 의 옛 값이 마지막에 덮었다.
        var state = EnumCompositeEventStatus.Normal;
        Thread? nats = null;
        SymbolEventManager? sem = null;
        var queue = new Mock<IEventQueueManager>();
        queue.Setup(q => q.GetDeviceState(5, EnumDeviceType.Fence)).Returns(() =>
        {
            var read = state;
            if (nats == null)
            {
                state = EnumCompositeEventStatus.Detecting;   // 읽은 직후 큐가 바뀌고 그 전이가 다른 스레드에서 온다
                nats = new Thread(() => sem!.HandleDeviceStateChanged(5, EnumDeviceType.Fence, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Detecting));
                nats.Start();
                nats.Join(TimeSpan.FromMilliseconds(300));   // 막히지 않으면 먼저 칠한다(종전 순서 재현)
            }
            return read;
        });
        sem = CreateManager(queue.Object);
        var symbol = Symbol(EnumDeviceType.Fence);
        sem.RegisterDeviceSymbol(new BaseDeviceModel { Id = 5, DeviceType = EnumDeviceType.Fence, Status = EnumDeviceStatus.ERROR }, symbol);

        sem.RefreshDeviceSymbol(5, EnumDeviceType.Fence);
        Assert.True(nats!.Join(TimeSpan.FromSeconds(5)));

        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.CompositeStatus);
    }
}

/// <summary>
/// M3 · M6 — 이미 조치된 카드는 목록에 뒤늦게 오르지 않고, 두 번 조치보고되지 않는다.
/// </summary>
[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적 — 다른 IoC 스텁 시험과 병렬로 돌리면 서로 덮는다
public class EventCardListCorrectnessTests
{
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IEventApiService> _api = new();
    private readonly Mock<IEventQueueManager> _queue = new();
    private static readonly EventSetupModel SetupModel = new(new Mock<IEventSetupModel>().Object);

    public EventCardListCorrectnessTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return _ea.Object;
            if (type == typeof(ILogService)) return _log.Object;
            if (type == typeof(EventSetupModel)) return SetupModel;
            return null!;
        };
        _ea.Setup(ea => ea.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
    }

    private EventCardListPanelViewModel CreateSut()
        => new(_ea.Object, _log.Object, null!, new Mock<IAccountModel>().Object, _api.Object,
               new Mock<ISymbolEventManager>().Object, _queue.Object, new ActionReportGuard());

    private static DetectionEventCardViewModel Card(int eventId)
    {
        var model = new Mock<IDetectionEventModel>();
        model.Setup(m => m.Id).Returns(eventId);
        model.Setup(m => m.MessageType).Returns(EnumEventType.Intrusion);
        return new DetectionEventCardViewModel(model.Object);
    }

    [Fact]
    public async Task should_not_insert_card_when_remote_report_is_processed_before_background_insert()
    {
        // M3: 버퍼 검사(타이머 스레드) 뒤, Background 삽입보다 Normal 우선순위의 원격 ACTION_REPORT 가 먼저 처리됐다 —
        //     카드가 없어 엔트리만 빼고 보류 EntryId 를 지운 뒤, 카드가 뒤늦게 올라와 영영 남았다(자동 조치보고도 없음).
        var sut = CreateSut();
        _queue.Setup(q => q.FindEntryByEventId(5, EnumEventType.Intrusion)).Returns(new EventEntry { EntryId = "e5", EventId = 5, EventType = EnumEventType.Intrusion });
        await sut.HandleAsync(new EventEntryEnqueuedMessage("e5", 5, 3, EnumDeviceType.Fence, EnumEventType.Intrusion), CancellationToken.None);
        var card = Card(5);
        sut.EnqueueCard(card);

        var remoteFirst = false;
        sut.DispatchToUi = (action, _) =>
        {
            if (!remoteFirst)
            {
                remoteFirst = true;
                sut.CloseByRemoteActionReport(ActionReportKind.Detection, 5, EnumEventType.Intrusion);
            }
            action();
            return Task.CompletedTask;
        };

        await sut.FlushPendingCardsNowAsync();

        Assert.True(remoteFirst);
        Assert.Empty(sut.ViewModelProvider);
        _queue.Verify(q => q.Dequeue("e5"), Times.Once);
    }

    [Fact]
    public async Task should_skip_card_closed_meanwhile_when_batch_report_runs()
    {
        // M6: 전체 조치보고는 목록을 한 번 베껴 돈다 — 그 사이 원격 · 자동으로 닫힌 카드를 다시 보고했다(서버 조치 중복 + ACTION_REPORT 중복).
        var sut = CreateSut();
        var first = Card(1);
        var second = Card(2);
        sut.ViewModelProvider.Add(first);
        sut.ViewModelProvider.Add(second);
        _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns((ActionEventCreateDto dto, CancellationToken _) =>
            {
                if (dto.FromEventId == 1)
                    sut.CloseByRemoteActionReport(ActionReportKind.Detection, 2, EnumEventType.Intrusion);   // 보고하는 사이 다른 GIS 가 2 를 조치
                return Task.FromResult(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto() });
            });

        await sut.ExecuteBatchReportAsync();

        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == 2), It.IsAny<CancellationToken>()), Times.Never);
        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == 1), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(sut.ViewModelProvider);
    }
}
