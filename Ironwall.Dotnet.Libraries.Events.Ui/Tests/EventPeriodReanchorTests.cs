using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 기간 칩 '오늘 · 24시간 · 7일' 은 <b>지금까지</b>를 뜻한다 — [갱신] · 같은 칩 다시 누르기가 끝을 지금으로 옮겨야 한다.
/// </summary>
/// <remarks>
/// 2026-09-28 헤디드 SC-EVT-027 · SC-EVT-042 — 창을 연 뒤 만든 조치가 조치 레일에 끝내 나오지 않았다: 기간 끝이 '창을 연 때'로 굳어
/// [갱신]도, 이미 눌린 [24시간]을 다시 눌러도(값이 같아 아무 일도 안 함) 그 뒤의 이벤트를 불러오지 않았다(덤프: 기간 09-27 18:41 ~ 09-28 18:41,
/// 조치는 18:42 에 생김). '직접' 기간은 사람이 정한 범위라 그대로 둔다.
/// </remarks>
[Collection("IoC-Dependent")]
public class EventPeriodReanchorTests : IDisposable
{
    private readonly EventDashboardViewModel _console;

    public EventPeriodReanchorTests()
    {
        var events = new EventProvider();
        var devices = new DeviceProvider();
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var api = BuildApi();
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Name).Returns("tester");

        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? ea
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? api
            : type == typeof(EventProvider) ? events
            : type == typeof(DeviceProvider) ? devices
            : type == typeof(IAccountModel) ? account.Object
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new DefaultPlatformProvider();

        var providerService = new EventProviderService(log, api, devices, events);
        _console = new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, providerService, devices, events),
            new MalfunctionEventPanelViewModel(ea, log, providerService, devices, events),
            new ConnectionEventPanelViewModel(ea, log, providerService, devices, events),
            new ActionEventPanelViewModel(ea, log, providerService, events),
            new EventInfoViewModel(devices, events, providerService, ea, log),
            new CameraEventInfoViewModel(events, ea, log),
            new DataChartPanelViewModel(ea, log, providerService));
        _console.UseUiThread(ImmediateUiThread.Instance);
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    private async Task OpenOnActions()
    {
        await ((IActivate)_console).ActivateAsync();
        await _console.SelectRailAsync(EventDashboardViewModel.ActionRailKey);
    }

    [Fact]
    public async Task should_move_the_period_end_to_now_when_a_rolling_preset_is_refreshed()
    {
        await OpenOnActions();
        Assert.Equal("24시간", _console.Period);
        var openedAt = _console.EndDate;
        await Task.Delay(40);

        _console.Reload();

        Assert.True(_console.EndDate > openedAt, $"[갱신] 뒤 끝 {_console.EndDate:HH:mm:ss.fff} — 연 때 {openedAt:HH:mm:ss.fff} 에 굳었다");
        Assert.Equal(_console.EndDate.AddDays(-1), _console.StartDate);
    }

    [Fact]
    public async Task should_move_the_period_end_to_now_when_the_selected_preset_chip_is_pressed_again()
    {
        await OpenOnActions();
        var openedAt = _console.EndDate;
        await Task.Delay(40);

        _console.SelectPeriod("24시간");

        Assert.True(_console.EndDate > openedAt, $"같은 칩을 다시 눌러도 끝 {_console.EndDate:HH:mm:ss.fff} 이 연 때 {openedAt:HH:mm:ss.fff} 그대로다");
    }

    [Fact]
    public async Task should_keep_a_custom_range_when_refreshed()
    {
        await OpenOnActions();
        _console.SelectPeriod("직접");
        var start = new DateTime(2026, 9, 1, 0, 0, 0);
        var end = new DateTime(2026, 9, 2, 0, 0, 0);
        _console.StartDate = start;
        _console.EndDate = end;

        _console.Reload();

        Assert.Equal(start, _console.StartDate);
        Assert.Equal(end, _console.EndDate);
    }

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
