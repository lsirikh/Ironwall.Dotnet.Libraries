using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dashboards;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using MaterialDesignThemes.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeviceConsolePreview;

/// <summary>
/// 장비 콘솔 미리보기 — <b>진짜 뷰 + 진짜 뷰모델</b>을 가짜 데이터 위에 띄운다(호스트 앱 · 서버 없음).
/// <c>--snapshot &lt;폴더&gt;</c> 면 입력 없이 여러 상태를 PNG 로 떠 놓고 끝낸다. <c>--legacy</c> 면 6.3 계약으로 띄운다.
/// </summary>
public partial class App : Application
{
    private const string DarkTokens = "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml";

    private DeviceDashboardViewModel _viewModel = null!;
    private DeviceDashboardView _view = null!;
    private Window _window = null!;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var snapshotAt = Array.IndexOf(e.Args, "--snapshot");
        var directory = snapshotAt >= 0 && snapshotAt + 1 < e.Args.Length ? e.Args[snapshotAt + 1] : null;

        try
        {
            var isAxis = !e.Args.Contains("--legacy");
            if (e.Args.Contains("--dark")) ApplyDark();

            // 조립기 · 펼치기 · 프리셋 · 등록 창 — 콘솔과 따로 뜬다(--assembly [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--assembly"))
            {
                await RunAssemblyAsync(directory, e.Args.Contains("--dark") ? "dark" : "light");
                if (directory is not null) Shutdown();
                return;
            }

            // 다이얼로그 가족 T4 — 콘솔과 따로 뜬다(--dialogs [--dark] [--snapshot <폴더>]). N-05.
            if (e.Args.Contains("--dialogs"))
            {
                await RunDialogsAsync(directory, e.Args.Contains("--dark") ? "dark" : "light");
                if (directory is not null) Shutdown();
                return;
            }

            // 셋업 · 결선 창 — 콘솔과 따로 뜬다(--wiring [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--wiring"))
            {
                await RunWiringAsync(directory, e.Args.Contains("--dark") ? "dark" : "light");
                if (directory is not null) Shutdown();
                return;
            }

            // 서버 모니터(N-12) — 콘솔과 따로 뜬다(--servers [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--servers"))
            {
                await RunServersAsync(directory, e.Args.Contains("--dark") ? "dark" : "light");
                if (directory is not null) Shutdown();
                return;
            }
            _viewModel = Build(isAxis);

            _view = new DeviceDashboardView { DataContext = _viewModel };
            _window = new Window
            {
                Title = "장비 콘솔 미리보기",
                Width = 1320,
                Height = 820,
                Background = (Brush)FindResource("SurfaceBrush"),
                Content = new Border { Margin = new Thickness(12), Child = _view },
            };
            _window.Show();

            await ((IActivate)_viewModel).ActivateAsync();

            if (directory is null) return;

            Directory.CreateDirectory(directory);
            await RunSnapshotsAsync(directory, isAxis ? "axis" : "legacy");
        }
        catch (Exception ex)
        {
            if (directory is null) MessageBox.Show(ex.ToString());
            else File.WriteAllText(Path.Combine(directory, "snapshot-error.txt"), ex.ToString());
        }

        if (directory is not null) Shutdown();
    }

    #region - Composition -
    private static DeviceDashboardViewModel Build(bool isAxis)
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var api = new MockDeviceApiService();
        var providerService = new MockDeviceProviderService();
        var policy = new DeviceQueryPolicy(new FixedProbe(isAxis), log);
        var catalog = new PreviewCatalog();

        // 패널 · 계약 게이트 · 종류 축 지원이 정적 IoC 로 의존을 찾는다 — 컨테이너 대신 여기서 대 준다.
        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? events
            : type == typeof(ICatalogService) ? catalog
            : type == typeof(DeviceQueryPolicy) ? policy
            : null!;
        IoC.GetAllInstances = type => type == typeof(DeviceQueryPolicy) ? new object[] { policy } : Array.Empty<object>();
        IoC.BuildUp = _ => { };

        // 호스트는 부트스트래퍼가 해 준다 — 없으면 Execute.BeginOnUIThread 가 제자리(작업 스레드)에서 돌아 콘솔이 교차 스레드로 화면을 만진다.
        PlatformProvider.Current = new XamlPlatformProvider();

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var servers = new ServerProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var viewModel = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService),
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, servers, api, catalog);

        // 카테고리별 프로바이더는 만들어진 뒤의 추가만 따라간다 — 실제 앱처럼 프로바이더가 다 선 뒤에 장비를 넣는다.
        PreviewData.Fill(devices, groups, isAxis);
        return viewModel;
    }
    #endregion

    #region - Snapshots -
    private async Task RunSnapshotsAsync(string directory, string prefix)
    {
        await Settle();
        Save(directory, $"{prefix}-01-groups");

        await _viewModel.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Camera));
        await Settle();
        Save(directory, $"{prefix}-02-camera-list");

        var grid = FindGrid();
        grid.SelectedItem = grid.Items[0];
        await Settle();
        Save(directory, $"{prefix}-03-camera-single");

        var name = _viewModel.Form.Fields.First(f => f.Key == "name_device");
        name.Text += " (수정)";
        _viewModel.Form.Fields.First(f => f.Key == "connection.ip_port").Text = "70000";
        await Settle();
        Save(directory, $"{prefix}-04-camera-dirty");

        _viewModel.Apply();          // 포트가 범위를 벗어났다 — 아무 행에도 쓰지 않고 칸에 까닭을 적는다
        await Settle();
        Save(directory, $"{prefix}-05-camera-invalid");

        _viewModel.Revert();
        grid.SelectedItems.Clear();
        foreach (var row in grid.Items.Cast<object>().Take(3)) grid.SelectedItems.Add(row);
        await Settle();
        Save(directory, $"{prefix}-06-camera-multi");

        grid.SelectedItems.Clear();
        _viewModel.Add();
        await Settle();
        Save(directory, $"{prefix}-07-camera-create");
        _viewModel.Revert();

        await _viewModel.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Sensor));
        await Settle();
        grid.SelectedItem = grid.Items[0];
        await Settle();
        Save(directory, $"{prefix}-08-sensor-single");

        if (_viewModel.RailEntries.Any(r => r.Key == DeviceDashboardViewModel.ByComponentRailKey))
        {
            grid.SelectedItems.Clear();
            await _viewModel.SelectRailAsync(DeviceDashboardViewModel.ByComponentRailKey);
            await Settle();
            Save(directory, $"{prefix}-09-by-component");
        }

        // 다크 — 토큰 사전을 뒤에 얹고 MDIX 기본 테마를 바꾼다.
        ApplyDark();
        _window.Background = (Brush)FindResource("SurfaceBrush");

        await _viewModel.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Camera));
        await Settle();
        grid.SelectedItem = grid.Items[1];
        await Settle();
        _viewModel.Form.Fields.First(f => f.Key == "name_device").Text += " (수정)";
        await Settle();
        Save(directory, $"{prefix}-10-dark-camera-dirty");

        // 좁은 폭 — 서랍(960~1279) · 접힘(<960)
        _viewModel.Revert();
        _window.Width = 1150;
        await Settle();
        Save(directory, $"{prefix}-11-dark-drawer-1150");

        _window.Width = 900;
        await Settle();
        Save(directory, $"{prefix}-12-dark-compact-900");
    }

    private async Task RunAssemblyAsync(string? directory, string theme)
    {
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new XamlPlatformProvider();

        var work = directory ?? Path.Combine(Path.GetTempPath(), "ironwall-assembly-preview");
        Directory.CreateDirectory(work);
        var presetFile = Path.Combine(work, "preview-presets.json");
        if (File.Exists(presetFile)) File.Delete(presetFile);

        var preview = new AssemblyPreview(work);
        _window = new Window { Title = "조립기 미리보기", Width = 1320, Height = 820, Background = (Brush)FindResource("SurfaceBrush") };
        _window.Show();

        async Task Show(FrameworkElement view, double width, double height, string name)
        {
            _window.Width = width + 40;
            _window.Height = height + 60;
            if (view.Parent is Border old) old.Child = null;     // 같은 뷰를 두 번 찍을 때 — 옛 부모에서 먼저 뗀다
            _window.Content = new Border { Margin = new Thickness(12), Child = view };
            await Settle();
            if (directory is not null) Save(directory, $"assembly-{theme}-{name}");
        }

        var (composeView, composeVm) = await preview.ComposeAsync();
        await Show(composeView, 1280, 760, "01-compose");
        if (directory is null) return;      // 손으로 써 볼 때는 조립기만 띄워 둔다

        preview.MakeKeyCollision(composeVm);
        await Show(composeView, 1280, 760, "02-key-collision");
        await Show(await preview.EditDeviceAsync(), 1280, 760, "03-edit-device-unknown-type");
        await Show(preview.RepeatExpand(withConflict: false), 440, 600, "04-repeat-expand");
        await Show(preview.RepeatExpand(withConflict: true), 440, 600, "05-repeat-expand-conflict");
        await Show(preview.PresetManager(), 720, 560, "06-preset-manager");
        await Show(preview.Register(withProblem: false), 980, 680, "07-register");
        await Show(preview.Register(withProblem: true), 980, 680, "08-register-problems");
    }

    /// <summary>다이얼로그 가족 T4 — 규격 갤러리 · 옮긴 창들 · 배정 창의 상태들을 띄우고(스냅샷이면) 찍는다.</summary>
    private async Task RunDialogsAsync(string? directory, string theme)
    {
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new XamlPlatformProvider();

        if (directory is not null) Directory.CreateDirectory(directory);

        var preview = new DialogPreview();
        _window = new Window { Title = "다이얼로그 가족 미리보기", Width = 1320, Height = 860, Background = (Brush)FindResource("BgBrush") };
        _window.Show();

        async Task Show(FrameworkElement view, double width, double height, string name)
        {
            _window.Width = width + 40;
            _window.Height = height + 60;
            if (view.Parent is Border old) old.Child = null;
            _window.Content = new Border { Margin = new Thickness(12), Child = view };
            await Settle();
            if (directory is not null) Save(directory, $"dialogs-{theme}-{name}");
        }

        await Show(preview.Gallery(), 1760, 520, "01-gallery-sml");
        if (directory is null) return;      // 손으로 써 볼 때는 갤러리만 띄워 둔다

        await Show(preview.ConfirmPrompt(), 448, 260, "02-confirm-s400");
        await Show(preview.TextPrompt(), 448, 240, "03-text-prompt-s400");
        await Show(preview.RepeatExpand(withConflict: true), 448, 620, "04-repeat-expand-s400");
        await Show(preview.WiringPrompt(), 448, 360, "05-wiring-prompt-s400");

        var work = Path.Combine(Path.GetTempPath(), "ironwall-dialog-preview");
        Directory.CreateDirectory(work);
        var presetFile = Path.Combine(work, "preview-presets.json");
        if (File.Exists(presetFile)) File.Delete(presetFile);

        await Show(preview.MakeSensors(withConflict: true), 608, 700, "14-make-sensors-m560");
        await Show(preview.PasteReport(), 608, 680, "15-paste-report-m560");
        await Show(preview.PresetManager(work), 768, 560, "16-preset-manager-l720");
        await Show(preview.Register(work, withProblem: true), 768, 660, "17-register-l720");

        await Show(preview.Assign(AssignState.Loaded).View, 768, 560, "06-assign-loaded-l720");
        await Show(preview.Assign(AssignState.MultiSelect).View, 768, 560, "07-assign-multi-select");
        // 끄는 동안의 모양 — 드롭존 상태를 실제로 물린 뒤에 찍는다(형태 피드백 증거).
        var blocked = preview.Assign(AssignState.Blocked);
        _window.Width = 768 + 40;
        _window.Height = 560 + 60;
        _window.Content = new Border { Margin = new Thickness(12), Child = blocked.View };
        await Settle();
        DialogPreview.ForceZoneState(blocked.View, "Devices.Assign.Assigned", DropZoneState.Blocked);
        DialogPreview.ForceZoneState(blocked.View, "Devices.Assign.Available", DropZoneState.Available);
        await Settle();
        if (directory is not null) Save(directory, $"dialogs-{theme}-08-assign-blocked-drop");

        var dragOver = preview.Assign(AssignState.DragOver);
        _window.Content = new Border { Margin = new Thickness(12), Child = dragOver.View };
        await Settle();
        DialogPreview.ForceZoneState(dragOver.View, "Devices.Assign.Assigned", DropZoneState.Hover);
        DialogPreview.ForceZoneState(dragOver.View, "Devices.Assign.Available", DropZoneState.Available);
        await Settle();
        if (directory is not null) Save(directory, $"dialogs-{theme}-08b-assign-drag-over");
        await Show(preview.Assign(AssignState.Dirty).View, 768, 560, "09-assign-dirty-delta");
        await Show(preview.Assign(AssignState.PartialFailure).View, 768, 620, "10-assign-partial-failure");
        await Show(preview.AssignUnsavedGroup(), 768, 620, "11-assign-unsaved-group");

        await Show(preview.Progress(cancelled: false).View, 448, 320, "12-progress-running");
        await Show(preview.Progress(cancelled: true).View, 448, 320, "13-progress-cancelled");
    }

    /// <summary>셋업 · 결선 창의 상태 8종을 띄우고(스냅샷이면) 찍는다.</summary>
    private async Task RunWiringAsync(string? directory, string theme)
    {
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new XamlPlatformProvider();

        if (directory is not null) Directory.CreateDirectory(directory);      // 오류 파일조차 못 쓰는 일이 없게

        var preview = new WiringPreview();
        _window = new Window { Title = "셋업 · 결선 미리보기", Width = 1320, Height = 860, Background = (Brush)FindResource("SurfaceBrush") };
        _window.Show();

        async Task Show(FrameworkElement view, double width, double height, string name)
        {
            _window.Width = width + 40;
            _window.Height = height + 60;
            if (view.Parent is Border old) old.Child = null;     // 같은 뷰를 두 번 찍을 때 — 옛 부모에서 먼저 뗀다
            _window.Content = new Border { Margin = new Thickness(12), Child = view };
            await Settle();
            if (directory is not null) Save(directory, $"wiring-{theme}-{name}");
        }

        var (emptyView, _) = preview.Empty();
        await Show(emptyView, 1280, 820, "01-empty-controller");
        if (directory is null) return;      // 손으로 써 볼 때는 창 하나만 띄워 둔다

        var (tableView, _) = preview.TableWithSelection();
        await Show(tableView, 1280, 820, "02-table-selection");

        var (wiredView, _) = preview.Wired();
        await Show(wiredView, 1280, 820, "03-wiring-placed");

        var (problemView, _) = preview.Problems();
        await Show(problemView, 1280, 820, "04-wiring-problems");

        var (changeView, _) = preview.ChangePreview();
        await Show(changeView, 1280, 820, "05-change-preview");

        var (groupView, _) = preview.GroupSelection();
        await Show(groupView, 1280, 820, "10-groups-tristate");

        var (slotView, _) = preview.SlotSelected();
        await Show(slotView, 1280, 820, "11-slot-selected");

        await Show(preview.MakeSensors(withConflict: false), 520, 640, "06-make-sensors");
        await Show(preview.MakeSensors(withConflict: true), 520, 640, "07-make-sensors-conflict");
        await Show(preview.PasteReport(), 640, 560, "08-paste-report");
        await Show(preview.SaveConfirm(), 520, 340, "09-save-confirm");
    }

/// <summary>
    /// 서버 모니터(N-12) 상태별 스냅숏 — 빈 화면 · 목록 · 선택+지표 · 보고 없음 · 미적용 변경 ·
    /// 드롭 가능/불가 · 등록 폼 · 모드 절 · 지표 이력 창 · 6.3 계약.
    /// </summary>
    private async Task RunServersAsync(string? directory, string theme)
    {
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        // 호스트는 부트스트래퍼가 해 준다 — 없으면 Execute 가 작업 스레드에서 돌아 교차 스레드로 화면을 만진다.
        PlatformProvider.Current = new XamlPlatformProvider();

        var preview = new ServersPreview();
        var view = await preview.BuildAsync(withData: false);

        _window = new Window
        {
            Title = "서버 모니터 미리보기",
            Width = 1320,
            Height = 820,
            Background = (Brush)FindResource("SurfaceBrush"),
            Content = new Border { Margin = new Thickness(12), Child = view },
        };
        _window.Show();
        await Settle();

        if (directory is null) return;      // 손으로 써 볼 때는 띄워만 둔다

        Directory.CreateDirectory(directory);
        await Settle();
        Save(directory, $"servers-{theme}-01-empty");

        await preview.LoadAsync();
        await Settle();
        Save(directory, $"servers-{theme}-02-loaded");

        preview.Select(preview.Row("방송서버-01"));       // 지표가 붙어 있는 행
        await Settle();
        Save(directory, $"servers-{theme}-03-selected-metrics");

        preview.Select(preview.Row("백업서버"));           // 한 번도 보고가 없는 행(status_observed_at = null)
        await Settle();
        ServersPreview.ScrollDetailToEnd(view);            // "상태(관측)" 절의 미수신 상자를 보이게 굴린다
        await Settle();
        Save(directory, $"servers-{theme}-04-never-reported");

        preview.Select(preview.Row("방송서버-01"));
        ServersPreview.ScrollDetailToTop(view);
        preview.ViewModel.BeginEdit();
        preview.ViewModel.NameText = "방송서버-01 (수정)";
        preview.ViewModel.CpuWarningText = "65";
        await Settle();
        Save(directory, $"servers-{theme}-05-dirty");

        preview.ViewModel.Revert();
        ServersPreview.ScrollDetailToTop(view);
        await Settle();

        // 드롭 불가 — 스피커를 NVR 행 위로. 끝은 반드시 취소라 서버 호출이 0 이다.
        using (preview.BeginDrag(view, preview.Row("NVR-01")))
        {
            await Settle();
            Save(directory, $"servers-{theme}-06-drop-refused");
        }
        await Settle();

        // 드롭 가능 + 지금 그 위 — 같은 스피커를 받는 서버 행 위로.
        using (preview.BeginDrag(view, preview.Row("방송서버-01")))
        {
            await Settle();
            Save(directory, $"servers-{theme}-07-drop-hover");
        }
        await Settle();

        // 모드 절 — PROXY 서버는 server_config.modes 를 갖는다(7.0+).
        preview.Select(preview.Row("PIDS 프록시"));
        preview.ViewModel.BeginEdit();
        ServersPreview.ScrollDetailToTop(view);
        await Settle();
        Save(directory, $"servers-{theme}-08-modes");
        preview.ViewModel.Revert();

        // 등록 폼 — 상태 칸이 없다.
        preview.ViewModel.Add();
        await Settle();
        Save(directory, $"servers-{theme}-09-create");
        preview.ViewModel.Revert();
        await Settle();

        // 6.3 계약 — "마지막 변화" 가 "—" 이고 배정 후보가 스피커뿐이다.
        var legacy = new ServersPreview(EnumServerContract.V6_3);
        var legacyView = await legacy.BuildAsync(withData: true);
        _window.Width = 1320;
        _window.Height = 820;
        _window.Content = new Border { Margin = new Thickness(12), Child = legacyView };
        await Settle();
        legacy.Select(legacy.Row("방송서버-01"));
        await Settle();
        Save(directory, $"servers-{theme}-11-legacy-6-3");
        // 지표 이력 창 — 임계 배지를 그리지 않는다.
        var history = await preview.MetricHistoryAsync(11, "방송서버-01");
        _window.Width = 600;
        _window.Height = 560;
        _window.Content = new Border { Margin = new Thickness(12), Child = history };
        await Settle();
        Save(directory, $"servers-{theme}-10-metric-history");

    }

    private void ApplyDark()
    {
        if (Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == DarkTokens)) return;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(DarkTokens) });
        foreach (var bundled in Resources.MergedDictionaries.OfType<BundledTheme>()) bundled.BaseTheme = BaseTheme.Dark;
    }

    private static Task Settle() => Task.Delay(450);

    private DataGrid FindGrid()
    {
        return Find(_view) ?? throw new InvalidOperationException("DataGrid 를 찾지 못했다");

        static DataGrid? Find(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is DataGrid grid && System.Windows.Automation.AutomationProperties.GetAutomationId(grid) == "Console.Devices.Grid") return grid;
                if (Find(child) is { } found) return found;
            }
            // 부대 콘솔(N-11) — 콘솔과 따로 뜬다(--units [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--units")) { await RunUnitsAsync(directory); if (directory is not null) Shutdown(); return; }

            return null;
        }
    }

    private void Save(string directory, string name)
    {
        var content = (FrameworkElement)_window.Content;
        var width = (int)Math.Ceiling(content.ActualWidth);
        var height = (int)Math.Ceiling(content.ActualHeight);
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

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(bool isAxis) => Contract = isAxis ? Enum.GetValues<EnumServerContract>().Max() : EnumServerContract.V6_3;

    /// <summary>
    /// 부대 콘솔 — 진짜 뷰 + 진짜 뷰모델을 가짜 창구 위에 띄운다. 서버에 한 줄도 나가지 않는다.
    /// </summary>
    private async Task RunUnitsAsync(string? directory)
    {
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        // 호스트는 부트스트래퍼가 해 준다 — 없으면 Execute.BeginOnUIThread 가 작업 스레드에서 돌아 교차 스레드가 된다.
        PlatformProvider.Current = new XamlPlatformProvider();

        var preview = new UnitsPreview();
        var console = preview.Build();
        var view = new UnitConsoleView { DataContext = console };

        _window = new Window
        {
            Title = "부대 콘솔 미리보기",
            Width = 1320,
            Height = 820,
            Background = (Brush)FindResource("SurfaceBrush"),
            Content = new Border { Margin = new Thickness(12), Child = view },
        };
        _window.Show();
        await ((IActivate)console).ActivateAsync();

        if (directory is null) return;
        Directory.CreateDirectory(directory);

        async Task Shot(string name)
        {
            await Settle();
            Save(directory, name);
        }

        async Task Sweep(string theme)
        {
            await Shot($"units-{theme}-01-tree");

            var company = console.Rows.First(r => r.Code == "c0206");
            await console.SelectRowAsync(company);
            await Shot($"units-{theme}-02-detail");

            console.Form.Name = "6중대 (개편)";
            await Shot($"units-{theme}-03-dirty");
            console.Revert();

            console.BeginCreate();
            console.Form.Code = "Bad.Code";
            console.Form.Name = "새 중대";
            await console.ApplyAsync();                   // 보내지 않고 칸에 까닭을 적는다
            await Shot($"units-{theme}-04-create-invalid");
            console.Revert();

            await console.SelectRowAsync(console.Rows.First(r => r.Code == "c0206"));
            preview.MakeNextWriteFail();
            await console.MoveAsync(console.Rows.First(r => r.Code == "c0206").Id, console.Rows.First(r => r.Code == "r0101").Id);
            await Shot($"units-{theme}-05-move-failed");

            await console.SelectRowAsync(console.Rows.First(r => r.Code == "c0206"));
            preview.MakeDeleteBlocked();
            await console.DeleteAsync();
            await Shot($"units-{theme}-06-delete-blocked");
            console.DismissDeleteBlock();

            // 끄는 쪽(장비)과 놓는 쪽(트리)이 한 화면에 같이 있어야 이 창의 대표 기능이 성립한다.
            console.SelectedRail = console.RailEntries.First(r => r.Key == UnitConsoleViewModel.RAIL_DEVICES);
            await Shot($"units-{theme}-07-devices");

            console.QueueAssign(console.Tree.Ordered.First(n => n.Code == "c0206").Id, console.DeviceRows.Take(3).ToList());
            await Shot($"units-{theme}-08-devices-draft");
            console.RevertAssigns();

            console.SelectedRail = console.RailEntries.First(r => r.Key == UnitConsoleViewModel.RAIL_ADJACENCY);
            await Shot($"units-{theme}-09-adjacency-placeholder");
            console.SelectedRail = console.RailEntries.First(r => r.Key == UnitConsoleViewModel.RAIL_TREE);

            // 막힌 드롭 — 같은 제대 위에 놓으려 하면 까닭이 상태 띠에 뜬다.
            var moving = console.Rows.First(r => r.Code == "c0206");
            var sameEchelon = console.Rows.First(r => r.Code == "c0205");
            console.Drop.Drop(new object[] { moving }, new DropTarget(UnitDropRules.ZONE_PARENT, sameEchelon, -1));
            await Shot($"units-{theme}-11-drop-blocked");

            // 필터가 걸리면 평면이다 — 부모가 걸러진 자식이 허공에 들여쓰기되지 않는다.
            console.SelectEchelon(console.EchelonFilters.First(f => f.Echelon == EnumUnitEchelon.Company));
            await Shot($"units-{theme}-12-filtered");
            console.SelectEchelon(console.EchelonFilters.First(f => f.Echelon is null));

            // 좁은 폭 — 서랍(960~1279) · 접힘(<960)
            var wide = _window.Width;
            _window.Width = 1150;
            await Shot($"units-{theme}-13-drawer-1150");
            _window.Width = 900;
            await Shot($"units-{theme}-14-compact-900");
            _window.Width = wide;
            await Settle();
        }

        await Sweep("light");

        // 옛 계약(6.3) — 부대 편제 자체가 없는 서버. 화면이 빈 채로 까닭을 말해야 한다.
        var legacy = preview.Build(legacy: true);
        ((Border)_window.Content).Child = new UnitConsoleView { DataContext = legacy };
        await ((IActivate)legacy).ActivateAsync();
        await Shot("units-light-10-legacy-empty");

        ApplyDark();
        _window.Background = (Brush)FindResource("SurfaceBrush");
        var dark = new UnitsPreview();
        var darkConsole = dark.Build();
        ((Border)_window.Content).Child = new UnitConsoleView { DataContext = darkConsole };
        await ((IActivate)darkConsole).ActivateAsync();

        var savedPreview = preview;
        preview = dark;
        console = darkConsole;
        await Sweep("dark");
        _ = savedPreview;
    }

        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract == EnumServerContract.V6_3 ? "6.3.2" : "8.0.1";
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
}
