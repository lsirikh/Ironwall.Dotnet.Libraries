using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 레이어 패널 선택 ↔ 지도 편집 선택 양방향 연동(편집 모드에서만).
                  패널 → 지도: 고른 잎의 마커를 지도 선택 깔때기(ApplyGroupSelectionByIds — 1개 = 단일 편집 · 2개 이상 = 묶음)로.
                  지도 → 패널: 단일 선택(SelectedMarker) · 묶음 선택(GroupSelectionService.SelectionChanged)이 바뀌면 잎 IsSelected 를
                  맞추고, 새로 고른 행의 접힌 묶음을 펼쳐 보이게 굴린다.
   Note         : ★ 되먹임 없음 — 패널은 사용자 조작에만 LayerSelectionChanged 를 내고, VM 은 노드 값을 직접(LayerSelection.Apply) 바꾼다.
                    패널 → 지도 적용 중(_isApplyingLayerSelection)에는 지도 → 패널 동기를 건너뛰고, 끝에 한 번 실제 지도 선택을 비춘다.
                  ★ 지도에서 고를 수 없는 것(잠김 · 숨김 — Ctrl+A 와 같은 규칙)은 지도로 가지 않고, 패널도 지도 선택을 비추므로 풀린다.
                  ★ 지도 마커가 없는 잎(오버레이 지도 등)은 지도 → 패널 동기가 건드리지 않는다(패널 안 선택 유지).
                  편집 모드가 아니면 패널 선택은 패널 안에서만 산다(지도 편집 선택과 무관 — 예전 동작).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private bool _isApplyingLayerSelection;

    /// <summary>패널에서 사용자가 선택을 바꿨다 — 편집 모드면 지도 선택으로 옮긴다.</summary>
    private void OnLayerSelectionChanged(object? sender, LayerSelectionChangedEventArgs e)
    {
        if (!IsEditModeEnabled) return;
        PushLayerSelectionToMap();
    }

    /// <summary>패널 선택 → 지도 선택(잠김 · 숨김 · 마커 없음 제외). 끝나면 실제 지도 선택을 패널에 비춘다(되먹임 없이).</summary>
    private void PushLayerSelectionToMap()
    {
        if (MainMap == null || _layerTreeNodes == null) return;
        var ids = new HashSet<(bool isImage, int id)>();
        foreach (var leaf in LayerSelection.SelectedLeaves(_layerTreeNodes))
            if (FindMarkerForLayerLeaf(leaf) is { } m && !m.IsLocked && m.IsLayerEnabled && m.Id > 0
                // 줌으로 안 보이는 심볼도 뺀다(Ctrl+A 와 같은 규칙). 오버레이 이미지 행은 그대로 넣는다(이미지 제외 규칙은 여기 쓰지 않음).
                && (m is GMapImageMarker || Helpers.ZoomLadder.IsVisibleAtEffectiveZoom(MainMap.EffectiveZoom, m.Zoom)))
                ids.Add((m is GMapImageMarker, m.Id));

        _isApplyingLayerSelection = true;
        try { ApplyGroupSelectionByIds(ids); }
        finally { _isApplyingLayerSelection = false; }
        SyncLayerSelectionFromMap(reveal: false);
    }

    /// <summary>
    /// 지도 선택 → 패널 잎 선택(편집 모드에서만). <paramref name="reveal"/> 이면 새로 고른 행의 접힌 묶음을 펼치고 첫 행이 보이게 굴린다.
    /// 지도 마커가 없는 잎은 그대로 둔다.
    /// </summary>
    private void SyncLayerSelectionFromMap(bool reveal)
    {
        if (_isApplyingLayerSelection || !IsEditModeEnabled || _layerTreeNodes == null || MainMap == null) return;
        var ids = CurrentSelectionIds();
        var newly = new List<LayerTreeNode>();
        LayerSelection.Apply(() =>
        {
            foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes))
            {
                bool? want = leaf.IsSymbolLeaf ? ids.Contains((false, leaf.Symbol!.Id))
                    : FindMarkerForLayerLeaf(leaf) is { } m ? ids.Contains((true, m.Id))
                    : null;
                if (want is not bool w || w == leaf.IsSelected) continue;
                leaf.IsSelected = w;
                if (w) newly.Add(leaf);
            }
        });
        if (!reveal || newly.Count == 0) return;
        LayerSelection.ExpandToShow(newly);
        LayerPanel?.BringNodeIntoView(newly[0]);
    }

    private void OnGroupSelectionChanged() => SyncLayerSelectionFromMap(reveal: true);

    /// <summary>잎의 지도 마커 — 심볼 잎 = 같은 Id 의 심볼 마커, 오버레이 이미지 잎 = 같은 파일의 이미지 마커. 없으면 null.</summary>
    private IEditableMarker? FindMarkerForLayerLeaf(LayerTreeNode leaf)
    {
        if (MainMap?.Markers == null) return null;
        if (leaf.Symbol != null)
            return MainMap.Markers.OfType<IEditableMarker>().FirstOrDefault(m => !m.IsDisposed && m is not GMapImageMarker && m.Id == leaf.Symbol.Id);
        if (leaf.Model?.LayerType == "OverlayImage" && !string.IsNullOrEmpty(leaf.Model.FilePath))
            return FindImageMarkerByFilePath(leaf.Model.FilePath) is { IsDisposed: false } img ? img : null;
        return null;
    }

    /// <summary>
    /// 재조회(트리 재구성) 뒤 같은 항목을 다시 고른다 — 사라진 항목(삭제)은 빠진다. 편집 모드면 실제 지도 선택으로 맞춘다.
    /// 새 트리는 묶음이 접혀 만들어지므로 고른 행의 묶음은 펼쳐 둔다(고른 행이 숨으면 Shift 범위 기준도 사라진다).
    /// </summary>
    private void RestoreLayerSelection(HashSet<string> selectedKeys)
    {
        if (_layerTreeNodes == null) return;
        if (selectedKeys.Count > 0)
            LayerSelection.Apply(() =>
            {
                foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes))
                    if (LayerSelection.KeyOf(leaf) is { } key && selectedKeys.Contains(key)) leaf.IsSelected = true;
            });
        SyncLayerSelectionFromMap(reveal: false);
        LayerSelection.ExpandToShow(LayerSelection.SelectedLeaves(_layerTreeNodes));
    }
}
