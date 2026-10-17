using System.IO;
using System.Runtime.CompilerServices;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 카탈로그 §2 EVM 의 「범위 밖 · 헤디드 남김」 으로 남았던 항목을 다시 본 헤드리스 시나리오(2026-10-11)
                  - 카드 글(그룹 - 장비타입 번호) · 제어기 장애 카드 · 자동 지도 이동 없음 · 청록 Connection 상태 없음 ·
                    배열 봉투 · 셸 배지 원천(UpdateAction) · 패널 활성 시점과 카드 쌓임 · 조치 창 [취소] · 조치 창이 열린 채 버스트 ·
                    이벤트 콘솔 트레이 · 행 조치 · 전체 조치 → 지도 카드 닫힘
                  - 장치는 EventMapRig(가짜 서버 → 실제 NATS 수신 서비스 → 실제 큐 · 심볼 관리자 · 카드 패널) 그대로.
                    콘솔은 호스트 EA 대역(진짜 EventAggregator)에 카드 패널을 구독시켜 호스트와 같은 길로 잇는다.
                  - 이벤트는 "가짜 API 기록 먼저 → 같은 번호의 NATS 봉투". 탐지 · 장애는 조치보고 전 같은 대상 중복 송신 없음.
                  - 창을 띄우지 않는다(Window.Show · ShowDialog 없음).
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // 카드 · 조치 창 · 콘솔이 IoC.GetInstance(전역 정적)를 읽는다
public class EventLeftoverScenarioTests
{
    // ───────────────────────── 장치 보조 ─────────────────────────

    /// <summary>
    /// 카드 · 조치 창이 IoC 로 찾는 것(이벤트 API · 조치 자물쇠 · 그룹 이름표 등)을 이 시험의 가짜로 덧댄다.
    /// rig 가 처음 IoC 를 기억해 두었다가 Dispose 때 되돌리므로 여기서 덮어써도 다음 시험에 새지 않는다.
    /// </summary>
    private static void Serve(EventMapRig rig, params (Type Type, object Instance)[] extra)
    {
        var inner = IoC.GetInstance;
        var guard = new ActionReportGuard();
        IoC.GetInstance = (type, key) =>
        {
            foreach (var (t, o) in extra) if (t == type) return o;
            if (type == typeof(IEventApiService)) return rig.EventApi.Object;
            if (type == typeof(IActionReportGuard)) return guard;
            if (type == typeof(DeviceProvider)) return rig.Devices;   // 조치 창 장비 칸(DetectionSelectionViewModel) — 호스트는 늘 등록돼 있다
            return inner(type, key);
        };
    }

    /// <summary>호스트(NatsDomainService)가 하듯 카드를 묶음 버퍼에만 넣는다 — 목록에 올리는 것은 패널 타이머 몫.</summary>
    private static DetectionEventCardViewModel EnqueueOnly(EventMapRig rig, int eventId)
    {
        var (deviceId, _) = rig.ServerEvents[(ActionReportKind.Detection, eventId)];
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(eventId);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        model.SetupGet(m => m.Device).Returns(rig.ServerDevices[deviceId]);
        model.SetupProperty(m => m.Status);
        var card = new DetectionEventCardViewModel(model.Object);
        rig.Panel.EnqueueCard(card);
        return card;
    }

    private static async Task WaitUntil(Func<bool> condition, string what)
    {
        var ok = await Task.Run(() => SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)));
        Assert.True(ok, what);
    }

    private static string Xaml(string relative, [CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", relative));

    // ───────────────────────── A. 카드 글 ─────────────────────────

    [Theory]
    [Trait("Scenario", "EVM-003,EVM-018")]
    [InlineData(new[] { 7 }, "GOP-1구역 - 센서 3")]                 // EVM-003 그룹 센서
    [InlineData(new[] { 7, 8 }, "GOP-1구역, GOP-2구역 - 센서 3")]    // 두 그룹이면 이름을 잇는다
    [InlineData(new int[0], "센서 3")]                               // EVM-018 그룹 없음 — 구분자 " - " 없음
    public async Task should_write_group_dash_type_and_number_on_the_card_and_drop_the_dash_without_group_evm003_018(int[] groups, string expected)
    {
        using var rig = new EventMapRig();
        var names = new DeviceGroupProvider(rig.Log.Object);
        names.Add(new DeviceGroupModel { Id = 7, Name = "GOP-1구역" });
        names.Add(new DeviceGroupModel { Id = 8, Name = "GOP-2구역" });
        Serve(rig, (typeof(DeviceGroupProvider), names));
        rig.Sensor(3001, groups: groups);
        ((SensorDeviceModel)rig.ServerDevices[3001]).DeviceNumber = 3;

        var id = await rig.DetectAsync(3001);
        var card = await rig.ShowDetectionCardAsync(id);

        Assert.Equal(expected, card.ZoneDeviceText);
        var view = Xaml("Events/DetectionEventCardView.xaml");
        Assert.Contains("Text=\"탐지 이벤트\"", view);                     // 카드 머리 「탐지 이벤트」
        Assert.Contains("{Binding ZoneDeviceText", view);                  // 1행이 바로 이 글을 그린다
        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Empty(rig.Panel.ViewModelProvider);
    }

    // ───────────────────────── C. 제어기 장애 카드 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-063")]
    public async Task should_list_controller_fault_card_with_korean_reason_controller_number_and_section_one_to_three_evm063()
    {
        using var rig = new EventMapRig();
        var (controller, symbol) = rig.Controller(4101);
        controller.DeviceNumber = 3;
        var id = rig.RecordMalfunction(4101, "FAULT_CONTROLLER");
        var json = $$"""
            { "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "MALFUNCTION", "from": "PidsProxy",
              "body": { "id": {{id}}, "category_event": "malfunction", "type_event": "Fault", "action_reported": false,
                        "reason": "FAULT_CONTROLLER", "device": { "id": 4101, "category_device": "controller" },
                        "detail": { "first_start": 1, "first_end": 3, "second_start": 0, "second_end": 0 } } }
            """;
        await rig.PublishRawAsync(json);

        // 호스트(ProcessFault)가 하는 일 — 같은 body 를 DTO → 모델 → 카드로(라이브러리 변환기)
        var dto = JObject.Parse(json)["body"]!.ToObject<MalfunctionEventDto>()!;
        var card = new MalfunctionEventCardViewModel(dto.ToMalfunctionEventModel(rig.Devices));
        rig.Panel.EnqueueCard(card);
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.Same(card, Assert.Single(rig.Panel.ViewModelProvider));
        Assert.Equal($"Events.Card.Malfunction.{id}", card.AutomationKey);
        Assert.Equal(EnumFaultType.FAULT_CONTROLLER, card.Reason);
        Assert.Equal("제어기 장애", Ironwall.Dotnet.Libraries.Events.Ui.Converters.EnumKoreanMap.To(card.Reason));
        Assert.Equal(3, card.ControllerDisplay);   // 제어기 칸 = 제어기 자기 번호
        Assert.Null(card.SensorDisplay);           // 센서 칸 비움
        Assert.Equal((1, 3), (card.FirstStart, card.FirstEnd));
        Assert.Equal("제어기 3", card.ZoneDeviceText);
        Assert.NotNull(card.EntryId);              // 큐 엔트리와 이어졌다 — 조치하면 블랙아웃이 풀린다
        Assert.Equal((0, 1), rig.Counts);

        rig.RemoteReport(ActionReportKind.Malfunction, id);
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal((0, 0), rig.Counts);
    }

    // ───────────────────────── A. 자동 지도 이동 없음 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-026")]
    public async Task should_not_ask_the_map_to_move_when_events_arrive_and_only_when_the_card_is_double_clicked_evm026()
    {
        using var rig = new EventMapRig();
        rig.Sensor(3101, groups: 4);
        rig.Sensor(3102, groups: 4);

        var detection = await rig.DetectAsync(3101);
        var fault = await rig.MalfunctionAsync(3102);
        var card = await rig.ShowDetectionCardAsync(detection);
        await rig.ShowMalfunctionCardAsync(fault);

        Assert.DoesNotContain(rig.Published, m => m is MapLocateRequest);   // 이벤트가 와도 지도는 스스로 움직이지 않는다

        Assert.True(await rig.Panel.LocateCardAsync(card));                  // 카드 더블클릭 길만
        var request = Assert.Single(rig.Published.OfType<MapLocateRequest>());
        Assert.Equal(new[] { 3101 }, request.DeviceIds);

        rig.RemoteReport(ActionReportKind.Detection, detection);
        rig.RemoteReport(ActionReportKind.Malfunction, fault);
    }

    // ───────────────────────── E. 청록 Connection 상태는 만들어지지 않는다 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-081")]
    public async Task should_never_produce_the_cyan_connection_state_on_symbol_or_zone_evm081()
    {
        // 큐의 단일 우선순위 지점은 8 조합 어느 것에서도 Connection 을 내지 않는다.
        foreach (var blackout in new[] { false, true })
            foreach (var fault in new[] { false, true })
                foreach (var detection in new[] { false, true })
                    Assert.NotEqual(EnumCompositeEventStatus.Connection, ControllerBlackoutModel.Resolve(blackout, fault, detection));

        using var rig = new EventMapRig();
        var symbol = rig.Sensor(3201, groups: 5);
        var zone = rig.Zone(5);
        void AssertNoCyan(string step)
        {
            Assert.True(symbol.EventStatus != EnumEventStatus.Connection && symbol.CompositeStatus != EnumCompositeEventStatus.Connection, $"{step}: 심볼이 Connection");
            Assert.True(zone.EventStatus != EnumEventStatus.Connection && zone.CompositeStatus != EnumCompositeEventStatus.Connection, $"{step}: 구역선이 Connection");
        }

        await rig.PublishRawAsync("""{ "id": "con-1", "m_type": "PUB", "cmd": "CONNECTION", "body": { "id": 1, "device": { "id": 3201, "category_device": "sensor" } } }""");
        AssertNoCyan("CONNECTION");
        foreach (var status in new[] { EnumDeviceStatus.ERROR, EnumDeviceStatus.DEACTIVATED, EnumDeviceStatus.ACTIVATED })
        {
            rig.Symbols.SyncDeviceStatus(3201, EnumDeviceType.Fence, status);
            AssertNoCyan($"SYNC_DEVICE {status}");
        }
        var detect = await rig.DetectAsync(3201);
        AssertNoCyan("탐지");
        rig.RemoteReport(ActionReportKind.Detection, detect);
        var malfunction = await rig.MalfunctionAsync(3201);
        AssertNoCyan("장애");
        rig.RemoteReport(ActionReportKind.Malfunction, malfunction);
        AssertNoCyan("조치 뒤");
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    // ───────────────────────── A. 배열 봉투 [DETECT, ACTION_REPORT] ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-035")]
    public async Task should_split_array_envelope_take_only_detect_item_and_leave_no_card_once_its_report_is_routed_evm035()
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(3301, groups: 6);
        var id = rig.RecordDetection(3301);
        var detect = rig.DetectJson(id);
        var report = $$"""
            { "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "ACTION_REPORT", "from": "GIS",
              "body": { "id": 88001, "type_event": "Action", "content": "현장 확인", "user": "kim(1234)",
                        "from_event": { "id": {{id}}, "category_event": "detection", "action_reported": true,
                                        "device": { "id": 3301, "category_device": "sensor" } } } }
            """;

        await rig.PublishRawAsync($"[{detect},{report}]");

        // 라이브러리 수신 서비스: 배열을 항목마다 나눠 DETECT 만 큐에 넣는다(ACTION_REPORT 항목을 탐지로 오인하지 않는다).
        Assert.Equal(1, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        rig.Log.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);

        // 호스트 라우터가 같은 배열을 순서대로: DETECT → 카드(버퍼) · ACTION_REPORT → 패널 원격 종결
        EnqueueOnly(rig, id);
        rig.RemoteReport(ActionReportKind.Detection, id);
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.Empty(rig.Panel.ViewModelProvider);                 // 카드 없음
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal((0, 0), rig.Counts);
    }

    // ───────────────────────── A. 셸 배지 원천 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-010")]
    public async Task should_raise_update_action_with_two_cards_listed_when_two_detections_arrive_evm010()
    {
        // 셸 배지 TotalEvents = 카드 목록 수를 UpdateAction 마다 다시 센다(호스트 ShellViewModel) — 라이브러리 쪽 원천만 본다.
        using var rig = new EventMapRig();
        rig.Sensor(3401);
        rig.Sensor(3402);
        var seen = new List<int>();
        rig.Panel.UpdateAction += () => seen.Add(rig.Panel.ViewModelProvider.Count);

        var a = await rig.DetectAsync(3401);
        var b = await rig.DetectAsync(3402);
        EnqueueOnly(rig, a);
        EnqueueOnly(rig, b);
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.Equal(2, seen.Last());          // 배지가 읽는 순간 카드 2장(+2)
        rig.RemoteReport(ActionReportKind.Detection, a);
        rig.RemoteReport(ActionReportKind.Detection, b);
        Assert.Empty(rig.Panel.ViewModelProvider);
    }

    // ───────────────────────── A. 패널 활성 시점과 카드 쌓임 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-027,EVM-028")]
    public async Task should_keep_listing_cards_by_the_panel_timer_alone_while_the_view_is_not_looked_at_evm027_028()
    {
        // 호스트는 카드 패널을 부팅 때 한 번 활성화하고(EventSectionShellViewModel) 셸 최소화 · 모달 열림에도 끄지 않는다.
        // 카드는 화면 없이 패널 타이머(150 ms 묶음)만으로 목록에 오른다 — 창 상태와 무관함을 뷰모델 수준에서 본다.
        using var rig = new EventMapRig();
        for (var i = 0; i < 5; i++) rig.Sensor(3501 + i);
        await ((IActivate)rig.Panel).ActivateAsync();
        try
        {
            var ids = new List<int>();
            for (var i = 0; i < 5; i++)
            {
                var id = await rig.DetectAsync(3501 + i);
                EnqueueOnly(rig, id);
                ids.Add(id);
            }

            await WaitUntil(() => rig.Panel.ViewModelProvider.Count == 5, "타이머가 카드 5장을 올리지 않았다");
            Assert.All(rig.Panel.ViewModelProvider, c => Assert.NotNull(c.EntryId));   // 모두 조치 가능(큐 엔트리와 이어짐)
            foreach (var id in ids) rig.RemoteReport(ActionReportKind.Detection, id);
            Assert.Empty(rig.Panel.ViewModelProvider);
        }
        finally
        {
            await ((IDeactivate)rig.Panel).DeactivateAsync(true);
        }
    }

    [Fact]
    [Trait("Scenario", "EVM-029")]
    public async Task should_show_every_card_that_arrived_before_the_panel_was_opened_once_it_is_opened_evm029()
    {
        using var rig = new EventMapRig();
        rig.Sensor(3601);
        rig.Sensor(3602);
        var a = await rig.DetectAsync(3601);
        var b = await rig.DetectAsync(3602);
        EnqueueOnly(rig, a);
        EnqueueOnly(rig, b);
        Assert.Empty(rig.Panel.ViewModelProvider);      // 패널이 열리기 전 — 버퍼에서 기다린다
        Assert.Equal(2, rig.Panel.PendingCardCount);

        await ((IActivate)rig.Panel).ActivateAsync();   // 패널 열기
        try
        {
            await WaitUntil(() => rig.Panel.ViewModelProvider.Count == 2, "패널을 열었는데 카드 2장이 오르지 않았다");
            Assert.Equal(new[] { b, a }, rig.Panel.ViewModelProvider.Select(c => c.Model!.Id));
            Assert.All(rig.Panel.ViewModelProvider, c => Assert.NotNull(c.EntryId));
            rig.RemoteReport(ActionReportKind.Detection, a);
            rig.RemoteReport(ActionReportKind.Detection, b);
            Assert.Empty(rig.Panel.ViewModelProvider);
        }
        finally
        {
            await ((IDeactivate)rig.Panel).DeactivateAsync(true);
        }
    }

    // ───────────────────────── F. 조치 창 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-092")]
    public async Task should_keep_card_symbol_and_alarm_and_write_nothing_when_report_dialog_is_cancelled_evm092()
    {
        using var rig = new EventMapRig();
        Serve(rig);
        var symbol = rig.Sensor(3701, groups: 3);
        var zone = rig.Zone(3);
        var id = await rig.DetectAsync(3701);
        var card = await rig.ShowDetectionCardAsync(id);
        var dialog = new DetectionReportDialogViewModel(IoC.Get<IEventAggregator>(), rig.Log.Object);
        dialog.UpdateData(card, IoC.Get<Ironwall.Dotnet.Monitoring.Models.Accounts.IAccountModel>());
        await ((IActivate)dialog).ActivateAsync();

        dialog.ClickCancel();

        Assert.Contains(rig.Published, m => m is CloseDialogMessageModel);     // 창만 닫힌다
        Assert.Same(card, Assert.Single(rig.Panel.ViewModelProvider));         // 카드 유지
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);           // 심볼 유지
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);
        Assert.Equal(0, rig.SoundStops);                                       // 소리 유지
        Assert.Equal((1, 0), rig.Counts);
        Assert.Empty(rig.ServerActions);                                       // 조치 쓰기 0
        rig.EventApi.Verify(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(rig.Published, m => m is DetectionReportedMessageModel or Ironwall.Dotnet.Monitoring.Models.Comms.SendActionRequestMessage);

        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    [Trait("Scenario", "EVM-040")]
    public async Task should_keep_the_typed_memo_and_choice_of_an_open_report_dialog_when_a_burst_arrives_and_send_that_memo_evm040()
    {
        using var rig = new EventMapRig();
        Serve(rig);
        rig.Sensor(3801);
        for (var i = 0; i < 5; i++) rig.Sensor(3811 + i);
        var id = await rig.DetectAsync(3801);
        var card = await rig.ShowDetectionCardAsync(id);
        var dialog = new DetectionReportDialogViewModel(IoC.Get<IEventAggregator>(), rig.Log.Object);
        dialog.UpdateData(card, IoC.Get<Ironwall.Dotnet.Monitoring.Models.Accounts.IAccountModel>());
        await ((IActivate)dialog).ActivateAsync();
        dialog.EtcViewModel!.IsSelected = true;                       // '기타'
        const string memo = "철책 하단 훼손 — 순찰 2조 확인 중";
        dialog.Memo = memo;                                            // 메모를 적는 중

        var burst = new List<int>();                                   // 다른 센서 5대에서 버스트(같은 대상 중복 없음)
        for (var i = 0; i < 5; i++)
        {
            var other = await rig.DetectAsync(3811 + i);
            await rig.ShowDetectionCardAsync(other);
            burst.Add(other);
        }

        Assert.Equal(memo, dialog.Memo);                               // 메모 글 유지
        Assert.Same(dialog.EtcViewModel, dialog.SelectableItemViewModel);
        Assert.Same(card, dialog.Model);                               // 다른 이벤트로 바뀌지 않는다
        Assert.True(dialog.CanClickOk);
        Assert.Equal(EventReportDialogViewModel.DEFAULT_NOTICE_TEXT, dialog.DialogNotice);
        Assert.Equal(6, rig.Panel.ViewModelProvider.Count);

        dialog.ClickOk();
        await WaitUntil(() => rig.ServerActions.Contains(id) && !dialog.IsSending, "조치보고가 끝나지 않았다");

        rig.EventApi.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == id && d.Content == memo), It.IsAny<CancellationToken>()), Times.Once);
        await rig.Panel.HandleAsync(rig.Published.OfType<DetectionReportedMessageModel>().Single(), CancellationToken.None);   // 호스트 EA 가 카드 목록에 전한다
        Assert.DoesNotContain(card, rig.Panel.ViewModelProvider);
        Assert.Equal(5, rig.Panel.ViewModelProvider.Count);            // 버스트 카드는 그대로
        foreach (var other in burst) rig.RemoteReport(ActionReportKind.Detection, other);
        Assert.Empty(rig.Panel.ViewModelProvider);
    }

    // ───────────────────────── F. 이벤트 콘솔 → 지도 카드 ─────────────────────────

    /// <summary>호스트 EA 대역 — 카드 목록이 구독해 있는 진짜 EventAggregator 와 그 EA 를 쓰는 이벤트 콘솔.</summary>
    private static (EventDashboardViewModel Console, EventAggregator Ea) OpenConsole(EventMapRig rig)
    {
        var ea = new EventAggregator();
        ea.SubscribeOnPublishedThread(rig.Panel);   // 호스트: 카드 목록은 같은 EA 를 듣는다(활성화 때 구독)
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
            new DataChartPanelViewModel(ea, log, service));
        console.UseUiThread(ImmediateUiThread.Instance);
        return (console, ea);
    }

    private static DetectionEventViewModel ListRow(EventMapRig rig, EventDashboardViewModel console, int eventId)
    {
        var (deviceId, _) = rig.ServerEvents[(ActionReportKind.Detection, eventId)];
        var model = new DetectionEventModel { Id = eventId, MessageType = EnumEventType.Intrusion, DateTime = DateTime.Now, Device = rig.ServerDevices[deviceId] };
        var row = new DetectionEventViewModel(model);
        console.DetectionPanelViewModel.ViewModelProvider.Add(row);   // 콘솔 탐지 목록에 그 이벤트 행이 있다(서버 조회 결과)
        return row;
    }

    [Fact]
    [Trait("Scenario", "EVM-098")]
    public async Task should_close_the_map_card_and_restore_the_symbol_when_the_console_tray_is_applied_evm098()
    {
        using var rig = new EventMapRig();
        Serve(rig);
        var symbol = rig.Sensor(3901, groups: 2);
        var id = await rig.DetectAsync(3901);
        await rig.ShowDetectionCardAsync(id);
        var (console, _) = OpenConsole(rig);
        var row = ListRow(rig, console, id);

        console.Tray.Enqueue(ActionTrayDrop.Plan(new[] { new ActionTrayCandidate(id, ActionTrayDrop.KindDetection, "센서 3901", false) }, canControl: true));
        console.Tray.Phrase = ActionTrayViewModel.Phrases[0];
        var summary = await console.ApplyTrayAsync();

        Assert.Equal(1, summary.Applied);
        Assert.Equal(new[] { id }, rig.ServerActions);                 // 조치 1건
        Assert.Empty(rig.Panel.ViewModelProvider);                     // 지도 카드 바로 닫힘
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(1, rig.SoundStops);
        Assert.Equal(EnumTrueFalse.True, row.Model!.Status);           // 콘솔 행도 '조치됨'
    }

    [Fact]
    [Trait("Scenario", "EVM-099")]
    public async Task should_close_the_map_card_when_a_console_row_is_reported_through_its_report_dialog_evm099()
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(4001, groups: 2);
        var id = await rig.DetectAsync(4001);
        await rig.ShowDetectionCardAsync(id);
        var (console, ea) = OpenConsole(rig);
        var dialog = new DetectionReportDialogViewModel(ea, rig.Log.Object);
        Serve(rig, (typeof(DetectionReportDialogViewModel), dialog));
        var opened = new List<OpenEventReportDialogMessageModel>();
        ea.Subscribe(new OpenSink(opened));
        var row = ListRow(rig, console, id);

        console.DetectionPanelViewModel.ReportCommand.Execute(row);     // 행 우클릭 [조치보고]
        await WaitUntil(() => opened.Count == 1, "조치 창 열기 요청이 없다");
        Assert.Equal("DETECTION", opened[0].EventType);
        Assert.Equal(id, dialog.Model!.Model.Id);

        await ((IActivate)dialog).ActivateAsync();                       // 호스트가 창을 연다(여기선 뷰모델만)
        dialog.ClickOk();                                                // 첫 문구로 [확인]
        await WaitUntil(() => rig.ServerActions.Contains(id) && !dialog.IsSending, "조치보고가 끝나지 않았다");

        Assert.Empty(rig.Panel.ViewModelProvider);                       // 지도 카드 닫힘
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Single(rig.ServerActions);
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

    [Fact]
    [Trait("Scenario", "EVM-102")]
    public async Task should_close_every_card_restore_every_symbol_and_write_one_action_each_when_report_all_is_confirmed_evm102()
    {
        using var rig = new EventMapRig();
        var symbols = new[] { rig.Sensor(4201, groups: 1), rig.Sensor(4202, groups: 1) };
        var faultSymbol = rig.Sensor(4203, groups: 1);
        var zone = rig.Zone(1);
        var d1 = await rig.DetectAsync(4201);
        var d2 = await rig.DetectAsync(4202);
        var f1 = await rig.MalfunctionAsync(4203);
        await rig.ShowDetectionCardAsync(d1);
        await rig.ShowDetectionCardAsync(d2);
        await rig.ShowMalfunctionCardAsync(f1);
        Assert.Equal((2, 1), rig.Counts);

        // [전체 조치보고] → 확인 팝업이 CallAllEventReportMessageModel 을 다시 보낸다
        await rig.Panel.HandleAsync(new CallAllEventReportMessageModel(), CancellationToken.None);

        Assert.Empty(rig.Panel.ViewModelProvider);                       // 모두 닫힘
        Assert.Equal(new[] { d1, d2, f1 }.OrderBy(x => x), rig.ServerActions.OrderBy(x => x));
        Assert.All(symbols.Append(faultSymbol), s => Assert.Equal(EnumEventStatus.Normal, s.EventStatus));
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(1, rig.SoundStops);
        Assert.Equal(3, rig.ActionReported.Count);                       // 이벤트 창 닫기 구독자도 3건을 듣는다
    }
}
