using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 레이어 패널 다중 선택(2026-10-19) — 클릭 · Ctrl · Shift · Ctrl+Shift 조합 · 접힌 묶음 범위 · 묶음 머리 · 키보드 폴백 ·
/// 체크박스 등 행 안 조작은 선택 불변 · UIA 선택 상태 · 재조회 뒤 기준 행 · 편집 모드가 아니면 지도와 무관.
/// 창을 띄우지 않는다(실제 LayerPanelControl + 제품 LayerPanelStyle.xaml, 부모 없는 Measure/Arrange). 지도 연동은 GMaps.MapEdit.Tests 몫.
/// </summary>
public class LayerPanelSelectionScenarioTests
{
    /// <summary>
    /// 오버레이 이미지 2행(11 · 12) · 기하학 도형 3개(1 가 · 2 나 · 3 다, 펼침) · PIDS 그룹 2개(31 · 32, 접힘).
    /// 보이는 순서 = 11, 12, 1, 2, 3 (접힌 PIDS 그룹 안은 건너뜀).
    /// </summary>
    private static (LayerPanelControl panel, ObservableCollection<LayerTreeNode> roots, List<int> events) Tree()
    {
        var layers = new IMapLayerModel[]
        {
            new MapLayerModel { Id = 11, Name = "항공 A", LayerType = "OverlayImage", Category = "OverlayImage", FilePath = @"C:\maps\a.png", IsVisible = true, Opacity = 1, ZOrder = 0 },
            new MapLayerModel { Id = 12, Name = "항공 B", LayerType = "OverlayImage", Category = "OverlayImage", FilePath = @"C:\maps\b.png", IsVisible = true, Opacity = 1, ZOrder = 1 },
        };
        var symbols = new ISymbolModel[]
        {
            new GeometricSymbolModel { Id = 1, Title = "가", Visible = true },
            new GeometricSymbolModel { Id = 2, Title = "나", Visible = true },
            new GeometricSymbolModel { Id = 3, Title = "다", Visible = true },
            new PidsGroupSymbolModel { Id = 31, Title = "구역-1", Visible = true },
            new PidsGroupSymbolModel { Id = 32, Title = "구역-2", Visible = true },
        };
        var roots = LayerTreeBuilder.Build(layers, symbols);
        Category(roots, "기하학 도형").IsExpanded = true;
        var panel = new LayerPanelControl { TreeNodes = roots };
        var events = new List<int>();
        panel.LayerSelectionChanged += (_, e) => events.Add(e.Selected.Count);
        return (panel, roots, events);
    }

    private static IEnumerable<LayerTreeNode> All(IEnumerable<LayerTreeNode> nodes) => nodes.SelectMany(n => new[] { n }.Concat(All(n.Children)));
    private static LayerTreeNode Category(IEnumerable<LayerTreeNode> roots, string name) => All(roots).Single(n => n.NodeType == LayerNodeType.Category && n.Name == name);
    private static LayerTreeNode Leaf(IEnumerable<LayerTreeNode> roots, int id) => LayerTreeBuilder.Flatten(roots).Single(n => n.Symbol?.Id == id || (n.Symbol == null && n.Model?.Id == id));
    private static int IdOf(LayerTreeNode n) => n.Symbol?.Id ?? n.Model!.Id;
    private static int[] Selected(IEnumerable<LayerTreeNode> roots) => LayerSelection.SelectedLeaves(roots).Select(IdOf).ToArray();

    private static ModifierKeys Mods(string m) => m switch
    {
        "^" => ModifierKeys.Control,
        "+" => ModifierKeys.Shift,
        "^+" => ModifierKeys.Control | ModifierKeys.Shift,
        _ => ModifierKeys.None,
    };

    // ── 클릭 조합 표 ──

    /// <summary>단계 = "수식키:Id"(수식키 없음 · ^ Ctrl · + Shift · ^+ Ctrl+Shift). 기대 = 트리 순서의 선택 Id.</summary>
    [Theory]
    [InlineData("2", "2")]
    [InlineData("1 ^3", "1,3")]
    [InlineData("1 ^3 ^1", "3")]
    [InlineData("1 2", "2")]
    [InlineData("11 +3", "11,12,1,2,3")]
    [InlineData("3 +11", "11,12,1,2,3")]
    [InlineData("12 +2 +1", "12,1")]
    [InlineData("11 ^2 +3", "2,3")]
    [InlineData("11 ^3 ^+1", "11,1,2,3")]
    [InlineData("+2", "2")]
    [InlineData("^+2", "2")]
    [InlineData("1 ^2 ^3 2", "2")]
    [InlineData("11 ^12 +2", "12,1,2")]
    public void should_select_by_explorer_rules_when_rows_are_clicked_with_modifiers(string steps, string expected) => Scenario.Sta(() =>
    {
        var (panel, roots, _) = Tree();
        foreach (var step in steps.Split(' '))
        {
            var mod = new string(step.TakeWhile(c => c is '^' or '+').ToArray());
            panel.SelectNode(Leaf(roots, int.Parse(step[mod.Length..])), Mods(mod));
        }
        Assert.Equal(expected.Split(',').Select(int.Parse).ToArray(), Selected(roots));
    });

    [Fact]
    public void should_skip_rows_inside_collapsed_group_when_shift_range_crosses_it() => Scenario.Sta(() =>
    {
        var (panel, roots, _) = Tree();
        var zones = Category(roots, "PIDS 그룹");
        Assert.False(zones.IsExpanded);                                // 전제: 접힘(구역-1 · 구역-2 는 범위 사이에 있다 — 트리 순서는 PIDS 그룹이 도형보다 앞)

        panel.SelectNode(Leaf(roots, 12), ModifierKeys.None);
        panel.SelectNode(Leaf(roots, 1), ModifierKeys.Shift);
        Assert.Equal(new[] { 12, 1 }, Selected(roots));                // 접힌 구역 행은 넣지 않는다

        zones.IsExpanded = true;                                       // 펼치면 같은 범위가 구역 행을 품는다
        panel.SelectNode(Leaf(roots, 1), ModifierKeys.Shift);
        Assert.Equal(new[] { 12, 31, 32, 1 }, Selected(roots));
    });

    [Fact]
    public void should_restart_range_from_clicked_row_when_anchor_was_folded_away() => Scenario.Sta(() =>
    {
        var (panel, roots, _) = Tree();
        Category(roots, "PIDS 그룹").IsExpanded = true;
        panel.SelectNode(Leaf(roots, 31), ModifierKeys.None);
        Category(roots, "PIDS 그룹").IsExpanded = false;               // 기준 행이 접힌 묶음 안으로 숨었다

        panel.SelectNode(Leaf(roots, 2), ModifierKeys.Shift);
        Assert.Equal(new[] { 2 }, Selected(roots));                    // 숨은 기준 대신 그 행 하나에서 다시
        panel.SelectNode(Leaf(roots, 3), ModifierKeys.Shift);
        Assert.Equal(new[] { 2, 3 }, Selected(roots));                 // 새 기준(2)부터 범위
    });

    [Fact]
    public void should_add_every_leaf_of_group_even_collapsed_when_header_is_ctrl_clicked() => Scenario.Sta(() =>
    {
        var (panel, roots, events) = Tree();
        panel.SelectNode(Leaf(roots, 1), ModifierKeys.None);

        Assert.True(panel.SelectGroup(Category(roots, "PIDS 그룹")));   // 접힌 묶음 — 안 행 전부 더한다
        Assert.Equal(new[] { 31, 32, 1 }, Selected(roots));
        Assert.False(panel.SelectGroup(Category(roots, "PIDS 그룹")));  // 이미 전부 → 바뀐 것 없음 · 알림 없음
        Assert.Equal(2, events.Count);
        Assert.False(panel.SelectGroup(roots[0]));                     // 섹션 머리는 묶음 선택 대상이 아니다
    });

    [Fact]
    public void should_raise_one_change_per_user_action_and_none_when_nothing_changed() => Scenario.Sta(() =>
    {
        var (panel, roots, events) = Tree();
        panel.SelectNode(Leaf(roots, 1), ModifierKeys.None);
        panel.SelectNode(Leaf(roots, 1), ModifierKeys.None);           // 같은 행 다시 — 바뀐 것 없음
        panel.SelectNode(Leaf(roots, 3), ModifierKeys.Shift);
        LayerSelection.Apply(() => Leaf(roots, 11).IsSelected = true); // 프로그램이 바꾼 선택(지도 → 패널)은 알림 없음

        Assert.Equal(new[] { 1, 3 }, events);                          // 알림마다 그때 선택 수
        Assert.Equal(new[] { 11, 1, 2, 3 }, panel.SelectedNodes.Select(IdOf));
    });

    // ── 키보드 폴백 ──

    [Fact]
    public void should_move_extend_toggle_select_all_and_clear_when_selection_keys_are_pressed() => Scenario.Sta(() =>
    {
        var (panel, roots, _) = Tree();
        bool K(Key k, ModifierKeys m = ModifierKeys.None) => panel.HandleSelectionKey(k, m);

        Assert.True(K(Key.Down));                                      // 초점 없음 → 첫 보이는 행
        Assert.Equal(new[] { 11 }, Selected(roots));
        Assert.True(K(Key.Down)); Assert.True(K(Key.Down));
        Assert.Equal(new[] { 1 }, Selected(roots));                    // 한 행씩 · 그 행만
        Assert.True(K(Key.Down, ModifierKeys.Shift)); Assert.True(K(Key.Down, ModifierKeys.Shift));
        Assert.Equal(new[] { 1, 2, 3 }, Selected(roots));              // 기준(1)부터 범위
        Assert.True(K(Key.Down, ModifierKeys.Shift));
        Assert.Equal(new[] { 1, 2, 3 }, Selected(roots));              // 끝에서 멈춤
        Assert.True(K(Key.Up, ModifierKeys.Shift));
        Assert.Equal(new[] { 1, 2 }, Selected(roots));                 // 범위 줄이기

        Assert.True(K(Key.Up, ModifierKeys.Control)); Assert.True(K(Key.Up, ModifierKeys.Control));
        Assert.Equal(new[] { 1, 2 }, Selected(roots));                 // Ctrl+↑ = 초점만
        Assert.True(Leaf(roots, 12).IsCurrent);
        Assert.True(K(Key.Space, ModifierKeys.Control));
        Assert.Equal(new[] { 12, 1, 2 }, Selected(roots));             // Ctrl+Space = 더하기
        Assert.True(K(Key.Space, ModifierKeys.Control));
        Assert.Equal(new[] { 1, 2 }, Selected(roots));                 // 다시 = 빼기
        Assert.True(K(Key.Space));
        Assert.Equal(new[] { 12 }, Selected(roots));                   // Space = 그 행만

        Assert.True(K(Key.A, ModifierKeys.Control));
        Assert.Equal(new[] { 11, 12, 1, 2, 3 }, Selected(roots));      // 보이는 항목 전부(접힌 구역 제외)
        Assert.True(K(Key.Escape));
        Assert.Empty(Selected(roots));
        Assert.False(K(Key.Escape));                                   // 고른 것이 없으면 Esc 는 지나간다(지도 · 다른 Esc 규칙 몫)

        Assert.False(K(Key.System, ModifierKeys.Alt));                 // Alt+↑/↓ = 순서 바꾸기 몫
        Assert.False(K(Key.Down, ModifierKeys.Alt));
        Assert.False(K(Key.A));                                        // 그냥 A 는 선택 키가 아니다
    });

    [Theory]
    [InlineData(Key.Up, ModifierKeys.None, true)]
    [InlineData(Key.Down, ModifierKeys.Shift, true)]
    [InlineData(Key.Down, ModifierKeys.Control, true)]
    [InlineData(Key.Space, ModifierKeys.Control, true)]
    [InlineData(Key.A, ModifierKeys.Control, true)]
    [InlineData(Key.Escape, ModifierKeys.None, true)]
    [InlineData(Key.Down, ModifierKeys.Alt, false)]
    [InlineData(Key.Left, ModifierKeys.None, false)]
    [InlineData(Key.Delete, ModifierKeys.None, false)]
    [InlineData(Key.A, ModifierKeys.None, false)]
    [InlineData(Key.Z, ModifierKeys.Control, false)]
    public void should_claim_only_selection_keys_when_map_hook_asks_layer_panel(Key key, ModifierKeys mods, bool claimed)
        => Assert.Equal(claimed, LayerPanelControl.IsSelectionKey(key, mods));

    // ── 재조회 ──

    [Fact]
    public void should_keep_shift_anchor_on_same_item_when_tree_is_rebuilt() => Scenario.Sta(() =>
    {
        var (panel, roots, _) = Tree();
        panel.SelectNode(Leaf(roots, 12), ModifierKeys.None);

        var (_, rebuilt, _) = Tree();                                  // 재조회 = 같은 항목의 새 노드
        panel.TreeNodes = rebuilt;
        panel.SelectNode(Leaf(rebuilt, 2), ModifierKeys.Shift);

        Assert.Equal(new[] { 12, 1, 2 }, Selected(rebuilt));           // 기준(12)이 새 트리의 같은 항목으로 옮겨졌다
        Assert.Equal(new[] { 12 }, Selected(roots));                   // 옛 트리는 건드리지 않는다
    });

    // ── 렌더(제품 템플릿) — 행 클릭 · 행 안 조작 · UIA ──

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

    private static (LayerPanelControl panel, ObservableCollection<LayerTreeNode> roots, List<int> events) Rendered()
    {
        var t = Tree();
        t.panel.Style = (Style)StyleDictionary()[typeof(LayerPanelControl)];
        Scenario.Layout(t.panel, 320, 700);
        return t;
    }

    /// <summary>
    /// 창 없이 누름을 그 요소에서 낸다 — 터널 PreviewMouseDown 을 올리면 경로의 요소마다 WPF 가 Preview(Left|Right)ButtonDown 으로 바꿔 낸다(실제 입력과 같은 길).
    /// 패널 처리기가 원본 요소로 행 · 조작을 가른다.
    /// </summary>
    private static bool Press(UIElement element, bool right = false)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, right ? MouseButton.Right : MouseButton.Left)
        { RoutedEvent = Mouse.PreviewMouseDownEvent };
        element.RaiseEvent(args);
        return args.Handled;
    }

    private static TextBlock NameText(LayerPanelControl panel, LayerTreeNode node)
        => Descendants<TextBlock>(panel).First(t => ReferenceEquals(t.DataContext, node) && t.Text == node.Name);

    [Fact]
    public void should_select_row_when_its_name_is_pressed_and_keep_selection_when_checkbox_lock_chevron_slider_or_handle_is_pressed() => Scenario.Sta(() =>
    {
        var (panel, roots, events) = Rendered();
        var image = Leaf(roots, 11);
        var symbol = Leaf(roots, 2);

        Assert.True(Press(NameText(panel, symbol)));                   // 행 이름 누름 = 선택(목록이 다시 고르지 않게 처리됨)
        Assert.Equal(new[] { 2 }, Selected(roots));
        Assert.True(Press(NameText(panel, image)));
        Assert.Equal(new[] { 11 }, Selected(roots));

        UIElement In<T>(LayerTreeNode n, Func<T, bool>? pick = null) where T : UIElement
            => Descendants<T>(panel).First(e => e is FrameworkElement { DataContext: var dc } && ReferenceEquals(dc, n) && (pick?.Invoke(e) ?? true));
        var untouched = new (string what, UIElement element)[]
        {
            ("심볼 체크박스", In<CheckBox>(symbol)),
            ("심볼 자물쇠", In<Button>(symbol)),
            ("이미지 체크박스", In<CheckBox>(image)),
            ("이미지 자물쇠", In<Button>(image)),
            ("이미지 불투명도", In<Slider>(image)),
            ("이미지 끌기 손잡이", In<DragHandle>(image)),
            ("카테고리 셰브런", In<ToggleButton>(Category(roots, "기하학 도형"), t => t is not CheckBox)),
            ("카테고리 체크박스", In<CheckBox>(Category(roots, "기하학 도형"))),
        };
        foreach (var (what, element) in untouched)
        {
            Assert.False(Press(element), $"{what} 누름을 패널이 가로챘다");
            Assert.Equal(new[] { 11 }, Selected(roots));
        }
        Assert.False(Press(NameText(panel, Category(roots, "기하학 도형"))));   // 묶음 머리 그냥 누름 = 예전 그대로(선택 아님)
        Assert.Equal(new[] { 11 }, Selected(roots));
        Assert.Equal(new[] { 1, 1 }, events);                          // 이름 누름 두 번만 알림
    });

    [Fact]
    public void should_select_unselected_row_and_keep_multi_selection_when_row_is_right_clicked() => Scenario.Sta(() =>
    {
        var (panel, roots, _) = Rendered();
        panel.SelectNode(Leaf(roots, 1), ModifierKeys.None);
        panel.SelectNode(Leaf(roots, 3), ModifierKeys.Shift);

        Press(NameText(panel, Leaf(roots, 2)), right: true);           // 고른 행 우클릭 = 그대로(묶음 메뉴 대상 유지)
        Assert.Equal(new[] { 1, 2, 3 }, Selected(roots));
        Press(NameText(panel, Leaf(roots, 12)), right: true);          // 안 고른 행 우클릭 = 그 행만
        Assert.Equal(new[] { 12 }, Selected(roots));
    });

    [Fact]
    public void should_expose_selection_to_uia_on_overlay_and_symbol_rows_when_rows_are_selected() => Scenario.Sta(() =>
    {
        var (panel, roots, _) = Rendered();
        panel.SelectNode(Leaf(roots, 12), ModifierKeys.None);
        panel.SelectNode(Leaf(roots, 2), ModifierKeys.Control);
        Scenario.Layout(panel, 320, 700);

        // 오버레이 행 — ListBoxItem SelectionItemPattern(목록이 노드 선택을 비춘다 · 여러 행)
        var rows = Descendants<ListBoxItem>(panel).ToDictionary(i => ((LayerTreeNode)i.DataContext).Model!.Id);
        var list = Descendants<ListBox>(panel).Single(l => l.Items.Contains(Leaf(roots, 12)));
        var itemPeers = UIElementAutomationPeer.CreatePeerForElement(list).GetChildren();
        bool UiaSelected(string name) => ((ISelectionItemProvider)itemPeers.Single(p => p.GetName() == name).GetPattern(PatternInterface.SelectionItem)).IsSelected;
        Assert.True(UiaSelected("항공 B"));
        Assert.False(UiaSelected("항공 A"));
        Assert.False(rows[11].IsSelected);
        Assert.Equal("GMaps.LayerPanel.Row.12", AutomationProperties.GetAutomationId(rows[12]));
        Assert.Equal("선택됨", AutomationProperties.GetItemStatus(rows[12]));
        Assert.Equal("", AutomationProperties.GetItemStatus(rows[11]));

        // 심볼 잎 칸 — 항목 id + ItemStatus "선택됨"(기존 관용구)
        var cells = Descendants<ContentPresenter>(panel).Where(c => AutomationProperties.GetAutomationId(c).StartsWith("GMaps.LayerPanel.Symbol.")).ToList();
        Assert.Equal(new[] { "GMaps.LayerPanel.Symbol.1", "GMaps.LayerPanel.Symbol.2", "GMaps.LayerPanel.Symbol.3" }, cells.Select(AutomationProperties.GetAutomationId));
        Assert.Equal(new[] { "", "선택됨", "" }, cells.Select(AutomationProperties.GetItemStatus));

        // 선택 표시 = 형태(좌측 세로 바)가 보이고 꺼진다
        Rectangle Bar(LayerTreeNode n) => Descendants<Rectangle>(panel).First(r => ReferenceEquals(r.DataContext, n) && r.Width == 3);
        Assert.Equal(Visibility.Visible, Bar(Leaf(roots, 2)).Visibility);
        Assert.Equal(Visibility.Collapsed, Bar(Leaf(roots, 1)).Visibility);
        panel.HandleSelectionKey(Key.Escape, ModifierKeys.None);
        Scenario.Layout(panel, 320, 700);
        Assert.Equal(Visibility.Collapsed, Bar(Leaf(roots, 2)).Visibility);
        Assert.False(rows[12].IsSelected);
    });

    [Fact]
    public void should_follow_handle_click_selection_of_list_as_single_row_selection_across_panel() => Scenario.Sta(() =>
    {
        var (panel, roots, events) = Rendered();
        panel.SelectNode(Leaf(roots, 1), ModifierKeys.None);
        panel.SelectNode(Leaf(roots, 2), ModifierKeys.Control);
        var handle = Descendants<DragHandle>(panel).First(h => ReferenceEquals(h.DataContext, Leaf(roots, 12)));

        Press(handle);                                                 // 손잡이 누름(데드존 미만 클릭) → 커널이 목록 선택을 그 행으로
        var list = Descendants<ListBox>(panel).Single(l => l.Items.Contains(Leaf(roots, 12)));
        list.SelectedItem = Leaf(roots, 12);                           // 커널 SelectOnly 와 같은 일

        Assert.Equal(new[] { 12 }, Selected(roots));                   // 패널 전체도 그 행만
        Assert.Equal(3, events.Count);
    });
}
