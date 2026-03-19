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
        // Arrange: 11개 심볼 레이어 (SeedDefault와 동일)
        var layers = CreateDefaultSymbolLayers();

        // Act
        var tree = LayerTreeBuilder.Build(layers);

        // Assert: 3개 Section
        Assert.Equal(3, tree.Count);
        Assert.Equal("OVERLAY MAP", tree[0].Name);
        Assert.Equal("OVERLAY IMAGE", tree[1].Name);
        Assert.Equal("SYMBOLS", tree[2].Name);

        // SYMBOLS Section 자식: PIDS 장비(Group) + 독립 5개 = 6개
        var symbols = tree[2];
        Assert.Equal(6, symbols.Children.Count);

        // PIDS 장비 그룹: 6개 자식
        var pidsGroup = symbols.Children[0];
        Assert.Equal(LayerNodeType.Group, pidsGroup.NodeType);
        Assert.Equal("PIDS 장비", pidsGroup.Name);
        Assert.Equal(6, pidsGroup.Children.Count);

        // 독립 심볼: Leaf 5개
        var independents = symbols.Children.Skip(1).ToList();
        Assert.Equal(5, independents.Count);
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

    [Fact(DisplayName = "Flatten — 전체 Leaf 노드 11개")]
    public void Flatten_ReturnsAllLeaves()
    {
        var layers = CreateDefaultSymbolLayers();
        var tree = LayerTreeBuilder.Build(layers);

        var leaves = LayerTreeBuilder.Flatten(tree).ToList();

        // 11개 Leaf (PIDS 6 + 독립 5)
        Assert.Equal(11, leaves.Count);
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

    #region Helpers

    private static List<IMapLayerModel> CreateDefaultSymbolLayers()
    {
        var categories = new[]
        {
            "PidsCamera", "PidsSensor", "PidsSpeaker",
            "PidsController", "PidsLamp", "PidsEnclosure",
            "PidsGroup", "Military", "Geometric", "Line", "Infra"
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
