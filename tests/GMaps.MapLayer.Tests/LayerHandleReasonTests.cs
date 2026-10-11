using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 MAP-066(편집 권한 없음 → 꺼짐 + 사유) 중 끌기 손잡이 쪽 — 선행 작업(787e)이 "결함 의심(시험 없음)"으로 남긴 것.
/// 같은 행의 자물쇠 단추는 꺼지면 「편집 권한이 없습니다」 툴팁을 꺼진 채로도 보여 주는데(ShowOnDisabled), 손잡이는 꺼지기만 하고
/// 사유가 안 나왔다(WPF 는 꺼진 요소의 툴팁을 기본으로 숨긴다) → 결함으로 판정(t-20261011-081026-0754).
/// 제품 LayerPanelStyle.xaml 의 오버레이 순서 목록 템플릿을 그대로 불러 창 없이 Measure/Arrange 한다.
/// </summary>
public class LayerHandleReasonTests
{
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    private static List<DragHandle> Handles(bool editable)
    {
        var tree = LayerTreeBuilder.Build(new IMapLayerModel[]
        {
            new MapLayerModel { Id = 51, Name = "항공 A", LayerType = "OverlayImage", FilePath = @"C:\maps\a.png", IsVisible = true, Opacity = 1, ZOrder = 0 },
            new MapLayerModel { Id = 52, Name = "항공 B", LayerType = "OverlayImage", FilePath = @"C:\maps\b.png", IsVisible = true, Opacity = 1, ZOrder = 1 },
        });
        var images = tree.Single(n => n.OverlayLayerType == "OverlayImage");
        foreach (var leaf in images.Children) leaf.IsMapEditable = editable;
        var dict = (ResourceDictionary)Application.LoadComponent(new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/LayerPanelStyle.xaml", UriKind.Relative));
        var list = (ListBox)((DataTemplate)dict["SectionChildrenReorderTemplate"]).LoadContent();
        list.DataContext = images;
        Scenario.Layout(list, 320, 200);
        return Descendants<DragHandle>(list).ToList();
    }

    [Trait("Scenario", "MAP-066")]
    [Trait("Defect", "MAP-066-handle")]
    [Fact]
    public void should_show_no_permission_reason_on_disabled_drag_handle_when_map_is_not_editable() => Scenario.Sta(() =>
    {
        var handles = Handles(editable: false);
        Assert.Equal(2, handles.Count);
        Assert.All(handles, h =>
        {
            Assert.False(h.IsEnabled);                                   // 꺼짐
            Assert.Equal("편집 권한이 없습니다", h.ToolTip);              // 사유 — 자물쇠 단추와 같은 글
            Assert.True(ToolTipService.GetShowOnDisabled(h));            // 꺼진 채로도 보인다
        });
    });

    [Trait("Scenario", "MAP-066")]
    [Fact]
    public void should_keep_keyboard_fallback_tooltip_on_enabled_drag_handle_when_map_is_editable() => Scenario.Sta(() =>
    {
        var handles = Handles(editable: true);
        Assert.All(handles, h =>
        {
            Assert.True(h.IsEnabled);
            Assert.Equal("순서 바꾸기 (Alt+↑ / Alt+↓)", h.ToolTip);       // 켜졌을 때는 키보드 대안 안내 그대로
        });
    });
}
