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
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 조치보고 → 이벤트 창 닫기 신호(EventCardListPanelViewModel.ActionReported) 시험 (PRD camera-popup-modes FR-15, T-06)
                  로컬(카드 · 임시 카드) · 원격 ACTION_REPORT · 자동 조치보고가 종류(탐지/장애)를 달고 한 번씩 알린다.
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적 — 스텁을 쓰는 다른 시험과 병렬로 돌면 서로 덮는다
public class EventWindowCloseOnReportTests : IDisposable
{
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IEventApiService> _api = new();
    private readonly Mock<IAccountModel> _account = new();
    private readonly Mock<ISymbolEventManager> _symbols = new();
    private readonly Mock<IEventQueueManager> _queue = new();
    private readonly ActionReportGuard _guard = new();
    private readonly EventSetupModel _setup = new(new Mock<IEventSetupModel>().Object);
    private readonly Func<Type, string, object> _previousGetInstance = IoC.GetInstance;

    public EventWindowCloseOnReportTests()
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
           .Returns(Task.CompletedTask);
    }

    public void Dispose() => IoC.GetInstance = _previousGetInstance;

    private EventCardListPanelViewModel CreatePanel(List<(string Kind, int Id)> reported)
    {
        var panel = new EventCardListPanelViewModel(_ea.Object, _log.Object, null!, _account.Object, _api.Object, _symbols.Object, _queue.Object, _guard);
        panel.ActionReported += (kind, id) => reported.Add((kind, id));
        return panel;
    }

    private static DetectionEventCardViewModel Detection(int id)
    {
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(id);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        model.SetupProperty(m => m.Status);
        return new DetectionEventCardViewModel(model.Object);
    }

    [Fact]
    public void should_signal_once_with_kind_when_remote_action_report_arrives_even_without_card()
    {
        var reported = new List<(string, int)>();
        var panel = CreatePanel(reported);

        panel.CloseByRemoteActionReport(ActionReportKind.Malfunction, 7, EnumEventType.Fault);
        panel.CloseByRemoteActionReport(ActionReportKind.Malfunction, 7, EnumEventType.Fault);   // 메아리

        Assert.Equal(new[] { (ActionReportKind.Malfunction, 7) }, reported);
    }

    [Fact]
    public async Task should_signal_detection_when_local_report_comes_from_temporary_card()
    {
        var reported = new List<(string, int)>();
        var panel = CreatePanel(reported);
        panel.ViewModelProvider.Add(Detection(9));

        await panel.HandleAsync(new DetectionReportedMessageModel(Detection(9), "순찰 조치", "kim(1234)"), CancellationToken.None);

        Assert.Equal(new[] { (ActionReportKind.Detection, 9) }, reported);
    }

    [Fact]
    public async Task should_signal_when_auto_report_succeeds()
    {
        var reported = new List<(string, int)>();
        var panel = CreatePanel(reported);
        _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 1 } });
        var card = Detection(11);
        panel.ViewModelProvider.Add(card);
        await panel.HandleAsync(new EventEntryEnqueuedMessage("entry-11", 11, 1, EnumDeviceType.Fence, EnumEventType.Intrusion), CancellationToken.None);

        await panel.HandleAutoReportAsync(new EventEntry { EntryId = "entry-11", EventId = 11, EventType = EnumEventType.Intrusion });

        Assert.Equal(new[] { (ActionReportKind.Detection, 11) }, reported);
    }

    [Fact]
    public void should_keep_closing_cards_when_window_subscriber_throws()
    {
        var panel = new EventCardListPanelViewModel(_ea.Object, _log.Object, null!, _account.Object, _api.Object, _symbols.Object, _queue.Object, _guard);
        panel.ActionReported += (_, _) => throw new InvalidOperationException("popup broke");

        var outcome = panel.CloseByRemoteActionReport(ActionReportKind.Detection, 3, EnumEventType.Intrusion);

        Assert.NotEqual(RemoteActionReportOutcome.Invalid, outcome);
    }
}
