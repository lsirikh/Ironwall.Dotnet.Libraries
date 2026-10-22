using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2026-10-22 사용자 — "너무 건물 설정 오버레이 윈도우 길어 좀 높이 줄이고 스크롤 넣어라"(1920×1032 에서 판 아래 단추가 화면 밖):
/// 창 없이 실제 판 템플릿(pack URI — 앱과 같은 사전)을 지도 Canvas 의 판 자리(ContentPresenter, Left 620 · Top 90 — MapView.xaml)에 넣고 재어
/// 판 높이 ≤ 지도 높이의 70% · 지도 안, 머리줄(✕) · 행동 줄([✓ 적용] 등)은 스크롤 밖이라 늘 보임, 내용은 ScrollViewer 안,
/// 판 위 휠은 판 스크롤만(지도 · 3D 로 안 샘), 포커스 칸은 보이는 자리로(BringIntoView)를 단정한다.
/// DataContext 는 비운다 — 보임 묶음이 모두 기본(보임)이라 판이 가장 길다.
/// </summary>
public class BuildingPanelScrollScenarioTests
{
    private const double SlotTop = 90;
    /// <summary>판 상한 = 지도 높이 × 0.7 과 판 자리 아래 남은 높이 − 8 중 작은 쪽(HeightPanelControlBase.FitMaxRatio · FitBottomMargin 과 같은 값 — 고치기 전 코드로도 돌게 숫자로 둔다).</summary>
    private const double CapRatio = 0.7, BottomMargin = 8;

    public static IEnumerable<object[]> Panels() => new[]
    {
        new object[] { "BuildingHeight", 600.0 }, new object[] { "UserBuilding", 600.0 }, new object[] { "Decoration", 600.0 },
        // 1366×768 · 1920×1032 화면의 지도 칸(제목줄 · 메뉴 122 px 아래 — 앞선 실측 UIA)
        new object[] { "BuildingHeight", 646.0 }, new object[] { "UserBuilding", 646.0 }, new object[] { "Decoration", 646.0 },
        new object[] { "UserBuilding", 910.0 },
    };

    private static (HeightPanelControlBase Panel, string Style, string[] Header, string[] Footer, string[] Content) Make(string kind) => kind switch
    {
        "BuildingHeight" => (new BuildingHeightPanelControl(), "BuildingHeightPanelStyle.xaml",
            new[] { "Map.BuildingHeight.Close" },
            new[] { "Map.BuildingHeight.Apply", "Map.BuildingHeight.Cancel", "Map.BuildingHeight.Reset", "Map.BuildingHeight.Undo", "Map.BuildingHeight.Summary" },
            new[] { "Map.BuildingHeight.Floors", "Map.BuildingHeight.Handle", "Map.BuildingHeight.ApplyLook", "Map.BuildingHeight.HideAllAuto", "Map.BuildingHeight.RestoreAll" }),
        "UserBuilding" => (new UserBuildingPanelControl(), "UserBuildingPanelStyle.xaml",
            new[] { "Map.UserBuilding.Close" },
            new[] { "Map.UserBuilding.Apply", "Map.UserBuilding.Cancel", "Map.UserBuilding.Undo", "Map.UserBuilding.Redo", "Map.UserBuilding.Delete", "Map.UserBuilding.Summary" },
            new[] { "Map.UserBuilding.Tool.Rect", "Map.UserBuilding.Floors", "Map.UserBuilding.Facade", "Map.Edit3D.To2D" }),
        _ => (new DecorationPanelControl(), "DecorationPanelStyle.xaml",
            new[] { "Map.Decoration.Close" },
            new[] { "Map.Decoration.Apply", "Map.Decoration.Replace", "Map.Decoration.Delete", "Map.Decoration.ClearAll", "Map.Decoration.Undo", "Map.Decoration.Redo" },
            new[] { "Map.Decoration.Tool.TreePoint", "Map.Decoration.Species", "Map.Decoration.List", "Map.Model.Delete" }),
    };

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    private static FrameworkElement ById(DependencyObject root, string id)
        => Descendants(root).OfType<FrameworkElement>().First(d => AutomationProperties.GetAutomationId(d) == id);

    private static bool IsInside(DependencyObject child, DependencyObject ancestor)
    {
        for (var p = VisualTreeHelper.GetParent(child); p != null; p = VisualTreeHelper.GetParent(p))
            if (ReferenceEquals(p, ancestor)) return true;
        return false;
    }

    /// <summary>판을 지도 Canvas(폭 1366 · 높이 mapH)의 판 자리에 넣고 잰다 — 지도 크기를 안 뒤 판이 다시 잰다(SizeChanged).</summary>
    private static Canvas HostInMap(HeightPanelControlBase panel, string styleFile, double mapH)
    {
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{styleFile}", UriKind.Relative) });
        panel.Style = (Style)panel.Resources[panel.GetType()];
        var slot = new ContentPresenter { Content = panel };
        Canvas.SetLeft(slot, 620);
        Canvas.SetTop(slot, SlotTop);
        var map = new Canvas();
        map.Children.Add(slot);
        Scenario.Layout(map, 1366, mapH);
        Scenario.Layout(map, 1366, mapH);
        return map;
    }

    [Theory]
    [MemberData(nameof(Panels))]
    public void should_cap_panel_height_and_keep_header_and_action_row_visible_with_content_in_scroll(string kind, double mapH) => Scenario.Sta(() =>
    {
        var (panel, style, header, footer, content) = Make(kind);
        var map = HostInMap(panel, style, mapH);
        double cap = Math.Min(mapH * CapRatio, mapH - SlotTop - BottomMargin);

        Assert.Equal(320, panel.ActualWidth);                                                       // 폭은 그대로
        Assert.True(panel.ActualHeight <= cap + 0.5, $"{kind}: 판 높이 {panel.ActualHeight:0} > 상한 {cap:0}(지도 {mapH})");
        Assert.True(SlotTop + panel.ActualHeight <= mapH, $"{kind}: 판 아래 끝 {SlotTop + panel.ActualHeight:0} > 지도 {mapH}");

        var scroll = (ScrollViewer)ById(panel, $"Map.{kind}.Scroll");
        Assert.True(scroll.ExtentHeight > scroll.ViewportHeight + 1, $"{kind}: 이 높이에서는 내용({scroll.ExtentHeight:0})이 넘쳐 스크롤이 생겨야 한다(보이는 칸 {scroll.ViewportHeight:0})");

        // 머리줄 · 행동 줄 = 스크롤 밖 · 판 안에 다 보인다
        foreach (var id in header.Concat(footer))
        {
            var e = ById(panel, id);
            Assert.False(IsInside(e, scroll), $"{kind}: {id} 가 스크롤 안 — 늘 보여야 한다");
            Assert.Equal(Visibility.Visible, e.Visibility);
            var r = e.TransformToAncestor(panel).TransformBounds(new Rect(e.RenderSize));
            Assert.True(r.Height > 0 && r.Top >= -0.5 && r.Bottom <= panel.ActualHeight + 0.5, $"{kind}: {id} {r} 가 판(0~{panel.ActualHeight:0}) 밖");
        }
        foreach (var id in content)
            Assert.True(IsInside(ById(panel, id), scroll), $"{kind}: {id} 가 스크롤 밖");
    });

    [Theory]
    [InlineData("BuildingHeight")]
    [InlineData("UserBuilding")]
    [InlineData("Decoration")]
    public void should_scroll_panel_only_on_wheel_and_bring_focused_field_into_view(string kind) => Scenario.Sta(() =>
    {
        var (panel, style, _, footer, content) = Make(kind);
        var map = HostInMap(panel, style, 600);
        var scroll = (ScrollViewer)ById(panel, $"Map.{kind}.Scroll");
        int leaked = 0;
        map.AddHandler(UIElement.MouseWheelEvent, new MouseWheelEventHandler((_, _) => leaked++));   // 지도 칸(Canvas)까지 오면 지도 · 3D 로 샌 것

        // 내용 위 휠 = 판 스크롤
        var first = ById(panel, content[0]);
        first.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.MouseWheelEvent });
        map.UpdateLayout();
        Assert.True(scroll.VerticalOffset > 0, $"{kind}: 휠로 판이 스크롤되지 않았다");
        // 행동 줄(스크롤 밖) 위 휠도 지도로 안 샌다
        ById(panel, footer[0]).RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.MouseWheelEvent });
        Assert.Equal(0, leaked);

        // Tab 으로 맨 아래 내용 칸에 포커스 → 보이는 칸 안으로, 다시 맨 위 칸 → 위로
        foreach (var id in new[] { content[^1], content[0] })
        {
            var e = ById(panel, id);
            e.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, null, e) { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
            map.UpdateLayout();
            var r = e.TransformToAncestor(scroll).TransformBounds(new Rect(e.RenderSize));
            Assert.True(r.Top >= -0.5 && r.Bottom <= scroll.ViewportHeight + 0.5, $"{kind}: 포커스 칸 {id} {r} 가 보이는 칸(0~{scroll.ViewportHeight:0}) 밖");
        }
    });

    /// <summary>
    /// 세로 스크롤바가 생겨 내용 폭이 줄어도 글자가 잘리지 않는다(2026-10-22 앞선 지적 「글자 잘림」의 되풀이 방지) —
    /// 보이는 글자를 담은 요소에 배치 잘림(LayoutClip, 들어가지 않는 요소에만 붙는다)이 없다. 스크롤 칸 자신(ScrollContentPresenter)은 뺀다.
    /// </summary>
    [Theory]
    [InlineData("BuildingHeight")]
    [InlineData("UserBuilding")]
    [InlineData("Decoration")]
    public void should_not_clip_texts_when_vertical_scrollbar_narrows_content(string kind) => Scenario.Sta(() =>
    {
        var (panel, style, _, _, _) = Make(kind);
        HostInMap(panel, style, 600);
        var scroll = (ScrollViewer)ById(panel, $"Map.{kind}.Scroll");
        Assert.Equal(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
        var bad = new List<string>();
        foreach (var tb in Descendants(panel).OfType<TextBlock>().Where(t => t.Text.Length > 0 && IsShown(t)))
            for (DependencyObject? e = tb; e != null && e != panel; e = VisualTreeHelper.GetParent(e))
                if (e is FrameworkElement fe && e is not ScrollContentPresenter && e is not ScrollViewer && LayoutInformation.GetLayoutClip(fe) != null)
                { bad.Add($"{tb.Text} @ {e.GetType().Name}({fe.ActualWidth:0.#})"); break; }
        Assert.True(bad.Count == 0, $"{kind}: {string.Join(" | ", bad)}");
    });

    /// <summary>창이 없어 IsVisible 은 늘 false — 조상까지 Visibility 가 모두 Visible 인가로 본다.</summary>
    private static bool IsShown(UIElement e)
    {
        for (DependencyObject? d = e; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    [Fact]
    public void should_shrink_again_when_panel_is_dragged_lower_or_map_gets_smaller() => Scenario.Sta(() =>
    {
        var panel = new UserBuildingPanelControl();
        var map = HostInMap(panel, "UserBuildingPanelStyle.xaml", 910);
        var slot = (ContentPresenter)VisualTreeHelper.GetParent(panel);
        Canvas.SetTop(slot, 500);   // 머리 띠 끌기와 같은 길(Canvas.Top + 다시 재기)
        panel.InvalidateMeasure();
        map.UpdateLayout();
        Assert.True(500 + panel.ActualHeight <= 910, $"끌어 놓은 뒤 판 아래 끝 {500 + panel.ActualHeight:0} > 지도 910");

        Canvas.SetTop(slot, SlotTop);
        Scenario.Layout(map, 1366, 500);   // 창을 줄이면(지도 SizeChanged) 다시 잰다
        map.UpdateLayout();
        Assert.True(panel.ActualHeight <= 500 * CapRatio + 0.5, $"지도를 줄인 뒤 판 높이 {panel.ActualHeight:0}");
    });
}
