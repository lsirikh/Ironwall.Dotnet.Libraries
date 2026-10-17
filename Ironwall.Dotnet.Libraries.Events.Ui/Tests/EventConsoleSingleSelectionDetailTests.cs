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
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// D-26 — 스냅샷 스윕이 잡은 "한 건 선택 시 상세가 비어 보인다"가 <b>VM 자체의 결함</b>인지
/// 미리보기 하네스(<c>grid.SelectedItem = grid.Items[0]</c>)의 배선 문제인지 가른다.
/// </summary>
/// <remarks>
/// 여기서는 실제 클릭과 <b>같은 진입점</b>(<see cref="EventDashboardViewModel.SetSelection"/>)을
/// 직접 부른다 — <c>EventDashboardView.OnGridSelectionChanged</c> 가 하는 일과 정확히 같다.
/// 이게 통과하면 VM/프리젠터는 정상이고 결함은 뷰 배선이나 하네스 쪽에 있다는 뜻이다.
/// </remarks>
[Collection("IoC-Dependent")]
public class EventConsoleSingleSelectionDetailTests : IDisposable
{
    private readonly EventProvider _events = new();
    private readonly DeviceProvider _devices = new();
    private readonly EventDashboardViewModel _console;

    public EventConsoleSingleSelectionDetailTests()
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

        // ★ 탐지만 한 건 채워 둔다 — AttachRows 가 ListCollectionView 를 만드는 순간(패널의
        //   활성화 로드가 끝난 뒤) 이미 소스가 차 있어야 CurrentPosition 이 0 으로 갈 조건이
        //   생긴다(빈 소스면 무엇을 고쳤든 항상 -1 이라 시험이 아무것도 못 가른다).
        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse<DetectionEventDto>
            {
                Success = true,
                Data = new List<DetectionEventDto> { new() { Id = 501, Result = "NONE" } },
                Pagination = new PaginationDto { Page = 1, Limit = 100, Total = 1, TotalPages = 1 },
            });
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
    public async Task should_report_one_selected_and_a_populated_title_when_a_single_detection_row_is_clicked()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        var row = new DetectionEventViewModel(TestEvents.Detection(501));
        _console.DetectionPanelViewModel.ViewModelProvider.Add(row);

        // 실제 클릭과 같은 진입점 — EventDashboardView.OnGridSelectionChanged 가 부르는 것과 동일.
        var accepted = _console.SetSelection(new object[] { row });

        Assert.True(accepted);
        Assert.Equal(1, _console.SelectedRows.Count);
        Assert.Equal(1, _console.Detail.SelectedCount);
        Assert.NotEqual("선택한 항목 없음", _console.Detail.Title);
    }

    [Fact]
    public async Task should_report_one_selected_and_a_populated_title_when_a_single_action_row_is_clicked()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.ActionRailKey);

        var model = new ActionEventModel { Id = 9001, Content = "케이블 재체결", User = "tester", DateTime = DateTime.Now };
        var row = new ActionEventViewModel(model);
        _console.ActionPanelViewModel.ViewModelProvider.Add(row);

        var accepted = _console.SetSelection(new object[] { row });

        Assert.True(accepted);
        Assert.Equal(1, _console.SelectedRows.Count);
        Assert.Equal(1, _console.Detail.SelectedCount);
        Assert.NotEqual("선택한 항목 없음", _console.Detail.Title);
    }

    /// <summary>
    /// D-26 근본 원인 고정 — <see cref="EventDashboardViewModel.AttachRows"/> 가 붙이는 순간 소스가
    /// 이미 차 있으면(패널의 활성화 로드가 먼저 끝난 뒤라 — 실제 레일 전환과 같은 순서), 새
    /// <c>ListCollectionView</c> 는 <c>CurrentPosition</c>이 첫 행(0)이 <b>된다</b>(실측: BuildApi 목이
    /// 빈 목록을 주면 소스가 비어 있어 항상 -1 이라 이 시험이 아무것도 못 가르는 가짜 통과가 된다 —
    /// 그래서 <see cref="BuildApi"/> 가 탐지 한 건을 미리 채워 준다). <c>view.MoveCurrentToPosition(-1)</c>
    /// 이 없으면 이 값은 0으로 남는다.
    /// </summary>
    [Fact]
    public async Task should_leave_current_position_at_none_when_rows_are_attached_to_a_rail()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        Assert.NotNull(_console.Rows);
        Assert.NotEmpty(_console.DetectionPanelViewModel.ViewModelProvider);   // 선행 조건 — 소스가 비지 않았다.
        Assert.Equal(-1, _console.Rows!.CurrentPosition);
        Assert.Null(_console.Rows.CurrentItem);
    }

    /// <summary>
    /// D-26 재발 방지 — 레일을 바꾸면 (a) <see cref="EventDashboardViewModel.SelectedRows"/> 가 비고
    /// (b) 상세 칸도 함께 비고 (c) 뜬 레일의 어떤 행도 <c>IsSelected</c> 로 남지 않는다.
    /// 셋 중 하나라도 어긋나면 "바닥 줄 선택 N건 vs 상세 선택 없음" 불일치나 유령 하이라이트가 남는다.
    /// </summary>
    [Fact]
    public async Task should_clear_selection_and_deselect_rows_when_switching_rails()
    {
        await Activate();
        await _console.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);

        var row0 = new DetectionEventViewModel(TestEvents.Detection(611));
        var row1 = new DetectionEventViewModel(TestEvents.Detection(612));
        _console.DetectionPanelViewModel.ViewModelProvider.Add(row0);
        _console.DetectionPanelViewModel.ViewModelProvider.Add(row1);

        Assert.True(_console.SetSelection(new object[] { row0 }));
        Assert.True(row0.IsSelected);          // 선행 조건 — 실제로 골랐다.

        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        var mrow = new MalfunctionEventViewModel(TestEvents.Malfunction(613));
        _console.MalfunctionPanelViewModel.ViewModelProvider.Add(mrow);

        Assert.Empty(_console.SelectedRows);
        Assert.Equal(0, _console.Detail.SelectedCount);
        Assert.False(row0.IsSelected);         // 뜬 레일(탐지)의 행도 꺼져야 한다.
        Assert.False(row1.IsSelected);
        Assert.False(mrow.IsSelected);         // 새 레일(장애)에도 아무도 안 골랐다.
    }

    /// <summary>
    /// D-26 근본 기제의 실물 확인 — <see cref="EventDashboardViewModel.AttachRows"/> 가 실제로 하는 배선
    /// (새 <c>ListCollectionView</c> 를 진짜 <see cref="System.Windows.Controls.DataGrid"/> 의
    /// <c>ItemsSource</c> 에 물리는 것)을 STA 스레드에서 그대로 재현한다 — VM·비동기 패널을 거치지 않고
    /// WPF 의 <c>Selector.IsSynchronizedWithCurrentItem</c> 자동 동기화 자체를 겨눈다.
    /// <c>moveCurrentToNone=false</c>(고치기 전 상태)는 그리드가 스스로 첫 행을 고른다는 것을 보여
    /// D-26 근본 원인이 추측이 아니라 실물임을 증명하고, <c>moveCurrentToNone=true</c>(고친 뒤 —
    /// <see cref="EventDashboardViewModel.AttachRows"/> 가 실제로 하는 것)는 아무것도 안 고른다는 것을 보여
    /// 그 수정이 실제로 통한다는 것을 증명한다. 렌더링(Window)과 무관하게 <c>ItemsSource</c> 프로퍼티
    /// 콜백에서 일어나는 동작이라 Window 없이 STA 스레드 하나로 충분하다.
    /// </summary>
    [Theory]
    [InlineData(false, true)]  // 고치기 전(레드) — 아무도 안 골랐는데 그리드가 첫 행을 스스로 선택한다.
    [InlineData(true, false)]  // 고친 뒤(그린) — AttachRows 와 같은 MoveCurrentToPosition(-1) 이면 안 고른다.
    public void should_auto_select_first_row_only_when_current_position_is_not_cleared(bool moveCurrentToNone, bool expectAutoSelection)
    {
        Exception? failure = null;
        object? selectedItem = null;
        var done = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                var rows = new System.Collections.ObjectModel.ObservableCollection<object> { "row-1", "row-2" };
                var view = new System.Windows.Data.ListCollectionView(rows);
                if (moveCurrentToNone) view.MoveCurrentToPosition(-1);

                // 실제 뷰(EventDashboardView.xaml)의 네 그리드와 같은 배선 — ItemsSource 하나에 그리드 하나.
                var grid = new System.Windows.Controls.DataGrid
                {
                    ItemsSource = view,
                    SelectionMode = System.Windows.Controls.DataGridSelectionMode.Extended,
                };

                // Selector 의 CurrentItem 동기화는 컨테이너가 실제로 만들어져야(= 레이아웃 패스를
                // 한 번 거쳐야) 반영된다 — Window 없이 속성만 건드리면 절대 안 일어난다.
                // 실제 뷰와 같은 조건(Window.Show)으로 세운다.
                // U-18 — 화면 밖(-20000)에 활성화 없이 띄운다: 레이아웃 패스는 똑같이 돌고, 사용자가 일하는 화면에 창이 튀지 않으며
                // 같은 PC 에서 돌고 있는 UI 자동화(SendInput)의 포커스를 빼앗지 않는다(tools/shared/OffscreenStage.cs 와 같은 원칙).
                var window = new System.Windows.Window
                {
                    Content = grid, Width = 400, Height = 300, ShowInTaskbar = false, WindowStyle = System.Windows.WindowStyle.None,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false,
                };
                window.Show();

                void Pump(System.Windows.Threading.DispatcherPriority priority)
                {
                    var frame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(priority, new System.Action(() => frame.Continue = false));
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }

                // 레이아웃(측정·배치)과 그 뒤에 깔린 유휴 작업을 모두 돈다.
                grid.UpdateLayout();
                Pump(System.Windows.Threading.DispatcherPriority.Loaded);
                Pump(System.Windows.Threading.DispatcherPriority.ContextIdle);
                Pump(System.Windows.Threading.DispatcherPriority.SystemIdle);

                selectedItem = grid.SelectedItem;
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(done.Wait(TimeSpan.FromSeconds(10)), "STA 스레드가 제시간에 끝나지 않았다");
        Assert.Null(failure);
        if (expectAutoSelection) Assert.NotNull(selectedItem);
        else Assert.Null(selectedItem);
    }
}
