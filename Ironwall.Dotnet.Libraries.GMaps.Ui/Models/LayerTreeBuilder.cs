using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// DB의 flat IMapLayerModel 목록 + _symbolProvider의 개별 ISymbolModel 을 3~4-Tier 트리 구조로 변환.
///
/// 트리 구조(symbols 전달 시):
/// ├── OVERLAY MAP (Section)
/// │   └── [오버레이 맵 아이템들] (Leaf, Opacity 슬라이더)
/// ├── OVERLAY IMAGE (Section)
/// │   └── [오버레이 이미지 아이템들] (Leaf, Opacity 슬라이더)
/// └── SYMBOLS (Section)
///     ├── PIDS 장비 (Group)
///     │   ├── 카메라 (Category) → [개별 카메라 심볼들 (SymbolLeaf)]
///     │   ├── 센서   (Category) → [개별 센서 심볼들]
///     │   └── …
///     ├── PIDS 그룹 (Category) → […]
///     ├── 군사 심볼 (Category) → […]
///     └── …
///
/// symbols == null 이면 구(舊) 동작(카테고리 단위 Leaf, 개별 심볼 없음) — 기존 호출/테스트 호환.
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

        // 독립 심볼 카테고리
        ["PidsGroup"]      = ("PIDS 그룹",  "Group",           ""),
        ["Military"]       = ("군사 심볼",   "Shield",          ""),
        ["Geometric"]      = ("기하학 도형", "ShapeOutline",    ""),
        ["Line"]           = ("선·경계",     "VectorLine",      ""),
        ["Infra"]          = ("인프라·시설", "OfficeBuildingOutline", ""),
        ["Vehicle"]        = ("차량",        "CarOutline",      ""),   // FR-02: EnumMarkerCategory.VEHICLES 매핑(누락 보강)
        ["Basic"]          = ("핀(단일)",    "MapMarkerOutline",""),
        ["Other"]          = ("기타",        "DotsHorizontal",  ""),   // 미매핑 폴백 버킷
    };

    /// <summary>독립 카테고리 표시 순서(결정적). PIDS 장비 그룹은 별도.</summary>
    private static readonly string[] IndependentOrder =
        { "PidsGroup", "Military", "Geometric", "Line", "Infra", "Vehicle", "Basic", "Other" };

    private static readonly string[] PidsOrder =
        { "PidsCamera", "PidsSensor", "PidsSpeaker", "PidsController", "PidsLamp", "PidsEnclosure" };

    #endregion

    #region Symbol → Category 귀속

    /// <summary>
    /// 개별 ISymbolModel 을 CategoryMap 키로 귀속.
    /// PIDS는 Category가 모두 PIDS_EQUIPMENT 단일값이라 DeviceType으로 6 하위카테고리 분기(FR-02 핵심).
    /// </summary>
    public static string ResolveCategoryKey(ISymbolModel s)
    {
        if (s is IPidsGroupSymbolModel) return "PidsGroup";
        if (s is IPidsSymbolModel pids)
        {
            return pids.DeviceType switch
            {
                EnumDeviceType.IpCamera => "PidsCamera",
                EnumDeviceType.IpSpeaker => "PidsSpeaker",
                EnumDeviceType.Controller or EnumDeviceType.IoController or EnumDeviceType.Multi => "PidsController",
                EnumDeviceType.Lamp => "PidsLamp",
                EnumDeviceType.Enclosure => "PidsEnclosure",
                _ => "PidsSensor",   // Fence/Underground/Contact/PIR/Laser/Cable/SmartSensor*/Radar/OpticalCable 등
            };
        }
        return s.Category switch
        {
            EnumMarkerCategory.BASIC_SHAPES => "Basic",
            EnumMarkerCategory.GEOMETRICS => "Geometric",
            EnumMarkerCategory.VEHICLES => "Vehicle",
            EnumMarkerCategory.MILITARY_SYMBOLS => "Military",
            EnumMarkerCategory.AREA_BOUNDARY => "Line",
            EnumMarkerCategory.INFRASTRUCTURE => "Infra",
            EnumMarkerCategory.PIDS_EQUIPMENT => "PidsSensor",   // 방어: PIDS인데 IPidsSymbolModel 아님
            _ => "Other",
        };
    }

    #endregion

    /// <summary>기존 시그니처 호환 — 개별 심볼 없이 카테고리 단위 Leaf만 빌드.</summary>
    public static ObservableCollection<LayerTreeNode> Build(IEnumerable<IMapLayerModel> layers)
        => Build(layers, null);

    /// <summary>
    /// DB 레이어 + 개별 심볼 → 트리 구조 변환. symbols != null 이면 카테고리 노드 아래 개별 심볼 자식 생성(FR-02).
    /// </summary>
    public static ObservableCollection<LayerTreeNode> Build(
        IEnumerable<IMapLayerModel> layers,
        IEnumerable<ISymbolModel>? symbols)
    {
        var result = new ObservableCollection<LayerTreeNode>();
        var layerList = layers.ToList();

        // Section 1: OVERLAY MAP
        var overlayMapSection = LayerTreeNode.CreateSection("OVERLAY MAP", "Map");
        foreach (var layer in layerList.Where(l => l.LayerType == "OverlayMap"))
            overlayMapSection.AddChild(LayerTreeNode.FromModel(layer, layer.Name ?? "지도", "Map"));
        result.Add(overlayMapSection);

        // Section 2: OVERLAY IMAGE
        var overlayImageSection = LayerTreeNode.CreateSection("OVERLAY IMAGE", "Image");
        foreach (var layer in layerList.Where(l => l.LayerType == "OverlayImage"))
            overlayImageSection.AddChild(LayerTreeNode.FromModel(layer, layer.Name ?? "이미지", "Image"));
        result.Add(overlayImageSection);

        // Section 3: SYMBOLS
        var symbolsSection = LayerTreeNode.CreateSection("SYMBOLS", "Drawing");
        var symbolLayers = layerList.Where(l => l.LayerType == "Symbol").ToList();

        if (symbols == null)
            BuildLegacyCategoryLeaves(symbolsSection, symbolLayers);
        else
            BuildCategoryNodesWithSymbols(symbolsSection, symbolLayers, symbols.ToList());

        result.Add(symbolsSection);
        return result;
    }

    #region 신(新) — 카테고리 노드 + 개별 심볼 자식

    private static void BuildCategoryNodesWithSymbols(
        LayerTreeNode symbolsSection, List<IMapLayerModel> symbolLayers, List<ISymbolModel> symbols)
    {
        // 심볼을 카테고리 키로 그룹화
        var byCategory = symbols
            .GroupBy(ResolveCategoryKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 카테고리 토글/영속용 레이어 매핑(있으면 Model attach)
        var layerByKey = symbolLayers
            .Where(l => !string.IsNullOrEmpty(l.Category))
            .GroupBy(l => l.Category!)
            .ToDictionary(g => g.Key, g => g.First());

        // PIDS 장비 그룹
        var pidsGroup = LayerTreeNode.CreateGroup("PIDS 장비", "Cctv", isExpanded: true);
        foreach (var key in PidsOrder)
        {
            var node = BuildCategoryNode(key, byCategory, layerByKey);
            if (node != null) pidsGroup.AddChild(node);
        }
        if (pidsGroup.Children.Count > 0)
        {
            pidsGroup.ItemCount = pidsGroup.Children.Sum(c => c.ItemCount);
            symbolsSection.AddChild(pidsGroup);
        }

        // 독립 카테고리
        foreach (var key in IndependentOrder)
        {
            var node = BuildCategoryNode(key, byCategory, layerByKey);
            if (node != null) symbolsSection.AddChild(node);
        }
    }

    /// <summary>
    /// 카테고리 노드 + 개별 심볼 자식 생성. 심볼 0개면 null(빈 카테고리 숨김, FR-07).
    /// v1: 자식을 즉시 생성(구독 안전) + 기본 접힘으로 렌더 비용 지연.
    /// </summary>
    private static LayerTreeNode? BuildCategoryNode(
        string key,
        Dictionary<string, List<ISymbolModel>> byCategory,
        Dictionary<string, IMapLayerModel> layerByKey)
    {
        if (!byCategory.TryGetValue(key, out var syms) || syms.Count == 0) return null;
        if (!CategoryMap.TryGetValue(key, out var meta)) meta = ("기타", "DotsHorizontal", "");

        layerByKey.TryGetValue(key, out var model);
        var cat = LayerTreeNode.CreateCategory(meta.DisplayName, meta.IconKind, key, model, isExpanded: false);

        // 중복 Title 식별(접미 #Id 표기용)
        var dupTitles = syms
            .Where(s => !string.IsNullOrWhiteSpace(s.Title))
            .GroupBy(s => s.Title)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        foreach (var s in syms.OrderBy(s => s.Title))
        {
            string name = string.IsNullOrWhiteSpace(s.Title)
                ? $"{meta.DisplayName} #{s.Id}"                      // 공백 폴백
                : (dupTitles.Contains(s.Title) ? $"{s.Title} #{s.Id}" : s.Title);  // 중복 접미
            cat.AddChild(LayerTreeNode.CreateSymbolLeaf(s, name, meta.IconKind));
        }
        cat.ItemCount = cat.Children.Count;
        return cat;
    }

    #endregion

    #region 구(舊) — 카테고리 단위 Leaf (호환)

    private static void BuildLegacyCategoryLeaves(LayerTreeNode symbolsSection, List<IMapLayerModel> symbolLayers)
    {
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

        foreach (var layer in symbolLayers.Where(l => CategoryMap.ContainsKey(l.Category ?? "") && CategoryMap[l.Category!].GroupKey == ""))
        {
            var (displayName, iconKind, _) = CategoryMap[layer.Category!];
            symbolsSection.AddChild(LayerTreeNode.FromModel(layer, displayName, iconKind));
        }
    }

    #endregion

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
