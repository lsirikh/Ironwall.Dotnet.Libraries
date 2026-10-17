using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Events.Ui.Views.Dashboards;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 이벤트 콘솔 <b>실제 뷰</b> — 탐지 목록 위 필터 칩 줄(정본 L2310-2313)이 좁은 폭에서도 모든 칩에 닿는다.
/// </summary>
/// <remarks>
/// 커밋 2c7030c2(커널 <see cref="ConsoleChipStrip"/> — 넘치면 끝 자리가 [⌄ 더 보기 +N])를 계정 콘솔에 이어 이 필터 줄에도 적용한
/// 뒤의 회귀 — <see cref="AccountPermissionGroupStripViewTests"/> 와 같은 결로, 진짜 UIA 클라이언트(다른 스레드)로 [더 보기]를
/// 토글해 접힌 칩까지 화면에 서는지 본다. 탐지 레일은 칩이 가장 많다(전체 · 침입 · 사전 경보 · 접점·강풍 · 미조치 · 조치 있음 — 6개,
/// <c>EventListFilter.ChipsFor</c>). 그 6개가 900 · 1100px 에서 실제로 넘치는지는 폰트 · DPI 에 달려 있어 단정하지 않는다 —
/// 넘치면 [더 보기] 경로를, 안 넘치면 이미 다 보이는 경로를 확인한다. 둘 다 "칩 전부에 닿는다"는 같은 계약이다.
/// </remarks>
public class EventConsoleFilterStripViewTests
{
    private const string StripId = "Console.Events.FilterStrip";
    private const string MoreId = StripId + ".More";

    [Fact]
    public void should_reach_every_filter_chip_by_automation_when_the_strip_is_1100_pixels_wide() => AppHost.Run(() =>
    {
        var (view, window, console) = HostDetectionRail(width: 1100);
        try
        {
            AssertAllFilterChipsReachable(view, window, console);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_reach_every_filter_chip_by_automation_when_the_strip_is_900_pixels_wide() => AppHost.Run(() =>
    {
        var (view, window, console) = HostDetectionRail(width: 900);
        try
        {
            AssertAllFilterChipsReachable(view, window, console);
        }
        finally { window.Close(); }
    });

    /// <summary>
    /// 강제로 좁혀 넘침을 만든다(300px — 칩 6개가 한 줄에 다 들어갈 수 없다) — [더 보기] 경로 자체를 반드시 지나가게 한다.
    /// </summary>
    [Fact]
    public void should_show_the_more_toggle_and_reach_every_hidden_chip_when_the_strip_overflows() => AppHost.Run(() =>
    {
        var (view, window, console) = HostDetectionRail(width: 300);
        try
        {
            var strip = RailProbe.Find<ConsoleChipStrip>(view, s => AutomationProperties.GetAutomationId(s) == StripId)!;
            Assert.True(strip.HiddenCount > 0, "300px 폭에서는 칩 6개가 다 들어가지 못해야 한다");
            var toggle = RailProbe.Find<ConsoleChipStripToggle>(view, t => AutomationProperties.GetAutomationId(t) == MoreId);
            Assert.True(toggle is { IsVisible: true }, "넘치면 [더 보기]가 보여야 한다");

            AssertAllFilterChipsReachable(view, window, console);
        }
        finally { window.Close(); }
    });

    /// <summary>접힌 · (필요하면) 펼친 뒤 <see cref="EventDashboardViewModel.FilterChips"/> 의 칩이 전부 화면에 서는지 — 진짜 UIA 로.</summary>
    private static void AssertAllFilterChipsReachable(EventDashboardView view, Window window, EventDashboardViewModel console)
    {
        var keys = console.FilterChips.Select(c => c.Key).ToList();
        Assert.True(keys.Count > 0, "탐지 레일은 칩이 있어야 한다");

        var strip = RailProbe.Find<ConsoleChipStrip>(view, s => AutomationProperties.GetAutomationId(s) == StripId)!;
        var hwnd = new WindowInteropHelper(window).Handle;

        if (strip.HiddenCount == 0)
        {
            // 넘치지 않았다 — 접힌 그대로 칩마다 폭이 이미 있어야 한다(가려진 칩이 없다)
            foreach (var key in keys)
            {
                var chip = RailProbe.Find<RadioButton>(view, r => AutomationProperties.GetAutomationId(r) == $"Console.Events.Filter.{key}");
                Assert.True(chip is { ActualWidth: > 0 }, $"넘치지 않았으면 칩 '{key}' 이 이미 화면에 있어야 한다");
            }
            return;
        }

        // 넘쳤다 — [더 보기]를 진짜 UIA 클라이언트(다른 스레드)로 토글해 전부 닿는지 본다
        var reading = RunUia(() =>
        {
            var top = AutomationElement.FromHandle(hwnd);
            var more = Wait(() => top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, MoreId)));
            Assert.True(more is not null, "[더 보기] 가 UIA 트리에 없다");
            ((TogglePattern)more!.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            Thread.Sleep(200);
            var widths = keys.ToDictionary(k => k, k => Wait(() =>
                top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, $"Console.Events.Filter.{k}"))
                   ?.Current.BoundingRectangle is { IsEmpty: false, Width: > 0 } r ? r : (Rect?)null));
            var expandedName = more.Current.Name;
            return (widths, expandedName);
        });

        var missing = reading.widths.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList();
        Assert.True(missing.Count == 0, "펼친 뒤 화면에 서지 않은 필터 칩: " + string.Join(",", missing));
        Assert.True(strip.IsExpanded, "UIA 토글이 줄을 펼쳐야 한다");
        Assert.Equal(ConsoleChipStripToggle.CollapseText, reading.expandedName);
    }

    private static (EventDashboardView View, Window Window, EventDashboardViewModel Console) HostDetectionRail(double width)
    {
        var events = new EventProvider();
        var devices = new DeviceProvider();
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var api = BuildApi();
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Name).Returns("tester");

        RailProbe.UseIoC(type =>
            type == typeof(IEventAggregator) ? ea
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? api
            : type == typeof(EventProvider) ? events
            : type == typeof(DeviceProvider) ? devices
            : type == typeof(IAccountModel) ? account.Object
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : null);

        var providerService = new EventProviderService(log, api, devices, events);
        var console = new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, providerService, devices, events),
            new MalfunctionEventPanelViewModel(ea, log, providerService, devices, events),
            new ConnectionEventPanelViewModel(ea, log, providerService, devices, events),
            new ActionEventPanelViewModel(ea, log, providerService, events),
            new EventInfoViewModel(devices, events, providerService, ea, log),
            new CameraEventInfoViewModel(events, ea, log),
            new DataChartPanelViewModel(ea, log, providerService));
        RailProbe.Wait(((IActivate)console).ActivateAsync());
        RailProbe.Wait(console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey));

        var view = new EventDashboardView { Width = width, Height = 900 };
        var window = RailProbe.Host(console, view);
        AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.True(console.HasFilterChips, "탐지 레일은 필터 칩 줄이 보여야 한다");
        return (view, window, console);
    }

    /// <summary>빈 목록을 돌려주는 가짜 이벤트 API(EventConsoleRailViewTests 와 같은 모양).</summary>
    private static IEventApiService BuildApi()
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Of(new List<DetectionEventDto>()));
        mock.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<MalfunctionEventDto>());
        mock.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<ConnectionEventDto>());
        mock.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                                               It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<ActionEventDto>());
        mock.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = new EventDashboardDto() });
        return mock.Object;

        static ApiListResponse<T> Empty<T>() => Of(new List<T>());

        static ApiListResponse<T> Of<T>(List<T> items)
            => new() { Success = true, Data = items, Pagination = new PaginationDto { Page = 1, Limit = 100, Total = items.Count, TotalPages = 1 } };
    }

    /// <summary>UIA 클라이언트는 다른 스레드에서 — 앱 디스패처는 그동안 돌려 둔다(같은 스레드면 교착).</summary>
    private static T RunUia<T>(Func<T> body)
    {
        var task = Task.Run(body);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(System.Windows.Threading.DispatcherPriority.Background);
        Assert.True(task.IsCompleted, "UIA 클라이언트 제한 시간 초과");
        return task.GetAwaiter().GetResult();
    }

    /// <summary>레이아웃 · UIA 트리 갱신이 한 바퀴 돌 때까지 짧게 다시 본다(최대 3초).</summary>
    private static T? Wait<T>(Func<T?> probe)
    {
        for (var i = 0; i < 30; i++)
        {
            var value = probe();
            if (value is not null) return value;
            Thread.Sleep(100);
        }
        return default;
    }
}
