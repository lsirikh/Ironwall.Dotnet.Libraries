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
using Ironwall.Dotnet.Libraries.Utils.Consoles;
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

            // 부대 관계도(unit-relationship-map IMPL-35) — 진짜 캔버스 + 진짜 뷰모델 · 가짜 배치 서버 · 200 부대.
            // --units-map [--layout supported|unsupported|fail|two-client] [--visible] [--view-only] [--call-log <파일>]
            //             [--snapshot <폴더>] [--bench <폴더>] [--dark]
            if (e.Args.Contains("--units-map"))
            {
                await RunUnitsMapAsync(e.Args);
                return;
            }

            // 부대 콘솔(N-11) — 콘솔과 따로 뜬다(--units [--dark] [--snapshot <폴더>]).
            if (e.Args.Contains("--units"))
            {
                await RunUnitsAsync(directory);
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

            // 결선 펜스 뷰(wiring-fence-view F-4 · fence-wiring-editor) — --wiring ring|pids|line|wall [--theme light|dark] [--shot <폴더>]
            //   --shot: 진짜 WiringView(앱 리소스 병합)를 라이트/다크 × 입체/평면 × 3 시나리오 × 1280×820 · 1440×900 으로 찍고 끝낸다.
            var wiringAt = Array.IndexOf(e.Args, "--wiring");
            var fenceScenario = wiringAt >= 0 && wiringAt + 1 < e.Args.Length && e.Args[wiringAt + 1] is "ring" or "pids" or "line" or "wall"
                ? e.Args[wiringAt + 1] : null;
            var shotAt = Array.IndexOf(e.Args, "--shot");
            if (wiringAt >= 0 && (fenceScenario is not null || shotAt >= 0))
            {
                var themeAt = Array.IndexOf(e.Args, "--theme");
                var theme = themeAt >= 0 && themeAt + 1 < e.Args.Length ? e.Args[themeAt + 1] : e.Args.Contains("--dark") ? "dark" : "light";
                var shotDir = shotAt >= 0 && shotAt + 1 < e.Args.Length ? e.Args[shotAt + 1] : null;
                await RunFenceAsync(fenceScenario ?? "ring", theme, shotDir);
                if (shotDir is not null) Shutdown();
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
            PreviewTools.Shared.OffscreenStage.ApplySurface(e.Args, _view, _window);
            PreviewTools.Shared.OffscreenStage.Hide(_window).Show();

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
        var probe = new FixedProbe(isAxis);
        var policy = new DeviceQueryPolicy(probe, log);
        var catalog = new PreviewCatalog();
        // D-14: "소속 부대" 열·칸이 실제 이름으로 뜨는지 눈으로 보려면 UnitNameDirectory 도 IoC 로 잡혀야 한다 —
        // 실제 앱과 같은 probe(V8.0/V6.3)를 공유해 IsUnitEra 판정이 정책과 어긋나지 않는다.
        var unitDirectory = new UnitNameDirectory(new PreviewUnitGraphApi(), probe, log);
        // 상세의 "그룹" 칸이 그룹 이름으로 뜨도록(실앱은 컨테이너가 준다) — 행 뷰모델이 IoC 로 찾는다.
        var groups = new DeviceGroupProvider(log);

        // 패널 · 계약 게이트 · 종류 축 지원이 정적 IoC 로 의존을 찾는다 — 컨테이너 대신 여기서 대 준다.
        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? events
            : type == typeof(ICatalogService) ? catalog
            : type == typeof(DeviceQueryPolicy) ? policy
            : type == typeof(UnitNameDirectory) ? unitDirectory
            : type == typeof(DeviceGroupProvider) ? groups
            : null!;
        IoC.GetAllInstances = type => type == typeof(DeviceQueryPolicy) ? new object[] { policy } : Array.Empty<object>();
        IoC.BuildUp = _ => { };

        // 렌더 전에 미리 채운다 — 스냅샷·첫 화면이 배경 재조회의 비동기 갱신(re-notify)에 기대지 않고
        // 곧장 실제 이름을 보이게 한다(1소초·2소초). id 999(그래프에 없음)·null(미배치)은 의도적으로 남겨 둔다.
        unitDirectory.EnsureLoadedAsync().GetAwaiter().GetResult();

        // 호스트는 부트스트래퍼가 해 준다 — 없으면 Execute.BeginOnUIThread 가 제자리(작업 스레드)에서 돌아 콘솔이 교차 스레드로 화면을 만진다.
        PlatformProvider.Current = new XamlPlatformProvider();

        var devices = new DeviceProvider();
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
            devices, groups, controllers, servers, api, catalog,
            policy,
            // 실앱처럼 조립기 · 프리셋 · 결선 입구가 툴바에 서야 툴바 폭(오버플로)을 실창과 같은 조건으로 본다 — 누르면 아무 일도 없다.
            assemblyLauncher: isAxis ? new Lazy<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.IAssemblyLauncher>(() => new PreviewAssemblyLauncher()) : null,
            wiringLauncher: isAxis ? new Lazy<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.IWiringLauncher>(() => new PreviewWiringLauncher()) : null);

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

        // D-14: "소속 부대" 는 선택 열(IsDefault=false)이라 열 메뉴를 거치지 않고는 기본 화면에 없다 —
        // ConsolePrefs(디스크 파일)를 건드리지 않고 그리드 열의 Visibility 만 직접 펼쳐 한 장 남긴다.
        // 카메라 1(부대 1·"1소초") · 4(부대 999·그래프에 없음 → 원값 id) · 나머지(UnitId 없음 → "미배치")가
        // 한 화면에 세 경로를 다 보인다.
        var hiddenByDefault = grid.Columns.Where(c => c.Visibility != Visibility.Visible).ToList();
        foreach (var c in hiddenByDefault) c.Visibility = Visibility.Visible;
        await Settle();
        // 그리드가 가로 스크롤 상태다 — "소속 부대" 열까지 스크롤해 실제로 프레임에 들어오게 한다.
        var unitColumn = grid.Columns.FirstOrDefault(c => ConsoleColumns.GetKey(c) == "unit");
        if (unitColumn is not null && grid.Items.Count > 0) grid.ScrollIntoView(grid.Items[0], unitColumn);
        await Settle();
        Save(directory, $"{prefix}-02b-camera-list-unit-column");
        foreach (var c in hiddenByDefault) c.Visibility = Visibility.Collapsed;

        grid.SelectedItem = grid.Items[0];
        await Settle();
        Save(directory, $"{prefix}-03-camera-single");

        // D-07 — 레거시 계약의 "제어 모드"·"카메라 유형(레거시)" 콤보(연결 절 맨 아래)는 고정 높이 미리보기
        // 창의 접힘 아래에 있어 지금까지 렌더된 채로 눈으로 확인된 적이 없다. 상세 칸을 끝까지 굴려 그 절을
        // 프레임 안으로 끌어온다 — --legacy 에서만 뜻이 있다(LegacyContractOnly, DevicePropertyCatalog.cs:318-331).
        ServersPreview.ScrollDetailToEnd(_view);
        await Settle();
        Save(directory, $"{prefix}-03c-camera-detail-scrolled");
        ServersPreview.ScrollDetailToTop(_view);
        await Settle();

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

        // 제어기 레일 — [셋업 · 결선] 은 여기서만 보인다(다른 레일에서는 숨긴다). 폭이 모자라면 [⋯] 로 옮겨진다.
        await _viewModel.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Controller));
        await Settle();
        grid.SelectedItem = grid.Items[0];
        await Settle();
        Save(directory, $"{prefix}-08b-controller-single");

        // 함체 — 접속(제어기 접점) · 부품 · 부품 상태 · 임계값 · 부품별 설정. 상세를 끝까지 굴려 아래 절도 본다.
        await _viewModel.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Enclosure));
        await Settle();
        grid.SelectedItem = grid.Items[0];
        await Settle();
        Save(directory, $"{prefix}-08c-enclosure-single");
        ServersPreview.ScrollDetailToEnd(_view);
        await Settle();
        Save(directory, $"{prefix}-08d-enclosure-detail-scrolled");
        ServersPreview.ScrollDetailToTop(_view);
        await Settle();

        // 빈 목록 안내 — 검색 결과가 없을 때.
        _viewModel.SearchText = "없는 장비";
        await Settle();
        Save(directory, $"{prefix}-08e-empty-search");
        _viewModel.SearchText = string.Empty;
        await Settle();

        await _viewModel.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Sensor));
        await Settle();

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
        grid.SelectedItem = grid.Items[0];
        await Settle();

        // D-14 다크: 소속 부대 열 · 칸이 다크에서도 글자가 안 죽는지(라이트/다크 가장 흔한 불만) 확인한다.
        // ⚠ 레일 전환(SelectRailAsync)이 RebuildColumns 로 그리드 열을 통째로 새로 만든다 — 위에서 잡아 둔
        // hiddenByDefault 는 이제 옛 열 인스턴스라 다시 그리드에서 새로 찾는다.
        var hiddenByDefaultDark = grid.Columns.Where(c => c.Visibility != Visibility.Visible).ToList();
        var unitColumnDark = grid.Columns.FirstOrDefault(c => ConsoleColumns.GetKey(c) == "unit");
        if (unitColumnDark is not null)
        {
            foreach (var c in hiddenByDefaultDark) c.Visibility = Visibility.Visible;
            grid.ScrollIntoView(grid.Items[0], unitColumnDark);
            await Settle();
            Save(directory, $"{prefix}-09b-dark-camera-list-unit-column");
            foreach (var c in hiddenByDefaultDark) c.Visibility = Visibility.Collapsed;
        }

        grid.SelectedItem = grid.Items[1];
        await Settle();
        _viewModel.Form.Fields.First(f => f.Key == "name_device").Text += " (수정)";
        await Settle();
        Save(directory, $"{prefix}-10-dark-camera-dirty");

        // D-07 다크 — 같은 절을 다크에서도 눈으로 확인한다(색만 다를 뿐 같은 자리).
        ServersPreview.ScrollDetailToEnd(_view);
        await Settle();
        Save(directory, $"{prefix}-09c-dark-camera-detail-scrolled");
        ServersPreview.ScrollDetailToTop(_view);
        await Settle();
        _viewModel.Revert();
        await Settle();

        // 좁은 폭 — 서랍(960~1279) · 접힘(<960). --surface 면 콘솔 폭 자체를 줄인다(SetWidth).
        var wideView = _view.Width;
        PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, 1150);
        await Settle();
        Save(directory, $"{prefix}-11-dark-drawer-1150");

        // 상세(서랍)를 닫은 좁은 폭 — 목록 · 툴바만 남은 모습.
        grid.SelectedItems.Clear();
        await Settle();
        Save(directory, $"{prefix}-11b-dark-drawer-1150-closed");
        grid.SelectedItem = grid.Items[0];

        PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, 900);
        await Settle();
        Save(directory, $"{prefix}-12-dark-compact-900");

        grid.SelectedItems.Clear();
        await Settle();
        Save(directory, $"{prefix}-12b-dark-compact-900-closed");

        if (!double.IsNaN(wideView)) PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, wideView);
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
        PreviewTools.Shared.OffscreenStage.Hide(_window).Show();

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
        PreviewTools.Shared.OffscreenStage.Hide(_window).Show();

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
        PreviewTools.Shared.OffscreenStage.Hide(_window).Show();

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
        // Job 2 회귀 확인 — 별 열("이름")이 좁은 폭에서 MinWidth(140) 를 지키는지 눈으로 본다.
        // (결선맵이 고정 360 을 먹는 두 칸 레이아웃이라 너무 좁으면 표 자체가 접힌다 — 1000 으로 잡는다.)
        await Show(tableView, 1000, 820, "02b-narrow-1000");

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
        PreviewTools.Shared.OffscreenStage.ApplySurface(Environment.GetCommandLineArgs(), view, _window);
        PreviewTools.Shared.OffscreenStage.Hide(_window).Show();
        await Settle();

        if (directory is null) return;      // 손으로 써 볼 때는 띄워만 둔다

        Directory.CreateDirectory(directory);
        await Settle();
        Save(directory, $"servers-{theme}-01-empty");

        await preview.LoadAsync();
        await Settle();
        Save(directory, $"servers-{theme}-02-loaded");

        // Job 2 회귀 확인 — 별 열("이름")이 좁은 폭에서 MinWidth(140) 를 지키는지 눈으로 본다
        // (900 = 장비 콘솔과 같은 "접힘" 경계 — 그보다 좁으면 셸 자체가 레일까지 못 그린다).
        var wideServers = _window.Width;
        var wideServersView = view.Width;
        PreviewTools.Shared.OffscreenStage.SetWidth(_window, view, 900);
        await Settle();
        Save(directory, $"servers-{theme}-02b-narrow-900");
        if (!double.IsNaN(wideServersView)) PreviewTools.Shared.OffscreenStage.SetWidth(_window, view, wideServersView);
        else _window.Width = wideServers;
        await Settle();

        preview.Select(preview.Row("방송서버-01"));       // 지표가 붙어 있는 행
        await Settle();
        Save(directory, $"servers-{theme}-03-selected-metrics");

        // 좁힌 서랍(표면 1000) — 고른 채로 지표 띠가 2칸 두 줄이 돼도 목록이 다섯 줄 이상 남는지 본다(GIS 실창 #1).
        PreviewTools.Shared.OffscreenStage.SetWidth(_window, view, 1000);
        await Settle();
        Save(directory, $"servers-{theme}-03b-selected-narrow-1000");
        if (!double.IsNaN(wideServersView)) PreviewTools.Shared.OffscreenStage.SetWidth(_window, view, wideServersView);
        else _window.Width = wideServers;
        await Settle();

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
        PreviewTools.Shared.OffscreenStage.ApplySurface(Environment.GetCommandLineArgs(), legacyView, _window);
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
        PreviewTools.Shared.OffscreenStage.ApplySurface(Environment.GetCommandLineArgs(), view, _window);
        PreviewTools.Shared.OffscreenStage.Hide(_window).Show();
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

            // 07 · 08(「미배치 장비」 칸 · 배치 대기)은 서버 회신 Q-1 ⓐ(개념 폐지)로 칸과 함께 없앴다 — 번호는 비워 둔다.

            // 부대 관계도 레일 — 툴바 둘째 줄(레이어 켜기/끄기 · 예하 포함) · 캔버스 · 상태 줄을 한 장에(2026-09-28 실앱 결함 3종 확인용).
            console.SelectedRail = console.RailEntries.First(r => r.Key == UnitConsoleViewModel.RAIL_ADJACENCY);
            await Shot($"units-{theme}-09-map");

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

            // 좁은 폭 — 서랍(960~1279) · 접힘(<960). --surface 면 콘솔 폭 자체를 줄인다.
            var wide = _window.Width;
            var unitsView = ((Border)_window.Content).Child as FrameworkElement;
            var wideView = unitsView?.Width ?? double.NaN;
            PreviewTools.Shared.OffscreenStage.SetWidth(_window, unitsView, 1150);
            await Shot($"units-{theme}-13-drawer-1150");
            PreviewTools.Shared.OffscreenStage.SetWidth(_window, unitsView, 900);
            await Shot($"units-{theme}-14-compact-900");
            if (!double.IsNaN(wideView)) PreviewTools.Shared.OffscreenStage.SetWidth(_window, unitsView, wideView);
            else _window.Width = wide;
            await Settle();
        }

        await Sweep("light");

        // 옛 계약(6.3) — 부대 편제 자체가 없는 서버. 화면이 빈 채로 까닭을 말해야 한다.
        var legacy = preview.Build(legacy: true);
        var legacyUnitsView = new UnitConsoleView { DataContext = legacy };
        PreviewTools.Shared.OffscreenStage.ApplySurface(Environment.GetCommandLineArgs(), legacyUnitsView, _window);
        ((Border)_window.Content).Child = legacyUnitsView;
        await ((IActivate)legacy).ActivateAsync();
        await Shot("units-light-10-legacy-empty");

        ApplyDark();
        _window.Background = (Brush)FindResource("SurfaceBrush");
        var dark = new UnitsPreview();
        var darkConsole = dark.Build();
        var darkUnitsView = new UnitConsoleView { DataContext = darkConsole };
        PreviewTools.Shared.OffscreenStage.ApplySurface(Environment.GetCommandLineArgs(), darkUnitsView, _window);
        ((Border)_window.Content).Child = darkUnitsView;
        await ((IActivate)darkConsole).ActivateAsync();

        var savedPreview = preview;
        preview = dark;
        console = darkConsole;
        await Sweep("dark");
        _ = savedPreview;
    }

    /// <summary>부대 관계도 미리보기 — 창 · 스냅숏 · 벤치(UnitsMapPreview.cs · UnitsMapPreviewRuns.cs).</summary>
    private async Task RunUnitsMapAsync(string[] args)
    {
        IoC.GetInstance = (_, _) => null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new XamlPlatformProvider();      // SubscribeOnUIThread · PublishOnUIThreadAsync 가 UI 스레드로 오게
        ShutdownMode = ShutdownMode.OnExplicitShutdown;            // 스냅숏은 라이트 창을 닫고 다크 창을 새로 띄운다

        var options = UnitsMapPreviewOptions.Parse(args);
        var output = options.SnapshotFolder ?? options.BenchFolder;
        try
        {
            if (options.SnapshotFolder is { } snapshots)
            {
                await UnitsMapPreviewRuns.SnapshotAsync(this, options, snapshots);
                Shutdown();
                return;
            }
            if (options.BenchFolder is { } bench)
            {
                await UnitsMapPreviewRuns.BenchAsync(this, options, bench);
                Shutdown();
                return;
            }

            if (options.Dark) ApplyDark();
            var preview = await UnitsMapPreview.CreateAsync(this, options);
            preview.Window.Closed += (_, _) => Shutdown();
        }
        catch (Exception ex)
        {
            if (output is null) MessageBox.Show(ex.ToString());
            else { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "units-map-error.txt"), ex.ToString()); Shutdown(); }
        }
    }

    /// <summary>관계도 미리보기가 한 실행 안에서 다크로 바꿀 때(스냅숏 · 테마 전환 벤치).</summary>
    internal void ApplyDarkForPreview() => ApplyDark();

    private void ApplyDark()
    {
        if (Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == DarkTokens)) return;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(DarkTokens) });
        foreach (var bundled in Resources.MergedDictionaries.OfType<BundledTheme>()) bundled.BaseTheme = BaseTheme.Dark;
        // 호스트(ThemeService.SyncMaterialDesignAndMahApps)는 MD 색만이 아니라 MahApps 크롬도 같이 바꾼다
        // (ThemeManager.Current.ChangeTheme(app, "Dark.Cyan")). 여기선 그 한 줄만 그대로 거울처럼 부른다 —
        // 안 부르면 이 콘솔이 쓰는 MahApps 스타일 컨트롤이 다크에서도 라이트 크롬으로 남는다.
        ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this, "Dark.Cyan");
    }

    private static Task Settle() => Task.Delay(450);

    /// <summary>
    /// 결선 펜스 뷰(F-4) — 한 시나리오를 띄우거나(<paramref name="shotDir"/> 없음), 테마 2 × 입체/평면 × 시나리오 3 × 크기 2 를 찍는다.
    /// 찍는 대상은 진짜 결선 창 뷰 전체(도구줄 · 보기 전환 · 오른쪽 칸 · 팔레트 · 아래 띠)다.
    /// </summary>
    private async Task RunFenceAsync(string scenario, string theme, string? shotDir)
    {
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new XamlPlatformProvider();
        Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanWordWrap.Install();      // 앱과 같게 — 한글은 띄어쓰기에서만 끊는다

        var preview = new WiringPreview();
        _window = new Window { Title = "결선 펜스 뷰 미리보기", Width = 1320, Height = 880, Background = (Brush)FindResource("SurfaceBrush") };

        if (shotDir is null)
        {
            if (theme == "dark") ApplyDark();
            var (view, vm) = preview.Scenario(scenario);
            _window.Content = new Border { Child = view };
            _window.Show();
            await Settle();
            vm.IsSoftwareRendering = false;
            return;
        }

        Directory.CreateDirectory(shotDir);
        PreviewTools.Shared.OffscreenStage.Hide(_window).Show();
        try
        {
            foreach (var t in new[] { "light", "dark" })
            {
                if (t == "dark") ApplyDark(); else ApplyLight();
                _window.Background = (Brush)FindResource("SurfaceBrush");
                foreach (var name in new[] { "ring", "pids", "line", "wall" })
                    foreach (var flat in new[] { false, true })
                        foreach (var (w, h) in new[] { (1280, 820), (1440, 900) })
                        {
                            var (view, vm) = preview.Scenario(name);
                            view.Width = w;
                            view.Height = h;
                            _window.Width = w + 40;
                            _window.Height = h + 60;
                            _window.Content = new Border { Child = view };
                            await Settle();
                            vm.IsSoftwareRendering = false;           // 화면 밖 창의 렌더 tier 와 무관하게 — 고른 보기를 찍는다
                            vm.IsFlatChosen = flat;
                            await Settle();
                            Save(shotDir, $"fence-{t}-{name}-{(flat ? "flat" : "tilt")}-{w}x{h}");
                        }

                // 표 보기(결선 단계의 [표 보기]) — 같은 시나리오 · 같은 두 크기.
                foreach (var name in new[] { "ring", "pids", "line", "wall" })
                    foreach (var (w, h) in new[] { (1280, 820), (1440, 900) })
                    {
                        var (view, vm) = preview.Scenario(name);
                        view.Width = w;
                        view.Height = h;
                        _window.Width = w + 40;
                        _window.Height = h + 60;
                        _window.Content = new Border { Child = view };
                        vm.ShowTableView();
                        await Settle();
                        Save(shotDir, $"table-{t}-{name}-{w}x{h}");
                    }
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(shotDir, "shot-error.txt"), ex.ToString());
        }
    }

    /// <summary>다크를 걷고 라이트로(한 실행에서 두 테마를 찍을 때).</summary>
    private void ApplyLight()
    {
        foreach (var dark in Resources.MergedDictionaries.Where(d => d.Source?.OriginalString == DarkTokens).ToList())
            Resources.MergedDictionaries.Remove(dark);
        foreach (var bundled in Resources.MergedDictionaries.OfType<BundledTheme>()) bundled.BaseTheme = BaseTheme.Light;
        ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this, "Light.Cyan");
    }

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

    /// <summary>
    /// D-05(2026-09-23) — <c>DrawingVisual</c> 에 <c>VisualBrush(content){Stretch=None}</c> 를 그려 간접 합성하던
    /// 옛 방식은, 창을 막 띄운 <b>첫 캡처</b>에서 자식의 최근 레이아웃 변경분을 브러시가 못 따라가는 경우가 있었다
    /// (실측: 서버 콘솔의 40px 헤더 Border 가 살아있는 트리에서 ActualHeight=40 · Visibility=Visible ·
    /// Background 도 올바르게 흰색으로 풀렸는데 — 즉 제품 바인딩·레이아웃은 처음부터 맞았는데 — 옛 저장 방식으로
    /// 뜬 PNG 에서만 그 40px 띠가 통째로 빠졌다. 같은 순간 같은 요소를 <c>RenderTargetBitmap.Render(element)</c> 로
    /// <b>직접</b> 찍으면 헤더가 제대로 나왔다 — 재현 트리거는 데이터 로딩이 아니라 "막 뜬 창의 첫 프레임"이었다).
    /// 그래서 지금은 배경 사각형을 그린 뒤 <c>content</c> 를 VisualBrush 없이 <b>직접 Render</b> 한다.
    /// <para>
    /// 바깥 여백을 두른 Border 가 아니라 콘솔 자체(<c>.Child</c>)를 찍는 이유도 그대로다 — 이 창은 전부
    /// <c>Margin(=12)</c> 을 쓴다(Padding 이 아니라서 Border 자신의 ActualWidth/Height 에는 안 잡힌다).
    /// Border 를 그대로 찍으면 그 Margin 이 Border 자신의 오프셋으로 실려 그림이 오른쪽·아래로 잘린다.
    /// </para>
    /// <para>
    /// ⚠ <c>.Child</c> 로 옮겨도 안심할 수 없다(실측, dialogs 갤러리) — <c>DialogPreview.Gallery()</c> 처럼
    /// 자식이 <c>HorizontalAlignment/VerticalAlignment=Center</c> 라 Border 를 꽉 채우지 않으면, 그 자식도
    /// 제 부모(Border) 안에서 가운데로 밀린 만큼 <b>제 오프셋</b>을 갖는다 — 곧이곧대로 Render 하면 이번엔
    /// <c>.Child</c> 자신이 잘린다(다이얼로그 갤러리 카드 하단이 통째로 잘려 나갔던 것을 실측으로 확인).
    /// 그래서 <see cref="SaveVisual"/> 로 그 오프셋만큼 캔버스를 더 크게 잡아 찍은 뒤 <c>.Child</c> 자신의
    /// 사각만 오려낸다 — Stretch 로 꽉 채우는 화면(오프셋 0)이든 Gallery 처럼 가운데 정렬(오프셋 ≠0)이든
    /// 같은 경로로 항상 옳게 나온다.
    /// </para>
    /// </summary>
    private void Save(string directory, string name)
    {
        var content = (FrameworkElement)((Border)_window.Content).Child;
        SaveVisual(Path.Combine(directory, name + ".png"), content, _window.Background);
        ProbeToolbar(directory, name, content);
        PreviewTools.Shared.ClipAudit.Frame(directory, name, content);
    }

    /// <summary>
    /// U-17 — 툴바 폭 판정(검색 접힘 · 오른쪽 묶음 [⋯])을 숫자로 남긴다. 그림만으로는 "왜 접혔나" 가 안 보인다.
    /// </summary>
    private static void ProbeToolbar(string directory, string name, DependencyObject root)
    {
        foreach (var toolbar in Descendants<ConsoleToolbar>(root))
        {
            // 두 줄 툴바 — 뿌리 StackPanel 의 첫 Border(동작 줄) 안 Grid.
            var grid = ((VisualTreeHelper.GetChild(toolbar, 0) as Panel)?.Children.OfType<Border>().FirstOrDefault())?.Child as Grid;
            var widths = grid?.Children.OfType<FrameworkElement>().Select(c => $"col{Grid.GetColumn(c)}={c.ActualWidth:0.#}") ?? Enumerable.Empty<string>();
            var extra = toolbar.Extra as FrameworkElement;
            File.AppendAllText(Path.Combine(directory, "toolbar-probe.txt"),
                $"{name}: toolbar={toolbar.ActualWidth:0.#}x{toolbar.ActualHeight:0.#} overflow={toolbar.IsExtraOverflow} columnsOverflow={toolbar.IsColumnsOverflow} searchCompact={toolbar.IsSearchCompact} search={toolbar.SearchBoxWidth:0.#} filterRow={toolbar.HasFilterRow} "
                + $"extraDesired={extra?.DesiredSize.Width:0.#} extraActual={extra?.ActualWidth:0.#} dc={extra?.DataContext?.GetType().Name ?? "null"} vparent={(extra is null ? "" : VisualTreeHelper.GetParent(extra)?.GetType().Name)} lparent={(extra is null ? "" : LogicalTreeHelper.GetParent(extra)?.GetType().Name)} {string.Join(" ", widths)}{Environment.NewLine}");
        }
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

    /// <summary>
    /// 어떤 요소든, 제 부모 안에서의 배치 위치(오프셋)에 상관없이 정확히 찍는다.
    /// <see cref="RenderTargetBitmap.Render(Visual)"/> 는 요소를 새 루트인 것처럼 그리지만, 그 요소가 부모로부터
    /// 받은 배치 오프셋(Margin·비-Stretch 정렬 등)은 요소 자신의 시각에 그대로 실려 있다 — 그래서 곧이곧대로
    /// Render 하면(요소의 ActualWidth/Height 로 잰 캔버스에) 그 오프셋만큼 내용이 밀려 오른쪽·아래가 잘린다.
    /// 요소의 부모 기준 오프셋만큼 캔버스를 <b>더 크게</b> 잡아 직접 Render 한 뒤, 요소 자신의 사각만 오려낸다.
    /// </summary>
    private static void SaveVisual(string path, FrameworkElement element, Brush background)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var parent = VisualTreeHelper.GetParent(element) as Visual;
        var offset = parent != null ? element.TransformToAncestor(parent).Transform(new Point(0, 0)) : new Point(0, 0);

        var dpi = VisualTreeHelper.GetDpi(element);
        var canvasWidth = offset.X + width;
        var canvasHeight = offset.Y + height;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(canvasWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(canvasHeight * dpi.DpiScaleY));

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        var backdrop = new DrawingVisual();
        using (var dc = backdrop.RenderOpen())
            dc.DrawRectangle(background, null, new Rect(0, 0, canvasWidth, canvasHeight));
        bitmap.Render(backdrop);
        bitmap.Render(element);

        var cropX = Math.Max(0, (int)Math.Round(offset.X * dpi.DpiScaleX));
        var cropY = Math.Max(0, (int)Math.Round(offset.Y * dpi.DpiScaleY));
        var cropW = Math.Max(1, Math.Min((int)Math.Ceiling(width * dpi.DpiScaleX), pixelWidth - cropX));
        var cropH = Math.Max(1, Math.Min((int)Math.Ceiling(height * dpi.DpiScaleY), pixelHeight - cropY));

        BitmapSource final = cropX == 0 && cropY == 0 && cropW == pixelWidth && cropH == pixelHeight
            ? bitmap
            : new CroppedBitmap(bitmap, new Int32Rect(cropX, cropY, cropW, cropH));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));
        using var stream = File.Create(path);
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

    private sealed class PreviewAssemblyLauncher : Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.IAssemblyLauncher
    {
        public bool IsAvailable => true;
        public Task<int?> ComposeAsync(EnumDeviceCategory category) => Task.FromResult<int?>(null);
        public Task<bool> EditDeviceAsync(IBaseDeviceModel device, EnumDeviceCategory category) => Task.FromResult(false);
        public Task<int?> RegisterFromPresetAsync(EnumDeviceCategory category) => Task.FromResult<int?>(null);
        public Task ManagePresetsAsync(EnumDeviceCategory category) => Task.CompletedTask;
    }

    private sealed class PreviewWiringLauncher : Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.IWiringLauncher
    {
        public bool IsAvailable => true;
        public Task<bool> OpenAsync(IBaseDeviceModel controller) => Task.FromResult(false);
    }
}