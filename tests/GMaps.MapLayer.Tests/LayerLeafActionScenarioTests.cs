using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 §1 레이어 패널 잎 동작 — 「지도 가운데로 이동」(MAP-049) · 종류 보이기 끔/켬(MAP-056) · 잎 「위로」/「아래로」(MAP-062) 헤드리스 짝.
/// 트리 노드 명령 → 패널 이벤트 → MapViewModel 처리기 → 지도 마커 · 가짜 지도 DB 경로를 그대로 탄다(창 · 실 DB 없음).
/// </summary>
public class LayerLeafActionScenarioTests
{
    private const string PathA = @"C:\maps\overlay-a.png", PathB = @"C:\maps\overlay-b.png";

    private static GMapPidsMarker Pids(int id, int deviceId, EnumDeviceType type, double lat, double lng, string title)
    {
        var model = new PidsSymbolModel { Id = id, LinkedDeviceId = deviceId, DeviceType = type, Latitude = lat, Longitude = lng, Title = title, Visible = true, Category = EnumMarkerCategory.PIDS_EQUIPMENT };
        return new GMapPidsMarker(new FakeLog(), model) { IsLayerEnabled = true };
    }

    /// <summary>오버레이 이미지 2장 · 카메라 2대 · 센서 1대를 지도에 올리고 레이어 패널을 연다(트리는 제품 LoadLayersFromDbAsync 로).</summary>
    private static (MapVm f, GMapImageMarker a, GMapImageMarker b, GMapPidsMarker[] cameras, GMapPidsMarker sensor) Open()
    {
        var f = new MapVm();
        var (a, la) = MapVm.Image(1, 11, "항공 A", PathA, zOrder: 0);
        var (b, lb) = MapVm.Image(2, 12, "항공 B", PathB, zOrder: 1);
        var cameras = new[]
        {
            Pids(5, 50, EnumDeviceType.IpCamera, 37.4512, 127.0123, "카메라-5"),
            Pids(6, 60, EnumDeviceType.IpCamera, 37.4100, 126.9800, "카메라-6"),
        };
        var sensor = Pids(7, 70, EnumDeviceType.Fence, 37.3950, 126.9650, "센서-7");
        f.Map.Markers.Add(a); f.Map.Markers.Add(b);
        foreach (var c in cameras) f.Map.Markers.Add(c);
        f.Map.Markers.Add(sensor);
        f.Layers.AddRange(new IMapLayerModel[] { la, lb });
        var provider = new Ironwall.Dotnet.Libraries.GMaps.Providers.SymbolProvider();
        foreach (var c in cameras) provider.Add(c.Model);
        provider.Add(sensor.Model);
        Scenario.SetField(f.Vm, "_symbolProvider", provider);
        f.SymDb.Setup(d => d.UpdateSymbolVisibleAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();
        f.Vm.ShowLayerPanel();
        return (f, a, b, cameras, sensor);
    }

    private static IEnumerable<LayerTreeNode> AllNodes(MapVm f)
    {
        IEnumerable<LayerTreeNode> Walk(IEnumerable<LayerTreeNode> nodes) => nodes.SelectMany(n => new[] { n }.Concat(Walk(n.Children)));
        return Walk(f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes"));
    }

    private static LayerTreeNode Category(MapVm f, string name) => AllNodes(f).Single(n => n.NodeType == LayerNodeType.Category && n.Name == name);

    // ── MAP-049 — 잎 우클릭 「지도 가운데로 이동」 → 그 노드로 이동 ──

    [Trait("Scenario", "MAP-049")]
    [Fact]
    public void should_pan_map_to_the_symbol_when_center_on_map_is_chosen_on_symbol_leaf() => Scenario.Sta(() =>
    {
        var (f, _, _, cameras, _) = Open();
        var leaf = AllNodes(f).Single(n => n.Symbol?.Id == 5);
        var before = f.Map.Position;

        Assert.True(leaf.NavigateCommand.CanExecute(null));          // 조회성 — 편집 권한과 무관하게 켜져 있다
        leaf.NavigateCommand.Execute(null);                          // 우클릭 「지도 가운데로 이동」 = 같은 명령(LeafNodeTemplate)

        Assert.NotEqual(before, f.Map.Position);
        Assert.Equal(cameras[0].Position.Lat, f.Map.Position.Lat, 6); // 지도 중심 = 그 심볼
        Assert.Equal(cameras[0].Position.Lng, f.Map.Position.Lng, 6);
        Assert.Contains(f.Log.Lines, l => l.Contains("심볼 이동: 카메라-5"));
    });

    [Trait("Scenario", "MAP-049")]
    [Fact]
    public void should_pan_map_to_the_image_center_when_center_on_map_is_chosen_on_overlay_row() => Scenario.Sta(() =>
    {
        var (f, _, b, _, _) = Open();
        f.Map.Position = new PointLatLng(37.50, 127.10);              // 이미지에서 먼 곳
        var row = AllNodes(f).Single(n => n.Model?.Id == 12);

        row.NavigateCommand.Execute(null);

        Assert.Equal(b.Center.Lat, f.Map.Position.Lat, 6);            // 오버레이 이미지 가운데로
        Assert.Equal(b.Center.Lng, f.Map.Position.Lng, 6);
        Assert.Contains(f.Log.Lines, l => l.Contains("[레이어 이동] 완료: 항공 B"));
    });

    [Trait("Scenario", "MAP-049")]
    [Fact]
    public void should_still_center_on_symbol_when_map_edit_permission_is_missing() => Scenario.Sta(() =>
    {
        // 조회성 명령은 권한 게이트 밖(LayerTreeNode.NavigateCommand 주석) — 비편집 계정도 위치로 갈 수 있다
        var f = new MapVm(canEdit: false);
        var sym = Pids(5, 50, EnumDeviceType.IpCamera, 37.4512, 127.0123, "카메라-5");
        f.Map.Markers.Add(sym);
        var provider = new Ironwall.Dotnet.Libraries.GMaps.Providers.SymbolProvider();
        provider.Add(sym.Model);
        Scenario.SetField(f.Vm, "_symbolProvider", provider);
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();
        f.Vm.ShowLayerPanel();
        var leaf = AllNodes(f).Single(n => n.Symbol?.Id == 5);

        Assert.False(leaf.RenameCommand.CanExecute(null));            // 편집성은 꺼지고
        leaf.NavigateCommand.Execute(null);                           // 이동은 된다
        Assert.Equal(37.4512, f.Map.Position.Lat, 6);
    });

    // ── MAP-056 — 종류 보이기 끔 → 켬 ──

    [Trait("Scenario", "MAP-056")]
    [Fact]
    public void should_hide_then_restore_only_that_kind_and_persist_each_symbol_when_kind_row_is_unchecked_and_checked() => Scenario.Sta(() =>
    {
        var (f, _, _, cameras, sensor) = Open();
        var kind = Category(f, "카메라");
        int ShownCameras() => cameras.Count(c => c.IsVisible && c.IsLayerEnabled);   // 지도 위 「카메라」 DataItem 수의 헤드리스 대응
        Assert.Equal(2, ShownCameras());

        kind.IsChecked = false;                                       // 종류 행 체크 끔(행 체크박스의 양방향 바인딩 대상)

        Assert.Equal(0, ShownCameras());                              // 그 종류 마커 사라짐
        Assert.True(sensor.IsVisible, "다른 종류(센서)까지 사라졌다");
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(5, false, It.IsAny<CancellationToken>()), Times.Once);   // 심볼별 부분 UPDATE(Visible 만)
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(6, false, It.IsAny<CancellationToken>()), Times.Once);
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(7, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        kind.IsChecked = true;                                        // 켬

        Assert.Equal(2, ShownCameras());                              // 돌아옴
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(5, true, It.IsAny<CancellationToken>()), Times.Once);    // DB 복원
        f.SymDb.Verify(d => d.UpdateSymbolVisibleAsync(6, true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.All(cameras, c => Assert.True(c.Visible));
    });

    [Trait("Scenario", "MAP-056")]
    [Trait("Defect", "LAY-D4")]
    [Fact]
    public void should_hide_that_kind_with_a_single_click_on_the_kind_row_checkbox() => Scenario.Sta(() =>
    {
        // 실제 행 체크박스(LayerPanelStyle.xaml NodeRowTemplate)를 한 번 누른다 — 누름은 ToggleButton.OnToggle(자동화 Toggle 과 같은 경로)
        // 결함 LAY-D4: 체크박스가 IsThreeState=True 라 켜짐 → 중간(null)으로 가고, 노드 세터는 null 을 자식에 내리지 않아 아무것도 숨지 않는다.
        //   도움말(MapsHelp "묶음 머리의 체크는 그 묶음 전체를 켜거나 끕니다") · 잎 체크박스(두 상태 — 한 번에 끔)와 어긋난다.
        var (f, _, _, cameras, _) = Open();
        var panel = f.Vm.LayerPanel!;
        panel.Style = (Style)((ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/LayerPanelStyle.xaml", UriKind.Relative)))[typeof(LayerPanelControl)];
        Scenario.Layout(panel, 320, 700);
        var kind = Category(f, "카메라");
        var box = Descendants<CheckBox>(panel).Single(c => ReferenceEquals(c.DataContext, kind));
        Assert.Equal(true, box.IsChecked);

        ((IToggleProvider)new ToggleButtonAutomationPeer(box)).Toggle();   // 한 번 클릭

        Assert.True(cameras.All(c => !c.IsVisible),
            $"켜진 종류 행 체크박스를 한 번 눌렀는데 카메라가 그대로 보인다 — 체크 상태={(box.IsChecked?.ToString() ?? "null(중간)")}, 노드={(kind.IsChecked?.ToString() ?? "null")}");
        Assert.Equal(false, kind.IsChecked);
    });

    // ── MAP-062 — 잎 「위로」/「아래로」 → DB ZOrder 교환 ──

    [Trait("Scenario", "MAP-062")]
    [Fact]
    public void should_swap_zorder_in_one_db_batch_when_overlay_row_menu_down_then_up_is_chosen() => Scenario.Sta(() =>
    {
        var (f, a, b, _, _) = Open();
        var images = AllNodes(f).Single(n => n.OverlayLayerType == "OverlayImage");
        var rowA = images.Children[0];
        Assert.False(rowA.MoveUpCommand.CanExecute(null));            // 맨 위 — 「위로」 꺼짐

        rowA.MoveDownCommand.Execute(null);                           // 우클릭 「아래로」

        Assert.Equal(new[] { "항공 B", "항공 A" }, images.Children.Select(c => c.Name));
        f.Db.Verify(d => d.BatchUpdateMapLayerZOrderAsync(It.Is<IReadOnlyList<(int Id, int ZOrder)>>(r =>
            r.Count == 2 && r[0].Id == 12 && r[0].ZOrder == 0 && r[1].Id == 11 && r[1].ZOrder == 1), It.IsAny<CancellationToken>()), Times.Once);   // 두 행 ZOrder 교환 · 한 번에
        Assert.True(a.ZIndex > b.ZIndex, $"그리기 순서: A={a.ZIndex} B={b.ZIndex}");

        rowA.MoveUpCommand.Execute(null);                             // 우클릭 「위로」 — 되돌림

        Assert.Equal(new[] { "항공 A", "항공 B" }, images.Children.Select(c => c.Name));
        f.Db.Verify(d => d.BatchUpdateMapLayerZOrderAsync(It.Is<IReadOnlyList<(int Id, int ZOrder)>>(r =>
            r.Count == 2 && r[0].Id == 11 && r[0].ZOrder == 0 && r[1].Id == 12 && r[1].ZOrder == 1), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(a.ZIndex < b.ZIndex);
        f.Db.Verify(d => d.UpdateMapLayerAsync(It.IsAny<IMapLayerModel>(), It.IsAny<CancellationToken>()), Times.Never);   // 옛 두 번 개별 UPDATE 경로 아님
    });

    [Trait("Scenario", "MAP-062")]
    [Fact]
    public void should_disable_up_down_and_write_nothing_when_map_edit_permission_is_missing() => Scenario.Sta(() =>
    {
        var f = new MapVm(canEdit: false);
        var (a, la) = MapVm.Image(1, 11, "항공 A", PathA, zOrder: 0);
        var (b, lb) = MapVm.Image(2, 12, "항공 B", PathB, zOrder: 1);
        f.Map.Markers.Add(a); f.Map.Markers.Add(b);
        f.Layers.AddRange(new IMapLayerModel[] { la, lb });
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();
        f.Vm.ShowLayerPanel();
        var images = AllNodes(f).Single(n => n.OverlayLayerType == "OverlayImage");

        Assert.False(images.Children[0].MoveDownCommand.CanExecute(null));
        Assert.False(images.Children[1].MoveUpCommand.CanExecute(null));
        images.Children[0].MoveDownCommand.Execute(null);             // 꺼진 메뉴를 우회해도
        f.Db.Verify(d => d.BatchUpdateMapLayerZOrderAsync(It.IsAny<IReadOnlyList<(int Id, int ZOrder)>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(new[] { "항공 A", "항공 B" }, images.Children.Select(c => c.Name));
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
