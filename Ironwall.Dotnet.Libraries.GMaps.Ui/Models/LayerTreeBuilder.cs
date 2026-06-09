using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// DB의 flat IMapLayerModel 목록을 3-Tier 트리 구조로 변환.
///
/// 트리 구조:
/// ├── OVERLAY MAP (Section)
/// │   └── [오버레이 맵 아이템들] (Leaf, Opacity 슬라이더)
/// ├── OVERLAY IMAGE (Section)
/// │   └── [오버레이 이미지 아이템들] (Leaf, Opacity 슬라이더)
/// └── SYMBOLS (Section)
///     ├── PIDS 장비 (Group, 접기/펼치기)
///     │   ├── 카메라 (Leaf)
///     │   ├── 센서 (Leaf)
///     │   ├── 스피커 (Leaf)
///     │   ├── 컨트롤러 (Leaf)
///     │   ├── 조명 (Leaf)
///     │   └── 함체 (Leaf)
///     ├── PIDS 그룹 (Leaf)
///     ├── 군사 심볼 (Leaf)
///     ├── 기하학 도형 (Leaf)
///     ├── 선/경계 (Leaf)
///     └── 인프라/시설 (Leaf)
/// </summary>
public static class LayerTreeBuilder
{
    #region Category → Display Mapping

    private static readonly Dictionary<string, (string DisplayName, string IconKind, string GroupKey)> CategoryMap = new()
    {
        // PIDS 장비 그룹
        ["PidsCamera"]     = ("카메라",     "Camera",          "PidsEquipment"),
        ["PidsSensor"]     = ("센서",       "AccessPoint",     "PidsEquipment"),
        ["PidsSpeaker"]    = ("스피커",     "Speaker",         "PidsEquipment"),
        ["PidsController"] = ("컨트롤러",   "Router",          "PidsEquipment"),
        ["PidsLamp"]       = ("조명",       "LightbulbOutline","PidsEquipment"),
        ["PidsEnclosure"]  = ("함체",       "PackageVariant",  "PidsEquipment"),

        // 독립 심볼
        ["Basic"]          = ("핀(단일)",    "MapMarkerOutline",""),
        ["PidsGroup"]      = ("PIDS 그룹",  "Group",           ""),
        ["Military"]       = ("군사 심볼",   "Shield",          ""),
        ["Geometric"]      = ("기하학 도형", "ShapeOutline",    ""),
        ["Line"]           = ("선/경계",     "VectorLine",      ""),
        ["Infra"]          = ("인프라/시설", "OfficeBuildingOutline", ""),
    };

    #endregion

    /// <summary>
    /// DB 레이어 목록 → 3-Tier 트리 구조 변환
    /// </summary>
    public static ObservableCollection<LayerTreeNode> Build(IEnumerable<IMapLayerModel> layers)
    {
        var result = new ObservableCollection<LayerTreeNode>();
        var layerList = layers.ToList();

        // Section 1: OVERLAY MAP
        var overlayMapSection = LayerTreeNode.CreateSection("OVERLAY MAP", "Map");
        foreach (var layer in layerList.Where(l => l.LayerType == "OverlayMap"))
        {
            overlayMapSection.AddChild(LayerTreeNode.FromModel(layer, layer.Name ?? "지도", "Map"));
        }
        result.Add(overlayMapSection);

        // Section 2: OVERLAY IMAGE
        var overlayImageSection = LayerTreeNode.CreateSection("OVERLAY IMAGE", "Image");
        foreach (var layer in layerList.Where(l => l.LayerType == "OverlayImage"))
        {
            overlayImageSection.AddChild(LayerTreeNode.FromModel(layer, layer.Name ?? "이미지", "Image"));
        }
        result.Add(overlayImageSection);

        // Section 3: SYMBOLS
        var symbolsSection = LayerTreeNode.CreateSection("SYMBOLS", "Drawing");
        var symbolLayers = layerList.Where(l => l.LayerType == "Symbol").ToList();

        // PIDS 장비 그룹 (자식이 있는 그룹)
        var pidsGroup = LayerTreeNode.CreateGroup("PIDS 장비", "Cctv", isExpanded: true);
        foreach (var layer in symbolLayers.Where(l => CategoryMap.ContainsKey(l.Category ?? "") && CategoryMap[l.Category!].GroupKey == "PidsEquipment"))
        {
            var (displayName, iconKind, _) = CategoryMap[layer.Category!];
            pidsGroup.AddChild(LayerTreeNode.FromModel(layer, displayName, iconKind));
        }
        if (pidsGroup.Children.Count > 0)
        {
            pidsGroup.ItemCount = pidsGroup.Children.Count;
            symbolsSection.AddChild(pidsGroup);
        }

        // 독립 심볼 (그룹 없이 직접 Section에 추가)
        foreach (var layer in symbolLayers.Where(l => CategoryMap.ContainsKey(l.Category ?? "") && CategoryMap[l.Category!].GroupKey == ""))
        {
            var (displayName, iconKind, _) = CategoryMap[layer.Category!];
            symbolsSection.AddChild(LayerTreeNode.FromModel(layer, displayName, iconKind));
        }

        result.Add(symbolsSection);

        return result;
    }

    /// <summary>
    /// IImageModel 목록에서 이미지 Section의 자식 노드 빌드
    /// </summary>
    public static void PopulateImageSection(LayerTreeNode imageSection, IEnumerable<IImageModel> images)
    {
        foreach (var img in images)
        {
            var node = new LayerTreeNode
            {
                Name = img.Title ?? Path.GetFileName(img.FilePath ?? "이미지"),
                IconKind = "Image",
                IsChecked = img.Visibility,
                Opacity = img.Opacity,
                Category = $"Image_{img.Id}",
                NodeType = LayerNodeType.Leaf,
                HasOpacitySlider = true,
            };
            imageSection.AddChild(node);
        }
    }

    /// <summary>
    /// 트리에서 모든 Leaf 노드를 평탄화하여 반환
    /// </summary>
    public static IEnumerable<LayerTreeNode> Flatten(IEnumerable<LayerTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == LayerNodeType.Leaf)
                yield return node;

            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    /// <summary>
    /// 카테고리명으로 특정 Leaf 노드 찾기
    /// </summary>
    public static LayerTreeNode? FindByCategory(IEnumerable<LayerTreeNode> nodes, string category)
    {
        return Flatten(nodes).FirstOrDefault(n => n.Category == category);
    }
}
