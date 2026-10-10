using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 카탈로그 §1 레이어 패널(MAP-052 ~ MAP-067) 헤드리스 짝 — 트리가 지도 심볼 수 · 보임 · 잠금 · 투명도를 그대로 싣고,
/// 바꾼 값이 심볼 모델(영속 대상)과 지도 마커로 내려가는가. 결함 D3(오버레이 이미지 투명도 재시작 뒤 어긋남) 회귀 포함.
/// </summary>
public class LayerPanelMapStateTests
{
    private static PidsSymbolModel Pids(int id, EnumDeviceType type, string title) => new() { Id = id, DeviceType = type, Title = title, Category = EnumMarkerCategory.PIDS_EQUIPMENT, Visible = true };
    private static GeometricSymbolModel Shape(int id, string title) => new() { Id = id, Title = title, Category = EnumMarkerCategory.GEOMETRICS, Visible = true };

    private static List<ISymbolModel> Symbols() => new()
    {
        Pids(1, EnumDeviceType.IpCamera, "카메라-1"), Pids(2, EnumDeviceType.IpCamera, "카메라-2"), Pids(3, EnumDeviceType.Fence, "센서-1"),
        Pids(4, EnumDeviceType.IpSpeaker, "스피커-1"), Shape(5, "원"), Shape(6, "원"), Shape(7, "사각"),
    };

    private static LayerTreeNode Section(IEnumerable<LayerTreeNode> tree, string name) => tree.Single(n => n.Name == name);
    private static LayerTreeNode Node(LayerTreeNode parent, string name) => parent.Children.Single(n => n.Name == name);

    // ── MAP-052 · MAP-053 · MAP-055 — 섹션 · 빈 섹션 · 수 배지 ──

    [Trait("Scenario", "MAP-052,MAP-055")]
    [Fact]
    public void should_show_three_sections_in_order_and_count_badge_equal_to_symbols_per_kind()
    {
        var tree = LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), Symbols());

        Assert.Equal(new[] { "오버레이 지도", "오버레이 이미지", "심볼" }, tree.Select(n => n.Name));
        Assert.Empty(tree[0].Children);   // 빈 섹션도 남는다(「등록된 항목이 없습니다」 자리)
        Assert.Empty(tree[1].Children);

        var symbols = Section(tree, "심볼");
        var pids = Node(symbols, "PIDS 장비");
        Assert.Equal(2, Node(pids, "카메라").ItemCount);
        Assert.Equal(1, Node(pids, "센서").ItemCount);
        Assert.Equal(1, Node(pids, "스피커").ItemCount);
        Assert.Equal(4, pids.ItemCount);                                     // 그룹 배지 = 아래 종류 합
        Assert.Equal(3, Node(symbols, "기하학 도형").ItemCount);
        Assert.DoesNotContain(pids.Children, c => c.Name is "컨트롤러" or "조명" or "함체");   // 0개 종류는 숨김
        // 배지 합 = 지도 위 심볼 수
        Assert.Equal(Symbols().Count, LayerTreeBuilder.Flatten(tree).Count(l => l.Symbol != null));
    }

    [Fact]
    public void should_suffix_id_only_for_duplicate_titles_so_rows_are_distinguishable()
    {
        var tree = LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), Symbols());
        var shapes = Node(Section(tree, "심볼"), "기하학 도형").Children.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "사각", "원 #5", "원 #6" }, shapes);
    }

    // ── MAP-056 · MAP-057 · MAP-058 — 종류 · 섹션 머리 · 잎 보이기 ──

    [Trait("Scenario", "MAP-057")]
    [Fact]
    public void should_hide_every_symbol_and_raise_each_leaf_when_symbols_section_header_is_unchecked()
    {
        var symbols = Symbols();
        var tree = LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), symbols);
        var leaves = LayerTreeBuilder.Flatten(tree).Where(l => l.Symbol != null).ToList();
        var raised = new List<LayerTreeNode>();
        foreach (var leaf in leaves) leaf.CheckChanged += (s, _) => raised.Add((LayerTreeNode)s!);

        Section(tree, "심볼").IsChecked = false;

        Assert.All(symbols, s => Assert.False(s.Visible));                 // 마스터 가시성(영속) 전원 끔
        Assert.Equal(leaves.Count, raised.Distinct().Count());              // 잎마다 한 번씩 → 지도 마커 토글(OnSymbolVisibilityChanged)

        Section(tree, "심볼").IsChecked = true;
        Assert.All(symbols, s => Assert.True(s.Visible));
    }

    [Trait("Scenario", "MAP-058")]
    [Fact]
    public void should_make_parent_indeterminate_and_persist_only_that_symbol_when_one_leaf_is_unchecked()
    {
        var symbols = Symbols();
        var tree = LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), symbols);
        var cameras = Node(Node(Section(tree, "심볼"), "PIDS 장비"), "카메라");
        cameras.IsChecked = true;

        cameras.Children[0].IsChecked = false;

        Assert.Null(cameras.IsChecked);                                      // 일부만 켜짐 → 3상태
        Assert.False(symbols.Single(s => s.Id == cameras.Children[0].Symbol!.Id).Visible);
        Assert.True(symbols.Single(s => s.Id == cameras.Children[1].Symbol!.Id).Visible);
        Assert.All(symbols.Where(s => s is not PidsSymbolModel { DeviceType: EnumDeviceType.IpCamera }), s => Assert.True(s.Visible));
    }

    [Fact]
    public void should_start_unchecked_and_locked_from_saved_symbol_state_when_tree_is_rebuilt()
    {
        // 다시 불러와도 같은가(MAP-058/059 영속) — 저장된 Visible=false · IsLocked=true 가 트리에 그대로
        var symbols = Symbols();
        symbols[0].Visible = false; symbols[0].IsLocked = true;
        var tree = LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), symbols);
        var leaf = LayerTreeBuilder.Flatten(tree).Single(l => l.Symbol?.Id == 1);
        Assert.False(leaf.IsChecked);
        Assert.True(leaf.IsLocked);
    }

    // ── MAP-067 — 오버레이 행 투명도 ──

    [Trait("Scenario", "MAP-067")]
    [Fact]
    public void should_write_opacity_to_layer_model_and_raise_once_when_overlay_row_slider_moves()
    {
        var layer = new MapLayerModel { Id = 9, Name = "안양 항공", LayerType = "OverlayImage", Category = "OverlayImage", IsVisible = true, Opacity = 1.0, FilePath = @"C:\maps\anyang.png" };
        var tree = LayerTreeBuilder.Build(new IMapLayerModel[] { layer }, Symbols());
        var row = Assert.Single(Section(tree, "오버레이 이미지").Children);
        Assert.True(row.HasOpacitySlider);
        int raised = 0;
        row.OpacityChanged += (_, _) => raised++;

        row.Opacity = 0.45;
        row.Opacity = 0.4504;   // 0.001 미만 흔들림은 무시(드래그 중 DB 폭주 방지)

        Assert.Equal(0.45, layer.Opacity, 3);
        Assert.Equal(1, raised);
    }

    // ── 결함 D3 — 재시작 뒤 오버레이 이미지 투명도 ──

    [Trait("Scenario", "MAP-067")]
    [Fact]
    public void should_restore_overlay_image_opacity_visibility_and_order_from_layer_row_when_map_reloads() => HousingTests.Sta(() =>
    {
        // Images 행은 옛 투명도(1.0) — 레이어 패널 슬라이더는 MapLayers 에만 0.35 를 저장했다
        var image = new ImageModel { Id = 3, Title = "안양 항공", FilePath = @"C:\maps\anyang.png", Left = 126.9, Right = 127.0, Top = 37.45, Bottom = 37.35, Opacity = 1.0, Visibility = true, Width = 200, Height = 150 };
        var marker = new GMapImageMarker(Mock.Of<ILogService>(), image);
        var layer = new MapLayerModel { Id = 9, LayerType = "OverlayImage", FilePath = image.FilePath, IsVisible = false, Opacity = 0.35, ZOrder = 7 };

        MapViewModel.ApplyOverlayImageLayerState(marker, layer);

        Assert.Equal(0.35, marker.Opacity, 3);         // D3 전에는 1.0 그대로(슬라이더 35% · 그림 100%)
        Assert.Equal(0.35, image.Opacity, 3);          // 다음 저장에 같은 값이 실린다
        Assert.False(marker.IsVisible);
        Assert.False(marker.IsLayerEnabled);
        Assert.Equal(7, ((IEditableMarker)marker).ZOrder);
    });
}
