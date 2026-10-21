using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : G45 · G46 — SYNC_MALFUNCTION · SYNC_ACTION_REPORT · SYNC_OPERATION_EVENT 루프백 봉투 주입 시험.
                  서비스: 자가 필터 · 봉투 id 중복 거르기 · 큐/카드 게이트 · 재조회 · 404=삭제 · 메시지 발행
                  카드 목록: 장애 카드 종류+Id 닫기(탐지 같은 번호 보호) · 조치됨 닫기 · 운영 카드 심각도 갱신 · 운영 카드 닫기
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventSyncNatsServiceTests
{
    private Func<MessageArgsModel, Task>? _handler;
    private readonly List<object> _published = new();
    private readonly Mock<IEventQueueManager> _queue = new();
    private readonly Mock<IEventApiService> _api = new();
    private Func<int, bool>? _hasOperationCard;

    private async Task<EventSyncNatsService> StartAsync()
    {
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handler = h);
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => _published.Add(m))
          .Returns(Task.CompletedTask);
        var service = new EventSyncNatsService(null, nats.Object, _queue.Object, _api.Object, ea.Object, null, _hasOperationCard);
        await service.StartService();
        return service;
    }

    private static MessageArgsModel Envelope(string cmd, string action, int resourceId, string id = "env-1", string? mType = null)
        => new("sensorway.unit001.all.sync.x", "sensorway.unit001.all.>", $$"""
        { "id": "{{id}}", "cmd": "{{cmd}}", {{(mType is null ? "" : $"\"m_type\": \"{mType}\",")}} "from": "DBApi",
          "body": { "action": "{{action}}", "resource_id": {{resourceId}} } }
        """);

    private async Task Inject(EventSyncNatsService service, MessageArgsModel message)
    {
        await _handler!(message);
        if (service.LastProcessingTask is { } t) await t;
    }

    private void QueueFault(int id)
        => _queue.Setup(q => q.FindEntryByEventId(id, EnumEventType.Fault)).Returns(new EventEntry { EventId = id, EventType = EnumEventType.Fault });

    // ── SYNC_MALFUNCTION ──────────────────────────────────────────────

    [Fact]
    public async Task 장애_DELETED_는_재조회_없이_닫기_메시지()
    {
        var service = await StartAsync();
        await Inject(service, Envelope("SYNC_MALFUNCTION", "DELETED", 2001));

        Assert.Contains(new MalfunctionDeletedOnServerMessage(2001), _published);
        _api.Verify(a => a.GetMalfunctionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task 장애_UPDATED_는_큐에_있을_때만_다시_읽는다()
    {
        var service = await StartAsync();
        await Inject(service, Envelope("SYNC_MALFUNCTION", "UPDATED", 2001));

        _api.Verify(a => a.GetMalfunctionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_published);
    }

    [Fact]
    public async Task 장애_UPDATED_재조회는_조치_상태와_재분류를_싣는다()
    {
        QueueFault(2001);
        _api.Setup(a => a.GetMalfunctionEventByIdAsync(2001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<MalfunctionEventDto> { Success = true, StatusCode = 200, Data = new MalfunctionEventDto { Id = 2001, TypeEvent = "Fault", ActionReported = "True" } });
        var service = await StartAsync();

        await Inject(service, Envelope("SYNC_MALFUNCTION", "UPDATED", 2001));

        Assert.Contains(new MalfunctionSyncedMessage(2001, true, "Fault"), _published);
    }

    [Fact]
    public async Task 장애_UPDATED_재조회_404_는_삭제로_본다()
    {
        QueueFault(2001);
        _api.Setup(a => a.GetMalfunctionEventByIdAsync(2001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<MalfunctionEventDto> { Success = false, StatusCode = 404 });
        var service = await StartAsync();

        await Inject(service, Envelope("SYNC_MALFUNCTION", "UPDATED", 2001));

        Assert.Contains(new MalfunctionDeletedOnServerMessage(2001), _published);
    }

    [Fact]
    public async Task 같은_봉투_id_는_한_번만_처리한다()
    {
        var service = await StartAsync();
        await Inject(service, Envelope("SYNC_MALFUNCTION", "DELETED", 2001, id: "dup"));
        await Inject(service, Envelope("SYNC_MALFUNCTION", "DELETED", 2001, id: "dup"));

        Assert.Single(_published.OfType<MalfunctionDeletedOnServerMessage>());
    }

    [Theory]
    [InlineData("sync_malfunction", null)]   // 소문자 — 모르는 cmd
    [InlineData("SYNC_MALFUNCTION", "RSP")]  // 응답은 알림이 아니다
    [InlineData("SYNC_DETECTION", null)]     // 다른 서비스 몫
    public async Task 자가_필터는_자기_cmd_알림만_받는다(string cmd, string? mType)
    {
        var service = await StartAsync();
        await Inject(service, Envelope(cmd, "DELETED", 2001, mType: mType));
        Assert.Empty(_published);
    }

    [Fact]
    public async Task 깨진_본문_재조회_예외도_서비스_밖으로_새지_않는다()
    {
        QueueFault(2001);
        _api.Setup(a => a.GetMalfunctionEventByIdAsync(2001, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var service = await StartAsync();

        var ex = await Record.ExceptionAsync(async () =>
        {
            await _handler!(new MessageArgsModel("s", "s", "{ not json"));
            await Inject(service, Envelope("SYNC_MALFUNCTION", "UPDATED", 2001));
        });
        Assert.Null(ex);
    }

    // ── SYNC_ACTION_REPORT ────────────────────────────────────────────

    [Fact]
    public async Task 조치_UPDATED_는_원본을_다시_읽어_조치_상태를_싣는다()
    {
        _api.Setup(a => a.GetActionEventByIdAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 77, FromEvent = new MalfunctionEventDto { Id = 2001 } } });
        _api.Setup(a => a.GetMalfunctionEventByIdAsync(2001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<MalfunctionEventDto> { Success = true, Data = new MalfunctionEventDto { Id = 2001, ActionReported = "False" } });
        var service = await StartAsync();

        await Inject(service, Envelope("SYNC_ACTION_REPORT", "UPDATED", 77));

        Assert.Contains(new ActionReportSyncedMessage(77, false, ActionReportKind.Malfunction, 2001, false), _published);
    }

    [Fact]
    public async Task 조치_DELETED_는_원본_모름으로_알린다()
    {
        var service = await StartAsync();
        await Inject(service, Envelope("SYNC_ACTION_REPORT", "DELETED", 77));

        Assert.Contains(new ActionReportSyncedMessage(77, true, null, null, null), _published);
        _api.Verify(a => a.GetActionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task 원본이_지워진_조치는_원본_모름으로_알린다()
    {
        _api.Setup(a => a.GetActionEventByIdAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 77, FromEvent = null } });
        var service = await StartAsync();

        await Inject(service, Envelope("SYNC_ACTION_REPORT", "UPDATED", 77));

        Assert.Contains(new ActionReportSyncedMessage(77, false, null, null, null), _published);
    }

    // ── SYNC_OPERATION_EVENT ──────────────────────────────────────────

    [Fact]
    public async Task 운영_UPDATED_는_카드가_없으면_다시_읽지_않는다()
    {
        _hasOperationCard = _ => false;
        var service = await StartAsync();

        await Inject(service, Envelope("SYNC_OPERATION_EVENT", "UPDATED", 360));

        _api.Verify(a => a.GetOperationEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_published);
    }

    [Fact]
    public async Task 운영_UPDATED_는_카드가_있으면_다시_읽어_심각도를_싣는다()
    {
        _hasOperationCard = id => id == 360;
        _api.Setup(a => a.GetOperationEventByIdAsync(360, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<OperationEventDto> { Success = true, Data = new OperationEventDto { Id = 360, Reason = "GATE_OPEN", Severity = "CRITICAL" } });
        var service = await StartAsync();

        await Inject(service, Envelope("SYNC_OPERATION_EVENT", "UPDATED", 360));

        var synced = Assert.Single(_published.OfType<OperationEventSyncedMessage>());
        Assert.Equal("CRITICAL", synced.Model.Severity);
        Assert.Empty(_published.OfType<OperationEventCardMessage>());   // 새 이벤트로 취급하지 않는다(팝업 연동 재실행 금지)
    }

    [Theory]
    [InlineData("DELETED", 0)]
    [InlineData("UPDATED", 404)]
    public async Task 운영_DELETED_와_재조회_404_는_닫기_메시지(string action, int status)
    {
        _hasOperationCard = _ => true;
        _api.Setup(a => a.GetOperationEventByIdAsync(360, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<OperationEventDto> { Success = false, StatusCode = status });
        var service = await StartAsync();

        await Inject(service, Envelope("SYNC_OPERATION_EVENT", action, 360));

        Assert.Contains(new OperationEventDeletedOnServerMessage(360), _published);
    }
}

[Collection("IoC-Dependent")]
public class EventSyncCardListTests : IDisposable
{
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IEventApiService> _api = new();
    private readonly Mock<IAccountModel> _account = new();
    private readonly Mock<ISymbolEventManager> _symbols = new();
    private readonly Mock<IEventQueueManager> _queue = new();
    private readonly List<object> _published = new();
    private readonly ActionReportGuard _guard = new();
    private readonly EventSetupModel _setup = new(new Mock<IEventSetupModel>().Object);
    private readonly Func<Type, string, object> _previousGetInstance = IoC.GetInstance;

    public EventSyncCardListTests()
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
    }

    public void Dispose() => IoC.GetInstance = _previousGetInstance;

    private EventCardListPanelViewModel CreatePanel()
        => new(_ea.Object, _log.Object, null!, _account.Object, _api.Object, _symbols.Object, _queue.Object, _guard);

    private static DetectionEventCardViewModel Detection(int id)
    {
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(id);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        model.SetupProperty(m => m.Status);
        return new DetectionEventCardViewModel(model.Object);
    }

    private static MalfunctionEventCardViewModel Malfunction(int id)
    {
        var model = new Mock<IMalfunctionEventModel>();
        model.SetupGet(m => m.Id).Returns(id);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Fault);
        model.SetupProperty(m => m.Status);
        return new MalfunctionEventCardViewModel(model.Object);
    }

    [Fact]
    public async Task 장애_삭제는_같은_번호의_탐지_카드를_건드리지_않고_장애_카드만_닫는다()
    {
        var panel = CreatePanel();
        var detection = Detection(42);
        var fault = Malfunction(42);
        fault.EntryId = "fault-42";
        panel.ViewModelProvider.Add(detection);
        panel.ViewModelProvider.Add(fault);

        await panel.HandleAsync(new MalfunctionDeletedOnServerMessage(42), CancellationToken.None);

        Assert.Same(detection, Assert.Single(panel.ViewModelProvider));
        _queue.Verify(q => q.Dequeue("fault-42"), Times.Once);
        Assert.Contains(new EventDeletedOnServerNotice(ActionReportKind.Malfunction, 42), _published);
    }

    [Fact]
    public async Task 장애_카드가_없어도_큐_엔트리를_뺀다()
    {
        var panel = CreatePanel();
        _queue.Setup(q => q.FindEntryByEventId(42, EnumEventType.Fault))
              .Returns(new EventEntry { EntryId = "fault-42", EventId = 42, EventType = EnumEventType.Fault });

        await panel.HandleAsync(new MalfunctionDeletedOnServerMessage(42), CancellationToken.None);

        _queue.Verify(q => q.Dequeue("fault-42"), Times.Once);
    }

    [Fact]
    public async Task 다른_곳에서_조치된_장애는_원격_조치보고와_같이_닫는다()
    {
        var panel = CreatePanel();
        panel.ViewModelProvider.Add(Malfunction(42));

        await panel.HandleAsync(new MalfunctionSyncedMessage(42, ActionReported: true, "Fault"), CancellationToken.None);

        Assert.Empty(panel.ViewModelProvider);
        Assert.Contains(new RemoteActionReportedMessage(ActionReportKind.Malfunction, 42, DeletedOnServer: false), _published);
    }

    [Fact]
    public async Task 조치_안_된_장애_재조회는_카드를_그대로_둔다()
    {
        var panel = CreatePanel();
        panel.ViewModelProvider.Add(Malfunction(42));

        await panel.HandleAsync(new MalfunctionSyncedMessage(42, ActionReported: false, "Fault"), CancellationToken.None);

        Assert.Single(panel.ViewModelProvider);
    }

    [Fact]
    public async Task 조치_재조회로_원본이_조치됨이면_그_카드를_닫는다()
    {
        var panel = CreatePanel();
        panel.ViewModelProvider.Add(Detection(42));
        panel.ViewModelProvider.Add(Malfunction(42));

        await panel.HandleAsync(new ActionReportSyncedMessage(77, false, ActionReportKind.Malfunction, 42, true), CancellationToken.None);

        Assert.IsType<DetectionEventCardViewModel>(Assert.Single(panel.ViewModelProvider));
    }

    [Fact]
    public async Task 탐지_재분류는_큐_엔트리_종류와_카드_종류를_고친다()
    {
        var panel = CreatePanel();
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(42);
        model.SetupProperty(m => m.MessageType, EnumEventType.Intrusion);
        model.SetupProperty(m => m.Status);
        panel.ViewModelProvider.Add(new DetectionEventCardViewModel(model.Object));
        var entry = new EventEntry { EntryId = "d-42", EventId = 42, EventType = EnumEventType.Intrusion, DeviceId = 7, DeviceType = EnumDeviceType.Fence };
        _queue.Setup(q => q.FindEntryByEventId(42, EnumEventType.Intrusion)).Returns(entry);

        await panel.HandleAsync(new DetectionReclassifiedMessage(42, EnumEventType.Intrusion, EnumEventType.ContactOn), CancellationToken.None);

        Assert.Equal(EnumEventType.ContactOn, entry.EventType);
        Assert.Equal(EnumEventType.ContactOn, model.Object.MessageType);
        _symbols.Verify(s => s.RefreshDeviceSymbol(7, EnumDeviceType.Fence), Times.Once);
    }

    [Fact]
    public async Task 운영_카드는_심각도만_갱신하고_삭제면_닫는다()
    {
        var panel = CreatePanel();
        var card = new OperationEventCardViewModel(new OperationEventModel { Id = 360, Reason = "GATE_OPEN", Severity = "INFO" });
        panel.ViewModelProvider.Add(card);
        Assert.False(card.IsWarning);
        Assert.True(panel.HasOperationCard(360));
        Assert.False(panel.HasOperationCard(361));

        await panel.HandleAsync(new OperationEventSyncedMessage(new OperationEventModel { Id = 360, Reason = "GATE_OPEN", Severity = "CRITICAL" }), CancellationToken.None);
        Assert.True(card.IsWarning);
        Assert.Single(panel.ViewModelProvider);

        await panel.HandleAsync(new OperationEventDeletedOnServerMessage(360), CancellationToken.None);
        Assert.Empty(panel.ViewModelProvider);
    }
}
