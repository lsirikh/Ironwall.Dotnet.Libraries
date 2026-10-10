using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 MAP-081(행 id) · MAP-066(꺼짐 사유) — 제품 LayerPanelStyle.xaml 의 오버레이 순서 목록 템플릿을 그대로 불러
/// 창 없이 부모 없는 Measure/Arrange 로 행을 만들고, 바인딩으로 계산된 AutomationId · IsEnabled · 툴팁을 읽는다.
/// </summary>
public class LayerPanelXamlTests
{
    private static ResourceDictionary Style()
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

    private static (ListBox list, LayerTreeNode images) Render(bool editable)
    {
        var tree = LayerTreeBuilder.Build(new IMapLayerModel[]
        {
            new MapLayerModel { Id = 41, Name = "항공 A", LayerType = "OverlayImage", FilePath = @"C:\maps\a.png", IsVisible = true, Opacity = 1, ZOrder = 0 },
            new MapLayerModel { Id = 42, Name = "항공 B", LayerType = "OverlayImage", FilePath = @"C:\maps\b.png", IsVisible = true, Opacity = 1, ZOrder = 1 },
        });
        var images = tree.Single(n => n.OverlayLayerType == "OverlayImage");
        foreach (var leaf in images.Children) leaf.IsMapEditable = editable;
        var template = (DataTemplate)Style()["SectionChildrenReorderTemplate"];
        var list = (ListBox)template.LoadContent();
        list.DataContext = images;
        Scenario.Layout(list, 320, 200);
        return (list, images);
    }

    [Trait("Scenario", "MAP-081")]
    [Fact]
    public void should_expose_row_list_and_drag_handle_automation_ids_bound_to_layer_id_when_overlay_list_is_rendered() => Scenario.Sta(() =>
    {
        var (list, images) = Render(editable: true);

        Assert.Equal("GMaps.LayerPanel.List.OverlayImage", AutomationProperties.GetAutomationId(list));
        var rows = images.Children.Select(c => (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(c)).ToList();
        Assert.Equal(new[] { "GMaps.LayerPanel.Row.41", "GMaps.LayerPanel.Row.42" }, rows.Select(AutomationProperties.GetAutomationId));
        Assert.Equal(new[] { "항공 A", "항공 B" }, rows.Select(AutomationProperties.GetName));
        var handles = Descendants<DragHandle>(list).ToList();
        Assert.Equal(new[] { "GMaps.LayerPanel.DragHandle.41", "GMaps.LayerPanel.DragHandle.42" }, handles.Select(AutomationProperties.GetAutomationId));
        Assert.All(handles, h => Assert.True(h.IsEnabled));
    });

    [Trait("Scenario", "MAP-066")]
    [Fact]
    public void should_disable_drag_handle_and_lock_button_with_reason_tooltip_when_map_is_not_editable() => Scenario.Sta(() =>
    {
        var (list, images) = Render(editable: false);

        Assert.All(Descendants<DragHandle>(list), h => Assert.False(h.IsEnabled));
        var locks = Descendants<Button>(list).Where(b => images.Children.Any(c => ReferenceEquals(b.Command, c.ToggleLockCommand))).ToList();
        Assert.Equal(2, locks.Count);
        Assert.All(locks, b =>
        {
            Assert.False(b.IsEnabled);                                       // 꺼짐
            Assert.Equal("편집 권한이 없습니다", b.ToolTip);                  // 사유
            Assert.True(ToolTipService.GetShowOnDisabled(b));
        });
    });
}
