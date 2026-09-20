using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dashboards;
using Ironwall.Dotnet.Libraries.Enums;
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

            // 서버 모니터(N-12) — 콘솔과 따로 뜬다(--servers [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--servers"))
            {
                await RunServersAsync(directory, e.Args.Contains("--dark") ? "dark" : "light");
                if (directory is not null) Shutdown();
                return;
            }

            // 조립기 · 펼치기 · 프리셋 · 등록 창 — 콘솔과 따로 뜬다(--assembly [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--assembly"))
            {
                await RunAssemblyAsync(directory, e.Args.Contains("--dark") ? "dark" : "light");
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

    /// <summary>
    /// 서버 모니터(N-12) 상태별 스냅숏 — 빈 화면 · 목록 · 선택+지표 · 보고 없음 · 미적용 변경 · 드롭 불가.
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
        await Settle();                                   // 첫 장면은 배치가 한 번 더 도는 것을 기다린다
        Save(directory, $"servers-{theme}-01-empty");

        await preview.LoadAsync();
        await Settle();
        Save(directory, $"servers-{theme}-02-loaded");

        preview.Select(preview.Row("방송서버-01"));       // 지표가 붙어 있는 행
        await Settle();
        Save(directory, $"servers-{theme}-03-selected-metrics");

        preview.Select(preview.Row("백업서버"));           // 한 번도 보고가 없는 행
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

        // 드롭 불가 — 스피커를 NVR 행 위로 끌어 본다. 끝은 반드시 취소라 서버 호출이 0 이다.
        using (preview.BeginRefusedDrag(view, preview.Row("NVR-01")))
        {
            await Settle();
            Save(directory, $"servers-{theme}-06-drop-refused");
        }
        await Settle();
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

        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract == EnumServerContract.V6_3 ? "6.3.2" : "8.0.1";
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
}
