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
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 이벤트 카드 목록 · 조치보고 수명주기 결함(WP-1, 2026-09-30 읽기 전용 감사) 회귀 시험.
                  ① 카드는 배치 버퍼를 거친다(500 상한 · 150 ms 묶음)
                  ② 원격 ACTION_REPORT · EntryId 매칭은 종류(탐지/장애)까지 본다 — 서버 id 는 종류마다 따로 센다
                  ③ 카드가 없을 때도 원격 조치보고가 큐 · 심볼을 푼다 + 열린 창은 [확인]을 끈다
                  ④ 임시 카드(트레이 · 우클릭 · 이력)로 조치보고하면 목록의 진짜 카드도 닫힌다
                  ⑤ 자동복구 조치보고가 실패하면 카드를 지우지 않고 NATS 도 보내지 않는다
                  ⑪ 카드 더블클릭 = 지도에서 보기 · ⑬ 보내는 중 [확인] 끔 · 보고자 표기 통일
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적 — 스텁을 쓰는 다른 시험과 병렬로 돌면 서로 덮는다
public class EventCardLifecycleDefectTests
{
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IEventApiService> _api = new();
    private readonly Mock<IAccountModel> _account = new();
    private readonly Mock<ISymbolEventManager> _symbols = new();
    private readonly Mock<IEventQueueManager> _queue = new();
    private readonly ActionReportGuard _guard = new();
    private readonly List<object> _published = new();
    private static readonly EventSetupModel _setup = new(new Mock<IEventSetupModel>().Object);

    public EventCardLifecycleDefectTests()
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
        _account.SetupGet(a => a.Name).Returns("홍길동");
        _account.SetupGet(a => a.Username).Returns("kim");
        _account.SetupGet(a => a.EmployeeNumber).Returns("1234");
    }

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

    private void ApiReturns(bool success)
        => _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = success, Data = success ? new ActionEventDto { Id = 77 } : null, Message = success ? null : "boom" });

    #region - ② EntryId 매칭은 종류까지 본다 -

    [Fact]
    public async Task should_give_the_fault_entry_to_the_malfunction_card_when_a_detection_card_has_the_same_id()
    {
        var panel = CreatePanel();
        var detection = Detection(7);
        var malfunction = Malfunction(7);
        panel.ViewModelProvider.Add(detection);     // 탐지가 앞에 있어도
        panel.ViewModelProvider.Add(malfunction);

        await panel.HandleAsync(new EventEntryEnqueuedMessage("fault-entry", 7, 1, EnumDeviceType.Fence, EnumEventType.Fault), CancellationToken.None);

        Assert.Null(detection.EntryId);
        Assert.Equal("fault-entry", malfunction.EntryId);
    }

    #endregion

    #region - ④ 임시 카드 조치보고 → 진짜 카드도 닫힌다 -

    [Fact]
    public async Task should_close_the_real_card_when_the_report_came_from_a_temporary_card_with_the_same_kind_and_id()
    {
        var panel = CreatePanel();
        var real = Detection(7);
        real.EntryId = "entry-7";
        panel.ViewModelProvider.Add(real);
        panel.ViewModelProvider.Add(Malfunction(7));        // 같은 번호의 장애 카드는 남아야 한다
        var temporary = Detection(7);                         // 트레이 · 우클릭 · 이력 다이얼로그가 만드는 임시 카드

        await panel.HandleAsync(new DetectionReportedMessageModel(temporary, "순찰 조치", "kim(1234)"), CancellationToken.None);

        Assert.DoesNotContain(real, panel.ViewModelProvider);
        Assert.Single(panel.ViewModelProvider.OfType<MalfunctionEventCardViewModel>());
        _queue.Verify(q => q.Dequeue("entry-7"), Times.Once);
    }

    #endregion

    #region - ⑤ 자동복구 실패는 카드를 지우지 않는다 -

    [Fact]
    public async Task should_keep_the_card_and_not_publish_when_the_auto_recovery_report_fails()
    {
        var panel = CreatePanel();
        var card = Malfunction(9);
        panel.ViewModelProvider.Add(card);
        await panel.HandleAsync(new EventEntryEnqueuedMessage("fault-9", 9, 1, EnumDeviceType.Fence, EnumEventType.Fault), CancellationToken.None);
        ApiReturns(success: false);

        await panel.HandleAutoRecoveryAsync("fault-9");

        Assert.Contains(card, panel.ViewModelProvider);
        Assert.DoesNotContain(_published, m => m is SendActionRequestMessage);
    }

    [Fact]
    public async Task should_remove_the_card_and_publish_when_the_auto_recovery_report_succeeds()
    {
        var panel = CreatePanel();
        var card = Malfunction(9);
        panel.ViewModelProvider.Add(card);
        await panel.HandleAsync(new EventEntryEnqueuedMessage("fault-9", 9, 1, EnumDeviceType.Fence, EnumEventType.Fault), CancellationToken.None);
        ApiReturns(success: true);

        await panel.HandleAutoRecoveryAsync("fault-9");

        Assert.DoesNotContain(card, panel.ViewModelProvider);
        var sent = Assert.Single(_published.OfType<SendActionRequestMessage>());
        Assert.Equal(77, sent.ActionId);
    }

    #endregion

    #region - ① 묶음 버퍼 · 표시 상한 · ⑲ 최신이 맨 위 -

    [Fact]
    public async Task should_hold_cards_in_the_batch_buffer_until_flushed_and_put_the_newest_on_top()
    {
        var panel = CreatePanel();
        var first = Detection(1);
        var second = Malfunction(2);

        panel.EnqueueCard(first);
        panel.EnqueueCard(second);

        Assert.Empty(panel.ViewModelProvider);                 // 곧장 넣지 않는다 — 150 ms 묶음
        Assert.Equal(2, panel.PendingCardCount);

        await panel.FlushPendingCardsNowAsync();

        Assert.Equal(0, panel.PendingCardCount);
        Assert.Same(second, panel.ViewModelProvider[0]);       // 나중에 온 것이 맨 위
        Assert.Same(first, panel.ViewModelProvider[1]);
    }

    [Fact]
    public async Task should_drop_the_oldest_cards_when_the_display_cap_is_exceeded()
    {
        var panel = CreatePanel();
        for (var id = 1; id <= 500; id++) panel.ViewModelProvider.Insert(0, Detection(id));   // 맨 아래 = 1번(가장 오래됨)

        panel.EnqueueCard(Detection(501));
        panel.EnqueueCard(Detection(502));
        await panel.FlushPendingCardsNowAsync();

        Assert.Equal(500, panel.ViewModelProvider.Count);
        Assert.Equal(502, panel.ViewModelProvider[0].Model.Id);
        Assert.DoesNotContain(panel.ViewModelProvider, c => c.Model.Id is 1 or 2);
    }

    [Fact]
    public async Task should_not_raise_a_buffered_card_when_its_event_was_already_reported()
    {
        var panel = CreatePanel();
        panel.EnqueueCard(Detection(8));                        // 아직 버퍼 안

        panel.CloseByRemoteActionReport(Services.ActionReportKind.Detection, 8, EnumEventType.Intrusion);
        await panel.FlushPendingCardsNowAsync();

        Assert.Empty(panel.ViewModelProvider);
    }

    [Fact]
    public async Task should_assign_a_pending_entry_only_to_the_card_of_the_same_kind_when_cards_arrive_later()
    {
        var panel = CreatePanel();
        await panel.HandleAsync(new EventEntryEnqueuedMessage("fault-entry", 7, 1, EnumDeviceType.Fence, EnumEventType.Fault), CancellationToken.None);
        var detection = Detection(7);
        var malfunction = Malfunction(7);

        panel.EnqueueCard(detection);
        panel.EnqueueCard(malfunction);
        await panel.FlushPendingCardsNowAsync();

        Assert.Null(detection.EntryId);
        Assert.Equal("fault-entry", malfunction.EntryId);
    }

    #endregion

    #region - ② · ③ · ⑮ 원격 조치보고(ACTION_REPORT) -

    [Fact]
    public void should_close_only_the_malfunction_card_when_the_remote_report_is_for_a_malfunction_with_a_shared_id()
    {
        var panel = CreatePanel();
        var detection = Detection(7);
        var malfunction = Malfunction(7);
        malfunction.EntryId = "fault-7";
        panel.ViewModelProvider.Add(detection);
        panel.ViewModelProvider.Add(malfunction);

        var outcome = panel.CloseByRemoteActionReport(Services.ActionReportKind.Malfunction, 7, EnumEventType.Fault);

        Assert.Equal(RemoteActionReportOutcome.CardClosed, outcome);
        Assert.Same(detection, Assert.Single(panel.ViewModelProvider));
        _queue.Verify(q => q.Dequeue("fault-7"), Times.Once);
    }

    [Fact]
    public void should_close_nothing_when_the_kind_is_unknown_and_two_cards_share_the_id()
    {
        var panel = CreatePanel();
        panel.ViewModelProvider.Add(Detection(7));
        panel.ViewModelProvider.Add(Malfunction(7));

        var outcome = panel.CloseByRemoteActionReport(null, 7);

        Assert.Equal(RemoteActionReportOutcome.Ambiguous, outcome);
        Assert.Equal(2, panel.ViewModelProvider.Count);
    }

    [Fact]
    public void should_close_the_only_card_when_the_kind_is_unknown_but_the_id_is_unique()
    {
        var panel = CreatePanel();
        panel.ViewModelProvider.Add(Malfunction(9));

        Assert.Equal(RemoteActionReportOutcome.CardClosed, panel.CloseByRemoteActionReport(null, 9));
        Assert.Empty(panel.ViewModelProvider);
    }

    [Fact]
    public void should_release_the_queue_entry_when_the_remote_report_has_no_card()
    {
        var panel = CreatePanel();
        _queue.Setup(q => q.FindEntryByEventId(12, EnumEventType.Intrusion))
              .Returns(new EventEntry { EntryId = "entry-12", EventId = 12, EventType = EnumEventType.Intrusion });

        var outcome = panel.CloseByRemoteActionReport(Services.ActionReportKind.Detection, 12, EnumEventType.Intrusion);

        Assert.Equal(RemoteActionReportOutcome.QueueEntryCleared, outcome);
        _queue.Verify(q => q.Dequeue("entry-12"), Times.Once);
    }

    [Fact]
    public void should_recompute_device_and_group_symbols_when_the_remote_report_finds_no_card_and_no_entry()
    {
        var panel = CreatePanel();
        var device = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        device.SetupGet(d => d.Id).Returns(349);
        device.SetupGet(d => d.DeviceType).Returns(EnumDeviceType.Fence);
        device.SetupGet(d => d.DeviceGroups).Returns(new List<int> { 3, 4 });

        var outcome = panel.CloseByRemoteActionReport(Services.ActionReportKind.Detection, 13, EnumEventType.Intrusion, device.Object);

        Assert.Equal(RemoteActionReportOutcome.SymbolRefreshed, outcome);
        _symbols.Verify(s => s.RefreshDeviceSymbol(349, EnumDeviceType.Fence), Times.Once);
        _symbols.Verify(s => s.RefreshGroupSymbol(3), Times.Once);
        _symbols.Verify(s => s.RefreshGroupSymbol(4), Times.Once);
        _queue.Verify(q => q.Dequeue(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void should_not_take_the_devices_oldest_entry_when_a_remote_report_card_has_no_entry_by_id()
    {
        // ⑮ — 카드는 있지만 번호로 엔트리를 못 찾았다. 같은 장비의 다른 활성 이벤트(가장 오래된 엔트리)를 지우면 안 된다.
        var panel = CreatePanel();
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(21);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        var device = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        device.SetupGet(d => d.Id).Returns(5);
        device.SetupGet(d => d.DeviceType).Returns(EnumDeviceType.Fence);
        model.SetupGet(m => m.Device).Returns(device.Object);
        panel.ViewModelProvider.Add(new DetectionEventCardViewModel(model.Object));
        _queue.Setup(q => q.FindEntryByDevice(5, EnumDeviceType.Fence))
              .Returns(new EventEntry { EntryId = "someone-else", EventId = 20, EventType = EnumEventType.Intrusion });

        panel.CloseByRemoteActionReport(Services.ActionReportKind.Detection, 21, EnumEventType.Intrusion);

        _queue.Verify(q => q.Dequeue("someone-else"), Times.Never);
        _symbols.Verify(s => s.RefreshDeviceSymbol(5, EnumDeviceType.Fence), Times.Once);
    }

    [Fact]
    public void should_tell_open_report_dialogs_when_a_remote_report_arrives()
    {
        var panel = CreatePanel();

        panel.CloseByRemoteActionReport(Services.ActionReportKind.Malfunction, 30, EnumEventType.Fault);

        Assert.Contains(new RemoteActionReportedMessage(Services.ActionReportKind.Malfunction, 30), _published);
    }

    [Fact]
    public async Task should_disable_ok_and_explain_when_another_operator_reported_the_open_dialogs_event()
    {
        var dialog = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs.DetectionReportDialogViewModel(_ea.Object, _log.Object);
        dialog.UpdateData(Detection(40), _account.Object);
        await ((IActivate)dialog).ActivateAsync();
        Assert.True(dialog.CanClickOk);

        await dialog.HandleAsync(new RemoteActionReportedMessage(Services.ActionReportKind.Malfunction, 40), CancellationToken.None);   // 다른 종류
        Assert.True(dialog.CanClickOk);

        await dialog.HandleAsync(new RemoteActionReportedMessage(Services.ActionReportKind.Detection, 40), CancellationToken.None);
        Assert.False(dialog.CanClickOk);
        Assert.Contains("다른 운영자가 이미 조치했습니다", dialog.DialogNotice);

        dialog.ClickOk();                                        // 눌러도 두 번째 조치를 만들지 않는다
        _api.Verify(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region - ⑬ 보내는 중 [확인] 끔 -

    [Fact]
    public async Task should_disable_ok_while_the_report_is_being_sent()
    {
        var gate = new TaskCompletionSource<ApiResponse<ActionEventDto>>();
        _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>())).Returns(gate.Task);
        var dialog = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs.DetectionReportDialogViewModel(_ea.Object, _log.Object);
        dialog.UpdateData(Detection(41), _account.Object);
        await ((IActivate)dialog).ActivateAsync();

        dialog.ClickOk();                                        // 첫 문구가 골라져 있다 — 보내기 시작
        Assert.False(dialog.CanClickOk);
        Assert.True(dialog.IsSending);
        dialog.ClickOk();                                        // 두 번째 누름은 무시

        gate.SetResult(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 5 } });
        await Task.Run(() => SpinWait.SpinUntil(() => !dialog.IsSending, TimeSpan.FromSeconds(5)));   // async void 의 끝을 기다린다(고정 지연 아님)

        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.User == "kim(1234)"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(dialog.IsSending);
    }

    #endregion

    #region - ⑪ 카드 더블클릭 = 지도에서 보기 -

    [Fact]
    public async Task should_select_the_card_and_ask_the_map_to_locate_its_device_when_double_clicked()
    {
        var panel = CreatePanel();
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(50);
        var device = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        device.SetupGet(d => d.Id).Returns(349);
        device.SetupGet(d => d.DeviceName).Returns("GOP-SNS-01-01");
        model.SetupGet(m => m.Device).Returns(device.Object);
        var card = new DetectionEventCardViewModel(model.Object);

        var sent = await panel.LocateCardAsync(card);

        Assert.True(sent);
        Assert.Same(card, panel.SelectedEventCardViewModel);
        var request = Assert.Single(_published.OfType<MapLocateRequest>());
        Assert.Equal(new[] { 349 }, request.DeviceIds);
        Assert.Equal("GOP-SNS-01-01", request.Title);
    }

    [Fact]
    public async Task should_only_select_when_the_card_has_no_device()
    {
        var panel = CreatePanel();
        var card = Detection(51);

        Assert.False(await panel.LocateCardAsync(card));
        Assert.Same(card, panel.SelectedEventCardViewModel);
        Assert.Empty(_published.OfType<MapLocateRequest>());
    }

    #endregion

    #region - ⑫ 카드 자동화 식별자 -

    [Fact]
    public void should_name_cards_by_kind_and_id_for_automation()
    {
        Assert.Equal("Events.Card.Detection.7", Detection(7).AutomationKey);
        Assert.Equal("Events.Card.Malfunction.7", Malfunction(7).AutomationKey);
        Assert.Equal("Events.Card.Malfunction.7.Report", Malfunction(7).ReportAutomationKey);
    }

    #endregion

    #region - ⑳ ACTION_REPORT 는 서버 data 를 그대로 -

    [Fact]
    public async Task should_carry_the_server_data_verbatim_when_a_card_report_is_published()
    {
        var raw = Newtonsoft.Json.Linq.JObject.Parse("""{"id":83,"type_event":"Action","content":"c","user":"kim(1234)","from_event":{"id":3,"category_event":"detection","device":{"id":349,"category_device":"sensor"},"action_reported":true}}""");
        _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 83 }, RawData = raw });

        await Detection(3).SendActionDetailed("c", "kim(1234)");

        var sent = Assert.Single(_published.OfType<SendActionRequestMessage>());
        Assert.True(Newtonsoft.Json.Linq.JToken.DeepEquals(raw, Newtonsoft.Json.Linq.JToken.Parse(sent.ServerActionJson!)));
        Assert.Equal(83, sent.ActionId);
    }

    #endregion

    #region - ⑬ 보고자 표기 — 창이 넘긴 "Username(EmployeeNumber)" 를 그대로 보낸다 -

    [Fact]
    public async Task should_send_the_dialog_user_string_when_a_card_reports()
    {
        ApiReturns(success: true);
        var card = Detection(3);

        await card.SendActionDetailed("순찰 조치", "kim(1234)");

        _api.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.User == "kim(1234)"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("kim(1234)", Assert.Single(_published.OfType<SendActionRequestMessage>()).ActionUser);
    }

    #endregion
}
