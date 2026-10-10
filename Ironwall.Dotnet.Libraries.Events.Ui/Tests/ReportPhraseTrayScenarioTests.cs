using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
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
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 시나리오 카탈로그 §3.4 RPT-025 — 조치보고 문구 콘솔에서 문구를 등록하면 이벤트 콘솔 조치 트레이의
                  문구 목록(Console.Events.Tray.Phrase)에 새 문구가 보인다.
   Notes        : 전 구간을 하나의 가짜 문구 서버(상태를 가진 Moq)로 잇는다 —
                  ① 문구 콘솔의 등록 = 같은 서버에 POST(보고서 쪽 등록 계약은 Reports.Ui/Tests/ReportLeftoverScenarioTests 가 단언)
                  ② 서버 SYNC_ACTION_REPORT_TEMPLATE → 호스트 라우터 → ActionReportTemplatesChangedMessage(호스트 몫 — 여기서는 그 메시지를 바로 준다)
                  ③ 이벤트 콘솔이 진짜 ActionReportPhraseSource 로 다시 읽어 트레이에 갈아 끼운다.
                  창은 띄우지 않는다. 운영 서버 · 실제 NATS 없음.
****************************************************************************/
[Collection("IoC-Dependent")]
public class ReportPhraseTrayScenarioTests : IDisposable
{
    private readonly EventProvider _events = new();
    private readonly DeviceProvider _devices = new();
    private readonly EventAggregator _ea = new();
    private readonly Mock<IEventApiService> _api = new(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
    private readonly Mock<IEventSuppressionApiService> _suppression = new(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };

    /// <summary>가짜 문구 서버의 저장소 — 문구 콘솔과 이벤트 콘솔이 같은 것을 본다.</summary>
    private readonly List<ActionReportTemplateDto> _server = new()
    {
        new() { Id = 1, Content = "야생동물출현", DisplayOrder = 0 },
        new() { Id = 2, Content = "강풍/폭우", DisplayOrder = 1 },
        new() { Id = 3, Content = "오경보", DisplayOrder = 2 },
    };
    private readonly Mock<IActionReportTemplateApiService> _phraseServer = new(MockBehavior.Loose);
    private EventDashboardViewModel _console = null!;

    public ReportPhraseTrayScenarioTests()
    {
        var log = new Mock<ILogService>().Object;
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

        SetupEventApi();
        _suppression.Setup(s => s.GetSuppressionSchedulesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                               It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse<EventSuppressionScheduleDto> { Success = true, Data = new List<EventSuppressionScheduleDto>() });
        _suppression.Setup(s => s.GetActiveSuppressionSchedulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse<EventSuppressionScheduleDto> { Success = true, Data = new List<EventSuppressionScheduleDto>() });

        // 가짜 문구 서버 — 목록은 늘 지금 저장소를, 등록은 저장소에 더한다(실제 서버처럼 id 를 매긴다).
        _phraseServer.Setup(s => s.GetTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApiListResponse<ActionReportTemplateDto> { Success = true, Data = _server.ToList() });
        _phraseServer.Setup(s => s.CreateTemplateAsync(It.IsAny<ActionReportTemplateCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionReportTemplateCreateDto dto, CancellationToken _) =>
            {
                var created = new ActionReportTemplateDto { Id = _server.Max(t => t.Id) + 1, Content = dto.Content, DisplayOrder = dto.DisplayOrder };
                _server.Add(created);
                return new ApiResponse<ActionReportTemplateDto> { Success = true, StatusCode = 201, Data = created };
            });

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
            phraseSource: new ActionReportPhraseSource(() => _phraseServer.Object, log));
        _console.UseUiThread(ImmediateUiThread.Instance);
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    private void SetupEventApi()
    {
        _api.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<DetectionEventDto>()));
        _api.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<MalfunctionEventDto>()));
        _api.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<ConnectionEventDto>()));
        _api.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => List(new List<ActionEventDto>()));
        _api.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = new EventDashboardDto() });

        static ApiListResponse<T> List<T>(List<T> items)
            => new() { Success = true, Data = items, Pagination = new PaginationDto { Page = 1, Limit = 100, Total = items.Count, TotalPages = 1 } };
    }

    private Task Activate() => ((IActivate)_console).ActivateAsync();

    /// <summary>문구 콘솔의 [등록] 이 서버에 남기는 것과 같은 POST — 맨 끝(display_order = 지금 최댓값 + 1).</summary>
    private async Task<int> RegisterPhraseAsync(string content)
    {
        var res = await _phraseServer.Object.CreateTemplateAsync(
            new ActionReportTemplateCreateDto { Content = content, DisplayOrder = _server.Max(t => t.DisplayOrder) + 1 });
        return res.Data!.Id;
    }

    private static readonly string[] Seeded = { "야생동물출현", "강풍/폭우", "오경보" };

    [Trait("Scenario", "RPT-025")]
    [Fact]
    public async Task should_show_the_newly_registered_phrase_in_the_event_tray_when_the_template_change_arrives()
    {
        await Activate();
        await _console.RefreshPhrasesAsync();
        Assert.Equal(Seeded.Append(ActionTrayViewModel.EtcPhrase), _console.Tray.PhraseOptions);
        Assert.True(_console.Tray.PhrasesFromServer);

        var id = await RegisterPhraseAsync("차량 통제");
        Assert.DoesNotContain("차량 통제", _console.Tray.PhraseOptions);         // 알림 전에는 그대로(트레이가 서버를 엿보지 않는다)

        await _console.HandleAsync(new ActionReportTemplatesChangedMessage("CREATED", id), CancellationToken.None);
        await _console.PhraseRefreshTask;

        // 새 문구는 서버 순서(display_order)대로 맨 끝, '기타' 는 여전히 마지막
        Assert.Equal(Seeded.Append("차량 통제").Append(ActionTrayViewModel.EtcPhrase), _console.Tray.PhraseOptions);
        Assert.True(_console.Tray.PhrasesFromServer);
    }

    [Trait("Scenario", "RPT-025")]
    [Fact]
    public async Task should_show_the_phrase_registered_while_the_event_console_was_closed_when_it_opens_again()
    {
        var id = await RegisterPhraseAsync("순찰 출동");
        await _console.HandleAsync(new ActionReportTemplatesChangedMessage("CREATED", id), CancellationToken.None);   // 닫혀 있으면 무시한다

        await Activate();                                                     // 열 때마다 다시 읽는다
        await _console.RefreshPhrasesAsync();

        Assert.Contains("순찰 출동", _console.Tray.PhraseOptions);
        Assert.Equal(ActionTrayViewModel.EtcPhrase, _console.Tray.PhraseOptions.Last());
    }

    // 자동화 id Console.Events.Tray.Phrase 가 바로 이 목록(Tray.PhraseOptions)을 보인다 — 위 두 시험이 단언한 목록이 화면의 그 칸이다.
    [Trait("Scenario", "RPT-025")]
    [Fact]
    public void should_bind_the_tray_phrase_box_to_the_phrase_options()
    {
        var xaml = File.ReadAllText(Path.Combine(EventsUiRoot(), "Views", "Consoles", "ActionTrayView.xaml"));
        var tag = Regex.Match(xaml, "<ComboBox\\b[^>]*AutomationProperties\\.AutomationId=\"Console\\.Events\\.Tray\\.Phrase\"[^>]*>", RegexOptions.Singleline);

        Assert.True(tag.Success, "Console.Events.Tray.Phrase 콤보를 찾지 못했다");
        Assert.Contains("ItemsSource=\"{Binding Tray.PhraseOptions}\"", tag.Value);
    }

    private static string EventsUiRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));
}
