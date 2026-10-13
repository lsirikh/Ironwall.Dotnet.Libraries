using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using MaterialDesignThemes.Wpf;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 운영 이벤트 → 이벤트 패널 운영 카드 헤드리스 시나리오(docs/prds/operation-event-card-prd.md §9)
                  EventMapRig(실제 큐 + 실제 수신 서비스 + 실제 카드 패널, 호스트 배선과 같게)로 OPERATION_EVENT 를 보낸다. 창은 띄우지 않는다.
   Created By   : Claude
   Created On   : 2026-10-13
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // 카드 패널이 IoC.GetInstance(전역 정적)를 읽는다
public class OperationEventCardScenarioTests
{
    private static List<OperationEventCardViewModel> Cards(EventMapRig rig)
        => rig.Panel.ViewModelProvider.OfType<OperationEventCardViewModel>().ToList();

    private static async Task<List<OperationEventCardViewModel>> SendAndFlushAsync(EventMapRig rig, int deviceId, string reason,
                                                                                    string? doorStatus = null, int eventId = 1, string? severity = null)
    {
        await rig.PublishOperationAsync(deviceId, reason, doorStatus, eventId, severity: severity);
        await rig.Panel.FlushPendingCardsNowAsync();
        return Cards(rig);
    }

    [Fact]
    [Trait("Scenario", "OPC-001,OPC-010")]
    public async Task should_show_operation_card_and_open_door_without_queue_count_or_sound_when_gate_opens()
    {
        using var rig = new EventMapRig();
        var gate = rig.Device(1601, EnumDeviceType.Gate);

        var cards = await SendAndFlushAsync(rig, 1601, "GATE_OPEN", "OPEN", eventId: 31);

        var card = Assert.Single(cards);
        Assert.Equal("통문 열림", card.ReasonText);
        Assert.True(card.IsDoor);
        Assert.Equal(1601, card.Device?.Id);
        Assert.Equal(EnumDoorState.Open, gate.DoorState);   // 지도 문 상태도 같이
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());    // 큐 · 신호등 · 소리에 넣지 않는다(PRD §4)
        Assert.Equal((-1, -1), rig.Counts);
        Assert.Empty(rig.SoundPlays);
    }

    [Fact]
    [Trait("Scenario", "OPC-002")]
    public async Task should_show_closed_card_and_close_door_when_enclosure_door_closes()
    {
        using var rig = new EventMapRig();
        var enclosure = rig.Device(1602, EnumDeviceType.Enclosure);

        var cards = await SendAndFlushAsync(rig, 1602, "ENCLOSURE_DOOR_CLOSED", "CLOSED", eventId: 32);

        Assert.Equal("함체 문 닫힘", Assert.Single(cards).ReasonText);
        Assert.Equal(EnumDoorState.Closed, enclosure.DoorState);
    }

    [Fact]
    [Trait("Scenario", "OPC-003")]
    public async Task should_show_warning_card_without_door_change_when_environment_alarm_arrives()
    {
        using var rig = new EventMapRig();
        var enclosure = rig.Device(1603, EnumDeviceType.Enclosure);

        var cards = await SendAndFlushAsync(rig, 1603, "ENCLOSURE_TEMP_HIGH", eventId: 33, severity: "WARNING");

        var card = Assert.Single(cards);
        Assert.Equal("함체 고온", card.ReasonText);
        Assert.True(card.IsWarning);
        Assert.Equal("경고", card.SeverityText);
        Assert.False(card.IsDoor);
        Assert.Equal(EnumDoorState.Unknown, enclosure.DoorState);
    }

    [Fact]
    [Trait("Scenario", "OPC-004")]
    public async Task should_show_raw_reason_when_reason_is_unknown()
    {
        using var rig = new EventMapRig();
        rig.Device(1604, EnumDeviceType.Enclosure);

        var cards = await SendAndFlushAsync(rig, 1604, "ENCLOSURE_SMOKE_DETECTED", eventId: 34);

        var card = Assert.Single(cards);
        Assert.Equal("ENCLOSURE_SMOKE_DETECTED", card.ReasonText);
        Assert.False(card.IsWarning);
    }

    [Fact]
    [Trait("Scenario", "OPC-004")]
    public async Task should_show_card_with_deleted_device_text_when_device_is_null()
    {
        using var rig = new EventMapRig();

        await rig.PublishRawAsync("""{ "id": "op-null", "m_type": "PUB", "cmd": "OPERATION_EVENT", "body": { "id": 35, "reason": "GATE_OPEN", "device": null, "detail": null } }""");
        await rig.Panel.FlushPendingCardsNowAsync();

        var card = Assert.Single(Cards(rig));
        Assert.Null(card.Device);
        Assert.Equal(OperationEventCardViewModel.DELETED_DEVICE_TEXT, card.ZoneDeviceText);
    }

    [Fact]
    [Trait("Scenario", "OPC-005")]
    public async Task should_show_one_card_when_same_envelope_is_published_three_times()
    {
        using var rig = new EventMapRig();
        rig.Device(1605, EnumDeviceType.Gate);

        for (int i = 0; i < 3; i++) await rig.PublishOperationAsync(1605, "GATE_OPEN", "OPEN", eventId: 36, envelopeId: "env-op-same");
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.Single(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPC-006")]
    public async Task should_show_one_card_when_same_event_is_resent_with_new_envelope_across_flushes()
    {
        using var rig = new EventMapRig();
        rig.Device(1606, EnumDeviceType.Gate);

        await SendAndFlushAsync(rig, 1606, "GATE_OPEN", "OPEN", eventId: 37);
        var cards = await SendAndFlushAsync(rig, 1606, "GATE_OPEN", "OPEN", eventId: 37);   // 매니저 재전송 — 봉투 id 만 새것

        Assert.Single(cards);
    }

    [Fact]
    [Trait("Scenario", "OPC-007")]
    public async Task should_put_newest_operation_card_on_top_when_two_arrive()
    {
        using var rig = new EventMapRig();
        rig.Device(1607, EnumDeviceType.Gate);

        await SendAndFlushAsync(rig, 1607, "GATE_OPEN", "OPEN", eventId: 41);
        var cards = await SendAndFlushAsync(rig, 1607, "GATE_CLOSED", "CLOSED", eventId: 42);

        Assert.Equal(new[] { 42, 41 }, cards.Select(c => c.Model.Id));
    }

    [Fact]
    [Trait("Scenario", "OPC-008")]
    public async Task should_wait_in_batch_buffer_when_not_yet_flushed()
    {
        using var rig = new EventMapRig();
        rig.Device(1608, EnumDeviceType.Gate);

        await rig.PublishOperationAsync(1608, "GATE_OPEN", "OPEN", eventId: 43);

        Assert.Empty(Cards(rig));
        Assert.Equal(1, rig.Panel.PendingCardCount);
        await rig.Panel.FlushPendingCardsNowAsync();
        Assert.Single(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPC-009")]
    public async Task should_close_card_without_server_action_and_not_reappear_when_envelope_is_redelivered()
    {
        using var rig = new EventMapRig();
        rig.Device(1609, EnumDeviceType.Gate);
        var card = Assert.Single(await SendAndFlushAsync(rig, 1609, "GATE_OPEN", "OPEN", eventId: 44));

        rig.Panel.CloseOperationCard(card);
        var after = await SendAndFlushAsync(rig, 1609, "GATE_OPEN", "OPEN", eventId: 44);   // 브로커 재배달

        Assert.Empty(after);
        Assert.Empty(rig.ServerActions);       // 조치 대상 아님
        Assert.Empty(rig.ActionReported);      // 이벤트 창 닫기 신호도 없다
    }

    [Fact]
    [Trait("Scenario", "OPC-011")]
    public async Task should_report_operation_card_to_server_too_when_batch_report_runs()
    {
        // v1.1 — 서버 FR-07: 운영도 조치 대상이라 [전체 조치보고] 가 운영 카드에도 서버 조치를 만든다(v1.0 은 치우기만 했다).
        using var rig = new EventMapRig();
        rig.Sensor(1611);
        rig.Device(1612, EnumDeviceType.Gate);
        var detection = await rig.DetectAsync(1611);
        await rig.ShowDetectionCardAsync(detection);
        await SendAndFlushAsync(rig, 1612, "GATE_OPEN", "OPEN", eventId: detection);   // 같은 번호 — 종류로 가른다

        await rig.Panel.ExecuteBatchReportAsync();

        Assert.Equal(new[] { detection, detection }, rig.ServerActions.OrderBy(i => i));   // 운영 · 탐지 각각 한 건
        Assert.Empty(rig.Panel.ViewModelProvider);
    }

    [Fact]
    [Trait("Scenario", "OPC-012")]
    public async Task should_keep_operation_card_when_remote_action_report_has_same_number()
    {
        using var rig = new EventMapRig();
        rig.Device(1613, EnumDeviceType.Gate);
        await SendAndFlushAsync(rig, 1613, "GATE_OPEN", "OPEN", eventId: 50);

        var byDetection = rig.Panel.CloseByRemoteActionReport(ActionReportKind.Detection, 50);
        var byUnknownKind = rig.Panel.CloseByRemoteActionReport(null, 50);

        Assert.Equal(RemoteActionReportOutcome.Nothing, byDetection);
        Assert.Equal(RemoteActionReportOutcome.Nothing, byUnknownKind);
        Assert.Single(Cards(rig));
    }

    [Fact]
    [Trait("Scenario", "OPC-013")]
    public async Task should_request_map_locate_when_operation_card_is_double_clicked()
    {
        using var rig = new EventMapRig();
        rig.Device(1614, EnumDeviceType.Gate);
        var card = Assert.Single(await SendAndFlushAsync(rig, 1614, "GATE_OPEN", "OPEN", eventId: 51));

        var sent = await rig.Panel.LocateCardAsync(card);

        Assert.True(sent);
        var request = Assert.Single(rig.Published.OfType<MapLocateRequest>());
        Assert.Equal(new[] { 1614 }, request.DeviceIds);
    }

    [Fact]
    [Trait("Scenario", "OPC-014")]
    public async Task should_mark_operation_card_with_report_and_close_buttons()
    {
        using var rig = new EventMapRig();
        rig.Device(1615, EnumDeviceType.Gate);
        var card = Assert.Single(await SendAndFlushAsync(rig, 1615, "GATE_OPEN", "OPEN", eventId: 52));

        Assert.Equal("Events.Card.Operation.52", card.AutomationKey);
        Assert.Equal("Events.Card.Operation.52.Close", card.CloseAutomationKey);
        Assert.Equal("Events.Card.Operation.52.Report", card.ReportAutomationKey);

        var xaml = OperationCardXaml();
        Assert.Contains("AutomationProperties.AutomationId=\"{Binding AutomationKey, Mode=OneWay}\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"{Binding CloseAutomationKey, Mode=OneWay}\"", xaml);
        Assert.Contains("[Action OnButtonCloseCard($source, $eventArgs)]", xaml);
        // v1.1 — 조치보고 단추가 생겼다(서버가 운영 조치를 받는 판본에서만 보인다).
        Assert.Contains("AutomationProperties.AutomationId=\"{Binding ReportAutomationKey, Mode=OneWay}\"", xaml);
        Assert.Contains("[Action OnButtonAction($source, $eventArgs)]", xaml);
        Assert.Contains("Visibility=\"{Binding IsReportSupported, Mode=OneWay, Converter={StaticResource BoolToVis}}\"", xaml);
        foreach (var kind in new[] { "InformationOutline", "DoorOpen", "AlertOutline", "Close", "CommentOutline" })
            Assert.True(Enum.TryParse<PackIconKind>(kind, out _), kind);
    }

    [Fact]
    [Trait("Scenario", "OPC-015")]
    public async Task should_cap_displayed_cards_at_500_dropping_oldest_operation_card()
    {
        using var rig = new EventMapRig();
        rig.Device(1616, EnumDeviceType.Gate);

        for (int id = 1; id <= 501; id++)
        {
            await rig.PublishOperationAsync(1616, "GATE_OPEN", "OPEN", eventId: id);
            if (id % 100 == 0) await rig.Panel.FlushPendingCardsNowAsync();   // 묶음 버퍼 상한(200) 안에서 비운다
        }
        await rig.Panel.FlushPendingCardsNowAsync();

        var cards = Cards(rig);
        Assert.Equal(500, cards.Count);
        Assert.DoesNotContain(cards, c => c.Model.Id == 1);
        Assert.Equal(501, cards[0].Model.Id);
    }

    [Fact]
    public async Task should_still_move_door_when_service_is_built_with_old_constructor_without_cards()
    {
        // 옛 생성자(카드 없이 개폐만) — 호스트가 새 생성자로 옮기기 전 동작 보존.
        Func<Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel, Task>? handler = null;
        var nats = new Moq.Mock<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += Moq.It.IsAny<Func<Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel, Task>>())
            .Callback<Func<Ironwall.Dotnet.Libraries.Nats.Models.MessageArgsModel, Task>>(h => handler = h);
        var symbols = new Moq.Mock<Ironwall.Dotnet.Libraries.Events.Ui.Managers.ISymbolEventManager>();
        await new OperationEventNatsSyncService(null, nats.Object, symbols.Object).StartService();

        await handler!(new(subject: "s", subscriptionSubject: null, data: """{ "cmd": "OPERATION_EVENT", "m_type": "PUB", "body": { "id": 9, "reason": "GATE_OPEN", "device": { "id": 3, "category_device": "gate" } } }"""));

        symbols.Verify(s => s.SetDoorState(3, EnumDeviceType.Gate, EnumDoorState.Open), Moq.Times.Once);
    }

    private static string OperationCardXaml([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", "Events", "OperationEventCardView.xaml"));
}
