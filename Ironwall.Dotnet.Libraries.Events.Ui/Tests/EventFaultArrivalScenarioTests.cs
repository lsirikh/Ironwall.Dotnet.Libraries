using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
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
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CloseDialogMessageModel = Ironwall.Dotnet.Libraries.ViewModel.Models.CloseDialogMessageModel;
using DeviceProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 카탈로그 WIR-503 「장애 도착」 헤드리스 시나리오(2026-10-11)
                  - 시드 제어기에 FAULT_CONTROLLER 장애(1차 · 2차 고장 구간)를 가짜 이벤트 API 에 먼저 기록하고
                    같은 번호의 MALFUNCTION 봉투를 NATS 경로(가짜 NATS → 실제 MalfunctionNatsSyncService)로 한 번만 보낸다.
                  - 카드: 실제 큐 · 심볼 관리자 · 카드 패널(EventMapRig) — 호스트가 하듯 같은 body 를 DTO → 모델 → 카드로.
                  - 이벤트 상세: 실제 이벤트 콘솔(EventDashboardViewModel)이 가짜 API 목록을 읽어 상세 「1차 선 a → b」 를 세운다.
                  - 조치보고: ① 카드 조치 창 [확인](로컬) ② 다른 운영자의 ACTION_REPORT(원격) — 둘 다 카드 · 조치 창이 닫힌다.
                  - 탐지 · 장애는 조치보고 전에 같은 대상을 두 번 보내지 않는다(운영 전제 — 중복 송신 없음).
                  - 창을 띄우지 않는다(Window.Show · ShowDialog 없음) · 실제 네트워크 · NATS · DB 없음.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // 카드 · 조치 창 · 콘솔이 IoC.GetInstance(전역 정적)를 읽는다
public class EventFaultArrivalScenarioTests : IDisposable
{
    private const int CONTROLLER_ID = 4510;
    private const int ZONE_GROUP = 45;
    private const int RING_SIZE = 6;

    private readonly Func<Type, string, object> _previousGetInstance = IoC.GetInstance;
    private readonly Func<Type, IEnumerable<object>> _previousGetAllInstances = IoC.GetAllInstances;
    private readonly Action<object> _previousBuildUp = IoC.BuildUp;
    private readonly EventProvider _events = new();
    private readonly Mock<IAccountModel> _account = new();
    private readonly List<MalfunctionEventDto> _serverMalfunctions = new();

    public EventFaultArrivalScenarioTests()
    {
        _account.SetupGet(a => a.Name).Returns("tester");
        _account.SetupGet(a => a.Username).Returns("kim");
        _account.SetupGet(a => a.EmployeeNumber).Returns("1234");
    }

    /// <summary>전역 IoC 를 시험 전 값으로 되돌린다 — 다음 시험(다른 클래스 포함)이 이 시험의 가짜를 물려받지 않게.</summary>
    public void Dispose()
    {
        IoC.GetInstance = _previousGetInstance;
        IoC.GetAllInstances = _previousGetAllInstances;
        IoC.BuildUp = _previousBuildUp;
    }

    // ───────────────────────── 장치 보조 ─────────────────────────

    /// <summary>
    /// 장치를 세운다 — EventMapRig(가짜 서버 → 실제 NATS 수신 서비스 → 실제 큐 · 심볼 · 카드 패널) 위에
    /// 카드 · 조치 창 · 콘솔이 IoC 로 찾는 것(이벤트 API · 조치 자물쇠 · 장비 캐시 · 이벤트 저장소 · 로그인 계정)을 덧댄다.
    /// rig 가 처음 IoC 를 기억해 두었다가 Dispose 때 되돌리고, 이 클래스의 Dispose 도 한 번 더 되돌린다.
    /// </summary>
    private EventMapRig CreateRig()
    {
        var rig = new EventMapRig();
        var inner = IoC.GetInstance;
        var guard = new ActionReportGuard();
        IoC.GetInstance = (type, key) =>
            type == typeof(IEventApiService) ? rig.EventApi.Object
            : type == typeof(IActionReportGuard) ? guard
            : type == typeof(DeviceProvider) ? rig.Devices
            : type == typeof(EventProvider) ? _events
            : type == typeof(IAccountModel) ? _account.Object
            : inner(type, key);
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new DefaultPlatformProvider();
        ServeApi(rig);
        return rig;
    }

    /// <summary>가짜 이벤트 API 의 목록 조회 — 장애 목록은 이 시험이 "서버에 기록한" 것을 그대로 돌려준다.</summary>
    private void ServeApi(EventMapRig rig)
    {
        var api = rig.EventApi;
        api.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                 It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                 It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Page(new List<DetectionEventDto>()));
        api.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .Returns(() => Task.FromResult(Page(_serverMalfunctions.ToList())));
        api.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Page(new List<ConnectionEventDto>()));
        api.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                                              It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Page(new List<ActionEventDto>()));
        api.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = new EventDashboardDto() });

        static ApiListResponse<T> Page<T>(List<T> items)
            => new() { Success = true, Data = items, Pagination = new PaginationDto { Page = 1, Limit = 100, Total = items.Count, TotalPages = 1 } };
    }

    /// <summary>
    /// 시드 제어기 1대 + 그 제어기 링에 체인 위치 1…N 이 저장된 센서 N대(모두 구역 <see cref="ZONE_GROUP"/>) + 그 구역선.
    /// 제어기 무통신이면 링 센서의 구역선이 검게 된다(GMap_Controller_Blackout).
    /// </summary>
    private static (ControllerDeviceModel Controller, PidsSymbolModel Symbol, PidsGroupSymbolModel Zone) SeedController(EventMapRig rig)
    {
        var (controller, symbol) = rig.Controller(CONTROLLER_ID);
        controller.DeviceNumber = 1;
        controller.DeviceName = "CTRL-북측-01";
        for (var order = 1; order <= RING_SIZE; order++)
        {
            var sensorId = CONTROLLER_ID + order;
            rig.Sensor(sensorId, controller: controller, groups: ZONE_GROUP);
            var sensor = (SensorDeviceModel)rig.ServerDevices[sensorId];
            sensor.DeviceNumber = order;
            sensor.DeviceName = $"북측 {order}구간";
            sensor.Axes = new DeviceAxesModel
            {
                HardwareSpec = new HardwareSpecModel
                {
                    Spec = new JObject { [WiringSpec.SPEC_KEY] = new JObject { ["v"] = 3, ["line"] = 1, ["order"] = order } },
                },
            };
        }
        return (controller, symbol, rig.Zone(ZONE_GROUP));
    }

    /// <summary>명세 §6.2 모양의 MALFUNCTION 봉투 — 시드 제어기 · FAULT_CONTROLLER · 1차 · 2차 고장 구간.</summary>
    private static string MalfunctionJson(EventMapRig rig, int eventId, (int Start, int End) first, (int Start, int End) second)
    {
        var created = DateTimeOffset.Now.AddMinutes(-1).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffffzzz");
        return $$"""
            { "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "MALFUNCTION", "from": "PidsProxy",
              "body": { "id": {{eventId}}, "category_event": "malfunction", "type_event": "Fault", "action_reported": false,
                        "reason": "FAULT_CONTROLLER", "device": { "id": {{CONTROLLER_ID}}, "category_device": "controller" },
                        "device_description": "[controller:Controller] CTRL-북측-01 (number: 1, id: {{CONTROLLER_ID}})",
                        "detail": { "first_start": {{first.Start}}, "first_end": {{first.End}}, "second_start": {{second.Start}}, "second_end": {{second.End}} },
                        "created_at": "{{created}}", "updated_at": "{{created}}" } }
            """;
    }

    /// <summary>
    /// 장애 도착 — ① 가짜 API 에 먼저 기록(콘솔 목록 조회가 돌려준다) → ② 같은 번호로 NATS MALFUNCTION 한 번
    /// → ③ 호스트(ProcessFault)가 하듯 같은 body 를 DTO → 모델 → 카드로 묶음 버퍼에 넣고 비운다.
    /// </summary>
    private async Task<(int EventId, MalfunctionEventCardViewModel Card)> ArriveAsync(EventMapRig rig, (int, int) first, (int, int) second)
    {
        var id = rig.RecordMalfunction(CONTROLLER_ID, "FAULT_CONTROLLER");
        var json = MalfunctionJson(rig, id, first, second);
        _serverMalfunctions.Add(JObject.Parse(json)["body"]!.ToObject<MalfunctionEventDto>()!);   // ① API 기록

        await rig.PublishRawAsync(json);                                                          // ② NATS 1회(중복 없음)

        var dto = JObject.Parse(json)["body"]!.ToObject<MalfunctionEventDto>()!;                 // ③ 호스트 카드
        var card = new MalfunctionEventCardViewModel(dto.ToMalfunctionEventModel(rig.Devices));
        rig.Panel.EnqueueCard(card);
        await rig.Panel.FlushPendingCardsNowAsync();
        return (id, card);
    }

    /// <summary>실제 이벤트 콘솔 — 장비 캐시(rig.Devices)와 가짜 API 를 쓴다(EventFaultSectionRingTests 의 장치와 같다).</summary>
    private EventDashboardViewModel OpenConsole(EventMapRig rig)
    {
        var ea = new EventAggregator();
        var log = rig.Log.Object;
        var api = rig.EventApi.Object;
        var devices = rig.Devices;
        var providerService = new EventProviderService(log, api, devices, _events);
        var console = new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, providerService, devices, _events),
            new MalfunctionEventPanelViewModel(ea, log, providerService, devices, _events),
            new ConnectionEventPanelViewModel(ea, log, providerService, devices, _events),
            new ActionEventPanelViewModel(ea, log, providerService, _events),
            new EventInfoViewModel(devices, _events, providerService, ea, log),
            new CameraEventInfoViewModel(_events, ea, log),
            new DataChartPanelViewModel(ea, log, providerService),
            deviceProvider: devices);
        console.UseUiThread(ImmediateUiThread.Instance);
        return console;
    }

    /// <summary>콘솔을 열고 장애 레일에서 그 이벤트 행(가짜 API 목록에서 읽은 것)을 골라 상세를 세운다.</summary>
    private async Task<EventDashboardViewModel> ShowInConsoleAsync(EventMapRig rig, int eventId)
    {
        var console = OpenConsole(rig);
        await ((IActivate)console).ActivateAsync();
        Assert.True(await console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey), "장애 레일로 바꾸지 못했다");
        await WaitUntil(() => console.MalfunctionPanelViewModel.ViewModelProvider.Any(r => r.Model.Id == eventId),
                        "콘솔 장애 목록이 가짜 API 기록을 읽지 못했다");
        var row = Assert.Single(console.MalfunctionPanelViewModel.ViewModelProvider, r => r.Model.Id == eventId);
        Assert.True(console.SetSelection(new object[] { row }), "행을 고르지 못했다");
        return console;
    }

    private static string? Field(EventDetailViewModel detail, string label)
        => detail.Sections.Single(s => s.Title == "고장 구간").Fields.FirstOrDefault(f => f.Label == label)?.Text;

    private static async Task WaitUntil(Func<bool> condition, string what)
    {
        var ok = await Task.Run(() => SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)));
        Assert.True(ok, what);
    }

    private static List<object> Snapshot(EventMapRig rig)
    {
        lock (rig.Published) return rig.Published.ToList();
    }

    /// <summary>도착 직후 공통 확인 — 큐 엔트리 · 카드 · 심볼 · 구역선 · 신호등 · 소리 · 서버 조치 0건.</summary>
    private static void AssertArrived(EventMapRig rig, int id, MalfunctionEventCardViewModel card, PidsSymbolModel symbol, PidsGroupSymbolModel zone,
                                      (int Start, int End) first, (int Start, int End) second)
    {
        // 카드 1장 — 종류 + 번호 · 사유 · 제어기 칸 · 고장 구간
        Assert.Same(card, Assert.Single(rig.Panel.ViewModelProvider));
        Assert.Equal($"Events.Card.Malfunction.{id}", card.AutomationKey);
        Assert.Equal(EnumFaultType.FAULT_CONTROLLER, card.Reason);
        Assert.Equal(1, card.ControllerDisplay);
        Assert.Null(card.SensorDisplay);
        Assert.Equal(first, (card.FirstStart, card.FirstEnd));
        Assert.Equal(second, (card.SecondStart, card.SecondEnd));

        // NATS 경로가 큐에 한 번 넣었고, 그 엔트리가 카드에 이어졌다(조치하면 풀 수 있다)
        var entry = rig.Queue.FindEntryByEventId(id, EnumEventType.Fault);
        Assert.NotNull(entry);
        Assert.True(entry!.IsControllerBlackout, "제어기 무통신 표식이 없다");
        Assert.Contains(ZONE_GROUP, entry.GroupIds ?? new List<int>());
        Assert.Equal(1, rig.Queue.GetTotalQueueCount());
        var enqueued = Assert.Single(Snapshot(rig).OfType<EventEntryEnqueuedMessage>());
        Assert.Equal((id, EnumEventType.Fault, EnumDeviceType.Controller), (enqueued.EventId, enqueued.EventType, enqueued.DeviceType));
        Assert.Equal(enqueued.EntryId, card.EntryId);

        // 지도 · 신호등 · 소리
        Assert.Equal(EnumEventStatus.Blackout, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Blackout, zone.CompositeStatus);
        Assert.Equal((0, 1), rig.Counts);
        Assert.Equal(new[] { EnumEventType.Fault }, rig.SoundPlays);
        Assert.Empty(rig.ServerActions);                                                   // 아직 조치 없음(자동 조치도 없음)
    }

    /// <summary>닫힌 뒤 공통 확인 — 카드 없음 · 큐 비움 · 심볼 · 구역선 정상 · 신호등 0 · 소리 끔 · 이벤트 창 닫기 신호 한 번.</summary>
    private static void AssertClosed(EventMapRig rig, int id, PidsSymbolModel symbol, PidsGroupSymbolModel zone)
    {
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(0, rig.Panel.PendingCardCount);
        Assert.Null(rig.Queue.FindEntryByEventId(id, EnumEventType.Fault));
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(1, rig.SoundStops);
        Assert.Equal(new[] { (ActionReportKind.Malfunction, id) }, rig.ActionReported);   // 카메라 팝업 이벤트 창 닫기 신호
        Assert.Equal(new[] { id }, rig.ServerActions);                                    // 서버 조치 정확히 1건
    }

    // ───────────────────────── WIR-503 ─────────────────────────

    [Fact]
    [Trait("Scenario", "WIR-503")]
    public async Task should_raise_one_card_and_show_first_section_in_event_detail_when_controller_fault_arrives_after_api_record_WIR503()
    {
        using var rig = CreateRig();
        var (_, symbol, zone) = SeedController(rig);

        var (id, card) = await ArriveAsync(rig, (4, 5), (4, 5));

        AssertArrived(rig, id, card, symbol, zone, (4, 5), (4, 5));

        // 이벤트 상세 — 가짜 API 가 돌려준 같은 번호의 기록으로 「1차 선 4 → 5」 · 「2차 선 4 → 5」 + 링 센서 이름
        var console = await ShowInConsoleAsync(rig, id);
        Assert.Equal("4 → 5", Field(console.DetailView, "1차 선"));
        Assert.Equal("4 → 5", Field(console.DetailView, "2차 선"));
        Assert.Equal("Ch1(A) 쪽 4~5 → 북측 4구간과 북측 5구간 사이", Field(console.DetailView, "1차 센서"));
        Assert.Equal("Ch2(B) 쪽 4~5 → 북측 3구간과 북측 2구간 사이", Field(console.DetailView, "2차 센서"));
        rig.EventApi.Verify(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);

        // 모든 시나리오는 조치로 닫는다(다른 운영자의 조치 → ACTION_REPORT)
        Assert.Equal(RemoteActionReportOutcome.CardClosed, rig.RemoteReport(ActionReportKind.Malfunction, id));
        AssertClosed(rig, id, symbol, zone);
        // 헤디드 남김: 실제 카드 그림(지도 카드 목록)과 콘솔 상세 칸의 글자 렌더는 실창에서 본다.
    }

    [Fact]
    [Trait("Scenario", "WIR-503")]
    public async Task should_close_card_and_report_dialog_and_write_one_action_when_the_fault_card_is_reported_WIR503()
    {
        using var rig = CreateRig();
        var (_, symbol, zone) = SeedController(rig);
        var (id, card) = await ArriveAsync(rig, (4, 5), (4, 5));
        AssertArrived(rig, id, card, symbol, zone, (4, 5), (4, 5));
        var console = await ShowInConsoleAsync(rig, id);
        Assert.Equal("4 → 5", Field(console.DetailView, "1차 선"));

        // 카드 [조치보고] → 장애 조치 창(뷰모델만) — 창의 「1차 구간 4 ~ 5」 · 「2차 구간 4 ~ 5」 원천 값
        var dialog = new MalfunctionReportDialogViewModel(IoC.Get<IEventAggregator>(), rig.Log.Object);
        dialog.UpdateData(card, _account.Object);
        var editor = Assert.IsType<MalfunctionSelectionViewModel>(dialog.SelectedItemEditor);
        Assert.Equal<(int?, int?)>((4, 5), (editor.FirstStart, editor.FirstEnd));
        Assert.Equal<(int?, int?)>((4, 5), (editor.SecondStart, editor.SecondEnd));
        Assert.Equal(EnumFaultType.FAULT_CONTROLLER, editor.Reason);
        await ((IActivate)dialog).ActivateAsync();
        Assert.True(dialog.CanClickOk);

        dialog.ClickOk();                                                                  // 첫 문구로 [확인]
        await WaitUntil(() => Snapshot(rig).OfType<CloseDialogMessageModel>().Any(), "조치 창 닫기 요청이 없다");

        // 서버 조치 1건(같은 대상 중복 없음) · ACTION_REPORT 송신 1건 · 창 닫힘
        rig.EventApi.Verify(a => a.CreateActionEventAsync(It.Is<ActionEventCreateDto>(d => d.FromEventId == id && d.User == "kim(1234)"), It.IsAny<CancellationToken>()), Times.Once);
        rig.EventApi.Verify(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()), Times.Once);
        var sent = Assert.Single(Snapshot(rig).OfType<SendActionRequestMessage>());
        Assert.Equal(50_001, sent.ActionId);
        Assert.False(dialog.IsSending);

        // 호스트 EA 가 카드 목록에 전한다 — 카드 · 큐 · 심볼 · 구역선이 풀린다
        var reported = Assert.Single(Snapshot(rig).OfType<MalfunctionReportedMessageModel>());
        Assert.Same(card, reported.ViewModel);
        await rig.Panel.HandleAsync(reported, CancellationToken.None);
        AssertClosed(rig, id, symbol, zone);

        // 자기 ACTION_REPORT 의 메아리 — 다시 닫을 것도, 다시 알릴 것도 없다(멱등)
        Assert.Equal(RemoteActionReportOutcome.Nothing, rig.Panel.CloseByRemoteActionReport(ActionReportKind.Malfunction, id, EnumEventType.Fault));
        Assert.Single(rig.ActionReported);
        Assert.Single(rig.ServerActions);
        // 헤디드 남김: 실제 조치 창(모달)이 화면에서 닫히고 카드가 지도 목록에서 사라지는 그림은 실창에서 본다.
    }

    [Fact]
    [Trait("Scenario", "WIR-503")]
    public async Task should_close_card_and_disable_open_report_dialog_when_another_operator_reports_the_fault_WIR503()
    {
        using var rig = CreateRig();
        var (_, symbol, zone) = SeedController(rig);

        // 1차 · 2차 구간이 서로 다른 값 — 상세가 두 줄을 바꿔 달지 않는지까지 본다
        var (id, card) = await ArriveAsync(rig, (2, 3), (5, 6));
        AssertArrived(rig, id, card, symbol, zone, (2, 3), (5, 6));
        var console = await ShowInConsoleAsync(rig, id);
        Assert.Equal("2 → 3", Field(console.DetailView, "1차 선"));
        Assert.Equal("5 → 6", Field(console.DetailView, "2차 선"));

        // 이 GIS 에서 조치 창을 열어 둔 채 — 다른 운영자가 조치(서버 POST events/actions → ACTION_REPORT)
        var dialog = new MalfunctionReportDialogViewModel(IoC.Get<IEventAggregator>(), rig.Log.Object);
        dialog.UpdateData(card, _account.Object);
        await ((IActivate)dialog).ActivateAsync();
        Assert.True(dialog.CanClickOk);

        // 호스트 라우터(NatsDomainService)가 ACTION_REPORT 를 패널로 옮긴다 — 라우터 자체는 호스트 담당이라 헤디드에서 본다.
        Assert.Equal(RemoteActionReportOutcome.CardClosed, rig.RemoteReport(ActionReportKind.Malfunction, id));
        AssertClosed(rig, id, symbol, zone);

        // 열린 조치 창은 [확인] 을 끈다 — 두 번째 조치를 만들지 않는다
        var remote = Assert.Single(Snapshot(rig).OfType<RemoteActionReportedMessage>());
        Assert.Equal((ActionReportKind.Malfunction, id), (remote.Kind, remote.EventId));
        await dialog.HandleAsync(remote, CancellationToken.None);
        Assert.False(dialog.CanClickOk);
        Assert.True(dialog.IsHandledElsewhere);
        dialog.ClickOk();
        rig.EventApi.Verify(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(Snapshot(rig), m => m is SendActionRequestMessage);
    }
}
