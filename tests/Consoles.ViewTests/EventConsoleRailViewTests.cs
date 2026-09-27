using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Events.Ui.Views.Dashboards;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 이벤트 콘솔 <b>실제 뷰</b> — 미적용 변경으로 레일 전환이 막히면 화면의 레일도 앞 레일('탐지')로 돌아와야 한다.
/// </summary>
public class EventConsoleRailViewTests
{
    [Fact]
    public void should_put_the_rail_selection_back_to_detections_when_the_switch_is_refused_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            Assert.True(RailProbe.Wait(console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey)));
            AppHost.Pump();
            console.Detail.Tracker.Touch(EventDetailProjection.FieldResult, "없음", "케이블 절단");
            Assert.True(console.Detail.Tracker.IsDirty);

            var rail = RailProbe.Rail(view, "Console.Events.Rail");
            Assert.Equal(EventDashboardViewModel.DetectionRailKey, RailProbe.SelectedKey(rail));

            RailProbe.Select(rail, EventDashboardViewModel.MalfunctionRailKey);
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(EventDashboardViewModel.DetectionRailKey, console.SelectedRail?.Key);
            Assert.Same(console.DetectionPanelViewModel, console.TabControlViewModel.ActiveItem);
            Assert.True(console.Detail.Tracker.IsDirty, "막힌 뒤에도 고친 칸은 남아야 한다");
            RailProbe.AssertShows(rail, EventDashboardViewModel.DetectionRailKey, refused: EventDashboardViewModel.MalfunctionRailKey);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_move_the_rail_when_there_are_no_unapplied_changes_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            Assert.True(RailProbe.Wait(console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey)));
            AppHost.Pump();
            var rail = RailProbe.Rail(view, "Console.Events.Rail");

            RailProbe.Select(rail, EventDashboardViewModel.MalfunctionRailKey);
            Assert.True(RailProbe.ItemSelected(rail, EventDashboardViewModel.MalfunctionRailKey), "누른 즉시 새 레일이 선택으로 보여야 한다 · " + RailProbe.State(rail));
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(EventDashboardViewModel.MalfunctionRailKey, console.SelectedRail?.Key);
            Assert.Same(console.MalfunctionPanelViewModel, console.TabControlViewModel.ActiveItem);
            RailProbe.AssertShows(rail, EventDashboardViewModel.MalfunctionRailKey, refused: EventDashboardViewModel.DetectionRailKey);
        }
        finally { window.Close(); }
    });

    private static (EventDashboardView View, System.Windows.Window Window, EventDashboardViewModel Console) HostConsole()
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

        var view = new EventDashboardView { Width = 1400, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window, console);
    }

    /// <summary>빈 목록을 돌려주는 가짜 이벤트 API(EventConsoleNavigationTests 와 같은 모양).</summary>
    private static IEventApiService BuildApi()
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<DetectionEventDto>());
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

        static ApiListResponse<T> Empty<T>()
            => new() { Success = true, Data = new List<T>(), Pagination = new PaginationDto { Page = 1, Limit = 100, Total = 0, TotalPages = 1 } };
    }
}
