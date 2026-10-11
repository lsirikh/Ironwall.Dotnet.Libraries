using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>
/// 카탈로그 §1 지도 툴바 — 운영 단추(MAP-194) · 넘침(MAP-195) · 넘침 실행(MAP-196).
/// MapView.xaml 의 ToolBarTray 를 원문 그대로 떼어(바인딩 · 명령 · ToolTip · AutomationId 불변) 실제 MaterialDesign ToolBar 스타일
/// (MaterialDesignTheme.ToolBar.xaml)로 템플릿을 입히고 뷰모델에 묶는다. 창은 띄우지 않는다 — 넘침 팝업은 열지 않고(열면 HWND),
/// 팝업 몸(ToolBarOverflowPanel)을 직접 측정해 숨은 항목이 그 안으로 옮겨지는지 본다.
/// </summary>
public class MapToolbarScenarioTests
{
    private const string MdixToolBar = "/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.ToolBar.xaml";

    internal sealed record ToolbarRig(GMapCustomControl Map, MapViewModel Vm, Grid Host)
    {
        public ToolBar ToolBar => NavXaml.All<ToolBar>(Host).Single();
        public ToggleButton EditToggle => (ToggleButton)ToolBar.FindName("EditModeToggle")!;

        /// <summary>툴바 템플릿의 넘침 단추(UIA AutomationId = x:Name "OverflowButton").</summary>
        public ToggleButton OverflowButton
            => NavXaml.All<ToggleButton>(ToolBar).Single(t => t.TemplatedParent == ToolBar && UIElementAutomationPeer.CreatePeerForElement(t)?.GetAutomationId() == "OverflowButton");

        public Popup OverflowPopup => NavXaml.All<Popup>(ToolBar).Single(p => p.TemplatedParent == ToolBar);
        public ToolBarOverflowPanel OverflowPanel => NavXaml.All<ToolBarOverflowPanel>(OverflowPopup.Child).Single();

        public void Width(double w) => Scenario.Layout(Host, w, 48);

        /// <summary>팝업 몸을 직접 측정 — 팝업을 열면(HWND) 일어날 배치만 창 없이 돌린다.</summary>
        public void MeasureOverflowBody()
        {
            var body = (FrameworkElement)OverflowPopup.Child;
            body.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            body.Arrange(new Rect(body.DesiredSize));
            body.UpdateLayout();
        }

        public ToggleButton ToggleByTip(string tip)
            => NavXaml.All<ToggleButton>(ToolBar).Single(t => (t.ToolTip as string) == tip);
    }

    internal static ToolbarRig NewToolbarRig(double width = 1400, MapProvider? maps = null, System.Action<MapViewModel>? beforeLoad = null)
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: 16);
        var vm = NavKit.NewVm(map);
        Scenario.Call(vm, "InitializeRotationCommands");
        Scenario.Call(vm, "InitializeEditCommands");
        Scenario.Call(vm, "InitializeMeasureCommands");
        Scenario.Call(vm, "AttachMeasureEvents");
        var provider = maps;
        if (provider == null)
        {
            provider = new MapProvider();
            provider.Add(new DefinedMapModel { Id = 1, Name = "일반지도", Category = EnumMapCategory.Standard, Vendor = EnumMapVendor.MBTiles, ServiceUrl = "normal.mbtiles", MinZoomLevel = 5, MaxZoomLevel = 19 });
            provider.Add(new DefinedMapModel { Id = 2, Name = "위성지도", Category = EnumMapCategory.Satellite, Vendor = EnumMapVendor.MBTiles, ServiceUrl = "satellite.mbtiles", MinZoomLevel = 5, MaxZoomLevel = 19 });
        }
        Scenario.SetField(vm, "_mapProvider", provider);
        beforeLoad?.Invoke(vm);
        var tray = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "ToolBarTray");
        var host = NavRemainingKit.Load(tray, vm, width, 48, toolBarStyleSource: MdixToolBar);
        return new ToolbarRig(map, vm, host);
    }

    private static AutomationPeer Peer(UIElement e) => UIElementAutomationPeer.CreatePeerForElement(e);
    private static bool SupportsToggle(AutomationPeer p) => p.GetPattern(PatternInterface.Toggle) != null;
    private static ToggleState StateOf(ToggleButton t) => ((IToggleProvider)Peer(t).GetPattern(PatternInterface.Toggle)).ToggleState;

    // ───────────────────────── MAP-194 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-194")]
    public void should_expose_map_combo_four_operational_buttons_and_mode_toggles_in_order_when_toolbar_rendered() => Scenario.Sta(() =>
    {
        var rig = NewToolbarRig();
        Assert.False(rig.ToolBar.HasOverflowItems);               // 넓은 창 — 전부 보임
        var all = Peer(rig.ToolBar).GetChildren() ?? new List<AutomationPeer>();   // UIA 의 ToolBar 직속 자식(FindAllChildren 과 같은 평탄화)
        Assert.Contains(all, p => p.GetAutomationId() == "OverflowButton" || ((UIElementAutomationPeer)p).Owner is FrameworkElement { TemplatedParent: ToolBar });
        // 템플릿 부품(손잡이 · 넘침 단추)을 뺀 XAML 선언 항목
        var children = all.Where(p => !(((UIElementAutomationPeer)p).Owner is FrameworkElement { TemplatedParent: ToolBar })).ToList();

        // ① 지도 콤보 — 맨 앞, HelpText "지도 선택", 항목 = 사용 가능한 지도
        var first = children.First();
        Assert.Equal(AutomationControlType.ComboBox, first.GetAutomationControlType());
        Assert.Equal("지도 선택", first.GetHelpText());
        var combo = NavXaml.All<ComboBox>(rig.ToolBar).Single();
        Assert.Equal(new[] { "일반지도", "위성지도" }, combo.Items.Cast<IMapModel>().Select(m => m.Name));

        // ② 비토글 단추 4 — 홈 · 관심지역 · 레이어 · 재생(HelpText 순서)
        var buttons = children.Where(p => p.GetAutomationControlType() == AutomationControlType.Button && p.GetAutomationId() != "OverflowButton" && !SupportsToggle(p)).ToList();
        Assert.Equal(new[] { "홈 위치 이동", "관심지역 관리", "레이어 관리", "추적 재생(Playback)" }, buttons.Select(b => b.GetHelpText()));
        Assert.Equal("GMaps.Toolbar.LayerPanelButton", buttons[2].GetAutomationId());

        // ③ 모드 토글 — 길이 · 넓이 · 회전 · (기울이기 한 쌍) · 편집 스위치. 카탈로그의 '토글 4'(길이 · 넓이 · 회전 · 편집)는 기울이기(map-tilt-25d) 이전 표기
        var toggles = children.Where(p => SupportsToggle(p) && p.GetAutomationId() != "OverflowButton" && !(p.GetAutomationId() ?? "").StartsWith("Help.", StringComparison.Ordinal)).ToList();
        var toggleIds = toggles.Select(p => string.IsNullOrEmpty(p.GetAutomationId()) ? p.GetHelpText() : p.GetAutomationId()).ToList();
        var four = new[] { "길이 측정", "넓이 측정", "지도 회전 모드 (Ctrl+Shift+R)", "EditModeToggle" };
        Assert.Equal(four, toggleIds.Where(four.Contains).ToArray());                              // 넷이 이 순서로 있다
        Assert.Equal(new[] { "GMaps.Tilt.Toggle", "GMaps.Tilt.AngleButton" }, toggleIds.Where(id => !four.Contains(id)).ToArray());   // 그 밖은 기울이기 한 쌍뿐
        Assert.All(toggles, t => Assert.Equal(ToggleState.Off, ((IToggleProvider)t.GetPattern(PatternInterface.Toggle)).ToggleState));
        Assert.Equal("편집 모드", Peer(rig.EditToggle).GetHelpText());
        Assert.Equal(Visibility.Visible, ((FrameworkElement)rig.EditToggle.Parent).Visibility);   // 편집 권한(폴백 허용) — 스위치 보임
    });

    // ───────────────────────── MAP-195 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-195")]
    public void should_move_hidden_items_into_overflow_popup_body_and_show_overflow_button_when_toolbar_narrowed() => Scenario.Sta(() =>
    {
        var rig = NewToolbarRig();
        Assert.False(rig.ToolBar.HasOverflowItems);
        Assert.NotEqual(Visibility.Visible, rig.OverflowButton.Visibility);   // 넘칠 것이 없으면 단추 접힘(peer 없음 — 헤디드 Skip 조건)

        rig.Width(330);                                           // 창 좁히기
        Assert.True(rig.ToolBar.HasOverflowItems, "좁혔는데 넘친 항목이 없다");
        Assert.Equal(Visibility.Visible, rig.OverflowButton.Visibility);
        Assert.True(rig.OverflowButton.IsEnabled);
        Assert.Equal("OverflowButton", Peer(rig.OverflowButton).GetAutomationId());
        Assert.Equal(ToggleState.Off, StateOf(rig.OverflowButton));
        Assert.False(rig.ToolBar.IsOverflowOpen);                 // 팝업은 닫힌 채(창 금지)

        var items = rig.ToolBar.Items.Cast<object>().OfType<UIElement>().ToList();
        var hidden = items.Where(ToolBar.GetIsOverflowItem).ToList();
        var shown = items.Except(hidden).ToList();
        Assert.NotEmpty(hidden);
        Assert.NotEmpty(shown);
        Assert.IsType<ComboBox>(shown.First());                   // 앞에서부터 남고 뒤에서부터 넘친다
        Assert.Contains(hidden, h => NavXaml.All<ToggleButton>(h).Any(t => t.Name == "EditModeToggle"));   // 편집 스위치는 넘침

        rig.MeasureOverflowBody();                                // 팝업이 열릴 때의 배치(창 없이)
        var panel = rig.OverflowPanel;
        Assert.Equal(hidden.Count, panel.Children.Count);
        Assert.All(hidden, h => Assert.Same(panel, VisualTreeHelper.GetParent(h)));   // 숨은 항목 = 넘침 팝업 몸 안
        Assert.All(shown, s => Assert.NotSame(panel, VisualTreeHelper.GetParent(s)));
        // 넘침 팝업 몸에서 UIA 로 닿는다(헤디드 FindOverflowSurface 와 같은 판정 — 팝업 HWND 의 내용)
        var bodyIds = NavXaml.All<FrameworkElement>(rig.OverflowPopup.Child).Select(e => AutomationProperties.GetAutomationId(e)).Where(id => !string.IsNullOrEmpty(id)).ToList();
        Assert.Contains("GMaps.EditSwitch.Host", bodyIds);

        rig.Width(1400);                                          // 다시 넓히면 돌아온다
        Assert.False(rig.ToolBar.HasOverflowItems);
        Assert.All(items, i => Assert.False(ToolBar.GetIsOverflowItem(i)));
    });

    // ───────────────────────── MAP-196 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-196")]
    public void should_run_measure_and_edit_from_overflow_items_when_toolbar_narrowed() => Scenario.Sta(() =>
    {
        try
        {
            var rig = NewToolbarRig();
            rig.Width(230);                                       // 측정 · 회전 · 편집 묶음까지 넘치는 폭
            rig.MeasureOverflowBody();
            var length = rig.ToggleByTip("길이 측정");
            var edit = rig.EditToggle;
            var panel = rig.OverflowPanel;
            bool InOverflow(DependencyObject d) { for (var p = d; p != null; p = VisualTreeHelper.GetParent(p)) if (ReferenceEquals(p, panel)) return true; return false; }
            Assert.True(InOverflow(length), "길이 측정이 넘침 팝업에 있지 않다(전제)");
            Assert.True(InOverflow(edit), "편집 스위치가 넘침 팝업에 있지 않다(전제)");

            // 측정 — 넘침 팝업의 「길이 측정」 클릭
            NavXaml.Click(length);
            NavKit.Pump();
            Assert.True(rig.Map.IsMeasuring);
            Assert.True(rig.Vm.IsMeasureLengthActive);
            Assert.Equal(ToggleState.On, StateOf(length));
            NavXaml.Click(length);                                // 다시 누르면 끔
            NavKit.Pump();
            Assert.False(rig.Map.IsMeasuring);
            Assert.Equal(ToggleState.Off, StateOf(length));

            // 편집 — 넘침 팝업의 편집 스위치 클릭 → 라벨 「편집 중」
            var label = NavXaml.All<TextBlock>((DependencyObject)edit.Parent).Single(t => t.Text is "편집" or "편집 중");
            Assert.Equal("편집", label.Text);
            NavXaml.Click(edit);
            NavKit.Pump();
            Assert.True(rig.Vm.IsEditModeEnabled);
            Assert.True(rig.Map.IsEditMode);
            Assert.Equal(ToggleState.On, StateOf(edit));
            Assert.Equal("편집 중", label.Text);
        }
        finally { Mouse.OverrideCursor = null; }
    });

    // ───────────────────────── MAP-040 ─────────────────────────

    /// <summary>시험용 .mbtiles(SQLite) — 메타데이터 이름으로 어느 파일이 열렸는지(타일 원천) 구분한다. 타일 행은 비워 둔다(기본 타일 폴백).</summary>
    private static void EnsureMbtiles(string path, string name)
    {
        if (File.Exists(path)) return;
        using var db = new System.Data.SQLite.SQLiteConnection($"Data Source=\"{path}\";Pooling=False");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE metadata (name TEXT, value TEXT);" +
            "CREATE TABLE tiles (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_data BLOB);" +
            $"INSERT INTO metadata VALUES ('name','{name}'),('format','png'),('minzoom','5'),('maxzoom','19'),('bounds','126.5,37.0,127.5,38.0');";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 바탕 지도 전환 — 툴바 「지도 선택」 콤보 → 다른 지도 → 지도(M) › 지도 전환 라디오로 원래 지도.
    /// 실제 전환 경로(SelectedMapItem → ChangeMapAsync → MapConfigureAsync → SwitchMBTilesMap → ConfigureCommonMapSettings → 설정 저장)를
    /// 시험 폴더의 .mbtiles 두 개(지도 DB 는 Moq 가짜, 네트워크 없음)로 돈다. '타일 바뀜' = 지도 공급자가 연 타일 파일(메타데이터 이름),
    /// '메뉴 라디오 일치' = MapView.xaml 의 지도 전환 항목 스타일(IsChecked = AreEqual(항목, SelectedMapItem)) 그대로 만든 항목의 체크.
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-040")]
    public void should_switch_base_tiles_and_keep_combo_and_menu_radio_in_sync_when_map_changed_and_restored()
        => Scenario.StaAsync(() => RunMapSwitch(checkPersistence: false));

    /// <summary>
    /// MAP-040 의 전환을 그대로 하면서, 전환이 끝날 때 저장(SaveCurrentMapSettingsAsync → MapSettingsHelper.SaveMapSettingsAsync)한
    /// 지도 이름이 설정 파일(AppSettings.MapName)에 실제로 남는지 본다 — 남지 않으면 재시작 때 이전 지도로 돌아간다.
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-040")]
    [Trait("Defect", "NAV-D1")]
    public void should_persist_selected_map_name_to_settings_file_when_map_switched()
        => Scenario.StaAsync(() => RunMapSwitch(checkPersistence: true));

    /// <summary>NAV-D1 원인 좁히기 — 지도 전환이 부르는 저장 함수만 직접. AppSettings 절이 없을 때는 남고, 이미 있으면(운영 파일은 항상 있다) 사라지는가.</summary>
    [Fact]
    [Trait("Scenario", "MAP-040")]
    [Trait("Defect", "NAV-D1")]
    public void should_write_map_name_into_existing_app_settings_section_when_map_settings_saved() => Scenario.StaAsync(async () =>
    {
        var setup = new Ironwall.Dotnet.Libraries.GMaps.Models.GMapSetupModel { MapName = "위성지도", MapType = "MBTiles" };
        try
        {
            File.WriteAllText(NavKit.SettingsPath, "{}");                                // ① 절 없음
            await Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.MapSettingsHelper.SaveMapSettingsAsync(setup);
            Assert.Equal("위성지도", (string?)NavKit.Setting("MapName"));               // 새 절을 만들 때는 남는다

            File.WriteAllText(NavKit.SettingsPath, "{\"AppSettings\":{\"Keep\":1}}");    // ② 절 있음(운영 appsettings.json 형태)
            await Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.MapSettingsHelper.SaveMapSettingsAsync(setup);
            var file = File.ReadAllText(NavKit.SettingsPath);
            Assert.Equal(1, (int?)NavKit.Setting("Keep"));                              // 기존 키는 그대로
            Assert.True("위성지도" == (string?)NavKit.Setting("MapName"),
                $"[NAV-D1] AppSettings 절이 이미 있으면 SaveMapSettingsAsync 가 MapName 을 쓰지 않는다 — 파일={file.Replace(Environment.NewLine, " ")}");
        }
        finally { NavKit.ResetSettings(); }
    });

    private static async Task RunMapSwitch(bool checkPersistence)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "mbtiles-fixture");
        Directory.CreateDirectory(dir);
        EnsureMbtiles(Path.Combine(dir, "w1-normal.mbtiles"), "W1 일반 타일");
        EnsureMbtiles(Path.Combine(dir, "w1-satellite.mbtiles"), "W1 위성 타일");

        var log = new NavRemainingKit.RecLog();
        var normal = new DefinedMapModel { Id = 1, Name = "일반지도", Category = EnumMapCategory.Standard, Vendor = EnumMapVendor.MBTiles, ServiceUrl = "w1-normal.mbtiles", MinZoomLevel = 5, MaxZoomLevel = 19 };
        var satellite = new DefinedMapModel { Id = 2, Name = "위성지도", Category = EnumMapCategory.Satellite, Vendor = EnumMapVendor.MBTiles, ServiceUrl = "w1-satellite.mbtiles", MinZoomLevel = 5, MaxZoomLevel = 19 };
        var provider = new MapProvider(); provider.Add(normal); provider.Add(satellite);
        var rig = NewToolbarRig(maps: provider, beforeLoad: vm => Scenario.SetField(vm, "_log", log));
        Scenario.SetField(rig.Vm, "_mapSwitchLock", new SemaphoreSlim(1, 1));   // 필드 초기화식(생성자 없이 만든 VM 이라 비어 있다)
        Scenario.SetField(rig.Vm, "_definedMapProvider", new DefinedMapProvider(log, provider));
        var db = new Moq.Mock<Ironwall.Dotnet.Libraries.GMaps.Db.Services.IGMapDbService>();   // 지도 DB 가짜 — 두 파일 모두 이미 등록됨(시드 무동작)
        db.Setup(d => d.FetchDefinedMapsAsync(Moq.It.IsAny<CancellationToken>())).Returns(Task.FromResult<List<IDefinedMapModel>?>(new List<IDefinedMapModel> { normal, satellite }));
        Scenario.SetField(rig.Vm, "_gMapDbService", db.Object);
        var eqm = new Moq.Mock<Ironwall.Dotnet.Libraries.Events.Ui.Managers.IEventQueueManager>();
        eqm.Setup(e => e.GetActiveCounts()).Returns((0, 0));
        Scenario.SetField(rig.Vm, "_eventQueueManager", eqm.Object);
        var setup = Scenario.GetField<Ironwall.Dotnet.Libraries.GMaps.Models.GMapSetupModel>(rig.Vm, "_setupModel");
        setup.MapDataDirectory = dir;
        setup.MapName = "일반지도";

        // 지도 전환 라디오 — MapView.xaml 「지도 전환」 항목의 ItemContainerStyle 원문으로 만든 메뉴(최상위라 팝업 창 없이 항목이 생긴다)
        var menuXaml = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "MenuItem" && (string?)e.Attribute("Header") == "지도 전환");
        var menuHost = NavRemainingKit.Load(new XElement(NavXaml.P + "Menu", new XAttribute("IsMainMenu", "False"), menuXaml), rig.Vm, 400, 30,
            extraResources: "<Style x:Key='MaterialDesignMenuItem' TargetType='MenuItem'/><symbolUtils:AreEqualMultiConverter x:Key='AreEqualMultiConverter'/>");
        var switchItem = NavXaml.All<MenuItem>(menuHost).Single(m => (m.Header as string) == "지도 전환");
        var presenter = new FrameworkElementFactory(typeof(Border));
        presenter.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));   // 창 없는 Menu 는 테마 템플릿을 못 받는다 — 항목 호스트만 단다
        var radioMenu = new Menu { DataContext = rig.Vm, ItemsSource = rig.Vm.AvailableMaps, ItemContainerStyle = switchItem.ItemContainerStyle, Template = new ControlTemplate(typeof(Menu)) { VisualTree = presenter } };
        Scenario.Layout(radioMenu, 400, 30);
        MenuItem Radio(IMapModel m) => (MenuItem)radioMenu.ItemContainerGenerator.ContainerFromItem(m);
        var combo = NavXaml.All<ComboBox>(rig.ToolBar).Single();
        var tiles = GMap.NET.MapProviders.MBTilesMapProvider.Instance;

        async Task WaitMap(IMapModel target)
        {
            await NavKit.WaitUntil(() => ReferenceEquals(rig.Vm.SelectedMap, target) && tiles.DataName == (target == normal ? "W1 일반 타일" : "W1 위성 타일") && !Scenario.GetField<SemaphoreSlim>(rig.Vm, "_mapSwitchLock").CurrentCount.Equals(0), 8000);
            Assert.True(ReferenceEquals(rig.Vm.SelectedMap, target), $"'{target.Name}' 로 전환되지 않았다 — 공급자 '{tiles.DataName}' · 로그: " + string.Join(" | ", log.Lines.TakeLast(25)));
            Scenario.Layout(radioMenu, 400, 30);
            NavKit.Pump();
        }
        void AssertShowing(IMapModel target, string tileName)
        {
            Assert.True(log.Errors.Count == 0, "전환 중 오류 로그: " + string.Join(" / ", log.Errors));
            Assert.Same(target, rig.Vm.SelectedMap);
            Assert.Same(tiles, rig.Map.MapProvider);                // 타일 = MBTiles 공급자
            Assert.Equal(tileName, tiles.DataName);                 // 타일 원천 파일이 바뀌었다
            Assert.Same(target, combo.SelectedItem);                // 콤보 값
            Assert.True(Radio(target).IsChecked, $"메뉴 라디오에 '{target.Name}' 체크가 없다");
            Assert.False(Radio(target == normal ? satellite : normal).IsChecked);   // 다른 항목은 체크 해제
            Assert.Equal(target.Name, setup.MapName);
        }

        // 시작: 일반지도
        rig.Vm.SelectedMapItem = normal;
        await WaitMap(normal);
        Assert.Equal(new[] { "일반지도", "위성지도" }, radioMenu.Items.Cast<IMapModel>().Select(m => m.Name));
        Assert.Equal(new object[] { "일반지도", "위성지도" }, new object[] { Radio(normal).Header, Radio(satellite).Header });
        AssertShowing(normal, "W1 일반 타일");
        var center = rig.Map.Position;

        // 툴바 「지도 선택」 콤보 → 위성지도
        combo.SelectedItem = satellite;
        await WaitMap(satellite);
        AssertShowing(satellite, "W1 위성 타일");
        Assert.Equal(center.Lat, rig.Map.Position.Lat, 9);         // 전환해도 보던 자리 유지
        if (checkPersistence)
        {
            Assert.Contains("현재 지도 설정 저장 완료", log.Lines.Select(l => l[2..]));   // 제품은 저장을 마쳤다고 기록한다
            var saved = await NavKit.WaitSetting("MapName", t => (string?)t == "위성지도");
            Assert.True("위성지도" == (string?)saved,
                $"[NAV-D1] 지도 전환 후 설정 파일에 MapName 이 남지 않는다(재시작 시 이전 지도로 복귀) — '저장 완료' 로그는 있으나 파일={File.ReadAllText(NavKit.SettingsPath).Replace(Environment.NewLine, " ")}. " +
                "원인 후보: MapSettingsHelper.SaveMultipleSettingsAsync 가 이미 있는 AppSettings 객체를 같은 키에 다시 대입 → Json.NET 이 복제본을 넣고 원본(분리됨)에만 값을 쓴다");
        }

        // 지도(M) › 지도 전환 › 일반지도(라디오) → 원래
        var radioNormal = Radio(normal);
        Assert.True(radioNormal.Command?.CanExecute(radioNormal.CommandParameter), "라디오 항목 명령이 없다(SelectMapCommand 결선)");
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(radioNormal).GetPattern(PatternInterface.Invoke)).Invoke();   // UIA Invoke = 메뉴 항목 클릭
        await WaitMap(normal);
        AssertShowing(normal, "W1 일반 타일");
        if (checkPersistence)
        {
            var saved = await NavKit.WaitSetting("MapName", t => (string?)t == "일반지도");
            Assert.Equal("일반지도", (string?)saved);
        }
    }
}
