using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Moq;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 운영 이벤트 조치보고 헤드리스 시나리오(docs/prds/operation-event-card-prd.md v1.1 §11, OPA-0nn)
                  서버 규격(operation-event PRD FR-07) — 운영 이벤트도 POST /api/events/actions(from_event_id = 운영 id) 로 조치한다.
                  EventMapRig(실제 큐 + 실제 수신 서비스 + 실제 카드 패널)와 실제 이벤트 콘솔 뷰모델로 돌린다. 창은 띄우지 않는다.
   Created By   : Claude
   Created On   : 2026-10-13
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // 카드 · 창 뷰모델이 IoC.GetInstance(전역 정적)를 읽는다
public class OperationEventActionReportScenarioTests
{
    private static List<OperationEventCardViewModel> Cards(EventMapRig rig)
        => rig.Panel.ViewModelProvider.OfType<OperationEventCardViewModel>().ToList();

    private static async Task<OperationEventCardViewModel> ShowOperationCardAsync(EventMapRig rig, int deviceId, int eventId, string reason = "GATE_OPEN")
    {
        await rig.PublishOperationAsync(deviceId, reason, "OPEN", eventId, envelopeId: $"env-op-{eventId}");
        await rig.Panel.FlushPendingCardsNowAsync();
        return Assert.Single(Cards(rig), c => c.Model.Id == eventId);
    }

    /// <summary>조치 생성 본문을 모은다 — 성공이면 201(조치 번호), 아니면 서버 거절.</summary>
    private static List<ActionEventCreateDto> CaptureActions(EventMapRig rig, bool success = true)
    {
        var bodies = new List<ActionEventCreateDto>();
        rig.EventApi.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ActionEventCreateDto dto, CancellationToken _) =>
                    {
                        lock (bodies) bodies.Add(dto);
                        if (!success) return new ApiResponse<ActionEventDto> { Success = false, StatusCode = 500, Message = "boom" };
                        lock (rig.ServerActions) rig.ServerActions.Add(dto.FromEventId);
                        return new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 777 } };
                    });
        return bodies;
    }

    private static List<SendActionRequestMessage> NatsReports(EventMapRig rig)
    {
        lock (rig.Published) return rig.Published.OfType<SendActionRequestMessage>().ToList();
    }

    // ───────────────────────── 카드 [조치보고] ─────────────────────────

    [Fact]
    [Trait("Scenario", "OPA-001")]
    public async Task should_post_action_with_operation_id_then_close_card_and_publish_nats_when_card_is_reported()
    {
        using var rig = new EventMapRig();
        rig.Device(2101, EnumDeviceType.Gate);
        var bodies = CaptureActions(rig);
        var card = await ShowOperationCardAsync(rig, 2101, eventId: 61);
        Assert.True(card.IsReportSupported);

        var result = await card.SendActionDetailed("문 닫음 확인", "operator(1)");

        Assert.Equal(ActionSendOutcome.Created, result.Outcome);
        var body = Assert.Single(bodies);
        Assert.Equal(61, body.FromEventId);
        Assert.Equal("문 닫음 확인", body.Content);
        Assert.Equal("operator(1)", body.User);
        Assert.Empty(Cards(rig));                                      // 성공 뒤 카드 종결
        var nats = Assert.Single(NatsReports(rig));                    // API 저장 성공 뒤에만 ACTION_REPORT
        Assert.Equal(61, nats.EventId);
        Assert.Equal(EnumEventType.Operation, nats.EventType);
        Assert.Equal(777, nats.ActionId);
        Assert.Contains((EventCardKind.Operation, 61), rig.ActionReported);
        Assert.Equal(EnumTrueFalse.True, card.Status);

        // 브로커가 같은 봉투를 다시 배달해도 다시 뜨지 않는다
        await rig.PublishOperationAsync(2101, "GATE_OPEN", "OPEN", 61, envelopeId: "env-op-61");
        await rig.Panel.FlushPendingCardsNowAsync();
        Assert.Empty(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPA-002")]
    public async Task should_keep_card_and_publish_nothing_when_server_refuses_operation_action()
    {
        using var rig = new EventMapRig();
        rig.Device(2102, EnumDeviceType.Enclosure);
        CaptureActions(rig, success: false);
        var card = await ShowOperationCardAsync(rig, 2102, eventId: 62, reason: "ENCLOSURE_DOOR_OPEN");

        var result = await card.SendActionDetailed("확인", "operator(1)");

        Assert.False(result.CanCloseDialog);
        Assert.Single(Cards(rig));                                     // 카드 유지
        Assert.Empty(NatsReports(rig));                                // 서버에 조치가 없으니 ACTION_REPORT 도 없다
        Assert.Empty(rig.ActionReported);
        Assert.NotEqual(EnumTrueFalse.True, card.Status);

        // 서버가 돌아오면 같은 카드로 다시 보고할 수 있다(닫힘 기억에 남지 않았다)
        CaptureActions(rig);
        Assert.Equal(ActionSendOutcome.Created, (await card.SendActionDetailed("확인", "operator(1)")).Outcome);
        Assert.Empty(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPA-003")]
    public async Task should_leave_same_number_detection_card_queue_and_symbol_when_operation_card_is_reported()
    {
        using var rig = new EventMapRig();
        var sensor = rig.Sensor(2103);
        rig.Device(2104, EnumDeviceType.Gate);
        CaptureActions(rig);
        var detection = await rig.DetectAsync(2103);
        var detectionCard = await rig.ShowDetectionCardAsync(detection);
        var card = await ShowOperationCardAsync(rig, 2104, eventId: detection);   // 같은 번호(서버 events 표 공유)
        var statusBefore = sensor.EventStatus;

        await card.SendActionDetailed("확인", "operator(1)");

        Assert.Empty(Cards(rig));
        Assert.Contains(detectionCard, rig.Panel.ViewModelProvider);   // 탐지 카드는 그대로
        Assert.Equal(1, rig.Queue.GetTotalQueueCount());               // 탐지 큐 엔트리도 그대로
        Assert.Equal(statusBefore, sensor.EventStatus);
    }

    // ───────────────────────── 원격 ACTION_REPORT ─────────────────────────

    [Theory]
    [Trait("Scenario", "OPA-004")]
    [InlineData("operation", "Operation")]
    [InlineData("OPERATION", null)]
    [InlineData(null, "Operation")]       // 판별자가 없어도 type_event 로 — 종전엔 탐지로 읽혔다
    public void should_read_operation_kind_from_remote_action_report(string? category, string? type)
        => Assert.Equal(EventCardKind.Operation, EventCardKind.FromActionReport(category, type));

    [Fact]
    [Trait("Scenario", "OPA-005")]
    public async Task should_close_only_operation_card_when_remote_operation_action_report_has_same_number_as_detection()
    {
        using var rig = new EventMapRig();
        rig.Sensor(2105);
        rig.Device(2106, EnumDeviceType.Gate);
        var detection = await rig.DetectAsync(2105);
        var detectionCard = await rig.ShowDetectionCardAsync(detection);
        await ShowOperationCardAsync(rig, 2106, eventId: detection);

        // 호스트 라우터(NatsDomainService)와 같은 판정 — from_event {category_event: operation, type_event: Operation}
        var kind = EventCardKind.FromActionReport("operation", "Operation");
        var outcome = rig.Panel.CloseByRemoteActionReport(kind, detection, EventCardKind.EventTypeOf(kind, "Operation"));

        Assert.Equal(RemoteActionReportOutcome.CardClosed, outcome);
        Assert.Empty(Cards(rig));
        Assert.Contains(detectionCard, rig.Panel.ViewModelProvider);   // 같은 번호 탐지 카드는 안 닫힌다
        Assert.Equal(1, rig.Queue.GetTotalQueueCount());
        Assert.Empty(NatsReports(rig));                                // 남의 조치 — 이 GIS 는 다시 발행하지 않는다

        // 같은 봉투 재배달에도 다시 뜨지 않는다
        await rig.PublishOperationAsync(2106, "GATE_OPEN", "OPEN", detection, envelopeId: $"env-op-{detection}");
        await rig.Panel.FlushPendingCardsNowAsync();
        Assert.Empty(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPA-006")]
    public async Task should_keep_operation_card_when_remote_detection_action_report_has_same_number()
    {
        using var rig = new EventMapRig();
        rig.Device(2107, EnumDeviceType.Gate);
        await ShowOperationCardAsync(rig, 2107, eventId: 66);

        var outcome = rig.Panel.CloseByRemoteActionReport(EventCardKind.FromActionReport("detection", "Intrusion"), 66);

        Assert.Equal(RemoteActionReportOutcome.Nothing, outcome);
        Assert.Single(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPA-007")]
    public async Task should_turn_off_ok_of_open_operation_dialog_only_for_remote_operation_report_of_same_number()
    {
        using var rig = new EventMapRig();
        rig.Device(2108, EnumDeviceType.Gate);
        var card = await ShowOperationCardAsync(rig, 2108, eventId: 67);
        var dialog = new OperationReportDialogViewModel(new EventAggregator(), rig.Log.Object);
        await ((IActivate)dialog).ActivateAsync();
        dialog.UpdateData(card, new Mock<Ironwall.Dotnet.Monitoring.Models.Accounts.IAccountModel>().Object);

        await dialog.HandleAsync(new RemoteActionReportedMessage(ActionReportKind.Detection, 67), CancellationToken.None);
        Assert.True(dialog.CanClickOk);                                // 탐지 67 은 다른 이벤트

        await dialog.HandleAsync(new RemoteActionReportedMessage(EventCardKind.Operation, 67), CancellationToken.None);
        Assert.False(dialog.CanClickOk);
        Assert.Equal(EventReportDialogViewModel.HANDLED_ELSEWHERE_TEXT, dialog.DialogNotice);
    }

    // ───────────────────────── [전체 조치보고] · 6.3 ─────────────────────────

    [Fact]
    [Trait("Scenario", "OPA-008")]
    public async Task should_report_every_operation_card_and_publish_one_nats_each_when_report_all_runs()
    {
        using var rig = new EventMapRig();
        rig.Device(2109, EnumDeviceType.Gate);
        rig.Device(2110, EnumDeviceType.Enclosure);
        var bodies = CaptureActions(rig);
        await ShowOperationCardAsync(rig, 2109, eventId: 68);
        await ShowOperationCardAsync(rig, 2110, eventId: 69, reason: "ENCLOSURE_TEMP_HIGH");

        await rig.Panel.HandleAsync(new CallAllEventReportMessageModel(), CancellationToken.None);

        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(new[] { 68, 69 }, bodies.Select(b => b.FromEventId).OrderBy(i => i));
        Assert.All(bodies, b => Assert.Equal("일괄처리", b.Content));
        Assert.Equal(new[] { 68, 69 }, NatsReports(rig).Select(n => n.EventId).OrderBy(i => i));
        Assert.All(NatsReports(rig), n => Assert.Equal(EnumEventType.Operation, n.EventType));
    }

    [Fact]
    [Trait("Scenario", "OPA-009")]
    public async Task should_hide_report_and_only_clear_operation_card_when_server_is_6_3()
    {
        using var rig = new EventMapRig { ServerContract = Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract.V6_3 };
        rig.Device(2111, EnumDeviceType.Gate);
        var bodies = CaptureActions(rig);
        var card = await ShowOperationCardAsync(rig, 2111, eventId: 70);

        Assert.False(card.IsReportSupported);                          // 카드 [조치보고] 단추가 숨는다(XAML Visibility 바인딩 — OPC-014)
        await rig.Panel.ExecuteBatchReportAsync();

        Assert.Empty(rig.Panel.ViewModelProvider);                     // 종전대로 치우기만
        Assert.Empty(bodies);                                          // 운영 경로가 없는 서버에 조치를 보내지 않는다
        Assert.Empty(NatsReports(rig));
    }

    [Fact]
    [Trait("Scenario", "OPA-009")]
    public void should_hide_report_when_contract_probe_is_missing()
    {
        var previous = IoC.GetInstance;
        try
        {
            IoC.GetInstance = (_, _) => null!;
            var card = new OperationEventCardViewModel(new EventAggregator(), new Mock<Ironwall.Dotnet.Libraries.Base.Services.ILogService>().Object,
                                                       new OperationEventModel { Id = 1, Reason = "GATE_OPEN" });
            Assert.False(card.IsReportSupported);                      // EventApiService 와 같은 보수적 기본값(6.3)
        }
        finally { IoC.GetInstance = previous; }
    }

    // ───────────────────────── 이벤트 콘솔 '운영' 레일 ─────────────────────────

    private static (EventDashboardViewModel Console, EventAggregator Ea) OpenConsole(EventMapRig rig)
    {
        var ea = new EventAggregator();
        ea.SubscribeOnPublishedThread(rig.Panel);   // 호스트: 카드 목록은 같은 EA 를 듣는다
        var log = rig.Log.Object;
        var devices = new DeviceProvider();
        var events = new EventProvider();
        var service = new EventProviderService(log, rig.EventApi.Object, devices, events);
        var console = new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, service, devices, events),
            new MalfunctionEventPanelViewModel(ea, log, service, devices, events),
            new ConnectionEventPanelViewModel(ea, log, service, devices, events),
            new ActionEventPanelViewModel(ea, log, service, events),
            new EventInfoViewModel(devices, events, service, ea, log),
            new CameraEventInfoViewModel(events, ea, log),
            new DataChartPanelViewModel(ea, log, service),
            operationEventPanelViewModel: new OperationEventPanelViewModel(ea, log, service));
        console.UseUiThread(ImmediateUiThread.Instance);
        ea.SubscribeOnPublishedThread(console);
        return (console, ea);
    }

    private static OperationEventViewModel ListRow(EventMapRig rig, EventDashboardViewModel console, int eventId, int deviceId)
    {
        var model = new OperationEventModel { Id = eventId, Reason = "GATE_OPEN", DateTime = DateTime.Now, Device = rig.ServerDevices[deviceId] };
        var row = new OperationEventViewModel(model);
        console.OperationPanelViewModel!.ViewModelProvider.Add(row);   // 콘솔 운영 목록에 그 행이 있다(서버 조회 결과)
        return row;
    }

    private static void ServeDialog(OperationReportDialogViewModel dialog)
    {
        var inner = IoC.GetInstance;
        IoC.GetInstance = (type, key) => type == typeof(OperationReportDialogViewModel) ? dialog : inner(type, key);
    }

    private static async Task WaitUntil(Func<bool> condition, string what)
    {
        var ok = await Task.Run(() => SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)));
        Assert.True(ok, what);
    }

    [Fact]
    [Trait("Scenario", "OPA-010")]
    public async Task should_report_operation_row_through_its_dialog_and_close_the_map_card()
    {
        using var rig = new EventMapRig();
        rig.Device(2112, EnumDeviceType.Gate);
        CaptureActions(rig);
        await ShowOperationCardAsync(rig, 2112, eventId: 71);
        var (console, ea) = OpenConsole(rig);
        var dialog = new OperationReportDialogViewModel(ea, rig.Log.Object);
        ServeDialog(dialog);
        var opened = new List<OpenEventReportDialogMessageModel>();
        ea.SubscribeOnPublishedThread(new OpenSink(opened));
        var nats = new NatsSink();
        ea.SubscribeOnPublishedThread(nats);
        var row = ListRow(rig, console, 71, 2112);

        console.OperationPanelViewModel!.ReportCommand.Execute(row);   // 행 우클릭 · 상세 [조치보고]
        await WaitUntil(() => opened.Count == 1, "조치 창 열기 요청이 없다");
        Assert.Equal(OperationReportDialogViewModel.EventType, opened[0].EventType);
        Assert.Equal(71, dialog.Operation!.Model.Id);

        await ((IActivate)dialog).ActivateAsync();
        dialog.ClickOk();                                              // 첫 문구로 [확인]
        await WaitUntil(() => rig.ServerActions.Contains(71) && !dialog.IsSending, "조치보고가 끝나지 않았다");

        Assert.Empty(Cards(rig));                                      // 지도 옆 카드도 닫힘
        Assert.True(row.IsActionReported);                             // 콘솔 행 '조치 있음'
        await WaitUntil(() => nats.Seen.Count == 1, "ACTION_REPORT 발행 요청이 없다");
        Assert.Equal(EnumEventType.Operation, nats.Seen[0].EventType);
    }

    [Fact]
    [Trait("Scenario", "OPA-011")]
    public async Task should_put_operation_rows_in_the_tray_and_apply_them_like_detection_rows()
    {
        using var rig = new EventMapRig();
        rig.Device(2113, EnumDeviceType.Gate);
        var bodies = CaptureActions(rig);
        await ShowOperationCardAsync(rig, 2113, eventId: 72);
        var (console, ea) = OpenConsole(rig);
        var nats = new NatsSink();
        ea.SubscribeOnPublishedThread(nats);
        var row = ListRow(rig, console, 72, 2113);

        var candidate = ActionTrayCandidateFactory.From(row)!.Value;
        Assert.Equal(ActionTrayDrop.KindOperation, candidate.Kind);
        console.Tray.Enqueue(ActionTrayDrop.Plan(new[] { candidate }, canControl: true));   // 끌어 담기 · 툴바 [트레이에 담기] 와 같은 판정
        console.Tray.Phrase = ActionTrayViewModel.Phrases[0];
        var summary = await console.ApplyTrayAsync();

        Assert.Equal(1, summary.Applied);
        Assert.Equal(72, Assert.Single(bodies).FromEventId);
        Assert.Empty(Cards(rig));
        Assert.Equal(EnumTrueFalse.True, row.Model!.Status);
        await WaitUntil(() => nats.Seen.Count == 1, "ACTION_REPORT 발행 요청이 없다");
    }

    [Fact]
    [Trait("Scenario", "OPA-012")]
    public async Task should_mark_operation_row_reported_when_another_gis_reports_it()
    {
        using var rig = new EventMapRig();
        rig.Device(2114, EnumDeviceType.Gate);
        var (console, _) = OpenConsole(rig);
        var row = ListRow(rig, console, 73, 2114);

        await console.HandleAsync(new RemoteActionReportedMessage(ActionReportKind.Detection, 73), CancellationToken.None);
        Assert.False(row.IsActionReported);                            // 탐지 73 은 다른 이벤트

        await console.HandleAsync(new RemoteActionReportedMessage(EventCardKind.Operation, 73), CancellationToken.None);
        Assert.True(row.IsActionReported);
    }

    [Fact]
    [Trait("Scenario", "OPA-013")]
    public void should_wire_operation_rail_and_dialog_like_detection_and_malfunction()
    {
        var dashboard = Xaml(Path.Combine("Dashboards", "EventDashboardView.xaml"));
        Assert.Contains("Data.OperationPanelViewModel.ReportCommand", dashboard);       // 행 우클릭 '조치보고'
        Assert.Contains("{StaticResource OperationRowMenu}", dashboard);

        var dialog = Xaml(Path.Combine("Dialogs", "OperationReportDialogView.xaml"));
        foreach (var id in new[] { "Events.OperationActionReport.Root", "Events.OperationActionReport.Ok", "Events.OperationActionReport.Cancel",
                                   "Events.OperationActionReport.Etc", "Events.OperationActionReport.Memo" })
            Assert.Contains(id, dialog);
        Assert.Contains("x:Name=\"ClickOk\"", dialog);                 // CanClickOk 가드(Caliburn 지시자)
        Assert.Contains("x:Name=\"ClickCancel\"", dialog);
    }

    private static string Xaml(string relative, [CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", relative));

    private sealed class NatsSink : IHandle<SendActionRequestMessage>
    {
        public readonly List<SendActionRequestMessage> Seen = new();
        public Task HandleAsync(SendActionRequestMessage message, CancellationToken cancellationToken)
        {
            lock (Seen) Seen.Add(message);
            return Task.CompletedTask;
        }
    }

    // ───────────────────────── 카드 [X] = '운영상태 확인' 조치보고 ─────────────────────────

    [Fact]
    [Trait("Scenario", "OPA-014")]
    public async Task should_post_operation_check_action_then_close_card_when_x_is_pressed()
    {
        using var rig = new EventMapRig();
        rig.Device(2121, EnumDeviceType.Gate);
        var bodies = CaptureActions(rig);
        var card = await ShowOperationCardAsync(rig, 2121, eventId: 81);

        await rig.Panel.CloseOperationCardWithReportAsync(card);

        var body = Assert.Single(bodies);
        Assert.Equal(81, body.FromEventId);
        Assert.Equal("운영상태 확인", body.Content);
        Assert.Empty(Cards(rig));
        Assert.Equal(81, Assert.Single(NatsReports(rig)).EventId);

        await rig.PublishOperationAsync(2121, "GATE_OPEN", "OPEN", 81, envelopeId: "env-op-81");   // 재배달
        await rig.Panel.FlushPendingCardsNowAsync();
        Assert.Empty(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPA-015")]
    public async Task should_keep_card_when_x_report_is_refused_by_server()
    {
        using var rig = new EventMapRig();
        rig.Device(2122, EnumDeviceType.Enclosure);
        CaptureActions(rig, success: false);
        var card = await ShowOperationCardAsync(rig, 2122, eventId: 82, reason: "ENCLOSURE_DOOR_OPEN");

        await rig.Panel.CloseOperationCardWithReportAsync(card);

        Assert.Single(Cards(rig));                                     // 조치가 저장되지 않았으니 카드를 남긴다
        Assert.Empty(NatsReports(rig));
    }

    [Fact]
    [Trait("Scenario", "OPA-016")]
    public async Task should_only_clear_card_without_action_when_x_is_pressed_on_6_3_server()
    {
        using var rig = new EventMapRig { ServerContract = Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract.V6_3 };
        rig.Device(2123, EnumDeviceType.Gate);
        var bodies = CaptureActions(rig);
        var card = await ShowOperationCardAsync(rig, 2123, eventId: 83);

        await rig.Panel.CloseOperationCardWithReportAsync(card);

        Assert.Empty(Cards(rig));
        Assert.Empty(bodies);                                          // 운영 조치를 못 받는 서버엔 보내지 않는다
    }

    private sealed class OpenSink : IHandle<OpenEventReportDialogMessageModel>
    {
        private readonly List<OpenEventReportDialogMessageModel> _seen;
        public OpenSink(List<OpenEventReportDialogMessageModel> seen) => _seen = seen;
        public Task HandleAsync(OpenEventReportDialogMessageModel message, CancellationToken cancellationToken)
        {
            lock (_seen) _seen.Add(message);
            return Task.CompletedTask;
        }
    }
}
