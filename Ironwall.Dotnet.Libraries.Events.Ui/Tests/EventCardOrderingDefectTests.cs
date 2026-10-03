using Xunit;
using Moq;
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
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 이벤트 순서가 뒤집히거나 카드가 아직 묶음 버퍼(150 ms)에 있을 때의 결함 재현 · 회귀
                  (이벤트 시나리오 점검 2026-10-03 — B4 · B5 · B6 · B7)
   Created By   : Claude
   Created On   : 2026-10-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적 — 다른 IoC 스텁 시험과 병렬로 돌리면 서로 덮는다
public class EventCardOrderingDefectTests
{
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IEventApiService> _api = new();
    private readonly Mock<IEventQueueManager> _queue = new();
    private static readonly EventSetupModel SetupModel = new(new Mock<IEventSetupModel>().Object);

    public EventCardOrderingDefectTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return _ea.Object;
            if (type == typeof(ILogService)) return _log.Object;
            if (type == typeof(EventSetupModel)) return SetupModel;
            if (type == typeof(IAccountModel)) return new Mock<IAccountModel>().Object;
            return null!;
        };
        _ea.Setup(ea => ea.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto() });
    }

    private EventCardListPanelViewModel CreateSut()
        => new(_ea.Object, _log.Object, null!, new Mock<IAccountModel>().Object, _api.Object,
               new Mock<ISymbolEventManager>().Object, _queue.Object, new ActionReportGuard());

    private static DetectionEventCardViewModel Detection(int eventId)
        => new(new DetectionEventModel { Id = eventId, MessageType = EnumEventType.Intrusion });

    private static EventEntry Entry(string entryId, int eventId, EnumEventType type = EnumEventType.Intrusion)
        => new() { EntryId = entryId, EventId = eventId, EventType = type };

    // ── B5: ACTION_REPORT 가 DETECT 보다 먼저 ──

    [Fact]
    public async Task should_release_queue_entry_when_detection_arrives_after_its_action_report()
    {
        // 다른 GIS 가 먼저 조치했고(ACTION_REPORT), 그 뒤에 같은 탐지의 DETECT 가 닿았다. 카드는 '이미 조치됨' 기억으로 걸러지지만,
        // 라이브러리가 큐에 넣은 엔트리는 아무도 빼지 않아 심볼이 깜빡이고 알람이 울린 채 남았다(자동 조치보고가 꺼져 있으면 영영).
        var sut = CreateSut();
        sut.CloseByRemoteActionReport(ActionReportKind.Detection, 9, EnumEventType.Intrusion);

        await sut.HandleAsync(new EventEntryEnqueuedMessage("e9", 9, 3, EnumDeviceType.Fence, EnumEventType.Intrusion), CancellationToken.None);
        sut.EnqueueCard(Detection(9));
        await sut.FlushPendingCardsNowAsync();

        _queue.Verify(q => q.Dequeue("e9"), Times.Once);
        Assert.Empty(sut.ViewModelProvider);
    }

    [Fact]
    public async Task should_keep_queue_entry_when_only_a_different_kind_with_the_same_number_was_reported()
    {
        // 장애 9 조치가 탐지 9 엔트리를 풀어 버리면 안 된다(서버는 종류마다 번호를 매긴다).
        var sut = CreateSut();
        sut.CloseByRemoteActionReport(ActionReportKind.Malfunction, 9, EnumEventType.Fault);

        await sut.HandleAsync(new EventEntryEnqueuedMessage("e9", 9, 3, EnumDeviceType.Fence, EnumEventType.Intrusion), CancellationToken.None);

        _queue.Verify(q => q.Dequeue("e9"), Times.Never);
    }

    // ── B6: 자동 조치보고 — 카드가 아직 버퍼에 있거나 표시 상한으로 밀려났을 때 ──

    [Fact]
    public async Task should_report_to_server_when_auto_report_fires_while_card_is_still_buffered()
    {
        // 카드가 목록(_cardByEntryId)에 없다는 이유로 서버에 보고하지 않고 큐에서만 뺐다 — 서버엔 미조치로 남고, 카드는 뒤늦게 올라왔다.
        var sut = CreateSut();
        await sut.HandleAsync(new EventEntryEnqueuedMessage("e3", 3, 3, EnumDeviceType.Fence, EnumEventType.Intrusion), CancellationToken.None);
        sut.EnqueueCard(Detection(3));   // 아직 버퍼

        await sut.HandleAutoReportAsync(Entry("e3", 3));
        await sut.FlushPendingCardsNowAsync();

        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == 3), It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.Dequeue("e3"), Times.Once);
        Assert.Empty(sut.ViewModelProvider);   // 보고한 카드는 뒤늦게 올라오지 않는다
    }

    [Fact]
    public async Task should_report_to_server_when_auto_report_fires_after_card_was_pushed_out_by_display_cap()
    {
        // 표시 상한으로 카드가 밀려나도 큐 엔트리 · 서버 이벤트는 남는다 — 그 엔트리의 자동 조치보고가 서버로 가야 한다.
        var sut = CreateSut();

        await sut.HandleAutoReportAsync(Entry("e4", 4));

        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == 4), It.IsAny<CancellationToken>()), Times.Once);
        _queue.Verify(q => q.Dequeue("e4"), Times.Once);
    }

    [Fact]
    public async Task should_not_report_again_when_auto_report_fires_for_an_event_closed_elsewhere()
    {
        var sut = CreateSut();
        sut.CloseByRemoteActionReport(ActionReportKind.Detection, 5, EnumEventType.Intrusion);

        await sut.HandleAutoReportAsync(Entry("e5", 5));

        _api.Verify(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _queue.Verify(q => q.Dequeue("e5"), Times.Once);
    }

    // ── B7: 같은 번호의 탐지 · 장애가 서로의 자동 처리를 막지 않는다 ──

    [Fact]
    public async Task should_auto_report_detection_while_malfunction_with_the_same_number_is_being_reported()
    {
        var guard = new ActionReportGuard();
        var sut = new EventCardListPanelViewModel(_ea.Object, _log.Object, null!, new Mock<IAccountModel>().Object, _api.Object,
            new Mock<ISymbolEventManager>().Object, _queue.Object, guard);
        Assert.True(guard.TryEnter(ActionReportKind.Malfunction, 7));   // 장애 7 조치보고 진행 중

        await sut.HandleAutoReportAsync(Entry("e7", 7));

        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == 7), It.IsAny<CancellationToken>()), Times.Once);
        guard.Exit(ActionReportKind.Malfunction, 7);
    }

    // ── B4: 버퍼에 있는 카드의 늦은 썸네일 ──

    [Fact]
    public async Task should_apply_late_thumbnail_when_card_is_still_buffered()
    {
        // 썸네일 동기화가 목록만 찾아, 카드가 아직 버퍼에 있으면 갱신이 버려졌다(폭주 중 사진이 안 뜨던 길).
        var sut = CreateSut();
        var card = Detection(6);
        sut.EnqueueCard(card);

        await sut.HandleAsync(new DetectionThumbnailSyncedMessage(6, "/api/thumbnails/images/CAM-1_x.jpg", 1920, 1080), CancellationToken.None);
        await sut.FlushPendingCardsNowAsync();

        var shown = Assert.Single(sut.ViewModelProvider);
        Assert.Same(card, shown);
        Assert.Equal("/api/thumbnails/images/CAM-1_x.jpg", ((IDetectionEventModel)shown.Model!).Thumbnail);
    }
}
