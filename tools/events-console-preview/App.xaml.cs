using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Events.Ui.Views.Dashboards;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using MaterialDesignThemes.Wpf;
using Moq;
using System.IO;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Text;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EventsConsolePreview;

/// <summary>
/// 이벤트 콘솔 미리보기 — <b>진짜 뷰 + 진짜 뷰모델</b>을 가짜 데이터 위에 띄운다(호스트 앱 · 서버 없음).
/// <c>--snapshot &lt;폴더&gt;</c> 면 입력 없이 여러 상태를 PNG 로 떠 놓고 끝낸다. <c>--dark</c> 면 다크로 시작한다.
/// </summary>
public partial class App : Application
{
    private const string DarkTokens = "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml";

    private EventDashboardViewModel _viewModel = null!;
    private EventDashboardView _view = null!;
    private Window _window = null!;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var snapshotAt = Array.IndexOf(e.Args, "--snapshot");
        var directory = snapshotAt >= 0 && snapshotAt + 1 < e.Args.Length ? e.Args[snapshotAt + 1] : null;
        var startDark = e.Args.Contains("--dark");

        try
        {
            if (startDark) ApplyDark();

            // N-13 이벤트 맵핑 워크벤치 — 콘솔과 따로 뜬다(--mapping [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--mapping"))
            {
                await RunMappingAsync(directory, startDark);
                if (directory is not null) Shutdown();
                return;
            }

            _viewModel = Build();
            _view = new EventDashboardView { DataContext = _viewModel };
            _window = new Window
            {
                Title = "이벤트 콘솔 미리보기",
                Width = 1360,
                Height = 820,
                Background = (Brush)FindResource("SurfaceBrush"),
                Content = new Border { Margin = new Thickness(12), Child = _view, ClipToBounds = true },
            };
            _window.Show();

            await ((IActivate)_viewModel).ActivateAsync();

            if (directory is null) return;

            Directory.CreateDirectory(directory);
            await RunSnapshotsAsync(directory, startDark);
        }
        catch (Exception ex)
        {
            if (directory is null) MessageBox.Show(ex.ToString());
            else File.WriteAllText(Path.Combine(directory, "snapshot-error.txt"), ex.ToString());
        }

        if (directory is not null) Shutdown();
    }

    #region - Composition -
    private static EventDashboardViewModel Build()
    {
        var log = new PreviewLog();
        var events = new EventAggregator();
        var api = BuildApi();
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Username).Returns("미리보기");
        account.SetupGet(a => a.EmployeeNumber).Returns("00-00000");

        var deviceProvider = new DeviceProvider();
        var eventProvider = new EventProvider();
        var providerService = new EventProviderService(log, api, deviceProvider, eventProvider);

        // 패널 · 카드 뷰모델이 정적 IoC 로 의존을 찾는다 — 컨테이너 대신 여기서 대 준다.
        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? events
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? api
            : type == typeof(EventProvider) ? eventProvider
            : type == typeof(DeviceProvider) ? deviceProvider
            : type == typeof(IAccountModel) ? account.Object
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : type == typeof(IPermissionService) ? null!
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };

        // 호스트는 부트스트래퍼가 해 준다 — 없으면 Execute.BeginOnUIThread 가 제자리(작업 스레드)에서 돌아
        // 콘솔이 교차 스레드로 화면을 만진다.
        PlatformProvider.Current = new XamlPlatformProvider();

        return new EventDashboardViewModel(
            events, log,
            new EventTabControlViewModel(events, log),
            new DetectionEventPanelViewModel(events, log, providerService, deviceProvider, eventProvider),
            new MalfunctionEventPanelViewModel(events, log, providerService, deviceProvider, eventProvider),
            new ConnectionEventPanelViewModel(events, log, providerService, deviceProvider, eventProvider),
            new ActionEventPanelViewModel(events, log, providerService, eventProvider),
            new EventInfoViewModel(deviceProvider, eventProvider, providerService, events, log),
            new CameraEventInfoViewModel(eventProvider, events, log),
            new DataChartPanelViewModel(events, log, providerService));
    }

    /// <summary>가짜 서버 — 네 목록과 통계만 답한다. 나머지는 부르지 않는다.</summary>
    private static IEventApiService BuildApi()
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };

        var detections = PreviewData.Detections();
        var malfunctions = PreviewData.Malfunctions();
        var connections = PreviewData.Connections();
        var actions = PreviewData.Actions();

        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(List(detections));

        mock.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(List(malfunctions));

        mock.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(List(connections));

        mock.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                                               It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(List(actions));

        // 조치 내역(원본별) — 상세 칸이 열 때 한 번 부른다.
        mock.Setup(a => a.GetDetectionActionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => List(PreviewData.ActionsFor(id)));
        mock.Setup(a => a.GetMalfunctionActionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => List(PreviewData.ActionsFor(id)));

        mock.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = PreviewData.Dashboard() });

        // 조치 생성 — 트레이의 [적용] 이 부른다. 마지막 두 건은 일부러 실패시켜 부분 실패 화면을 볼 수 있게 한다.
        var created = 0;
        mock.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                created++;
                return created % 3 == 0
                    ? new ApiResponse<ActionEventDto> { Success = false, Message = "서버가 거절했습니다(예시)" }
                    : new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 9000 + created } };
            });

        return mock.Object;

        static ApiListResponse<T> List<T>(List<T> items)
            => new() { Success = true, Data = items, Pagination = PreviewData.Page(items.Count) };
    }
    #endregion

    #region - Snapshots -
    private async Task RunSnapshotsAsync(string directory, bool startedDark)
    {
        await Shot(directory, startedDark ? "dark" : "light");

        if (startedDark) return;        // 다크로 시작했으면 다크만 찍는다(호출부가 두 번 돌린다)

        ApplyDark();
        _window.Background = (Brush)FindResource("SurfaceBrush");
        await Shot(directory, "dark");
    }

    private async Task Shot(string directory, string theme)
    {
        // 1) 개요(T3)
        await _viewModel.SelectRailAsync(EventDashboardViewModel.OverviewRailKey);
        await Settle();
        Save(directory, $"{theme}-01-overview");

        // 2) 탐지 목록 — 선택 없음
        await _viewModel.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        await Settle();
        Save(directory, $"{theme}-02-detection-list");

        // 3) 한 건 선택 — 상세(판정만 상자)
        var grid = FindGrid("Console.Events.Grid.Detection");
        if (grid.Items.Count > 0)
        {
            grid.SelectedItem = grid.Items[0];
            await Settle();
            Save(directory, $"{theme}-03-detection-single");

            // 4) 여러 건 선택 — 트레이로 보내는 화면
            grid.SelectedItems.Clear();
            foreach (var row in grid.Items.Cast<object>().Take(4)) grid.SelectedItems.Add(row);
            await Settle();
            Save(directory, $"{theme}-04-detection-multi");

            // 5) 트레이에 Draft 를 담은 상태(드래그의 폴백 경로 = 같은 함수)
            _viewModel.Tray.Phrase = ActionTrayViewModel.Phrases[0];   // 문구를 명시적으로 고른다(R14)
            _viewModel.QueueSelection();
            await Settle();
            Save(directory, $"{theme}-05-tray-drafts");

            // 6) 적용 뒤 — 진행률이 끝나고 실패한 줄이 남는다
            _viewModel.ApplyTray();
            await Settle(900);
            Save(directory, $"{theme}-06-tray-applied-with-failures");
            _viewModel.RevertTray();
            grid.SelectedItems.Clear();
            await Settle();
        }

        // 6b) 거르기 — 칩 + 검색
        _viewModel.SelectFilterChip("open");
        await Settle();
        Save(directory, $"{theme}-06b-filter-open");

        _viewModel.SearchText = "북측";
        await Settle();
        Save(directory, $"{theme}-06c-search");
        _viewModel.SearchText = string.Empty;
        _viewModel.SelectFilterChip("all");
        await Settle();

        // 7~9) 나머지 세 목록
        await _viewModel.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        await Settle();
        Save(directory, $"{theme}-07-malfunction-list");

        await _viewModel.SelectRailAsync(EventDashboardViewModel.ConnectionRailKey);
        await Settle();
        Save(directory, $"{theme}-08-connection-list");

        await _viewModel.SelectRailAsync(EventDashboardViewModel.ActionRailKey);
        await Settle();
        Save(directory, $"{theme}-09-action-list");

        // 9b) 트레이가 다른 레일에서 줄어든 채로 남아 있다(R7)
        await _viewModel.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        await Settle();
        var keep = FindGrid("Console.Events.Grid.Detection");
        if (keep.Items.Count > 0)
        {
            keep.SelectedItems.Clear();
            foreach (var row in keep.Items.Cast<object>().Take(2)) keep.SelectedItems.Add(row);
            _viewModel.Tray.Phrase = ActionTrayViewModel.Phrases[0];
            _viewModel.QueueSelection();
            await Settle();
            await _viewModel.SelectRailAsync(EventDashboardViewModel.ConnectionRailKey);
            await Settle();
            Save(directory, $"{theme}-09b-tray-collapsed-other-rail");
            _viewModel.RevertTray();
            await _viewModel.SelectRailAsync(EventDashboardViewModel.ActionRailKey);
            await Settle();
        }

        // 10) 조치 한 건 — 내용만 고칠 수 있다
        var actionGrid = FindGrid("Console.Events.Grid.Action");
        if (actionGrid.Items.Count > 0)
        {
            actionGrid.SelectedItem = actionGrid.Items[0];
            await Settle();
            Save(directory, $"{theme}-10-action-single");
            actionGrid.SelectedItems.Clear();
        }

        // 11~12) 좁은 폭 — 서랍(960~1279) · 접힘(<960)
        await _viewModel.SelectRailAsync(EventDashboardViewModel.DetectionRailKey);
        await Settle();
        _window.Width = 1150;
        await Settle();
        Save(directory, $"{theme}-11-drawer-1150");

        _window.Width = 900;
        await Settle();
        Save(directory, $"{theme}-12-compact-900");

        _window.Width = 1360;
        await Settle();
    }

    #region - N-13 이벤트 맵핑 워크벤치 -
    /// <summary>워크벤치를 띄우고, 스냅샷 폴더가 있으면 상태별 PNG 를 찍는다.</summary>
    private async Task RunMappingAsync(string? directory, bool dark)
    {
        var theme = dark ? "dark" : "light";
        var model = MappingPreview.Build();
        var view = new MappingWorkbenchView { DataContext = model };

        _window = new Window
        {
            Title = "이벤트 맵핑 워크벤치 미리보기",
            Width = 1360,      // 셸이 1280 이상이어야 3단 도킹이다 — 딱 1280 이면 테두리만큼 모자라 서랍으로 내려간다
            Height = 800,
            Background = (Brush)FindResource("BgBrush"),
            Content = new Border { Child = view, ClipToBounds = true },
        };
        _window.Show();
        await ((IActivate)model).ActivateAsync();
        await Settle();

        if (directory is null) return;
        Directory.CreateDirectory(directory);

        Save(directory, $"mapping-loaded-{theme}");

        // 팔레트 검색
        model.PaletteSearch = "초소";
        await Settle();
        Save(directory, $"mapping-palette-filtered-{theme}");
        model.PaletteSearch = string.Empty;
        await Settle();

        // 투입 — 드래그와 같은 경로
        model.AddDevices(new[] { 373, 374 }, -1);
        await Settle();
        Save(directory, $"mapping-dragged-in-{theme}");

        // 순서 바꾸기
        model.SelectedBoardRows.Clear();
        model.SelectedBoardRows.Add(model.BoardRows[^1]);
        model.OnSelectionChanged();
        model.MoveUp();
        await Settle();
        Save(directory, $"mapping-reordered-{theme}");

        // 해제 — 취소선으로 남는다
        model.SelectedBoardRows.Clear();
        model.SelectedBoardRows.Add(model.BoardRows[0]);
        model.OnSelectionChanged();
        model.ReleaseSelected();
        await Settle();
        Save(directory, $"mapping-dirty-{theme}");

        // 좁은 폭 — 창 최소값(1300)과 그 아래(1140) 둘 다.
        _window.Width = 1300;
        await Settle();
        Save(directory, $"mapping-min-{theme}");
        _window.Width = 1140;
        await Settle();
        Save(directory, $"mapping-narrow-{theme}");
        _window.Width = 1360;
        await Settle();

        // 새 맵핑 — 검증 실패(이름 없음)로 [등록] 이 꺼진 상태
        var fresh = MappingPreview.Build();
        var freshView = new MappingWorkbenchView { DataContext = fresh };
        _window.Content = new Border { Child = freshView, ClipToBounds = true };
        await ((IActivate)fresh).ActivateAsync();
        await Settle();
        fresh.BeginCreateMapping();
        await Settle();
        Save(directory, $"mapping-invalid-{theme}");

        // 읽기 전용 — 툴바·버튼이 전부 꺼진 상태(숨김 아님)
        var readOnly = MappingPreview.Build(readOnly: true);
        var readOnlyView = new MappingWorkbenchView { DataContext = readOnly };
        _window.Content = new Border { Child = readOnlyView, ClipToBounds = true };
        await ((IActivate)readOnly).ActivateAsync();
        await Settle();
        Save(directory, $"mapping-readonly-{theme}");

        // 진짜 드래그 — 손잡이를 잡고 보드 위로 옮긴 상태를 그대로 찍는다.
        var dragModel = MappingPreview.Build();
        var dragView = new MappingWorkbenchView { DataContext = dragModel };
        _window.Content = new Border { Child = dragView, ClipToBounds = true };
        await ((IActivate)dragModel).ActivateAsync();
        await Settle();
        await SimulateMappingDragAsync(directory, theme, dragView, dragModel);

        // 빈 목록
        var emptyGateway = new PreviewMappingGateway { IsEmpty = true };
        var empty = new MappingWorkbenchViewModel(emptyGateway, new PreviewDeviceSource());
        var emptyView = new MappingWorkbenchView { DataContext = empty };
        _window.Content = new Border { Child = emptyView, ClipToBounds = true };
        await ((IActivate)empty).ActivateAsync();
        await Settle();
        Save(directory, $"mapping-empty-{theme}");
    }
    #endregion


    /// <summary>
    /// 드래그를 <b>입력 없이</b> 재현한다 — 손잡이의 Thumb 이벤트를 직접 일으키고
    /// 포인터 자리만 <see cref="DragPointer.Override"/> 로 알려 준다. 사용자의 마우스는 건드리지 않는다.
    /// </summary>
    /// <remarks>
    /// 🔴 이 재현이 없으면 "드롭이 핸들러에 닿는가" 를 그림으로도 시험으로도 못 본다 —
    /// 이전 판은 스냅샷이 <c>AddDevices</c> 를 직접 불러서, 드롭존이 잘못된 타입 위에 있는 것을 놓쳤다.
    /// </remarks>
    private async Task SimulateMappingDragAsync(string directory, string theme, FrameworkElement view, MappingWorkbenchViewModel model)
    {
        var log = new StringBuilder();
        var before = model.BoardRows.Count;

        var handle = Descendants<DragHandle>(view)
            .FirstOrDefault(h => h.DataContext is MappingPaletteItemViewModel { IsRegistered: false });
        var board = Descendants<ListBox>(view)
            .FirstOrDefault(l => System.Windows.Automation.AutomationProperties.GetAutomationId(l) == "Integrations.Board.List");

        if (handle is null || board is null)
        {
            log.AppendLine("손잡이나 보드를 못 찾았다 — 드래그 재현을 건너뛴다.");
            File.WriteAllText(Path.Combine(directory, $"drag-simulation-{theme}.txt"), log.ToString());
            return;
        }

        var pointer = new Point();
        DragPointer.Override = relativeTo => view.TranslatePoint(pointer, (UIElement)relativeTo);
        try
        {
            Point CenterOf(FrameworkElement e, double fy = 0.5)
                => e.TranslatePoint(new Point(e.ActualWidth / 2, e.ActualHeight * fy), view);

            pointer = CenterOf(handle);
            handle.RaiseEvent(new DragStartedEventArgs(0, 0));

            // 데드존 안 — 아직 아무 일도 없어야 한다(8.0 DIU 미만).
            pointer = new Point(pointer.X + 4, pointer.Y + 4);
            handle.RaiseEvent(new DragDeltaEventArgs(0, 0));
            log.AppendLine($"데드존 안  boardZone={DropZone.GetState(board)}  (기대 None)");

            // 보드 위로 — 첫 행과 둘째 행 사이를 노린다.
            pointer = CenterOf(board, 0.22);
            handle.RaiseEvent(new DragDeltaEventArgs(0, 0));
            log.AppendLine($"보드 위    boardZone={DropZone.GetState(board)}  (기대 Hover)");
            await Settle(260);
            Save(directory, $"mapping-dragging-{theme}");

            handle.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
            await Settle(260);
            log.AppendLine($"드롭 뒤    보드 {before} → {model.BoardRows.Count}  (기대 +1 이상)");
            log.AppendLine($"Draft      {model.DraftText}");
            Save(directory, $"mapping-dropped-{theme}");
        }
        finally
        {
            DragPointer.Override = null;
        }

        File.WriteAllText(Path.Combine(directory, $"drag-simulation-{theme}.txt"), log.ToString());
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
    }

    private void ApplyDark()
    {
        if (Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == DarkTokens)) return;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(DarkTokens) });
        foreach (var bundled in Resources.MergedDictionaries.OfType<BundledTheme>()) bundled.BaseTheme = BaseTheme.Dark;
    }

    private static Task Settle(int ms = 480) => Task.Delay(ms);

    private DataGrid FindGrid(string automationId)
        => Find(_view) ?? throw new InvalidOperationException($"{automationId} 를 찾지 못했다");

    DataGrid? Find(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is DataGrid grid && grid.Visibility == Visibility.Visible) return grid;
            if (Find(child) is { } found) return found;
        }
        return null;
    }

    private void Save(string directory, string name)
    {
        var content = (FrameworkElement)_window.Content;
        var width = (int)Math.Ceiling(content.ActualWidth);
        var height = (int)Math.Ceiling(content.ActualHeight);
        if (width <= 0 || height <= 0) return;

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(_window.Background, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.None }, null, new Rect(0, 0, width, height));
        }
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(stream);
    }
    #endregion

    /// <summary>미리보기 로그 — 디버그 출력으로만 흘린다.</summary>
    private sealed class PreviewLog : ILogService
    {
        public event EventHandler<LogEventArgs>? LogEvent;

        public void Info(string msg, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                         [System.Runtime.CompilerServices.CallerFilePath] string filePath = "",
                         [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => Write("INFO ", msg);

        public void Warning(string msg, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                            [System.Runtime.CompilerServices.CallerFilePath] string filePath = "",
                            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => Write("WARN ", msg);

        public void Error(string msg, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string filePath = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => Write("ERROR", msg);

        private void Write(string level, string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {msg}");
            _ = LogEvent;   // 쓰이지 않는 이벤트 경고를 막는다
        }
    }
}
