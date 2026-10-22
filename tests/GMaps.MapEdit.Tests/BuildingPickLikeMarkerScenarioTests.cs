using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2026-10-22 사용자(3D 「건물 높이」 판 캡처) — "글자가 잘리네 · 건물을 고르는 게 힘들다 · 2D 에서 Marker 수정하고 선택할 때와 같은 방식으로":
/// 창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 map-3d 엔진 + 실제 판 템플릿으로
/// ① 클릭 = 하나만 · Ctrl+클릭 = 넣기 · 빼기(엔진 키 · 옛 엔진은 호스트 키) · Shift+끌기(building.box, Ctrl 함께 = 더함) · 빈 곳 · Esc = 풀기,
///   고른 건물 모두 강조(building.select ids) · 편집 모드 꺼짐 = 무시 · 모르는/숨긴/직접 그린 건물 빼기,
/// ② 판 폭(320)에서 단추 · 줄 · 콤보 글자가 잘리지 않음(창 없는 측정 — 잘린 요소에는 배치 잘림(LayoutClip)이 생긴다).
/// </summary>
public partial class TwinContractScenarioTests
{
    private static readonly JObject Ctrl = new() { ["ctrl"] = true };

    private static JObject BoxItem(int id, JObject? extra = null)
    {
        var o = new JObject { ["id"] = id, ["origin"] = "photo", ["height"] = 12.0, ["height_from"] = "shadow", ["lon"] = 127.0, ["lat"] = 37.5 };
        if (extra != null) o.Merge(extra);
        return o;
    }

    private static JObject Box(bool add, params JObject[] items)
        => new() { ["type"] = "building.box", ["add"] = add, ["items"] = new JArray(items.Cast<object>().ToArray()) };

    /// <summary>마지막 강조 메시지 — "판의 건물 id|고른 id 들"(id null = 빈칸, ids 키가 없으면 "-").</summary>
    private static string LastSelect(Rig rig)
    {
        var m = rig.Sent("building.select").Last();
        var ids = m["ids"] as JArray;
        return $"{(m["id"]!.Type == JTokenType.Null ? "" : m.Value<int>("id").ToString())}|{(ids == null ? "-" : string.Join(",", ids.Select(t => t.Value<string>())))}";
    }

    /// <summary>판의 건물 · 고른 건물 — "판 id|고른 id 들".</summary>
    private static string Picked(Rig rig) => $"{rig.Vm.BuildingHeightBuilding?.Id}|{string.Join(",", rig.Vm.BuildingSelectionIds)}";

    private static bool PressEsc(Rig rig)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, new KeySource(rig.W.Deco), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        return (bool)Scenario.Call(rig.Vm, "TryHandleMap3DKey", e)!;
    }

    private sealed class KeySource : PresentationSource
    {
        private Visual? _root;
        public KeySource(Visual root) { RootVisual = root; }
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static string AutomationPropertiesId(DependencyObject d) => System.Windows.Automation.AutomationProperties.GetAutomationId(d);

    private static Task LooksLoaded(Rig rig) => Rig.Until(() => rig.Vm.IsBuildingLookAvailable && rig.Server.Requests.Any(r => r.Path.EndsWith("/looks")), "외관 목록");

    // ─────────────── ① 고르기 규칙 ───────────────

    [Fact]
    public void should_select_one_on_click_toggle_on_ctrl_click_and_clear_on_empty_click_or_escape_like_2d_markers() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(8);
        await LooksLoaded(rig);
        Assert.Equal("8|8", Picked(rig));
        Assert.Empty(rig.Sent("building.select"));                                          // 하나 고름 강조는 엔진이 스스로 그렸다
        Assert.False(rig.Vm.IsBuildingMultiSelect);

        // Ctrl+클릭(엔진이 보낸 키) = 넣기 — 판은 넣은 건물로, 모두 강조
        rig.PickBuilding(9, extra: Ctrl);
        Assert.Equal(new[] { 8, 9 }, rig.Vm.BuildingSelectionIds);
        Assert.Equal(9, rig.Vm.BuildingHeightBuilding!.Id);
        Assert.Equal("9|8,9", LastSelect(rig));
        Assert.True(rig.Vm.IsBuildingMultiSelect);
        Assert.Equal(("고른 건물 2개", "고른 2개 지우기"), (rig.Vm.BuildingMultiSelectText, rig.Vm.BuildingMultiDeleteText));

        // 옛 엔진(키 필드 없음) — 호스트 Ctrl
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.Control));
        rig.PickBuilding(10);
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.None));
        Assert.Equal(new[] { 8, 9, 10 }, rig.Vm.BuildingSelectionIds);
        Assert.Equal("10|8,9,10", LastSelect(rig));

        // 다시 Ctrl+클릭 = 빼기 — 판의 건물이 아니면 판은 그대로, 판의 건물이면 남은 것 중 마지막으로
        rig.PickBuilding(9, extra: Ctrl);
        Assert.Equal("10|8,10", Picked(rig));
        Assert.Equal("10|8,10", LastSelect(rig));
        rig.PickBuilding(10, extra: Ctrl);
        Assert.Equal("8|8", Picked(rig));
        Assert.Equal("8|8", LastSelect(rig));
        Assert.False(rig.Vm.IsBuildingMultiSelect);
        Assert.False(rig.Vm.HideSelectedBuildingsCommand.CanExecute(null));
        rig.PickBuilding(8, extra: Ctrl);                                                   // 마지막 하나를 빼면 = 고름 풀기(2D "선택 해제")
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Vm.BuildingSelectionIds);
        Assert.Equal("|-", LastSelect(rig));

        // Shift 만 누른 클릭도 고름을 날리지 않고 넣는다 · 일반 클릭 = 그 건물 하나만(여러 강조를 하나로)
        rig.PickBuilding(8);
        rig.PickBuilding(9, extra: new JObject { ["shift"] = true });
        Assert.Equal(new[] { 8, 9 }, rig.Vm.BuildingSelectionIds);
        int selects = rig.Sent("building.select").Count;
        rig.PickBuilding(11);
        Assert.Equal("11|11", Picked(rig));
        Assert.Equal(selects + 1, rig.Sent("building.select").Count);
        Assert.Equal("11|11", LastSelect(rig));
        rig.PickBuilding(12);                                                               // 하나 → 하나 = 엔진이 옮긴다(보내지 않음)
        Assert.Equal(new[] { 12 }, rig.Vm.BuildingSelectionIds);
        Assert.Equal(selects + 1, rig.Sent("building.select").Count);

        // 빈 곳 클릭(building.none) = 풀기
        rig.PickBuilding(13, extra: Ctrl);
        Assert.Equal(new[] { 12, 13 }, rig.Vm.BuildingSelectionIds);
        rig.From3D(new JObject { ["type"] = "building.none" });
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Vm.BuildingSelectionIds);
        Assert.Equal("|-", LastSelect(rig));

        // Esc = 풀기(3D 는 그대로)
        rig.PickBuilding(8);
        rig.PickBuilding(9, extra: Ctrl);
        Assert.True(PressEsc(rig));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Vm.BuildingSelectionIds);
        Assert.Equal("|-", LastSelect(rig));
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
        Assert.Empty(rig.Writes);                                                           // 고르기만으로는 저장하지 않는다
    });

    [Fact]
    public void should_select_buildings_in_shift_drag_box_new_or_added_and_skip_unknown_hidden_and_drawn_ones() => RunAsync(async rig =>
    {
        rig.Server.Looks[12] = new JObject { ["hidden"] = true, ["wall_color"] = null, ["roof_color"] = null, ["facade"] = "auto" };
        rig.Enter3D();
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Vm.HasBuildingHidden, "숨긴 건물 목록");

        // Shift+끌기 = 상자 안 건물로 새로 고름 — 숨긴(12 · 메시지 hidden 14) · 직접 그린(15 user_uid · 16 origin user) · 모르는 id 는 뺀다
        rig.From3D(Box(false, BoxItem(9), BoxItem(10), BoxItem(12), BoxItem(14, new JObject { ["hidden"] = true }), BoxItem(15, new JObject { ["user_uid"] = Uid }),
                       BoxItem(16, new JObject { ["origin"] = "user" }), new JObject { ["id"] = "x", ["height"] = 5.0 }, new JObject { ["id"] = -3, ["height"] = 5.0 }));
        Assert.Equal(new[] { 9, 10 }, rig.Vm.BuildingSelectionIds);
        Assert.Equal(9, rig.Vm.BuildingHeightBuilding!.Id);                                 // 앞 건물(8)은 상자 밖 — 판은 고른 것 중 첫 건물로
        Assert.Equal("9|9,10", LastSelect(rig));
        Assert.True(rig.Vm.IsBuildingMultiSelect);
        Assert.Null(rig.Vm.UserBuildingEditingUid);                                         // 직접 그린 건물 판은 열지 않는다

        // Ctrl+Shift+끌기 = 지금 고름에 더함(겹친 것은 그대로) — 판은 그대로
        rig.From3D(Box(true, BoxItem(10), BoxItem(11)));
        Assert.Equal(new[] { 9, 10, 11 }, rig.Vm.BuildingSelectionIds);
        Assert.Equal("9|9,10,11", LastSelect(rig));
        rig.From3D(Box(true));                                                              // 빈 상자 + 더함 = 그대로
        Assert.Equal(new[] { 9, 10, 11 }, rig.Vm.BuildingSelectionIds);

        // 판의 건물이 새 상자에 있으면 판은 그대로
        rig.From3D(Box(false, BoxItem(11), BoxItem(9)));
        Assert.Equal("9|11,9", Picked(rig));
        Assert.Equal("9|11,9", LastSelect(rig));

        // 빈 상자(새로) = 고름 풀기
        rig.From3D(Box(false));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Vm.BuildingSelectionIds);
        Assert.Equal("|-", LastSelect(rig));

        // 판이 닫혀 있을 때 Ctrl+Shift+끌기 = 상자 안 것만
        rig.From3D(Box(true, BoxItem(8)));
        Assert.Equal("8|8", Picked(rig));
        Assert.Equal("8|8", LastSelect(rig));
        Assert.Empty(rig.Writes);
    });

    [Fact]
    public void should_ignore_click_ctrl_click_and_box_when_map_edit_mode_is_off() => RunAsync(async rig =>
    {
        rig.Enter3D();
        Scenario.SetField(rig.Vm, "_isEditModeEnabled", false);                             // 보기 전용(2026-10-21 규칙)
        rig.W.Map.SetEditMode(false);
        rig.Posts.Clear();
        rig.PickBuilding(8);
        rig.PickBuilding(9, extra: Ctrl);
        rig.From3D(Box(false, BoxItem(10), BoxItem(11)));
        rig.From3D(Box(true, BoxItem(12)));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Vm.BuildingSelectionIds);
        Assert.Equal(4, rig.Sent("building.select").Count);                                 // 엔진이 그린 윤곽 · 상자 강조는 매번 지운다
        Assert.All(rig.Sent("building.select"), m => Assert.Equal(JTokenType.Null, m["id"]!.Type));
        Assert.All(rig.Sent("building.select"), m => Assert.Null(m["ids"]));
        Assert.Empty(rig.Writes);
        await Task.CompletedTask;
    });

    [Fact]
    public void should_keep_drawn_building_routes_when_ctrl_clicked_or_while_drawing_in_3d() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.PickBuilding(8);
        rig.PickBuilding(9, extra: Ctrl);
        Assert.Equal(new[] { 8, 9 }, rig.Vm.BuildingSelectionIds);

        // Ctrl+클릭한 것이 다시 만든 직접 그린 건물(user_uid) — 고름에 넣지 않고 그 건물 고치기 판(기존 길)
        rig.PickBuilding(15, origin: "user", extra: new JObject { ["user_uid"] = Uid, ["ctrl"] = true });
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Vm.BuildingSelectionIds);

        // 3D 그리기 중 Shift+끌기 상자 = 무시(그리기 몫)
        rig.Vm.ExitUserBuildingDrawCommand.Execute(null);
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Vm.SelectUserBuildingToolCommand.Execute("Rect");
        Assert.Equal("rect", rig.Vm.Edit3DDrawMode);
        rig.Posts.Clear();
        rig.From3D(Box(false, BoxItem(10), BoxItem(11)));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal("rect", rig.Vm.Edit3DDrawMode);
        Assert.Equal(new[] { "building.select" }, rig.Posts.Select(p => p.Value<string>("type")));
        Assert.Equal(JTokenType.Null, rig.Posts[0]["id"]!.Type);
    });

    // ─────────────── ② 판 글자 잘림(창 없는 측정) ───────────────

    /// <summary>
    /// 보이는 글자(TextBlock) 중 판 폭에 다 들어가지 않는 것 — 그 글자나 그것을 담은 단추 · 칸이 배치에서 잘렸거나(LayoutClip, WPF 는 들어가지 않는 요소에만 붙인다)
    /// 줄임표(…)로 줄었으면 잘림이다. "글자 @ 잘린 요소" 목록.
    /// </summary>
    private static List<string> ClippedTexts(FrameworkElement root)
    {
        var bad = new List<string>();
        void Walk(DependencyObject d)
        {
            if (d is UIElement { Visibility: not Visibility.Visible }) return;
            if (d is TextBlock { Text.Length: > 0 } tb)
            {
                for (DependencyObject? e = tb; e != null && e != root; e = VisualTreeHelper.GetParent(e))
                    if (e is FrameworkElement fe && e is not ScrollContentPresenter && LayoutInformation.GetLayoutClip(fe) != null)
                    { bad.Add($"{tb.Text} @ {e.GetType().Name}({fe.ActualWidth:0.#})"); break; }
                if (tb.TextTrimming != TextTrimming.None)
                {
                    var full = new FormattedText(tb.Text, System.Globalization.CultureInfo.CurrentUICulture, tb.FlowDirection,
                                                 new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), tb.FontSize, Brushes.Black, 1.0);
                    if (full.WidthIncludingTrailingWhitespace > tb.ActualWidth + 0.5) bad.Add($"{tb.Text} @ 줄임표({tb.ActualWidth:0.#} < {full.WidthIncludingTrailingWhitespace:0.#})");
                }
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Walk(VisualTreeHelper.GetChild(d, i));
        }
        Walk(root);
        return bad;
    }

    [Fact]
    public void should_fit_every_button_line_and_combo_text_in_height_panel_width_without_clipping() => RunAsync(async rig =>
    {
        rig.Server.Looks[12] = new JObject { ["hidden"] = true, ["wall_color"] = null, ["roof_color"] = null, ["facade"] = "auto" };
        rig.Enter3D();
        rig.PickBuilding(9);
        await Rig.Until(() => rig.Vm.HasBuildingHidden && rig.Vm.IsBuildingLookAvailable, "숨긴 건물 · 외관 목록");
        rig.PickBuilding(10, extra: Ctrl);                                                  // [고른 n개 지우기] · 「고른 건물 n개」 보임
        rig.PickBuilding(8, extra: new JObject { ["ctrl"] = true, ["wall_color"] = "#123456" });   // 판 = 8, 목록 밖 색 = "지정 색 #123456"(가장 긴 색 이름)
        Assert.True(rig.Vm.IsBuildingMultiSelect);
        var panel = rig.Vm.BuildingHeightPanel;
        Render(panel, "BuildingHeightPanelStyle.xaml");
        Assert.Equal(320, panel.ActualWidth);                                               // 실제 판 폭(HeightPanelControlBase.Width)
        Assert.Equal(Visibility.Visible, Find<Button>(panel, "Map.BuildingHeight.HideSelected").Visibility);
        Assert.Contains(Find<ComboBox>(panel, "Map.BuildingHeight.WallColor").Items.Cast<KeyValuePair<string, string>>(), p => p.Value == "지정 색 #123456");
        Assert.Empty(ClippedTexts(panel));

        // 단추마다 글자를 다 보일 폭(제약 없이 잰 DesiredSize) ≤ 실제 폭
        var buttons = new List<Button>();
        void Collect(DependencyObject d)
        {
            if (d is UIElement { Visibility: not Visibility.Visible }) return;
            if (d is Button b) buttons.Add(b);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Collect(VisualTreeHelper.GetChild(d, i));
        }
        Collect(panel);
        Assert.Contains(buttons, b => AutomationPropertiesId(b) == "Map.BuildingHeight.HideAndRedraw");
        foreach (var b in buttons)
        {
            double actual = b.ActualWidth;
            b.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double need = b.DesiredSize.Width - b.Margin.Left - b.Margin.Right;   // DesiredSize 에는 바깥 여백이 들어 있다
            Assert.True(need <= actual + 0.5, $"{AutomationPropertiesId(b)} '{b.Content}' 필요 {need:0.#} > 실제 {actual:0.#}");
        }
        Scenario.Layout(panel, 320, 1400);

        // 콤보는 고른 값마다 — 가장 긴 외벽 무늬("공장 · 샌드위치 패널") · 색 이름까지
        foreach (var comboId in new[] { "Map.BuildingHeight.Facade", "Map.BuildingHeight.WallColor", "Map.BuildingHeight.RoofColor" })
        {
            var combo = Find<ComboBox>(panel, comboId);
            Assert.True(combo.Items.Count > 1, comboId);
            for (int i = 0; i < combo.Items.Count; i++)
            {
                combo.SelectedIndex = i;
                Scenario.Layout(panel, 320, 1400);
                var bad = ClippedTexts(panel);
                Assert.True(bad.Count == 0, $"{comboId}[{i}] {combo.Text}: {string.Join(" | ", bad)}");
            }
        }
    });
}
