using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// MAP-056 결함 LAY-D4 수정 확인 — 레이어 패널 묶음 행(종류 · 「PIDS 장비」 그룹) 체크박스의 누름 순환(세 상태)과 부모/자식 동기.
/// 실제 LayerPanelStyle.xaml 템플릿을 입힌 패널(창 없음 · 부모 없는 Measure/Arrange)의 행 체크박스를 자동화 Toggle(= 한 번 클릭 경로)로 누른다.
/// 기대: 켜짐 → 꺼짐(묶음 전체 숨김) · 꺼짐 → 켜짐 · 중간(일부만 켜짐) → 꺼짐. 누름으로 중간(null)에 머무는 일은 없다.
/// </summary>
public class LayerKindCheckboxCycleScenarioTests
{
    private static GMapPidsMarker Pids(int id, int deviceId, EnumDeviceType type, double lat, double lng, string title)
    {
        var model = new PidsSymbolModel { Id = id, LinkedDeviceId = deviceId, DeviceType = type, Latitude = lat, Longitude = lng, Title = title, Visible = true, Category = EnumMarkerCategory.PIDS_EQUIPMENT };
        return new GMapPidsMarker(new FakeLog(), model) { IsLayerEnabled = true };
    }

    /// <summary>카메라 2대 · 센서 1대를 올리고 레이어 패널을 실제 스타일로 그린다.</summary>
    private static (MapVm f, LayerPanelControl panel, GMapPidsMarker[] cameras, GMapPidsMarker sensor) Open()
    {
        var f = new MapVm();
        var cameras = new[]
        {
            Pids(5, 50, EnumDeviceType.IpCamera, 37.4512, 127.0123, "카메라-5"),
            Pids(6, 60, EnumDeviceType.IpCamera, 37.4100, 126.9800, "카메라-6"),
        };
        var sensor = Pids(7, 70, EnumDeviceType.Fence, 37.3950, 126.9650, "센서-7");
        foreach (var c in cameras) f.Map.Markers.Add(c);
        f.Map.Markers.Add(sensor);
        var provider = new Ironwall.Dotnet.Libraries.GMaps.Providers.SymbolProvider();
        foreach (var c in cameras) provider.Add(c.Model);
        provider.Add(sensor.Model);
        Scenario.SetField(f.Vm, "_symbolProvider", provider);
        f.SymDb.Setup(d => d.UpdateSymbolVisibleAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();
        f.Vm.ShowLayerPanel();
        var panel = f.Vm.LayerPanel!;
        panel.Style = (Style)((ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/LayerPanelStyle.xaml", UriKind.Relative)))[typeof(LayerPanelControl)];
        Scenario.Layout(panel, 320, 700);
        return (f, panel, cameras, sensor);
    }

    private static IEnumerable<LayerTreeNode> AllNodes(MapVm f)
    {
        IEnumerable<LayerTreeNode> Walk(IEnumerable<LayerTreeNode> nodes) => nodes.SelectMany(n => new[] { n }.Concat(Walk(n.Children)));
        return Walk(f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes"));
    }

    private static CheckBox RowBox(LayerPanelControl panel, LayerTreeNode node)
    {
        panel.UpdateLayout();
        return Descendants<CheckBox>(panel).Single(c => ReferenceEquals(c.DataContext, node));
    }

    private static void Click(CheckBox box) => ((IToggleProvider)new ToggleButtonAutomationPeer(box)).Toggle();

    [Trait("Scenario", "MAP-056")]
    [Trait("Defect", "LAY-D4")]
    [Fact]
    public void should_cycle_kind_row_checkbox_on_off_and_mixed_to_off_without_ever_resting_in_indeterminate() => Scenario.Sta(() =>
    {
        var (f, panel, cameras, sensor) = Open();
        var kind = AllNodes(f).Single(n => n.NodeType == LayerNodeType.Category && n.Name == "카메라");
        var box = RowBox(panel, kind);
        int Shown() => cameras.Count(c => c.IsVisible);

        // ① 켜짐 → 한 번 → 꺼짐: 그 종류만 숨고 DB 에 심볼별 Visible=false
        Click(box);
        Assert.Equal(false, box.IsChecked);
        Assert.Equal(false, kind.IsChecked);
        Assert.Equal(0, Shown());
        Assert.True(sensor.IsVisible, "다른 종류(센서)까지 숨었다");
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(5, false, It.IsAny<CancellationToken>()), Times.Once);
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(6, false, It.IsAny<CancellationToken>()), Times.Once);

        // ② 꺼짐 → 한 번 → 켜짐: 둘 다 돌아오고 DB 복원
        Click(box);
        Assert.Equal(true, box.IsChecked);
        Assert.Equal(2, Shown());
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(5, true, It.IsAny<CancellationToken>()), Times.Once);
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(6, true, It.IsAny<CancellationToken>()), Times.Once);

        // ③ 잎 하나만 끄면 종류 행은 중간(null)으로 표시된다(자식 → 부모 집계)
        var leaf5 = kind.Children.Single(c => c.Symbol?.Id == 5);
        leaf5.IsChecked = false;                                      // 잎 체크박스의 양방향 바인딩 대상
        Assert.Null(kind.IsChecked);
        Assert.Null(box.IsChecked);                                   // 체크박스도 중간 표시(IsThreeState=False 여도 바인딩 null 은 그대로 보인다)
        Assert.Equal(1, Shown());

        // ④ 중간 → 한 번 → 꺼짐: 남은 카메라도 숨는다
        Click(box);
        Assert.Equal(false, box.IsChecked);
        Assert.Equal(false, kind.IsChecked);
        Assert.Equal(0, Shown());
        Assert.All(kind.Children, c => Assert.Equal(false, c.IsChecked));   // 부모 → 자식 전파

        // ⑤ 다시 한 번 → 켜짐: 전부 돌아온다
        Click(box);
        Assert.Equal(true, kind.IsChecked);
        Assert.Equal(2, Shown());
        Assert.All(kind.Children, c => Assert.Equal(true, c.IsChecked));
        Assert.All(cameras, c => Assert.True(c.Visible));
    });

    [Trait("Scenario", "MAP-056")]
    [Trait("Defect", "LAY-D4")]
    [Fact]
    public void should_keep_group_row_in_sync_with_kinds_and_toggle_whole_group_with_one_click() => Scenario.Sta(() =>
    {
        var (f, panel, cameras, sensor) = Open();
        var kind = AllNodes(f).Single(n => n.NodeType == LayerNodeType.Category && n.Name == "카메라");
        var group = kind.Parent!;
        Assert.Equal(LayerNodeType.Group, group.NodeType);           // 「PIDS 장비」 묶음
        var groupBox = RowBox(panel, group);
        var kindBox = RowBox(panel, kind);
        Assert.Equal(true, groupBox.IsChecked);

        // 종류 하나 끔 → 그룹은 중간(자식 → 부모)
        Click(kindBox);
        Assert.Null(group.IsChecked);
        Assert.Null(groupBox.IsChecked);

        // 중간인 그룹 행 한 번 → 꺼짐: 그룹 안 모든 종류 · 심볼 숨김(부모 → 자식)
        Click(groupBox);
        Assert.Equal(false, group.IsChecked);
        Assert.All(group.Children, c => Assert.Equal(false, c.IsChecked));
        Assert.True(cameras.All(c => !c.IsVisible) && !sensor.IsVisible, "그룹을 껐는데 보이는 심볼이 남았다");
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(7, false, It.IsAny<CancellationToken>()), Times.Once);

        // 꺼짐 → 한 번 → 켜짐: 전부 돌아오고 종류 행 체크박스도 따라 켜짐
        Click(groupBox);
        Assert.Equal(true, group.IsChecked);
        Assert.Equal(true, kindBox.IsChecked);
        Assert.True(cameras.All(c => c.IsVisible) && sensor.IsVisible);

        // 켜짐 → 한 번 → 꺼짐(중간으로 가지 않는다)
        Click(groupBox);
        Assert.Equal(false, groupBox.IsChecked);
        Assert.True(cameras.All(c => !c.IsVisible) && !sensor.IsVisible);
    });

    [Trait("Scenario", "MAP-056")]
    [Trait("Defect", "LAY-D4")]
    [Fact]
    public void should_make_every_group_row_checkbox_two_state_on_click_in_the_rendered_panel() => Scenario.Sta(() =>
    {
        // 회귀 방지: 자식이 있는 노드(섹션 · 그룹 · 종류)에 묶인 행 체크박스는 누름 순환이 두 상태여야 한다
        var (_, panel, _, _) = Open();
        panel.UpdateLayout();
        var boxes = Descendants<CheckBox>(panel).Where(c => c.DataContext is LayerTreeNode n && n.Children.Count > 0).ToList();
        Assert.NotEmpty(boxes);
        Assert.All(boxes, b => Assert.False(b.IsThreeState, $"'{((LayerTreeNode)b.DataContext).Name}' 행 체크박스가 IsThreeState=True — 한 번 눌러도 중간(null)에 머문다"));
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }
}
