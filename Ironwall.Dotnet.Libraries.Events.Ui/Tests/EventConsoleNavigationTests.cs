using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 콘솔의 <b>레일 전환</b>과 <b>미적용 이동 차단</b>(N-07 적대 검토 R11 · R12 · FR-55).
/// </summary>
/// <remarks>
/// 옛 화면은 평범한 <c>TabControl</c> 이라 탭을 바꿔도 Caliburn 활성화가 일어나지 않았다 —
/// 레일이 정말 패널을 활성화하는지, 그리고 A→B→A 뒤에도 행 변화를 따라가는지 여기서 단언한다.
/// </remarks>
[Collection("IoC-Dependent")]   // 패널 · 행 뷰모델이 생성자에서 IoC 를 본다
public class EventConsoleNavigationTests : IDisposable
{
    private readonly EventProvider _events = new();
    private readonly DeviceProvider _devices = new();
    private readonly EventDashboardViewModel _console;

    public EventConsoleNavigationTests()
    {
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var api = BuildApi();
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Name).Returns("tester");

        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? ea
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
        _console = new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, providerService, _devices, _events),
            new MalfunctionEventPanelViewModel(ea, log, providerService, _devices, _events),
            new ConnectionEventPanelViewModel(ea, log, providerService, _devices, _events),
            new ActionEventPanelViewModel(ea, log, providerService, _events),
            new EventInfoViewModel(_devices, _events, providerService, ea, log),
            new CameraEventInfoViewModel(_events, ea, log),
            new DataChartPanelViewModel(ea, log, providerService));

        // 스레드 전환을 없앤다 — 전역 Dispatcher 에 기대면 헤드리스에서 동작이 갈려 간헐 실패한다.
        _console.UseUiThread(ImmediateUiThread.Instance);
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
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

    private Task Activate() => ((IActivate)_console).ActivateAsync();

    [Fact]
    public async Task should_activate_the_matching_panel_when_a_rail_is_chosen()
    {
        await Activate();

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        Assert.Same(_console.DetectionPanelViewModel, _console.TabControlViewModel.ActiveItem);
        Assert.True(_console.DetectionPanelViewModel.IsActive);

        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        Assert.Same(_console.MalfunctionPanelViewModel, _console.TabControlViewModel.ActiveItem);
        Assert.True(_console.MalfunctionPanelViewModel.IsActive);
        Assert.False(_console.DetectionPanelViewModel.IsActive);      // 이전 패널은 닫힌다
    }

    [Fact]
    public async Task should_follow_new_rows_again_when_a_rail_is_revisited()
    {
        // (R12) A→B→A 뒤에도 구독이 살아 있어야 한다 — 패널은 SingleInstance 이고
        // 구독을 OnActivate 에서 걸고 OnDeactivate 에서 푼다(대칭)이라 재활성화가 다시 건다.
        await Activate();

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        // 콘솔이 다시 붙었는가 — 패널 콜렉션에 들어온 행이 그대로 보여야 한다.
        // (배지 수는 패널의 비동기 재조회와 경합해 단언하지 않는다 — 관찰만 한다.)
        var added = new DetectionEventViewModel(TestEvents.Detection(4242));
        _console.DetectionPanelViewModel.ViewModelProvider.Add(added);

        Assert.NotNull(_console.Rows);
        Assert.Contains(added, _console.Rows!.Cast<object>());
    }

    [Fact]
    public async Task should_not_leave_a_view_attached_to_a_rail_that_was_left()
    {
        // ★ 간헐 실패의 진짜 기제: 기본 뷰(GetDefaultView)는 콜렉션마다 전역 캐시되고 띆 수가 없어,
        //   레일을 떠난 뒤에도 패널 목록에 매달려 남았다. 그 패널이 다른 스레드에서
        //   목록을 비우면 CollectionView 가 NotSupportedException 으로 터졌다.
        //   이제는 자기 ListCollectionView 를 만들고 떠날 때 DetachFromSourceCollection() 한다.
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        var left = _console.DetectionPanelViewModel.ViewModelProvider;
        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);

        // 떠난 레일의 목록을 다른 스레드에서 비우고 다시 채운다 — 매달린 뷰가 있으면 여기서 터진다.
        var fault = await Task.Run(() =>
        {
            try
            {
                left.Clear();
                left.Add(new DetectionEventViewModel(TestEvents.Detection(777)));
                return (Exception?)null;
            }
            catch (Exception ex) { return ex; }
        });

        Assert.Null(fault);
    }

    [Fact]
    public async Task should_refuse_the_rail_switch_when_changes_are_unapplied()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        // 손댄 칸을 하나 만든다 — 커널 추적기가 곧 '미적용 변경' 이다.
        _console.Detail.Tracker.Touch(EventDetailProjection.FieldResult, "없음", "케이블 절단");
        Assert.True(_console.Detail.Tracker.IsDirty);

        var moved = await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);

        Assert.False(moved);
        Assert.Same(_console.DetectionPanelViewModel, _console.TabControlViewModel.ActiveItem);   // 그대로다
    }

    [Fact]
    public async Task should_refuse_the_row_selection_when_changes_are_unapplied()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        var first = new DetectionEventViewModel(TestEvents.Detection(1));
        var second = new DetectionEventViewModel(TestEvents.Detection(2));
        Assert.True(_console.SetSelection(new object[] { first }));

        _console.Detail.Tracker.Touch(EventDetailProjection.FieldResult, "없음", "케이블 절단");

        Assert.False(_console.SetSelection(new object[] { second }));     // (R6) 조용히 버리지 않는다
        Assert.Same(first, Assert.Single(_console.SelectedRows));
    }

    [Fact]
    public async Task should_allow_the_same_selection_again_when_changes_are_unapplied()
    {
        // 뷰가 선택을 되돌린 직후 올라오는 알림은 '이동'이 아니다 — 막으면 무한 재귀가 된다.
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        var row = new DetectionEventViewModel(TestEvents.Detection(1));
        _console.SetSelection(new object[] { row });
        _console.Detail.Tracker.Touch(EventDetailProjection.FieldResult, "없음", "케이블 절단");

        Assert.True(_console.SetSelection(new object[] { row }));
    }

    [Fact]
    public async Task should_not_block_searching_when_changes_are_unapplied()
    {
        // 검색은 선택을 바꾸지 않으므로 막지 않는다(커널 ConsoleNavigation.Search).
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        _console.Detail.Tracker.Touch(EventDetailProjection.FieldResult, "없음", "케이블 절단");

        _console.SearchText = "북측";

        Assert.Equal("북측", _console.SearchText);
        Assert.True(_console.IsFiltered);
    }

    [Fact]
    public async Task should_show_the_filter_chips_of_the_current_rail()
    {
        await Activate();

        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        Assert.Contains(_console.FilterChips, c => c.Key == EventListFilterKeys.Alert);

        // 연결: 서버가 상태 칸을 주지 않으므로(type_event=Connection 하나) '끊김 / 연결' 칩을 내지 않는다(E6a).
        await _console.SelectRailAsync(EventDashboardViewModel.ConnectionRailKey);
        Assert.DoesNotContain(_console.FilterChips, c => c.Key == EventListFilterKeys.Disconnected);
        Assert.DoesNotContain(_console.FilterChips, c => c.Key == EventListFilterKeys.Alert);

        await _console.SelectRailAsync(EventDashboardViewModel.ActionRailKey);
        Assert.Empty(_console.FilterChips);          // 조치 내역엔 칩이 없다(정본 FILTERS 에 항목 없음)
    }
}

/// <summary>테스트가 읽기 쉬우라고 둔 별칭.</summary>
internal static class EventListFilterKeys
{
    public const string Alert = Consoles.Lists.EventListFilter.ChipAlert;
    public const string Disconnected = Consoles.Lists.EventListFilter.ChipDisconnected;
}
