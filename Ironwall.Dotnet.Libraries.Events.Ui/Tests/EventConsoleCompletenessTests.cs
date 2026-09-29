using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
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
   Purpose      : 이벤트 콘솔 완성도 수정 패스(2026-09-27) — 감사 E-2 · E-4 · E-5 · E-6 의 결함마다 시험 하나.
                  문구 원천(조치보고 문구 관리 목록) · 거르기 탭 · 닫기 확인 · 조치 적용 뒤 상세 · 상세 동작 줄.
****************************************************************************/

#region - 조치보고 문구 원천 -
public class ActionReportPhraseSourceTests
{
    private static ActionReportTemplateDto T(int id, string content, int order) => new() { Id = id, Content = content, DisplayOrder = order };

    [Fact]
    public void should_order_by_display_order_then_id_when_the_server_gives_templates()
    {
        var ordered = ActionReportPhraseSource.Order(new[]
        {
            T(3, "울타리 점검", 1), T(1, "오경보", 0), T(2, "강풍", 1), T(4, "  ", 0), T(5, "오경보", 2), T(6, "기타", 0),
        });

        // 빈 글 · 중복 · '기타' 는 뺀다('기타' 는 화면이 끝에 붙인다).
        Assert.Equal(new[] { "오경보", "강풍", "울타리 점검" }, ordered);
    }

    [Fact]
    public async Task should_use_the_server_list_when_the_template_api_answers()
    {
        var api = new Mock<IActionReportTemplateApiService>();
        api.Setup(a => a.GetTemplatesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<ActionReportTemplateDto> { Success = true, Data = new List<ActionReportTemplateDto> { T(9, "현장 확인 완료", 0) } });
        var source = new ActionReportPhraseSource(() => api.Object);

        var set = await source.LoadAsync();

        Assert.True(set.FromServer);
        Assert.Equal(new[] { "현장 확인 완료" }, set.Phrases);
        Assert.Same(set, source.LastKnown);
    }

    [Fact]
    public async Task should_fall_back_to_the_built_in_phrases_when_the_server_has_no_template_api()
    {
        var api = new Mock<IActionReportTemplateApiService>();
        api.Setup(a => a.GetTemplatesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiListResponse<ActionReportTemplateDto>.CreateError("NOT_SUPPORTED", "지원하지 않음", "GET → 404"));

        var set = await new ActionReportPhraseSource(() => api.Object).LoadAsync();

        Assert.False(set.FromServer);
        Assert.Equal(ActionReportPhraseSource.Fallback, set.Phrases);
    }

    [Fact]
    public async Task should_fall_back_to_the_built_in_phrases_when_the_server_list_is_empty()
    {
        var api = new Mock<IActionReportTemplateApiService>();
        api.Setup(a => a.GetTemplatesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<ActionReportTemplateDto> { Success = true, Data = new List<ActionReportTemplateDto>() });

        var set = await new ActionReportPhraseSource(() => api.Object).LoadAsync();

        Assert.False(set.FromServer);
        Assert.Equal(ActionReportPhraseSource.Fallback, set.Phrases);
    }

    [Fact]
    public async Task should_fall_back_without_throwing_when_the_api_cannot_be_resolved_or_throws()
    {
        var unresolved = await new ActionReportPhraseSource(() => null).LoadAsync();
        var throwing = await new ActionReportPhraseSource(() => throw new InvalidOperationException("no container")).LoadAsync();

        var api = new Mock<IActionReportTemplateApiService>();
        api.Setup(a => a.GetTemplatesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestExceptionStub());
        var failing = await new ActionReportPhraseSource(() => api.Object).LoadAsync();

        Assert.All(new[] { unresolved, throwing, failing }, s => Assert.Equal(ActionReportPhraseSource.Fallback, s.Phrases));
    }

    private sealed class HttpRequestExceptionStub : Exception { }
}

public class ActionTrayPhraseTests
{
    private static ActionTrayViewModel Tray() => new((_, _, _) => Task.FromResult(Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.DraftOutcome.Applied));

    [Fact]
    public void should_offer_the_managed_phrases_in_order_with_etc_last_when_the_server_list_arrives()
    {
        var tray = Tray();

        tray.ApplyPhrases(new ActionReportPhraseSet(new[] { "현장 확인 완료", "오경보(조류)" }, true));

        Assert.Equal(new[] { "현장 확인 완료", "오경보(조류)", ActionTrayViewModel.EtcPhrase }, tray.PhraseOptions);
        Assert.True(tray.PhrasesFromServer);
    }

    [Fact]
    public void should_keep_the_chosen_phrase_when_it_is_still_in_the_new_list()
    {
        var tray = Tray();
        tray.ApplyPhrases(new ActionReportPhraseSet(new[] { "A", "B" }, true));
        tray.Phrase = "B";

        tray.ApplyPhrases(new ActionReportPhraseSet(new[] { "B", "C" }, true));

        Assert.Equal("B", tray.Phrase);
    }

    [Fact]
    public void should_clear_the_chosen_phrase_when_it_was_removed_from_the_managed_list()
    {
        // 문구 콘솔에서 지운 문구로 조치보고가 나가면 안 된다 — 비우면 [조치 적용] 이 꺼지고 까닭을 말한다.
        var tray = Tray();
        tray.ApplyPhrases(new ActionReportPhraseSet(new[] { "A", "B" }, true));
        tray.Phrase = "A";

        tray.ApplyPhrases(new ActionReportPhraseSet(new[] { "B" }, true));

        Assert.Equal(string.Empty, tray.Phrase);
        Assert.False(tray.CanApply);
    }

    [Fact]
    public void should_not_say_server_calls_when_the_tray_is_emptied()
    {
        var tray = Tray();

        tray.Revert();

        Assert.DoesNotContain("서버 호출", tray.StatusLine);
        Assert.EndsWith("습니다.", tray.StatusLine);
    }
}
#endregion

#region - 억제 목록 열 폭 -
public class SuppressionGridColumnTests
{
    private static List<(string Width, double Min)> Columns()
    {
        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Views", "Consoles", "SuppressionListView.xaml");
        var xaml = System.IO.File.ReadAllText(path);
        return System.Text.RegularExpressions.Regex.Matches(xaml, @"<DataGrid(?:Text|Template)Column\b[^>]*>", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(m => (W: System.Text.RegularExpressions.Regex.Match(m.Value, @"\bWidth=""([^""]+)"""), M: System.Text.RegularExpressions.Regex.Match(m.Value, @"MinWidth=""([^""]+)""")))
            .Where(x => x.W.Success && x.M.Success)
            .Select(x => (x.W.Groups[1].Value, double.Parse(x.M.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }

    [Fact]
    public void should_fit_every_column_floor_inside_the_list_when_the_events_surface_is_1340()
    {
        // 1340 도킹 표면의 목록 보이는 폭 816(세로 스크롤바 10 을 빼면 806) — 바닥 합이 넘으면 가로 스크롤이 선다.
        Assert.True(Columns().Sum(c => c.Min) <= 806, $"바닥 합 {Columns().Sum(c => c.Min)} > 806");
    }

    [Fact]
    public void should_keep_star_ratios_equal_to_floor_ratios_when_columns_share_the_width()
    {
        // 비율이 바닥과 어긋나면 WPF 가 한 칸을 바닥으로 끌어올리며 합이 넘쳐 6px 가로 스크롤이 섰다(잘림 감사 after 1차).
        var stars = Columns().Where(c => c.Width.EndsWith("*", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(stars);
        // "*" 는 "1*" 이다.
        var ratios = stars.Select(c => c.Min / (c.Width == "*" ? 1 : double.Parse(c.Width.TrimEnd('*'), System.Globalization.CultureInfo.InvariantCulture))).ToList();
        Assert.All(ratios, r => Assert.InRange(r, ratios[0] - 0.5, ratios[0] + 0.5));
    }

    [Fact]
    public void should_leave_room_for_the_whole_target_label_when_the_target_column_renders()
    {
        // "전체 · 감지+감시" 는 글자 약 104 + 칸 여백 24 — 대상 칸이 그 아래로 줄면 잘린다(E-7 #8).
        // 범위 칸은 이제 억제 범위 하나만 싣는다(감지/감시는 대상 칸 — 실창 검토 #22).
        Assert.True(HeaderMin("대상") >= 128, $"대상 바닥 {HeaderMin("대상")}");
        Assert.True(HeaderMin("범위") >= 80, $"범위 바닥 {HeaderMin("범위")}");   // "알 수 없음" 약 55 + 여백 24
    }

    [Fact]
    public void should_give_the_leftover_width_to_the_name_column_only_when_the_list_widens()
    {
        // 실창 검토 #22 — 모든 작업명이 "[시드] 다…" 로 잘리는데 대상 · 범위는 넓었다. 남는 폭은 작업명 하나가 받는다.
        var stars = Columns().Where(c => c.Width.EndsWith("*", StringComparison.Ordinal)).ToList();
        var name = Assert.Single(stars);
        Assert.True(name.Min >= 120, $"작업명 바닥 {name.Min}");
        Assert.True(HeaderMin("반복") >= 136, $"반복 바닥 {HeaderMin("반복")}");   // "월~금 08:00~18:00" 약 108 + 여백 24
    }

    private static double HeaderMin(string header)
    {
        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Views", "Consoles", "SuppressionListView.xaml");
        var xaml = System.IO.File.ReadAllText(path);
        var column = System.Text.RegularExpressions.Regex.Matches(xaml, @"<DataGrid(?:Text|Template)Column\b[^>]*>", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(m => m.Value)
            .Single(v => v.Contains($"Header=\"{header}\"", StringComparison.Ordinal));
        return double.Parse(System.Text.RegularExpressions.Regex.Match(column, @"MinWidth=""([^""]+)""").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
#endregion

#region - 행 표시 -
[Collection("IoC-Dependent")]   // 행 뷰모델이 생성자에서 IoC 를 본다
public class EventRowLabelTests : IDisposable
{
    public EventRowLabelTests()
    {
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? ea
            : type == typeof(ILogService) ? log
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public void should_show_kind_and_id_when_the_action_row_names_its_origin()
    {
        var fromDetection = new ActionEventViewModel(new ActionEventModel { Id = 1, OriginEvent = TestEvents.Detection(607) });
        var fromMalfunction = new ActionEventViewModel(new ActionEventModel { Id = 2, OriginEvent = TestEvents.Malfunction(31) });
        var orphan = new ActionEventViewModel(new ActionEventModel { Id = 3 });

        Assert.Equal("탐지 · 607", fromDetection.OriginLabel);
        Assert.Equal("장애 · 31", fromMalfunction.OriginLabel);
        Assert.Equal("—", orphan.OriginLabel);
    }

    [Fact]
    public void should_name_the_row_for_screen_readers_instead_of_the_type_name()
    {
        var row = new DetectionEventViewModel(TestEvents.Detection(5));

        Assert.Contains("정문 1구간", row.RowSummary);
        Assert.DoesNotContain("ViewModel", row.RowSummary);
    }

    [Fact]
    public void should_hide_the_raw_server_text_when_the_action_history_fails()
    {
        var api = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        api.Setup(a => a.GetDetectionActionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<ActionEventDto> { Success = false, Message = "Internal Server Error: sqlalchemy.exc" });
        var history = new EventActionHistoryViewModel(() => api.Object);

        history.LoadAsync(EventDetailKind.Detection, 7, hasActions: true).GetAwaiter().GetResult();

        Assert.True(history.IsFailed);
        Assert.Equal(EventActionHistoryViewModel.FailureText, history.FailureReason);
        Assert.DoesNotContain("sqlalchemy", history.FailureReason);
    }
}
#endregion

#region - 콘솔 -
/// <summary>
/// 대시보드 완성도 — 거르기 탭은 목록 레일에서만 · 레일을 옮기면 상태 줄을 비운다 · [추가] 는 억제에서만 ·
/// 닫기 전에 묻는다 · 조치 적용 뒤 상세가 새 상태를 보인다 · 상세 동작 줄.
/// </summary>
[Collection("IoC-Dependent")]
public class EventConsoleCompletenessTests : IDisposable
{
    private readonly EventProvider _events = new();
    private readonly DeviceProvider _devices = new();
    private readonly EventAggregator _ea = new();
    private readonly Mock<IEventApiService> _api;
    private readonly Mock<IEventSuppressionApiService> _suppression = new(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
    private readonly PopupSink _sink = new();
    private readonly List<ActionEventDto> _serverActions = new();
    private EventDashboardViewModel _console = null!;

    public EventConsoleCompletenessTests()
    {
        var log = new Mock<ILogService>().Object;
        _api = BuildApi();
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Username).Returns("tester");

        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? _ea
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? _api.Object
            : type == typeof(EventProvider) ? _events
            : type == typeof(DeviceProvider) ? _devices
            : type == typeof(IAccountModel) ? account.Object
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new DefaultPlatformProvider();

        _suppression.Setup(s => s.GetSuppressionSchedulesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                               It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse<EventSuppressionScheduleDto> { Success = true, Data = new List<EventSuppressionScheduleDto>() });
        _suppression.Setup(s => s.GetActiveSuppressionSchedulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse<EventSuppressionScheduleDto> { Success = true, Data = new List<EventSuppressionScheduleDto>() });

        _ea.SubscribeOnPublishedThread(_sink);
        Build(phrases: null);
    }

    private void Build(IActionReportPhraseSource? phrases)
    {
        var log = IoC.Get<ILogService>();
        var providerService = new EventProviderService(log, _api.Object, _devices, _events);
        _console = new EventDashboardViewModel(
            _ea, log,
            new EventTabControlViewModel(_ea, log),
            new DetectionEventPanelViewModel(_ea, log, providerService, _devices, _events),
            new MalfunctionEventPanelViewModel(_ea, log, providerService, _devices, _events),
            new ConnectionEventPanelViewModel(_ea, log, providerService, _devices, _events),
            new ActionEventPanelViewModel(_ea, log, providerService, _events),
            new EventInfoViewModel(_devices, _events, providerService, _ea, log),
            new CameraEventInfoViewModel(_events, _ea, log),
            new DataChartPanelViewModel(_ea, log, providerService),
            suppressionApi: _suppression.Object,
            phraseSource: phrases);
        _console.UseUiThread(ImmediateUiThread.Instance);
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    private Mock<IEventApiService> BuildApi()
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<DetectionEventDto> { new() { Id = 7, TypeEvent = "Intrusion", Result = "PIR_SENSOR" } }));
        mock.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<MalfunctionEventDto>()));
        mock.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<ConnectionEventDto>()));
        mock.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<ActionEventDto>()));
        mock.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = new EventDashboardDto() });
        mock.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionEventCreateDto dto, CancellationToken _) =>
            {
                var created = new ActionEventDto { Id = 9000 + _serverActions.Count, Content = dto.Content, User = dto.User, CreatedAt = "2026-09-27T01:00:00+09:00" };
                _serverActions.Add(created);
                return new ApiResponse<ActionEventDto> { Success = true, StatusCode = 201, Data = created };
            });
        mock.Setup(a => a.GetDetectionActionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(_serverActions.ToList()));
        return mock;

        static ApiListResponse<T> List<T>(List<T> items)
            => new() { Success = true, Data = items, Pagination = new PaginationDto { Page = 1, Limit = 100, Total = items.Count, TotalPages = 1 } };
    }

    private Task Activate() => ((IActivate)_console).ActivateAsync();

    public sealed class PopupSink : IHandle<OpenConfirmPopupMessageModel>, IHandle<ClosePanelMessageModel>, IHandle<OpenDetectionHistoryDialogMessageModel>
    {
        public readonly List<OpenConfirmPopupMessageModel> Confirms = new();
        public int ClosePanels;
        public readonly List<OpenDetectionHistoryDialogMessageModel> Histories = new();
        public Task HandleAsync(OpenConfirmPopupMessageModel m, CancellationToken ct) { Confirms.Add(m); return Task.CompletedTask; }
        public Task HandleAsync(ClosePanelMessageModel m, CancellationToken ct) { ClosePanels++; return Task.CompletedTask; }
        public Task HandleAsync(OpenDetectionHistoryDialogMessageModel m, CancellationToken ct) { Histories.Add(m); return Task.CompletedTask; }
    }

    // ── 실창 검토 #18 레일 바닥 · #23 억제 레일 배지 ──
    [Fact]
    public async Task should_not_paint_the_fault_line_red_when_no_fault_is_in_progress()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        Assert.True(_console.HasRailCounts);
        Assert.Equal(0, _console.FaultCount);
        Assert.False(_console.HasFaultInProgress);
    }

    // 2026-09-28 헤디드 SC-KRN-001 — 기간 안에 이벤트가 0건이면(주말 억제) 바닥 띠가 끝내 뜨지 않았다.
    // 다른 콘솔은 '장애 0대' 처럼 0 을 보인다. 목록을 불러왔으면 0건도 센 것이다.
    [Fact]
    public async Task should_show_the_rail_footer_with_zero_counts_when_the_event_lists_load_with_no_rows()
    {
        SetupDetections(() => Page(new List<DetectionEventDto>()));
        await Activate();
        Assert.False(_console.ShowRailFooter);                      // 첫 조회 전(개요)에는 숨긴다 — 그대로

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        Assert.Equal(0, _console.DetectionPanelViewModel.ViewModelProvider.Count);
        Assert.True(_console.HasRailCounts);
        Assert.True(_console.ShowRailFooter);
        Assert.Equal("0건", _console.OpenCountText);
        Assert.Equal("0건", _console.FaultCountText);
        Assert.False(_console.HasFaultInProgress);                  // 0건은 경보색이 아니다

        await _console.SelectRailAsync(EventDashboardViewModel.SuppressionRailKey);
        Assert.False(_console.ShowRailFooter);                      // 억제 스케줄 레일에서는 여전히 숨긴다
    }

    [Fact]
    public async Task should_keep_the_rail_footer_hidden_when_the_first_list_load_fails()
    {
        SetupDetections(() => ApiListResponse<DetectionEventDto>.CreateError("SERVER_ERROR", "실패", "GET → 500"));
        await Activate();

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        Assert.False(_console.HasRailCounts);                       // 못 불러온 것을 '0건' 이라 하면 거짓이다
        Assert.False(_console.ShowRailFooter);
    }

    private void SetupDetections(Func<ApiListResponse<DetectionEventDto>> answer)
        => _api.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                      It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                      It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(answer);

    private static ApiListResponse<T> Page<T>(List<T> items)
        => new() { Success = true, Data = items, Pagination = new PaginationDto { Page = 1, Limit = 100, Total = items.Count, TotalPages = 1 } };

    [Fact]
    public async Task should_show_the_schedule_count_on_the_suppression_rail_when_none_is_suppressing_now()
    {
        var rows = Enumerable.Range(1, 3).Select(i => new EventSuppressionScheduleDto
        {
            Id = i, Name = $"정비-{i}", Status = "pending", IsSuppressingNow = false,
            TargetType = "all", WindowStart = "2026-09-20T09:00:00.000+09:00", WindowEnd = "2026-09-20T18:00:00.000+09:00",
        }).ToList();
        _suppression.Setup(s => s.GetSuppressionSchedulesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                               It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse<EventSuppressionScheduleDto>
            {
                Success = true, Data = rows, Pagination = new PaginationDto { Page = 1, Limit = 100, Total = rows.Count, TotalPages = 1 },
            });
        await Activate();
        var rail = _console.RailEntries.Single(e => e.Key == EventDashboardViewModel.SuppressionRailKey);
        Assert.Equal(string.Empty, rail.CountText);                  // 한 번도 불러오기 전에는 "0" 이라고 거짓말하지 않는다

        await _console.SelectRailAsync(EventDashboardViewModel.SuppressionRailKey);

        Assert.Equal("3", rail.CountText);                           // 예전엔 '억제중 0'(목록 55건 옆에 0 — 실창 #23)
    }

    // ── E-2 #1 · #2 거르기 탭 ──
    [Fact]
    public async Task should_hide_the_detection_filter_tabs_when_the_overview_or_suppression_rail_is_shown()
    {
        await Activate();

        await _console.SelectRailAsync(EventDashboardViewModel.OverviewRailKey);
        Assert.False(_console.HasFilterChips);
        Assert.Equal(EventDetailKind.Overview, _console.CurrentKind);

        await _console.SelectRailAsync(EventDashboardViewModel.SuppressionRailKey);
        Assert.False(_console.HasFilterChips);
        Assert.Empty(_console.FilterChips);

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        Assert.True(_console.HasFilterChips);
    }

    // ── E-3 #6 개요 상세 ──
    [Fact]
    public async Task should_title_the_detail_as_overview_when_the_overview_rail_is_shown()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        await _console.SelectRailAsync(EventDashboardViewModel.OverviewRailKey);

        Assert.Equal("개요", _console.Detail.Kind);
        Assert.DoesNotContain("탐지", _console.DetailView.EmptyHint);
    }

    // ── 실창 육안 검토 #19 — 개요는 고를 행이 없어 상세 340 이 늘 비었다 ──
    [Fact]
    public async Task should_fold_the_detail_column_only_on_the_overview_rail()
    {
        await Activate();

        await _console.SelectRailAsync(EventDashboardViewModel.OverviewRailKey);
        Assert.False(_console.IsDetailAvailable);

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        Assert.True(_console.IsDetailAvailable);

        await _console.SelectRailAsync(EventDashboardViewModel.SuppressionRailKey);
        Assert.True(_console.IsDetailAvailable);
    }

    // ── E-2 #3 상태 줄 ──
    [Fact]
    public async Task should_clear_the_status_message_when_the_rail_changes()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        _console.CancelQuery();                         // 상태 줄에 한 줄을 남긴다
        Assert.NotEqual(string.Empty, _console.StatusText);

        await _console.SelectRailAsync(EventDashboardViewModel.ConnectionRailKey);

        Assert.Equal(string.Empty, _console.StatusText);
    }

    // ── E-2 #10 · E-3 #7 [추가] · [삭제] ──
    [Fact]
    public async Task should_offer_add_only_on_the_suppression_rail_and_no_delete_on_the_overview()
    {
        await Activate();

        await _console.SelectRailAsync(EventDashboardViewModel.OverviewRailKey);
        Assert.False(_console.ShowAdd);
        Assert.False(_console.ShowDelete);

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        Assert.False(_console.ShowAdd);            // 빈 행을 목록 끝에 붙이던 [이벤트 추가] 는 감춘다
        Assert.False(_console.CanAdd);
        Assert.True(_console.ShowDelete);

        await _console.SelectRailAsync(EventDashboardViewModel.SuppressionRailKey);
        Assert.True(_console.ShowAdd);
        Assert.Equal("새 스케줄", _console.AddButtonText);
    }

    // ── E-2 #6 트레이 담기 버튼 ──
    [Fact]
    public async Task should_call_the_toolbar_button_put_in_tray_when_rows_are_selected()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        _console.SetSelection(new object[] { new DetectionEventViewModel(TestEvents.Detection(1)), new DetectionEventViewModel(TestEvents.Detection(2)) });

        Assert.Equal("2건 트레이에 담기", _console.QueueButtonText);
    }

    // ── E-2 #9 닫기 확인 ──
    [Fact]
    public async Task should_ask_before_closing_when_the_tray_holds_unsent_reports()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        var row = _console.DetectionPanelViewModel.ViewModelProvider.First();
        _console.SetSelection(new object[] { row });
        _console.QueueSelection();
        Assert.True(_console.Tray.HasEntries);

        var canClose = await _console.CanCloseAsync();

        Assert.False(canClose);
        var confirm = Assert.Single(_sink.Confirms);
        Assert.Contains("보내지 않은 조치보고 1건", confirm.Explain);
        Assert.IsType<CallCloseEventConsoleMessageModel>(confirm.MessageModel);
    }

    [Fact]
    public async Task should_close_without_asking_again_when_the_close_was_confirmed()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        _console.SetSelection(new object[] { _console.DetectionPanelViewModel.ViewModelProvider.First() });
        _console.QueueSelection();

        await _console.HandleAsync(new CallCloseEventConsoleMessageModel(), CancellationToken.None);

        Assert.Equal(1, _sink.ClosePanels);            // 닫기를 다시 청한다
        Assert.True(await _console.CanCloseAsync());   // 이번에는 묻지 않는다
    }

    [Fact]
    public async Task should_close_without_asking_when_nothing_is_pending()
    {
        await Activate();

        Assert.True(await _console.CanCloseAsync());
        Assert.Empty(_sink.Confirms);
        Assert.Null(_console.PendingWorkSummary());
    }

    [Fact]
    public async Task should_ask_before_closing_when_the_detail_has_unapplied_changes()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        _console.SetSelection(new object[] { _console.DetectionPanelViewModel.ViewModelProvider.First() });
        _console.Detail.Tracker.Touch(EventDetailProjection.FieldResult, "없음", "케이블 절단");

        Assert.False(await _console.CanCloseAsync());
        Assert.Contains("상세", _sink.Confirms.Single().Explain);
    }

    // ── E-5 #1 조치 적용 뒤 상세 ──
    [Fact]
    public async Task should_show_the_new_action_state_in_the_detail_when_the_tray_is_applied()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        var row = (DetectionEventViewModel)_console.DetectionPanelViewModel.ViewModelProvider.First();
        _console.SetSelection(new object[] { row });
        Assert.Equal("미조치", StatusFieldText());

        _console.QueueSelection();
        _console.Tray.Phrase = _console.Tray.PhraseOptions[0];
        var summary = await _console.ApplyTrayAsync();
        await WaitUntil(() => _console.DetailView.Actions.State == ActionHistoryState.Loaded);

        Assert.Equal(1, summary.Applied);
        Assert.True(row.IsActionReported);
        Assert.Equal("조치 1건", StatusFieldText());                 // 예전엔 '미조치' 로 남았다(실창 020)
        Assert.Single(_console.DetailView.Actions.Lines);             // 조치 내역을 서버에 다시 물었다
        Assert.Equal("조치보고 추가", _console.DetailView.ReportButtonText);
    }

    // ── GAP-C10 (헤디드 r18-e1 EVT-E2E-059): 다른 GIS 가 조치하면 열린 콘솔 행이 다시 조회 없이 '조치됨' 으로 ──
    //   서버는 조치 뒤 SYNC_DETECTION 을 내지 않는다(EVT-E2E-140 관측) — 원격 ACTION_REPORT 만이 알 길인데
    //   콘솔은 로컬 보고(Detection/MalfunctionReportedMessageModel)만 받아 행이 '미조치' 로 남았다.
    [Fact]
    public async Task should_mark_the_row_reported_when_another_gis_reports_its_event()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        var row = (DetectionEventViewModel)_console.DetectionPanelViewModel.ViewModelProvider.First();
        Assert.False(row.IsActionReported);

        await _console.HandleAsync(new Ironwall.Dotnet.Libraries.Events.Ui.Models.RemoteActionReportedMessage(ActionReportKind.Detection, row.Model!.Id), CancellationToken.None);

        Assert.True(row.IsActionReported);
    }

    [Fact]
    public async Task should_leave_the_detection_row_when_a_malfunction_with_the_same_id_is_reported_elsewhere()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        var row = (DetectionEventViewModel)_console.DetectionPanelViewModel.ViewModelProvider.First();

        await _console.HandleAsync(new Ironwall.Dotnet.Libraries.Events.Ui.Models.RemoteActionReportedMessage(ActionReportKind.Malfunction, row.Model!.Id), CancellationToken.None);

        Assert.False(row.IsActionReported);
    }

    private string StatusFieldText()
        => _console.DetailView.Sections.SelectMany(s => s.Fields).First(f => f.Key == "status").Text;

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(10);
    }

    // ── E-6 #2 문구 원천 ──
    [Fact]
    public async Task should_offer_the_managed_phrases_in_the_tray_when_the_console_opens()
    {
        var source = new Mock<IActionReportPhraseSource>();
        source.SetupGet(s => s.LastKnown).Returns(new ActionReportPhraseSet(ActionReportPhraseSource.Fallback, false));
        source.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ActionReportPhraseSet(new[] { "현장 확인 완료", "순찰 출동" }, true));
        Build(source.Object);

        await _console.RefreshPhrasesAsync();

        Assert.Equal(new[] { "현장 확인 완료", "순찰 출동", ActionTrayViewModel.EtcPhrase }, _console.Tray.PhraseOptions);
    }

    [Fact]
    public void should_start_with_the_built_in_phrases_when_no_phrase_source_is_registered()
    {
        Assert.Equal(ActionReportPhraseSource.Fallback.Concat(new[] { ActionTrayViewModel.EtcPhrase }), _console.Tray.PhraseOptions);
    }

    // ── SYNC_ACTION_REPORT_TEMPLATE — 다른 곳에서 문구 목록이 바뀌었다 ──
    private (Mock<IActionReportPhraseSource> Source, Func<int> Loads, Action<string[]> Serve) PhraseSourceServing(params string[] first)
    {
        var current = new ActionReportPhraseSet(first, true);
        var loads = 0;
        var source = new Mock<IActionReportPhraseSource>();
        source.SetupGet(s => s.LastKnown).Returns(new ActionReportPhraseSet(ActionReportPhraseSource.Fallback, false));
        source.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => { loads++; return current; });
        return (source, () => loads, phrases => current = new ActionReportPhraseSet(phrases, true));
    }

    [Fact]
    public async Task should_swap_the_tray_phrases_when_the_templates_change_elsewhere_and_nothing_waits_in_the_tray()
    {
        var (source, _, serve) = PhraseSourceServing("현장 확인 완료");
        Build(source.Object);
        await Activate();
        serve(new[] { "현장 확인 완료", "순찰 출동" });

        await _console.HandleAsync(new ActionReportTemplatesChangedMessage("CREATED", 9), CancellationToken.None);
        await _console.PhraseRefreshTask;

        Assert.Equal(new[] { "현장 확인 완료", "순찰 출동", ActionTrayViewModel.EtcPhrase }, _console.Tray.PhraseOptions);
    }

    [Fact]
    public async Task should_leave_the_tray_phrases_alone_when_reports_wait_in_the_tray()
    {
        var (source, loads, serve) = PhraseSourceServing("현장 확인 완료", "순찰 출동");
        Build(source.Object);
        await Activate();
        _console.Tray.Enqueue(ActionTrayDrop.Plan(new[] { new ActionTrayCandidate(41, ActionTrayDrop.KindDetection, "센서-41", false) }, canControl: true));
        _console.Tray.Phrase = "순찰 출동";
        var before = loads();
        serve(new[] { "현장 확인 완료" });                       // 고른 문구가 다른 곳에서 지워졌다

        await _console.HandleAsync(new ActionReportTemplatesChangedMessage("DELETED", 2), CancellationToken.None);
        await _console.PhraseRefreshTask;

        Assert.Equal(before, loads());
        Assert.Equal("순찰 출동", _console.Tray.Phrase);          // 보내기 직전의 선택을 비우지 않는다
    }

    // ── E-5 #2 상세 동작 줄 ──
    [Fact]
    public async Task should_offer_report_and_history_actions_when_one_sensor_detection_is_selected()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        var row = new DetectionEventViewModel(TestEvents.Detection(41));

        _console.SetSelection(new object[] { row });

        Assert.True(_console.DetailView.ShowReportAction);
        Assert.True(_console.DetailView.ShowHistoryAction);
        Assert.False(_console.DetailView.ShowOpenOriginAction);
        Assert.True(_console.DetailView.HasActionRow);
    }

    [Fact]
    public async Task should_open_the_detection_history_for_that_sensor_when_the_detail_asks()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        _console.SetSelection(new object[] { new DetectionEventViewModel(TestEvents.Detection(41)) });

        _console.DetailView.Request(EventDetailAction.DetectionHistory);
        await WaitUntil(() => _sink.Histories.Count > 0);

        var message = Assert.Single(_sink.Histories);
        Assert.Equal(101, message.DeviceId);
        Assert.Equal("정문 1구간", message.DeviceName);
    }

    [Fact]
    public async Task should_offer_tray_queue_instead_of_report_when_several_rows_are_selected()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        _console.SetSelection(new object[] { new DetectionEventViewModel(TestEvents.Detection(1)), new DetectionEventViewModel(TestEvents.Detection(2)) });

        Assert.True(_console.DetailView.ShowQueueAction);
        Assert.False(_console.DetailView.ShowReportAction);
        Assert.Equal("2건 트레이에 담기", _console.DetailView.ReportButtonText);
    }

    [Fact]
    public async Task should_select_the_origin_row_in_its_rail_when_open_origin_is_asked_from_an_action_row()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.ActionRailKey);
        var action = new ActionEventViewModel(new ActionEventModel { Id = 55, OriginEvent = TestEvents.Detection(7), Content = "오경보" });
        _console.SetSelection(new object[] { action });
        Assert.True(_console.DetailView.ShowOpenOriginAction);

        await _console.OpenOriginAsync();

        Assert.True(_console.IsDetectionRail);
        var selected = Assert.Single(_console.SelectedRows);
        Assert.Equal(7, ((DetectionEventViewModel)selected).Model!.Id);
    }

    [Fact]
    public async Task should_say_the_origin_is_outside_the_period_when_it_is_not_loaded()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.ActionRailKey);
        _console.SetSelection(new object[] { new ActionEventViewModel(new ActionEventModel { Id = 56, OriginEvent = TestEvents.Detection(999) }) });

        await _console.OpenOriginAsync();

        Assert.True(_console.IsDetectionRail);
        Assert.Contains("기간", _console.StatusText);
        Assert.Empty(_console.SelectedRows);
    }
}
#endregion
