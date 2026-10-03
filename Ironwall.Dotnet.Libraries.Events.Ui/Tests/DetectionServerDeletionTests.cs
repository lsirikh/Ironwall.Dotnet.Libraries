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
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : SYNC_DETECTION{DELETED} — 서버에서 지워진 탐지를 GIS 가 닫는다.
                  서비스: 카드 닫기 메시지 발행(재조회 없음, 진행 중 UPDATED 응답 폐기)
                  카드 목록: 카드 · 큐 엔트리 해제(신호등 건수 감소) + "서버에서 삭제됨" 토스트 알림 + 열린 창 [확인] 끔
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class DetectionSyncDeletedServiceTests
{
    private const int DetectionId = 42;
    private Func<MessageArgsModel, Task>? _handler;
    private readonly List<object> _published = new();

    private DetectionSyncNatsService CreateService(Mock<IEventQueueManager> queue, Mock<IEventApiService> api)
    {
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handler = h);
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => _published.Add(m))
          .Returns(Task.CompletedTask);
        return new DetectionSyncNatsService(null, nats.Object, queue.Object, api.Object, ea.Object);
    }

    private static MessageArgsModel Sync(string action, int resourceId = DetectionId)
        => new(subject: "sensorway.unit001.all.sync.detection", subscriptionSubject: "sensorway.unit001.all.>", data: $$"""
        { "id": "sync-uuid", "cmd": "SYNC_DETECTION", "from": "DBApi", "body": { "action": "{{action}}", "resource_id": {{resourceId}} } }
        """);

    [Theory]
    [InlineData(EnumEventType.Intrusion)]
    [InlineData(EnumEventType.ContactOn)]
    public async Task should_publish_close_request_with_queued_type_when_deleted(EnumEventType queuedType)
    {
        var queue = new Mock<IEventQueueManager>();
        queue.Setup(q => q.FindEntryByEventId(DetectionId, queuedType)).Returns(new EventEntry { EventId = DetectionId, EventType = queuedType });
        var api = new Mock<IEventApiService>();
        var service = CreateService(queue, api);
        await service.StartService();

        await _handler!(Sync("DELETED"));
        await service.LastProcessingTask!;

        Assert.Contains(new DetectionDeletedOnServerMessage(DetectionId, queuedType), _published);
        api.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task should_still_publish_when_deleted_detection_is_not_queued()
    {
        // 카드만 남은 경우(엔트리가 먼저 빠짐)도 닫아야 한다 — 닫을 것이 없으면 카드 목록이 멱등 no-op.
        var service = CreateService(new Mock<IEventQueueManager>(), new Mock<IEventApiService>());
        await service.StartService();

        await _handler!(Sync("DELETED"));
        await service.LastProcessingTask!;

        Assert.Contains(new DetectionDeletedOnServerMessage(DetectionId, null), _published);
    }

    [Fact]
    public async Task should_ignore_deleted_without_valid_resource_id()
    {
        var service = CreateService(new Mock<IEventQueueManager>(), new Mock<IEventApiService>());
        await service.StartService();

        await _handler!(Sync("DELETED", resourceId: 0));

        Assert.Empty(_published);
    }

    [Fact]
    public async Task should_drop_inflight_thumbnail_update_when_deleted_arrives_meanwhile()
    {
        var queue = new Mock<IEventQueueManager>();
        queue.Setup(q => q.FindEntryByEventId(DetectionId, EnumEventType.Intrusion)).Returns(new EventEntry { EventId = DetectionId, EventType = EnumEventType.Intrusion });
        var gate = new TaskCompletionSource<ApiResponse<DetectionEventDto>>();
        var api = new Mock<IEventApiService>();
        api.Setup(a => a.GetDetectionEventByIdAsync(DetectionId, It.IsAny<CancellationToken>())).Returns(gate.Task);
        var service = CreateService(queue, api);
        await service.StartService();

        await _handler!(Sync("UPDATED"));
        var updating = service.LastProcessingTask!;
        await _handler!(Sync("DELETED"));
        gate.SetResult(new ApiResponse<DetectionEventDto> { Success = true, Data = new DetectionEventDto { Id = DetectionId, Detail = new DetectionDetailDto { Thumbnail = "/t.jpg" } } });
        await updating;

        Assert.DoesNotContain(_published, m => m is DetectionThumbnailSyncedMessage);
    }
}

[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적 — 스텁을 쓰는 다른 시험과 병렬로 돌면 서로 덮는다
public class DetectionServerDeletionPanelTests : IDisposable
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

    public DetectionServerDeletionPanelTests()
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
    public async Task should_close_card_release_queue_and_notify_when_detection_deleted_on_server()
    {
        var panel = CreatePanel();
        var card = Detection(42);
        card.EntryId = "entry-42";
        panel.ViewModelProvider.Add(card);
        string? closed = null;
        panel.ActionReported += (kind, id) => closed = $"{kind}:{id}";

        await panel.HandleAsync(new DetectionDeletedOnServerMessage(42, EnumEventType.Intrusion), CancellationToken.None);

        Assert.Empty(panel.ViewModelProvider);                 // 카드가 닫히고
        _queue.Verify(q => q.Dequeue("entry-42"), Times.Once); // 큐 엔트리가 빠져 신호등 건수가 준다
        Assert.Contains(new EventDeletedOnServerNotice(ActionReportKind.Detection, 42), _published);   // 토스트
        Assert.Contains(new RemoteActionReportedMessage(ActionReportKind.Detection, 42, DeletedOnServer: true), _published);   // 열린 창
        Assert.Equal($"{ActionReportKind.Detection}:42", closed);   // 이벤트 창도 닫힌다
    }

    [Fact]
    public async Task should_release_queue_entry_when_card_is_not_shown()
    {
        var panel = CreatePanel();
        _queue.Setup(q => q.FindEntryByEventId(42, EnumEventType.ContactOn))
              .Returns(new EventEntry { EntryId = "entry-42", EventId = 42, EventType = EnumEventType.ContactOn });

        await panel.HandleAsync(new DetectionDeletedOnServerMessage(42, EnumEventType.ContactOn), CancellationToken.None);

        _queue.Verify(q => q.Dequeue("entry-42"), Times.Once);
        Assert.Contains(new EventDeletedOnServerNotice(ActionReportKind.Detection, 42), _published);
    }

    [Fact]
    public async Task should_not_touch_malfunction_card_with_same_id()
    {
        // 탐지 · 장애는 서버 번호를 따로 센다 — 탐지 42 삭제가 장애 42 카드를 닫으면 안 된다.
        var panel = CreatePanel();
        var fault = Malfunction(42);
        panel.ViewModelProvider.Add(fault);

        await panel.HandleAsync(new DetectionDeletedOnServerMessage(42, null), CancellationToken.None);

        Assert.Same(fault, Assert.Single(panel.ViewModelProvider));
        Assert.DoesNotContain(_published, m => m is EventDeletedOnServerNotice);   // 닫은 것이 없으면 토스트도 없다
        _queue.Verify(q => q.Dequeue(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task should_disable_ok_with_deletion_notice_even_while_sending()
    {
        var gate = new TaskCompletionSource<ApiResponse<ActionEventDto>>();
        _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>())).Returns(gate.Task);
        var dialog = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs.DetectionReportDialogViewModel(_ea.Object, _log.Object);
        dialog.UpdateData(Detection(42), _account.Object);
        await ((IActivate)dialog).ActivateAsync();
        dialog.ClickOk();
        Assert.True(dialog.IsSending);

        // 서버 삭제는 자기 조치의 메아리일 수 없다 — 보내는 중이어도 곧바로 끈다.
        await dialog.HandleAsync(new RemoteActionReportedMessage(ActionReportKind.Detection, 42, DeletedOnServer: true), CancellationToken.None);

        Assert.True(dialog.IsHandledElsewhere);
        Assert.Contains("서버에서 삭제된 이벤트", dialog.DialogNotice);
        gate.SetResult(new ApiResponse<ActionEventDto> { Success = false, Message = "not found" });
        await Task.Run(() => SpinWait.SpinUntil(() => !dialog.IsSending, TimeSpan.FromSeconds(5)));
        Assert.False(dialog.CanClickOk);
    }
}
