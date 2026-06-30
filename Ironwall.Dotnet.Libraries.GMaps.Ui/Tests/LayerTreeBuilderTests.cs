using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

public class LayerTreeBuilderTests
{
    #region Test 1.3: 올바른 계층 구조

    [Fact(DisplayName = "Build — 11개 심볼 → 3 Section, PIDS 그룹 6자식, 독립 5개")]
    public void Build_CreatesCorrectHierarchy()
    {
        // Arrange: 12개 심볼 레이어 (SeedDefault와 동일)
        var layers = CreateDefaultSymbolLayers();

        // Act
        var tree = LayerTreeBuilder.Build(layers);

        // Assert: 3개 Section
        Assert.Equal(3, tree.Count);
        Assert.Equal("OVERLAY MAP", tree[0].Name);
        Assert.Equal("OVERLAY IMAGE", tree[1].Name);
        Assert.Equal("SYMBOLS", tree[2].Name);

        // SYMBOLS Section 자식: PIDS 장비(Group) + 독립 6개 = 7개
        var symbols = tree[2];
        Assert.Equal(7, symbols.Children.Count);

        // PIDS 장비 그룹: 6개 자식
        var pidsGroup = symbols.Children[0];
        Assert.Equal(LayerNodeType.Group, pidsGroup.NodeType);
        Assert.Equal("PIDS 장비", pidsGroup.Name);
        Assert.Equal(6, pidsGroup.Children.Count);

        // 독립 심볼: Leaf 6개
        var independents = symbols.Children.Skip(1).ToList();
        Assert.Equal(6, independents.Count);
        Assert.All(independents, n => Assert.Equal(LayerNodeType.Leaf, n.NodeType));
    }

    #endregion

    #region Test 1.4: 빈 Section은 자식 없음

    [Fact(DisplayName = "Build — OverlayMap 0개 → Section 자식 없음")]
    public void Build_EmptyOverlayMap_NoChildren()
    {
        var layers = CreateDefaultSymbolLayers(); // Symbol만 있음

        var tree = LayerTreeBuilder.Build(layers);

        // OVERLAY MAP Section: 자식 0개
        Assert.Empty(tree[0].Children);
        // OVERLAY IMAGE Section: 자식 0개
        Assert.Empty(tree[1].Children);
    }

    #endregion

    #region Test 1.5: Flatten 유틸리티

    [Fact(DisplayName = "Flatten — 전체 Leaf 노드 12개")]
    public void Flatten_ReturnsAllLeaves()
    {
        var layers = CreateDefaultSymbolLayers();
        var tree = LayerTreeBuilder.Build(layers);

        var leaves = LayerTreeBuilder.Flatten(tree).ToList();

        // 12개 Leaf (PIDS 6 + 독립 6)
        Assert.Equal(12, leaves.Count);
        Assert.All(leaves, n => Assert.Equal(LayerNodeType.Leaf, n.NodeType));
    }

    [Fact(DisplayName = "FindByCategory — PidsCamera 찾기")]
    public void FindByCategory_ReturnsCorrectNode()
    {
        var layers = CreateDefaultSymbolLayers();
        var tree = LayerTreeBuilder.Build(layers);

        var camera = LayerTreeBuilder.FindByCategory(tree, "PidsCamera");

        Assert.NotNull(camera);
        Assert.Equal("카메라", camera!.Name);
    }

    #endregion

    #region Test 1.5: 이미지 모델에서 트리 빌드

    [Fact(DisplayName = "PopulateImageSection — IImageModel 2개 → Leaf 2개, HasOpacitySlider")]
    public void PopulateImageSection_CreatesLeafNodes()
    {
        // Arrange
        var imageSection = LayerTreeNode.CreateSection("OVERLAY IMAGE", "Image");
        var images = new List<IImageModel>
        {
            new ImageModel { Id = 1, Title = "군사지도_A", FilePath = "C:\\Maps\\A.tif", Visibility = true, Opacity = 0.7 },
            new ImageModel { Id = 2, Title = null, FilePath = "C:\\Maps\\drone_B.png", Visibility = false, Opacity = 0.5 },
        };

        // Act
        LayerTreeBuilder.PopulateImageSection(imageSection, images);

        // Assert
        Assert.Equal(2, imageSection.Children.Count);

        var first = imageSection.Children[0];
        Assert.Equal("군사지도_A", first.Name);
        Assert.True(first.IsChecked);
        Assert.Equal(0.7, first.Opacity);
        Assert.True(first.HasOpacitySlider);

        var second = imageSection.Children[1];
        Assert.Equal("drone_B.png", second.Name); // Title null → 파일명
        Assert.False(second.IsChecked);
    }

    #endregion

    #region 개별 심볼 노드화 (FR-02) — ResolveCategoryKey + Build(symbols) + tri-state

    [Theory(DisplayName = "ResolveCategoryKey — PIDS는 DeviceType별 하위카테고리 분기")]
    [InlineData(EnumDeviceType.IpCamera, "PidsCamera")]
    [InlineData(EnumDeviceType.IpSpeaker, "PidsSpeaker")]
    [InlineData(EnumDeviceType.Controller, "PidsController")]
    [InlineData(EnumDeviceType.IoController, "PidsController")]
    [InlineData(EnumDeviceType.Multi, "PidsController")]
    [InlineData(EnumDeviceType.Lamp, "PidsLamp")]
    [InlineData(EnumDeviceType.Enclosure, "PidsEnclosure")]
    [InlineData(EnumDeviceType.Fence, "PidsSensor")]
    [InlineData(EnumDeviceType.Radar, "PidsSensor")]
    public void should_resolve_pids_category_by_devicetype(EnumDeviceType type, string expectedKey)
    {
        var s = new PidsSymbolModel { Id = 1, DeviceType = type, Category = EnumMarkerCategory.PIDS_EQUIPMENT };
        Assert.Equal(expectedKey, LayerTreeBuilder.ResolveCategoryKey(s));
    }

    [Theory(DisplayName = "ResolveCategoryKey — 비PIDS는 EnumMarkerCategory 직매핑(VEHICLES 포함)")]
    [InlineData(EnumMarkerCategory.MILITARY_SYMBOLS, "Military")]
    [InlineData(EnumMarkerCategory.GEOMETRICS, "Geometric")]
    [InlineData(EnumMarkerCategory.AREA_BOUNDARY, "Line")]
    [InlineData(EnumMarkerCategory.INFRASTRUCTURE, "Infra")]
    [InlineData(EnumMarkerCategory.VEHICLES, "Vehicle")]
    [InlineData(EnumMarkerCategory.BASIC_SHAPES, "Basic")]
    public void should_resolve_nonpids_category_by_enum(EnumMarkerCategory cat, string expectedKey)
    {
        var s = new SymbolModel { Id = 1, Category = cat };
        Assert.Equal(expectedKey, LayerTreeBuilder.ResolveCategoryKey(s));
    }

    [Fact(DisplayName = "Build(symbols) — 카테고리 노드 아래 개별 심볼 자식 + 카운트, 빈 카테고리 숨김")]
    public void should_nest_individual_symbols_when_symbols_provided()
    {
        var layers = CreateDefaultSymbolLayers();
        var symbols = new List<ISymbolModel>
        {
            new PidsSymbolModel { Id = 101, Title = "정문 카메라", DeviceType = EnumDeviceType.IpCamera, Category = EnumMarkerCategory.PIDS_EQUIPMENT, ShowShape = true },
            new PidsSymbolModel { Id = 102, Title = "후문 카메라", DeviceType = EnumDeviceType.IpCamera, Category = EnumMarkerCategory.PIDS_EQUIPMENT, ShowShape = true },
            new PidsSymbolModel { Id = 103, Title = "펜스01",     DeviceType = EnumDeviceType.Fence,    Category = EnumMarkerCategory.PIDS_EQUIPMENT, ShowShape = true },
            new SymbolModel    { Id = 201, Title = "적 보병",     Category = EnumMarkerCategory.MILITARY_SYMBOLS, ShowShape = true },
        };

        var tree = LayerTreeBuilder.Build(layers, symbols);
        var symbolsSection = tree[2];
        var pidsGroup = symbolsSection.Children.First(n => n.Name == "PIDS 장비");

        var camera = pidsGroup.Children.First(c => c.Name == "카메라");
        Assert.Equal(LayerNodeType.Category, camera.NodeType);
        Assert.Equal(2, camera.Children.Count);
        Assert.Equal(2, camera.ItemCount);
        Assert.All(camera.Children, leaf => Assert.True(leaf.IsSymbolLeaf));
        Assert.Contains(camera.Children, leaf => leaf.Name == "정문 카메라");

        Assert.Single(pidsGroup.Children.First(c => c.Name == "센서").Children);
        Assert.DoesNotContain(pidsGroup.Children, c => c.Name == "스피커");  // 0개 → 숨김

        var military = symbolsSection.Children.First(c => c.Name == "군사 심볼");
        Assert.Equal(LayerNodeType.Category, military.NodeType);
        Assert.Single(military.Children);
    }

    [Fact(DisplayName = "Build(symbols) — Title 공백 심볼은 '{카테고리} #{Id}' 폴백")]
    public void should_fallback_symbol_name_when_title_blank()
    {
        var symbols = new List<ISymbolModel>
        {
            new PidsSymbolModel { Id = 57, Title = "", DeviceType = EnumDeviceType.IpCamera, Category = EnumMarkerCategory.PIDS_EQUIPMENT, ShowShape = true },
        };
        var tree = LayerTreeBuilder.Build(CreateDefaultSymbolLayers(), symbols);
        var leaf = LayerTreeBuilder.Flatten(tree).First(n => n.IsSymbolLeaf);
        Assert.Equal("카메라 #57", leaf.Name);
    }

    [Fact(DisplayName = "tri-state — 카테고리 OFF → 자식 심볼 cascade OFF + ShowShape 동기")]
    public void should_cascade_category_toggle_to_symbol_children()
    {
        var symbols = new List<ISymbolModel>
        {
            new PidsSymbolModel { Id = 1, Title = "c1", DeviceType = EnumDeviceType.IpCamera, Category = EnumMarkerCategory.PIDS_EQUIPMENT, ShowShape = true },
            new PidsSymbolModel { Id = 2, Title = "c2", DeviceType = EnumDeviceType.IpCamera, Category = EnumMarkerCategory.PIDS_EQUIPMENT, ShowShape = true },
        };
        var tree = LayerTreeBuilder.Build(CreateDefaultSymbolLayers(), symbols);
        var camera = tree[2].Children.First(n => n.Name == "PIDS 장비").Children.First(c => c.Name == "카메라");

        camera.IsChecked = false;   // 카테고리 마스터 OFF → cascade

        Assert.All(camera.Children, leaf => Assert.False(leaf.IsChecked));
        Assert.All(camera.Children, leaf => Assert.False(leaf.Symbol!.ShowShape));   // ShowShape 영속필드 동기(FR-03)
    }

    [Fact(DisplayName = "Build(symbols=null) — 레거시 카테고리 단위 Leaf 유지(무회귀)")]
    public void should_keep_legacy_category_leaves_when_no_symbols()
    {
        var tree = LayerTreeBuilder.Build(CreateDefaultSymbolLayers(), null);
        var symbolsSection = tree[2];
        Assert.Equal(7, symbolsSection.Children.Count);   // PIDS 그룹 + 독립 6
        Assert.All(symbolsSection.Children[0].Children, c => Assert.Equal(LayerNodeType.Leaf, c.NodeType));
    }

    #endregion

    #region Helpers

    private static List<IMapLayerModel> CreateDefaultSymbolLayers()
    {
        var categories = new[]
        {
            "PidsCamera", "PidsSensor", "PidsSpeaker",
            "PidsController", "PidsLamp", "PidsEnclosure",
            "PidsGroup", "Military", "Geometric", "Line", "Infra", "Basic"
        };

        return categories.Select((cat, i) => (IMapLayerModel)new MapLayerModel
        {
            Id = i + 1,
            Name = cat,
            LayerType = "Symbol",
            Category = cat,
            IsVisible = true,
            Opacity = 1.0,
            ZOrder = i
        }).ToList();
    }

    #endregion
}
