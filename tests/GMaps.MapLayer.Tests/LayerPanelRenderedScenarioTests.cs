using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Help;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 §1 레이어 패널 — 열기/닫기(MAP-051) · 빈 섹션(MAP-053) · 펼치기/접기(MAP-054) · 잎 우클릭 메뉴(MAP-061) · "?" Esc(MAP-079) 헤드리스 짝.
/// VM.ShowLayerPanel() 이 만든 실제 LayerPanelControl 에 제품 LayerPanelStyle.xaml 의 템플릿을 입혀 창 없이 부모 없는 Measure/Arrange 로 그린다.
/// "보인다" 는 조상 전부가 Visibility=Visible 이고 생성돼 있다는 뜻으로 본다(UIA DataItem 수의 헤드리스 대응). 실제 픽셀 · 마우스는 실창 몫.
/// </summary>
public class LayerPanelRenderedScenarioTests
{
    private const string NoItemsText = "등록된 항목이 없습니다";

    private static ResourceDictionary StyleDictionary()
        => (ResourceDictionary)Application.LoadComponent(new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/LayerPanelStyle.xaml", UriKind.Relative));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    /// <summary>패널까지 올라가는 조상이 전부 Visibility=Visible 인가(창 없는 IsVisible 대용).</summary>
    private static bool Shown(DependencyObject element, DependencyObject root)
    {
        for (var node = element; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement ui && ui.Visibility != Visibility.Visible) return false;
            if (ReferenceEquals(node, root)) return true;
        }
        return false;
    }

    /// <summary>오버레이 이미지 2장 · PIDS 그룹 심볼 2개(구역-1 · 구역-2) · 카메라 심볼 1개, 오버레이 지도 0행으로 패널을 연다.</summary>
    private static (MapVm f, LayerPanelControl panel) OpenRendered(bool withHelp = false)
    {
        if (withHelp) RuntimeHelpers.RunModuleConstructor(typeof(MapsHelp).Module.ModuleHandle);   // "?" 설명 등록(지도 어셈블리 모듈 초기화)
        var f = new MapVm();
        var (a, la) = MapVm.Image(1, 11, "항공 A", @"C:\maps\overlay-a.png", zOrder: 0);
        var (b, lb) = MapVm.Image(2, 12, "항공 B", @"C:\maps\overlay-b.png", zOrder: 1);
        var camera = MapVm.Pids(5, 50, 37.391, 126.961, "카메라-5");
        f.Map.Markers.Add(a); f.Map.Markers.Add(b); f.Map.Markers.Add(camera);
        f.Layers.AddRange(new IMapLayerModel[] { la, lb });
        var provider = new Ironwall.Dotnet.Libraries.GMaps.Providers.SymbolProvider();
        ((PidsSymbolModel)camera.Model).DeviceType = Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.IpCamera;
        provider.Add(camera.Model);
        provider.Add(new PidsGroupSymbolModel { Id = 31, Title = "구역-1", Latitude = 37.392, Longitude = 126.962, Visible = true });
        provider.Add(new PidsGroupSymbolModel { Id = 32, Title = "구역-2", Latitude = 37.393, Longitude = 126.963, Visible = true });
        Scenario.SetField(f.Vm, "_symbolProvider", provider);
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();
        f.Vm.ShowLayerPanel();
        var panel = f.Vm.LayerPanel!;
        panel.Style = (Style)StyleDictionary()[typeof(LayerPanelControl)];
        Scenario.Layout(panel, 320, 700);
        return (f, panel);
    }

    private static LayerTreeNode Node(MapVm f, Func<LayerTreeNode, bool> match)
    {
        IEnumerable<LayerTreeNode> All(IEnumerable<LayerTreeNode> nodes) => nodes.SelectMany(n => new[] { n }.Concat(All(n.Children)));
        return All(f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes")).Single(match);
    }

    // ── MAP-051 — 레이어 단추로 열림 · 머리 ✕ 로 닫힘(단추는 열기 전용) ──

    [Trait("Scenario", "MAP-051")]
    [Fact]
    public void should_open_with_toolbar_command_stay_open_on_repeat_and_close_with_header_x_when_layer_panel_is_toggled() => Scenario.Sta(() =>
    {
        // Arrange — 툴바 단추(GMaps.Toolbar.LayerPanelButton)가 묶인 명령(MapView.xaml)을 제품 초기화로 만든다
        var f = new MapVm();
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();  // 지도 DB(가짜)에 레이어 0 — 섹션 셋만 선다
        Scenario.Call(f.Vm, "InitializeNavigationCommands");
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
        Assert.Matches(new Regex("AutomationId=\"GMaps\\.Toolbar\\.LayerPanelButton\"\\s+Command=\"\\{Binding ShowLayerPanelCommand\\}\""), xaml);

        // Act ① — 레이어 단추
        f.Vm.ShowLayerPanelCommand!.Execute(null);
        var panel = f.Vm.LayerPanel;

        // Assert ① — 열림 · 목록 id(GMaps.LayerPanel.List.*)
        Assert.NotNull(panel);
        Assert.True(f.Vm.IsLayerPanelVisible);
        panel!.Style = (Style)StyleDictionary()[typeof(LayerPanelControl)];
        Scenario.Layout(panel, 320, 700);
        var lists = Descendants<ListBox>(panel).Select(AutomationProperties.GetAutomationId).ToList();
        Assert.Equal(new[] { "GMaps.LayerPanel.List.OverlayMap", "GMaps.LayerPanel.List.OverlayImage" }, lists);

        // Act ② — 단추를 또 눌러도 닫히지 않는다(열기 전용)
        f.Vm.ShowLayerPanelCommand.Execute(null);
        Assert.True(f.Vm.IsLayerPanelVisible);
        Assert.Same(panel, f.Vm.LayerPanel);

        // Act ③ — 머리 ✕(PART_CloseButton)
        var close = (Button)panel.Template.FindName("PART_CloseButton", panel);
        ((IInvokeProvider)new ButtonAutomationPeer(close)).Invoke();
        Scenario.Pump();

        // Assert ③ — 닫힘(패널 내림) · 다시 열면 새 패널
        Assert.False(f.Vm.IsLayerPanelVisible);
        Assert.Null(f.Vm.LayerPanel);
        f.Vm.ShowLayerPanelCommand.Execute(null);
        Assert.True(f.Vm.IsLayerPanelVisible);
        Assert.NotSame(panel, f.Vm.LayerPanel);
    });

    // ── MAP-053 — 오버레이 지도 행 0 → 「등록된 항목이 없습니다」 ──

    [Trait("Scenario", "MAP-053")]
    [Fact]
    public void should_show_no_items_text_only_under_the_empty_overlay_map_section_and_hide_it_once_a_row_exists() => Scenario.Sta(() =>
    {
        var (f, panel) = OpenRendered();
        var mapSection = Node(f, n => n.OverlayLayerType == "OverlayMap");
        var imageSection = Node(f, n => n.OverlayLayerType == "OverlayImage");
        TextBlock EmptyText(LayerTreeNode section) => Descendants<TextBlock>(panel).Single(t => t.Text == NoItemsText && ReferenceEquals(t.DataContext, section));

        Assert.Empty(mapSection.Children);                            // 전제: 오버레이 지도 행 0
        Assert.True(Shown(EmptyText(mapSection), panel), "빈 오버레이 지도 섹션에 「등록된 항목이 없습니다」가 보이지 않는다");
        Assert.False(Shown(EmptyText(imageSection), panel), "행이 있는 오버레이 이미지 섹션에 빈 글이 보인다");
        Assert.Single(Descendants<TextBlock>(panel), t => t.Text == NoItemsText && Shown(t, panel));   // 보이는 빈 글은 하나뿐

        // 행이 생기면 빈 글이 사라진다(개수 바인딩이 살아 있다)
        mapSection.AddChild(LayerTreeNode.FromModel(new MapLayerModel { Id = 70, Name = "안양 타일", LayerType = "OverlayMap", IsVisible = true, Opacity = 1 }, "안양 타일", "Map"));
        Scenario.Layout(panel, 320, 700);
        Assert.False(Shown(EmptyText(mapSection), panel));
    });

    // ── MAP-054 — 「PIDS 그룹」 펼침 → 접음 ──

    [Trait("Scenario", "MAP-054")]
    [Fact]
    public void should_show_zone_leaves_when_pids_group_is_expanded_and_hide_them_when_collapsed() => Scenario.Sta(() =>
    {
        var (f, panel) = OpenRendered();
        var group = Node(f, n => n.NodeType == LayerNodeType.Category && n.Name == "PIDS 그룹");
        int ZoneLeaves() => Descendants<TextBlock>(panel).Count(t => t.Text.StartsWith("구역-") && Shown(t, panel));
        var chevron = Descendants<ToggleButton>(panel).Single(t => t is not HelpTip && t is not CheckBox && ReferenceEquals(t.DataContext, group));
        var toggle = (IToggleProvider)new ToggleButtonAutomationPeer(chevron);

        Assert.False(group.IsExpanded);                                // 기본 접힘
        Assert.Equal(0, ZoneLeaves());

        toggle.Toggle();                                               // 셰브런 → 펼침
        Scenario.Layout(panel, 320, 700);
        Assert.True(group.IsExpanded);
        Assert.Equal(2, ZoneLeaves());                                 // 「구역-1」 · 「구역-2」 보임
        Assert.Equal(2, group.ItemCount);                              // 머리 배지 수와 같다

        toggle.Toggle();                                               // 다시 → 접음
        Scenario.Layout(panel, 320, 700);
        Assert.False(group.IsExpanded);
        Assert.Equal(0, ZoneLeaves());                                 // 숨음
    });

    // ── MAP-061 — 잎 우클릭 메뉴 ──

    [Trait("Scenario", "MAP-061")]
    [Fact]
    public void should_offer_center_rename_lock_on_symbol_leaf_and_add_delete_up_down_on_overlay_row_when_leaf_is_right_clicked() => Scenario.Sta(() =>
    {
        var (f, panel) = OpenRendered();
        var cameras = Node(f, n => n.NodeType == LayerNodeType.Category && n.Name == "카메라");
        cameras.IsExpanded = true;                                     // 잎은 카테고리를 펼쳐야 그려진다
        Scenario.Layout(panel, 320, 700);
        var symbolLeaf = Node(f, n => n.Symbol?.Id == 5);
        var firstOverlay = Node(f, n => n.Model?.Id == 11);

        (string Header, bool Enabled, ICommand? Command)[] Menu(LayerTreeNode node)
        {
            var host = Descendants<FrameworkElement>(panel).Single(e => e.ContextMenu != null && ReferenceEquals(e.DataContext, node));
            var menu = host.ContextMenu!;
            menu.DataContext ??= host.DataContext;                    // 열릴 때 자리 대상(잎 행)의 DataContext 를 받는 것과 같게 — 팝업(HWND)은 띄우지 않는다
            Scenario.Layout(menu, 300, 400);                           // 열릴 때처럼 메뉴를 한 번 재서 항목 바인딩을 붙인다(창 없이)
            Scenario.Pump();
            return menu.Items.OfType<MenuItem>().Select(m => ((string)m.Header, m.IsEnabled, m.Command)).ToArray();
        }

        // 심볼 잎 — 지도 가운데로 이동 · 이름 바꾸기 · 잠금
        var symbolMenu = Menu(symbolLeaf);
        Assert.Equal(new[] { "지도 가운데로 이동", "이름 바꾸기", "잠금" }, symbolMenu.Select(m => m.Header));
        Assert.All(symbolMenu, m => Assert.True(m.Enabled, $"'{m.Header}' 가 꺼져 있다"));
        Assert.Same(symbolLeaf.NavigateCommand, symbolMenu[0].Command);
        Assert.Same(symbolLeaf.RenameCommand, symbolMenu[1].Command);
        Assert.Same(symbolLeaf.ToggleLockCommand, symbolMenu[2].Command);

        // 오버레이 행 — 삭제 · 위로 · 아래로가 더해진다(맨 위 행이라 '위로' 만 꺼짐)
        var overlayMenu = Menu(firstOverlay);
        Assert.Equal(new[] { "삭제", "이름 바꾸기", "위로", "아래로", "지도 가운데로 이동", "잠금" }, overlayMenu.Select(m => m.Header));
        Assert.Equal(new[] { true, true, false, true, true, true }, overlayMenu.Select(m => m.Enabled));
        Assert.Same(firstOverlay.DeleteCommand, overlayMenu[0].Command);
        Assert.Same(firstOverlay.MoveDownCommand, overlayMenu[3].Command);

        // 잠그면 메뉴 머리가 「잠금 해제」로 바뀐다(바인딩된 머리)
        symbolLeaf.ToggleLockCommand.Execute(null);
        Assert.Equal("잠금 해제", Menu(symbolLeaf)[2].Header);
    });

    // ── MAP-079 — 레이어 "?" → Esc: 말풍선 닫힘 · 패널 유지 ──

    [Trait("Scenario", "MAP-079")]
    [Fact]
    public void should_close_only_the_callout_and_keep_layer_panel_open_when_escape_is_pressed_on_layer_help() => Scenario.Sta(() =>
    {
        var (f, panel) = OpenRendered(withHelp: true);
        var tip = Descendants<HelpTip>(panel).Single(t => t.HelpKey == "Map.LayerPanel.Layers");
        Assert.Equal("Help.Map.LayerPanel.Layers", AutomationProperties.GetAutomationId(tip));
        Assert.True(tip.IsAvailable && tip.IsEnabled, "레이어 \"?\" 가 꺼져 있다(설명 목록에 없음)");
        Assert.Same(panel, tip.Scope);                                 // 범위 = 패널(F1 · 키보드 폴백 묶음 대상)
        Scenario.SetField(tip, "_popup", null);                        // 말풍선 팝업 창(HWND)은 띄우지 않는다 — 몸(HelpCallout) · 판정만 본다
        var source = new HeadlessSource { RootVisual = panel };
        try
        {
            // 연다 — 몸 id 「Help.Map.LayerPanel.Layers.Body」 · 패널 설명 + 키보드 폴백
            tip.Open();
            Assert.Same(tip, HelpTip.Current);
            Assert.NotNull(tip.Callout);
            Assert.Equal("Help.Map.LayerPanel.Layers.Body", AutomationProperties.GetAutomationId(tip.Callout!));
            var body = tip.Callout!.Entry!.ToPlainText();
            Assert.Contains("묶음 전체", body);
            Assert.Contains("Alt+↑ · Alt+↓", body);

            // Esc — 말풍선만 닫고 키를 먹는다(창 · 지도 모드의 Esc 로 새지 않는다), 패널은 그대로
            Assert.True(PressEscape(source, tip));
            Assert.NotEqual(true, tip.IsChecked);
            Assert.Null(HelpTip.Current);
            Assert.True(f.Vm.IsLayerPanelVisible);
            Assert.Same(panel, f.Vm.LayerPanel);

            Assert.False(PressEscape(source, tip));                    // 닫혀 있으면 Esc 는 지나간다
            Assert.True(f.Vm.IsLayerPanelVisible);                      // 그래도 패널은 Esc 로 닫히지 않는다(닫기는 ✕ 만)
        }
        finally { source.RootVisual = null!; }
    });

    private static bool PressEscape(HeadlessSource source, UIElement target)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    /// <summary>HWND 없는 입력 원천 — "?" 가 Esc 를 들을 뿌리를 준다(창을 띄우지 않는다).</summary>
    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual
        {
            get => _root!;
            set { var old = _root; _root = value; RootChanged(old!, value); }
        }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static string RepoRoot([CallerFilePath] string? path = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
