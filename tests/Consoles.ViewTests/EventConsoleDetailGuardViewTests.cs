using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
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
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 이벤트 콘솔 <b>실제 뷰</b> — 상세에 적용하지 않은 판정이 있으면 다른 행을 골라도 원래 행 · 고친 칸이 그대로 남아야 한다(SC-EVT-028).
/// </summary>
/// <remarks>
/// 행 선택은 사람 · UIA 와 같은 길(DataGrid 항목 peer 의 Select)로, 판정은 상세의 실제 사유 콤보로 고친다.
/// </remarks>
public class EventConsoleDetailGuardViewTests
{
    [Fact]
    public void should_keep_the_original_row_and_its_unapplied_reason_when_another_row_is_chosen_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            Assert.True(Wait(console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey)));
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            var first = new MalfunctionEventViewModel(Malfunction(701));
            var second = new MalfunctionEventViewModel(Malfunction(702));
            console.MalfunctionPanelViewModel.ViewModelProvider.Add(first);
            console.MalfunctionPanelViewModel.ViewModelProvider.Add(second);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            var grid = RailProbe.Find<DataGrid>(view, g => System.Windows.Automation.AutomationProperties.GetAutomationId(g) == "Console.Events.Grid.Malfunction");
            Assert.True(grid is not null && grid.IsVisible, "장애 목록이 화면에 없다");
            SelectRow(grid!, first);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.Same(first, Assert.Single(console.SelectedRows));

            var reason = RailProbe.Find<ComboBox>(view, c => System.Windows.Automation.AutomationProperties.GetAutomationId(c) == "Console.Events.Detail.Field.reason");
            Assert.True(reason is not null, "상세 사유 칸이 없다");
            var original = reason!.SelectedItem;
            reason.SelectedIndex = reason.SelectedIndex == 0 ? 1 : 0;
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.True(console.Detail.CanRevert, "사유를 고쳤으면 [되돌리기] 가 켜져야 한다");

            // 헤디드 순서 그대로 — 먼저 [연결] 레일로 가 보고(SC-EVT-008 · 막힌다) 그다음 다른 행을 고른다.
            var rail = RailProbe.Rail(view, "Console.Events.Rail");
            RailProbe.Select(rail, EventDashboardViewModel.ConnectionRailKey);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.Equal(EventDashboardViewModel.MalfunctionRailKey, console.SelectedRail?.Key);
            Assert.True(console.Detail.CanRevert, $"막힌 레일 전환 뒤에도 고친 사유는 남아야 한다 · 상태={console.Detail.State}");
            Assert.Same(first, Assert.Single(console.SelectedRows));

            SelectRow(grid, second);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            Assert.True(console.Detail.CanRevert, $"다른 행을 골라도 고친 사유는 남아야 한다 · 상태={console.Detail.State}");
            Assert.Same(first, Assert.Single(console.SelectedRows));
            Assert.Equal(new object[] { first }, grid.SelectedItems.Cast<object>().ToArray());
            Assert.True(Container(grid, first)?.IsSelected == true, "화면의 원래 행이 선택으로 남아야 한다");
            Assert.False(Container(grid, second)?.IsSelected == true, "거절된 행이 선택으로 보이면 안 된다");
            Assert.NotEqual(original, reason.SelectedItem);
        }
        finally { window.Close(); }
    });

    /// <summary>
    /// 불변식(경로 무관) — 적용하지 않은 판정이 있으면 다른 행을 <b>어떻게 골라도</b>(UIA Select · 마우스 · 키보드 ↓) 고친 칸이 사라지지 않는다:
    /// 원래 행 선택 · 고친 사유 · [되돌리기] 가 그대로 남는다. 사유는 헤디드와 같은 길(콤보 펼침 → 항목 Select → 접음)로 고친다.
    /// </summary>
    [Theory]
    [InlineData("uia")]
    [InlineData("mouse")]
    [InlineData("keyboard")]
    public void should_keep_the_unapplied_reason_whatever_way_another_row_is_chosen_in_the_real_console_view(string how) => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            Assert.True(Wait(console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey)));
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            var first = new MalfunctionEventViewModel(Malfunction(701));
            var second = new MalfunctionEventViewModel(Malfunction(702));
            var third = new MalfunctionEventViewModel(Malfunction(703));
            console.MalfunctionPanelViewModel.ViewModelProvider.Add(first);
            console.MalfunctionPanelViewModel.ViewModelProvider.Add(second);
            console.MalfunctionPanelViewModel.ViewModelProvider.Add(third);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            var grid = RailProbe.Find<DataGrid>(view, g => System.Windows.Automation.AutomationProperties.GetAutomationId(g) == "Console.Events.Grid.Malfunction")!;

            ChooseRow(grid, first, how);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.Same(first, Assert.Single(console.SelectedRows));

            var reason = RailProbe.Find<ComboBox>(view, c => System.Windows.Automation.AutomationProperties.GetAutomationId(c) == "Console.Events.Detail.Field.reason")!;
            var picked = PickOtherByAutomation(reason);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.True(console.Detail.CanRevert, "사유를 고쳤으면 [되돌리기] 가 켜져야 한다");

            var rail = RailProbe.Rail(view, "Console.Events.Rail");
            RailProbe.Select(rail, EventDashboardViewModel.ConnectionRailKey);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            ChooseRow(grid, how == "keyboard" ? first : second, how);   // 키보드는 원래 행에서 ↓ 한 칸
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            Assert.True(console.Detail.CanRevert, $"[{how}] 다른 행을 골라도 고친 사유는 남아야 한다 · 상태={console.Detail.State} · 선택={console.SelectedRows.Count}");
            Assert.Same(first, Assert.Single(console.SelectedRows));
            Assert.Equal(picked, reason.SelectedItem);
            Assert.True(grid.SelectedItems.Count == 1 && ReferenceEquals(grid.SelectedItems[0], first),
                $"[{how}] 화면도 원래 행에 머물러야 한다 · 화면 선택=[{string.Join(",", grid.SelectedItems.Cast<MalfunctionEventViewModel>().Select(r => r.Model?.Id))}]");
        }
        finally { window.Close(); }
    });

    /// <summary>헤디드와 같은 길로 콤보 값을 바꾼다 — 펼침 → 다른 항목 Select → 접음(자동화 peer).</summary>
    private static object? PickOtherByAutomation(ComboBox combo)
    {
        var peer = (ComboBoxAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(combo)!;
        ((IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)!).Expand();
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
        peer.ResetChildrenCache();
        var current = combo.SelectedItem;
        var item = peer.GetChildren()!.OfType<ItemAutomationPeer>().First(p => !Equals(p.Item, current));
        ((ISelectionItemProvider)item.GetPattern(PatternInterface.SelectionItem)!).Select();
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
        try { ((IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)!).Collapse(); } catch { }
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
        return combo.SelectedItem;
    }

    /// <summary>행을 고른다 — uia: 항목 peer Select · mouse: 칸에 마우스 누름(미리보기 → 누름) · keyboard: 그 행 칸에 초점 → ↓.</summary>
    private static void ChooseRow(DataGrid grid, object row, string how)
    {
        switch (how)
        {
            case "uia":
                SelectRow(grid, row);
                return;
            case "mouse":
            {
                var cell = FirstCell(grid, row);
                foreach (var ev in new[] { UIElement.PreviewMouseLeftButtonDownEvent, UIElement.MouseLeftButtonDownEvent })
                    cell.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = ev, Source = cell });
                foreach (var ev in new[] { UIElement.PreviewMouseLeftButtonUpEvent, UIElement.MouseLeftButtonUpEvent })
                    cell.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = ev, Source = cell });
                return;
            }
            default:
            {
                var cell = FirstCell(grid, row);
                if (!cell.IsKeyboardFocusWithin) { cell.Focus(); AppHost.Pump(DispatcherPriority.Input); }
                if (grid.SelectedItems.Count == 0 || !grid.SelectedItems.Contains(row)) { ChooseRow(grid, row, "mouse"); return; }
                var source = PresentationSource.FromVisual(cell)!;
                var down = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, Environment.TickCount, System.Windows.Input.Key.Down)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
                    Source = cell,
                };
                System.Windows.Input.InputManager.Current.ProcessInput(down);
                return;
            }
        }
    }

    private static DataGridCell FirstCell(DataGrid grid, object row)
    {
        grid.ScrollIntoView(row);
        AppHost.Pump(DispatcherPriority.Loaded);
        var container = Container(grid, row)!;
        var cell = RailProbe.Find<DataGridCell>(container, c => c.Column is DataGridTextColumn)!;
        return cell;
    }

    [Fact]
    public void should_keep_the_unapplied_reason_and_reselect_the_same_event_when_the_list_is_reloaded_in_the_real_console_view() => AppHost.Run(() =>
    {
        // 헤디드 SC-EVT-028(2026-09-28 r7): 장애 레일로 옮기면 패널이 목록을 다시 읽는다(swap-on-success — 컬렉션을 비우고 새 인스턴스로 채움).
        // 캐시로 먼저 그린 행을 고르고 사유를 고친 사이에 새 목록(같은 id · 새 인스턴스)이 들어오면 옛 행이 빠지며 그리드가 '선택 없음'을 넘긴다.
        var dtos = new List<MalfunctionEventDto>
        {
            new() { Id = 701, TypeEvent = "Fault", Reason = "FAULT_CONTROLLER", CreatedAt = DateTime.Now.AddMinutes(-2).ToString("o") },
            new() { Id = 702, TypeEvent = "Fault", Reason = "FAULT_CONTROLLER", CreatedAt = DateTime.Now.AddMinutes(-1).ToString("o") },
        };
        var (view, window, console) = HostConsole(dtos);
        try
        {
            Assert.True(Wait(console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey)));
            WaitFor(() => console.MalfunctionPanelViewModel.ViewModelProvider.Count == 2, "장애 2건 로드");
            var grid = RailProbe.Find<DataGrid>(view, g => System.Windows.Automation.AutomationProperties.GetAutomationId(g) == "Console.Events.Grid.Malfunction")!;
            var firstOld = console.MalfunctionPanelViewModel.ViewModelProvider.First(r => r.Model?.Id == 701);
            SelectRow(grid, firstOld);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.Same(firstOld, Assert.Single(console.SelectedRows));

            var reason = RailProbe.Find<ComboBox>(view, c => System.Windows.Automation.AutomationProperties.GetAutomationId(c) == "Console.Events.Detail.Field.reason")!;
            reason.SelectedIndex = reason.SelectedIndex == 0 ? 1 : 0;
            var picked = reason.SelectedItem;
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.True(console.Detail.CanRevert);

            // 패널이 스스로 다시 읽는다(레일 활성화 · [갱신] 과 같은 DataInitialize 길) — 콘솔의 [갱신] 관문을 거치지 않는 길
            console.MalfunctionPanelViewModel.ClickSearch();
            WaitFor(() => console.MalfunctionPanelViewModel.ViewModelProvider.Count == 2
                          && !console.MalfunctionPanelViewModel.ViewModelProvider.Contains(firstOld), "다시 읽기");
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            Assert.True(console.Detail.CanRevert, $"다시 읽기 뒤에도 고친 사유는 남아야 한다 · 상태={console.Detail.State}");

            // 헤디드 그대로 — 다시 읽은 뒤 다른 행(새 인스턴스)을 골라 본다: 막혀야 하고, 원래 이벤트 · 고친 사유가 남아야 한다
            var secondNew = console.MalfunctionPanelViewModel.ViewModelProvider.First(r => r.Model?.Id == 702);
            SelectRow(grid, secondNew);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.True(console.Detail.CanRevert, $"막힌 행 선택 뒤에도 [되돌리기] 가 켜져 있어야 한다 · 상태={console.Detail.State} · 선택=[{string.Join(",", console.SelectedRows.Cast<MalfunctionEventViewModel>().Select(r => r.Model?.Id))}]");
            Assert.False(grid.SelectedItems.Contains(secondNew), "거절된 행이 화면 선택으로 남으면 안 된다");

            var selected = Assert.Single(console.SelectedRows);
            Assert.Equal(701, ((MalfunctionEventViewModel)selected).Model?.Id);
            Assert.True(grid.SelectedItems.Count == 1 && ((MalfunctionEventViewModel)grid.SelectedItems[0]!).Model?.Id == 701,
                $"화면도 같은 이벤트(701)를 골라야 한다 · 화면 선택=[{string.Join(",", grid.SelectedItems.Cast<MalfunctionEventViewModel>().Select(r => r.Model?.Id))}]");
            Assert.Equal(picked, reason.SelectedItem);
        }
        finally { window.Close(); }
    });

    private static void WaitFor(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        Assert.True(condition(), what + " — 시간 안에 되지 않았다");
    }

    private static void SelectRow(DataGrid grid, object item)
    {
        grid.ScrollIntoView(item);
        AppHost.Pump(DispatcherPriority.Loaded);
        var peer = (DataGridAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(grid)!;
        peer.ResetChildrenCache();
        var itemPeer = peer.GetChildren()!.OfType<DataGridItemAutomationPeer>().First(p => ReferenceEquals(p.Item, item));
        ((ISelectionItemProvider)itemPeer.GetPattern(PatternInterface.SelectionItem)!).Select();
    }

    private static DataGridRow? Container(DataGrid grid, object item) => grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;

    private static IMalfunctionEventModel Malfunction(int id) => new MalfunctionEventModel
    {
        Id = id,
        MessageType = EnumEventType.Fault,
        Status = EnumTrueFalse.False,
        Reason = EnumFaultType.FAULT_CONTROLLER,
        DateTime = DateTime.Now.AddMinutes(-id % 10),
        Device = new SensorDeviceModel { Id = 101, DeviceNumber = 101, DeviceName = "정문 1구간", DeviceType = EnumDeviceType.Fence },
    };

    private static T Wait<T>(Task<T> task) => RailProbe.Wait(task);

    private static (EventDashboardView View, System.Windows.Window Window, EventDashboardViewModel Console) HostConsole(IReadOnlyList<MalfunctionEventDto>? malfunctions = null)
    {
        var events = new EventProvider();
        var devices = new DeviceProvider();
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var api = BuildApi(malfunctions ?? Array.Empty<MalfunctionEventDto>());
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

    private static IEventApiService BuildApi(IReadOnlyList<MalfunctionEventDto> malfunctions)
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<DetectionEventDto>());
        mock.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Of(malfunctions.ToList()));
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
}
