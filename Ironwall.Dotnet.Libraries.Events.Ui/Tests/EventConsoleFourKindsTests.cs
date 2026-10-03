using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 이벤트 콘솔이 <b>탐지 · 장애 · 연결 · 운영</b> 네 종류를 모두 보이는가 — 서버가 준 행이 그 레일의 목록 · 상세에
/// 그대로 닿는지 확인한다(2026-10-03 요청: "탐지, 장애, 연결, 운영이벤트도 확인 · 창도 있어야").
/// </summary>
/// <remarks>
/// 운영 이벤트(<c>GET /api/events/operations</c>)는 API 만 있고 어느 창도 쓰지 않았다 — '운영' 레일을 새로 세웠다.
/// 운영은 서버가 스스로 만드는 기록이라 조회 전용이다(추가 · 저장 · 삭제 · 조치보고 없음).
/// </remarks>
[Collection("IoC-Dependent")]
public class EventConsoleFourKindsTests : IDisposable
{
    private readonly EventProvider _events = new();
    private readonly DeviceProvider _devices = new();
    private readonly EventAggregator _ea = new();
    private readonly PopupSink _popups = new();

    public EventConsoleFourKindsTests()
    {
        _ea.SubscribeOnPublishedThread(_popups);
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    // ───────────────────────────── 준비 ─────────────────────────────

    private EventDashboardViewModel Build(IEventApiService api, bool withOperation = true)
    {
        var log = new Mock<ILogService>().Object;
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Name).Returns("tester");

        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? _ea
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? api
            : type == typeof(EventProvider) ? _events
            : type == typeof(DeviceProvider) ? _devices
            : type == typeof(IAccountModel) ? account.Object
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new DefaultPlatformProvider();

        var providerService = new EventProviderService(log, api, _devices, _events);
        var console = new EventDashboardViewModel(
            _ea, log,
            new EventTabControlViewModel(_ea, log),
            new DetectionEventPanelViewModel(_ea, log, providerService, _devices, _events),
            new MalfunctionEventPanelViewModel(_ea, log, providerService, _devices, _events),
            new ConnectionEventPanelViewModel(_ea, log, providerService, _devices, _events),
            new ActionEventPanelViewModel(_ea, log, providerService, _events),
            new EventInfoViewModel(_devices, _events, providerService, _ea, log),
            new CameraEventInfoViewModel(_events, _ea, log),
            new DataChartPanelViewModel(_ea, log, providerService),
            operationEventPanelViewModel: withOperation ? new OperationEventPanelViewModel(_ea, log, providerService) : null);
        console.UseUiThread(ImmediateUiThread.Instance);
        return console;
    }

    private static Mock<IEventApiService> BuildApi(ApiListResponse<OperationEventDto>? operations = null)
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };

        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(new DetectionEventDto { Id = 701, TypeEvent = "Intrusion", Result = "NONE", ActionReported = "false", CreatedAt = "2026-10-03T10:00:00+09:00" }));
        mock.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(new MalfunctionEventDto { Id = 702, TypeEvent = "Fault", Reason = "FAULT_FENCE", ActionReported = "true", CreatedAt = "2026-10-03T10:01:00+09:00" }));
        mock.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(new ConnectionEventDto { Id = 703, TypeEvent = "Connection", CreatedAt = "2026-10-03T10:02:00+09:00" }));
        mock.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                                               It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page<ActionEventDto>());
        mock.Setup(a => a.GetOperationEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                                                  It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(operations ?? Page(
                new OperationEventDto
                {
                    Id = 704, Reason = "GATE_OPEN", Severity = "WARNING", ActionReported = "false",
                    DeviceDescription = "북문 통문", CreatedAt = "2026-10-03T10:03:00+09:00",
                    Detail = JObject.Parse("""{ "previous": "CLOSED", "gate_status": "OPEN" }"""),
                },
                new OperationEventDto
                {
                    Id = 705, Reason = "ENCLOSURE_TEMP_HIGH", Severity = "INFO", ActionReported = "false",
                    CreatedAt = "2026-10-03T10:04:00+09:00",
                    Detail = JObject.Parse("""{ "field": "temp", "value": 61.5, "threshold": 60 }"""),
                },
                new OperationEventDto
                {
                    // 서버가 늘린 모르는 어휘 — 지우거나 다른 말로 바꾸지 않고 원문을 보인다.
                    Id = 706, Reason = "ENCLOSURE_SMOKE", Severity = "CRITICAL", ActionReported = "true",
                    CreatedAt = "2026-10-03T10:05:00+09:00",
                }));
        mock.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = new EventDashboardDto() });
        return mock;
    }

    private static ApiListResponse<T> Page<T>(params T[] items)
        => new() { Success = true, Data = items.ToList(), Pagination = new PaginationDto { Page = 1, Limit = 100, Total = items.Length, TotalPages = 1 } };

    private static Task Activate(EventDashboardViewModel console) => ((IActivate)console).ActivateAsync();

    /// <summary>패널의 조회는 작업 스레드에서 끝난다 — 목록에 행이 붙을 때까지 기다린다(최대 5 초).</summary>
    private static async Task<List<object>> RowsOf(EventDashboardViewModel console, int expected)
    {
        for (var i = 0; i < 100; i++)
        {
            var rows = console.Rows?.Cast<object>().ToList() ?? new List<object>();
            if (rows.Count >= expected) return rows;
            await Task.Delay(50);
        }
        return console.Rows?.Cast<object>().ToList() ?? new List<object>();
    }

    private static string FieldText(EventDashboardViewModel console, string label)
        => console.DetailView.Sections.SelectMany(s => s.Fields).First(f => f.Label == label).Text;

    // ───────────────────────────── 레일 ─────────────────────────────

    [Fact]
    public void should_put_detection_malfunction_connection_and_operation_rails_in_the_console()
    {
        var console = Build(BuildApi().Object);

        var labels = console.RailEntries.Select(e => e.Label).ToList();

        Assert.Equal(new[] { "개요", "탐지", "장애", "연결", "운영", "조치" }, labels);
        Assert.Equal(EventDashboardViewModel.OperationRailKey, console.RailEntries[4].Key);
    }

    [Fact]
    public void should_not_raise_an_operation_rail_when_the_host_did_not_inject_the_panel()
    {
        // 이 뷰모델을 세우는 기존 곳(호스트 · 미리보기 · 가짜)은 그대로 — 운영 패널을 안 넘기면 레일이 없다.
        var console = Build(BuildApi().Object, withOperation: false);

        Assert.DoesNotContain(console.RailEntries, e => e.Key == EventDashboardViewModel.OperationRailKey);
        Assert.Null(console.OperationPanelViewModel);
    }

    [Theory]
    [InlineData(EventDashboardViewModel.DetectionRailKey, 701, EventDetailKind.Detection, "탐지")]
    [InlineData(EventDashboardViewModel.MalfunctionRailKey, 702, EventDetailKind.Malfunction, "장애")]
    [InlineData(EventDashboardViewModel.ConnectionRailKey, 703, EventDetailKind.Connection, "연결")]
    [InlineData(EventDashboardViewModel.OperationRailKey, 704, EventDetailKind.Operation, "운영")]
    public async Task should_show_the_server_row_in_its_own_rail_and_open_it_in_the_detail(string railKey, int id, EventDetailKind kind, string kindLabel)
    {
        var console = Build(BuildApi().Object);
        await Activate(console);

        await console.SelectRailAsync(railKey);
        var rows = await RowsOf(console, 1);

        Assert.Equal(kind, console.CurrentKind);
        var row = Assert.Single(rows.OfType<ExEventViewModel>(), r => r.Model?.Id == id);

        Assert.True(console.SetSelection(new object[] { row }));
        Assert.Equal(kind, console.DetailView.Kind);
        Assert.Equal(kindLabel, console.Detail.TypeName);
        Assert.Equal(id.ToString(), FieldText(console, "번호"));
        Assert.Empty(_popups.Seen);   // 네 종류 모두 실패 안내 없이 열린다
    }

    [Fact]
    public async Task should_list_operation_events_with_reason_severity_and_detail_as_the_server_sent_them()
    {
        var console = Build(BuildApi().Object);
        await Activate(console);

        await console.SelectRailAsync(EventDashboardViewModel.OperationRailKey);
        var rows = (await RowsOf(console, 3)).Cast<OperationEventViewModel>().ToDictionary(r => r.Model!.Id);

        Assert.Equal(3, rows.Count);
        Assert.Equal("통문 열림", rows[704].ReasonText);
        Assert.Equal("경고", rows[704].SeverityText);
        Assert.Equal("previous=CLOSED · gate_status=OPEN", rows[704].DetailText);
        Assert.Contains("북문 통문", rows[704].DeviceLabel);          // 지워진 장비는 서버 스냅샷 이름으로

        Assert.Equal("함체 고온", rows[705].ReasonText);
        Assert.Equal("정보", rows[705].SeverityText);
        Assert.Equal("field=temp · value=61.5 · threshold=60", rows[705].DetailText);

        Assert.Equal("ENCLOSURE_SMOKE", rows[706].ReasonText);       // 모르는 사유는 원문
        Assert.Equal("심각", rows[706].SeverityText);
        Assert.True(rows[706].IsActionReported);
        Assert.Equal("—", rows[706].DetailText);

        Assert.Contains("3 / 3건", console.ListStatusText);
    }

    [Fact]
    public async Task should_open_an_operation_row_read_only_with_the_raw_reason_beside_its_label()
    {
        var console = Build(BuildApi().Object);
        await Activate(console);
        await console.SelectRailAsync(EventDashboardViewModel.OperationRailKey);
        var row = (await RowsOf(console, 3)).Cast<OperationEventViewModel>().First(r => r.Model!.Id == 704);

        console.SetSelection(new object[] { row });

        Assert.Equal("통문 열림 (GATE_OPEN)", FieldText(console, "사유"));
        Assert.Equal("경고", FieldText(console, "심각도"));
        Assert.Equal("미조치", FieldText(console, "조치"));
        Assert.Equal("previous=CLOSED · gate_status=OPEN", FieldText(console, "상세"));
        Assert.All(console.DetailView.Sections.SelectMany(s => s.Fields), f => Assert.False(f.IsEditable));

        // 조회 전용 — 고치기 · 지우기 · 조치보고 · 트레이 끌기가 모두 꺼져 있다.
        Assert.False(console.CanEdit);
        Assert.False(console.CanDelete);
        Assert.False(console.CanReport);
        Assert.False(console.CanDragToTray);
        Assert.False(console.DetailView.CanShowReport);
    }

    [Theory]
    [InlineData(EventListFilter.ChipDoor, new[] { 704 })]
    [InlineData(EventListFilter.ChipEnvironment, new[] { 705, 706 })]
    [InlineData(EventListFilter.ChipWarning, new[] { 704, 706 })]
    [InlineData(EventListFilter.ChipAll, new[] { 704, 705, 706 })]
    public async Task should_filter_operation_rows_by_door_environment_and_warning_chips(string chip, int[] expected)
    {
        var console = Build(BuildApi().Object);
        await Activate(console);
        await console.SelectRailAsync(EventDashboardViewModel.OperationRailKey);
        await RowsOf(console, 3);

        Assert.Equal(new[] { "전체", "개폐", "환경 경보", "경고 이상" }, console.FilterChips.Select(c => c.Label));
        console.SelectFilterChip(chip);

        var shown = console.Rows!.Cast<OperationEventViewModel>().Select(r => r.Model!.Id).OrderBy(i => i);
        Assert.Equal(expected, shown);
    }

    [Fact]
    public async Task should_find_operation_rows_by_korean_reason_and_raw_reason()
    {
        var console = Build(BuildApi().Object);
        await Activate(console);
        await console.SelectRailAsync(EventDashboardViewModel.OperationRailKey);
        await RowsOf(console, 3);

        console.SearchText = "고온";
        Assert.Equal(new[] { 705 }, console.Rows!.Cast<OperationEventViewModel>().Select(r => r.Model!.Id));

        console.SearchText = "GATE_OPEN";
        Assert.Equal(new[] { 704 }, console.Rows!.Cast<OperationEventViewModel>().Select(r => r.Model!.Id));
    }

    [Fact]
    public async Task should_say_the_server_lacks_operation_events_instead_of_a_failure_popup_before_7_0()
    {
        // 운영(6.3.2)에는 /events/operations 가 없다 — API 서비스가 호출 전에 NOT_SUPPORTED 로 막는다.
        var unsupported = ApiListResponse<OperationEventDto>.CreateError("NOT_SUPPORTED", "운영 이벤트는 서버 7.0 이상에서만 제공됩니다.", "contract=V6_3");
        var console = Build(BuildApi(unsupported).Object);
        await Activate(console);

        await console.SelectRailAsync(EventDashboardViewModel.OperationRailKey);
        await RowsOf(console, 0);

        Assert.Empty(console.Rows!.Cast<object>());
        Assert.False(console.OperationPanelViewModel!.IsSupported);
        Assert.Contains("7.0", console.EmptyStateHint);
        Assert.Empty(_popups.Seen);
    }

    [Fact]
    public async Task should_keep_the_old_rows_and_tell_the_user_when_the_operation_query_fails()
    {
        var failing = ApiListResponse<OperationEventDto>.CreateError("INTERNAL_ERROR", "Failed to get operation events", "boom");
        var api = BuildApi();
        var console = Build(api.Object);
        await Activate(console);
        await console.SelectRailAsync(EventDashboardViewModel.OperationRailKey);
        await RowsOf(console, 3);

        api.Setup(a => a.GetOperationEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                                                 It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(failing);
        await console.OperationPanelViewModel!.DataInitialize();

        Assert.Equal(3, console.OperationPanelViewModel.ViewModelProvider.Count);   // swap-on-success — 지우지 않는다
        Assert.Contains(_popups.Seen, s => s.Contains("운영 이벤트를 불러오지 못했습니다"));
    }

    [Fact]
    public async Task should_ask_the_operations_endpoint_with_the_console_period_in_server_time()
    {
        var api = BuildApi();
        var console = Build(api.Object);
        await Activate(console);

        await console.SelectRailAsync(EventDashboardViewModel.OperationRailKey);
        await RowsOf(console, 3);

        // 기간은 콘솔의 기간 칩(24시간)과 같고, 서버 시각(+09:00 aware ISO)으로 간다.
        api.Verify(a => a.GetOperationEventsAsync(
            It.Is<string?>(s => s != null && s.EndsWith("+09:00")),
            It.Is<string?>(s => s != null && s.EndsWith("+09:00")),
            null, null, null, null, 1, OperationEventPanelViewModel.PageSize, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    // ───────────────────────────── 글자 규칙 ─────────────────────────────

    [Theory]
    [InlineData("ENCLOSURE_DOOR_OPEN", "함체 문 열림")]
    [InlineData("ENCLOSURE_DOOR_CLOSED", "함체 문 닫힘")]
    [InlineData("ENCLOSURE_TEMP_HIGH", "함체 고온")]
    [InlineData("ENCLOSURE_TEMP_LOW", "함체 저온")]
    [InlineData("ENCLOSURE_HUMIDITY_HIGH", "함체 고습")]
    [InlineData("ENCLOSURE_VOLTAGE_LOW", "함체 저전압")]
    [InlineData("ENCLOSURE_CURRENT_HIGH", "함체 과전류")]
    [InlineData("ENCLOSURE_VIBRATION_HIGH", "함체 진동")]
    [InlineData("ENCLOSURE_UPS_BATTERY_LOW", "UPS 배터리 부족")]
    [InlineData("GATE_OPEN", "통문 열림")]
    [InlineData("gate_closed", "통문 닫힘")]
    [InlineData("SOMETHING_NEW", "SOMETHING_NEW")]
    [InlineData("", "—")]
    [InlineData(null, "—")]
    public void should_name_every_server_operation_reason_and_keep_unknown_ones_verbatim(string? reason, string expected)
        => Assert.Equal(expected, OperationEventText.ReasonText(reason));

    [Theory]
    [InlineData("GATE_OPEN", true)]
    [InlineData("ENCLOSURE_DOOR_CLOSED", true)]
    [InlineData("DOOR_OPENED", true)]
    [InlineData("ENCLOSURE_TEMP_HIGH", false)]
    [InlineData(null, false)]
    public void should_tell_door_reasons_by_the_server_suffix_contract(string? reason, bool isDoor)
        => Assert.Equal(isDoor, OperationEventText.IsDoorReason(reason));

    [Fact]
    public void should_leave_the_detail_empty_when_the_server_sent_no_detail()
    {
        Assert.Null(OperationEventText.DetailText(null));
        Assert.Null(OperationEventText.DetailText(new JObject()));
        Assert.Equal("a=null · b={\"c\":1}", OperationEventText.DetailText(JObject.Parse("""{ "a": null, "b": { "c": 1 } }""")));
    }

    private sealed class PopupSink : IHandle<OpenInfoPopupMessageModel>
    {
        public readonly List<string> Seen = new();
        public Task HandleAsync(OpenInfoPopupMessageModel message, CancellationToken cancellationToken)
        {
            lock (Seen) Seen.Add($"{message.Title}|{message.Explain}");
            return Task.CompletedTask;
        }
    }
}
