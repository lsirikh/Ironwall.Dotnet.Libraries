using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : MapViewModel의 Undo/Redo 통합 — IUndoApplyContext 구현 + Undo/Redo 커맨드/상태.
                  커맨드는 이 seam으로 앱(맵/모델/DB/트리)에 적용. Map_Edit_Undo_Redo FR-10/11.
   Note         : 기존 편집 흐름 무변경(사후기록). Undo/Redo 실행 중 _isApplyingUndo=true →
                  SelectMarkerForEditing 암묵저장(deselect) 스킵. 보호 adorner 파일 무접촉.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public partial class MapViewModel : IUndoApplyContext
{
    // 생성자에서 주입(둘 다 SingleInstance).
    private IEditRecorder _editRecorder = default!;
    private IUndoService _undoService = default!;

    /// <summary>Undo/Redo 재적용 중 — deselect 암묵저장·바인딩 에코 재실행을 스킵. 재진입 depth 카운터로
    /// 매크로 자식별 apply가 조기 리셋하지 못하게(전체 replay 동안 유지). UndoCommand/RedoCommand 람다가 브래킷.</summary>
    private int _applyingUndoDepth;
    internal bool IsApplyingUndo => System.Threading.Volatile.Read(ref _applyingUndoDepth) > 0;

    #region - Undo/Redo 커맨드·상태 (FR-11) -
    public AsyncRelayCommand? UndoCommand { get; private set; }
    public AsyncRelayCommand? RedoCommand { get; private set; }

    /// <summary>되돌리기 가능 — 스택 비어있지않음 ∧ 편집모드 ∧ 맵편집권한.</summary>
    public bool CanUndo => _undoService != null && _undoService.CanUndo && IsEditModeEnabled && CanEditMap();
    public bool CanRedo => _undoService != null && _undoService.CanRedo && IsEditModeEnabled && CanEditMap();
    public string? NextUndoDescription => _undoService?.NextUndoDescription;
    public string? NextRedoDescription => _undoService?.NextRedoDescription;

    /// <summary>생성자 말미 호출 — 커맨드 생성 + Recorder Context 연결 + StateChanged 구독.</summary>
    private void InitializeUndoRedo()
    {
        if (_undoService == null) return;
        UndoCommand = new AsyncRelayCommand(async () =>
        {
            System.Threading.Interlocked.Increment(ref _applyingUndoDepth);
            try { await _undoService.UndoAsync(); }
            finally { System.Threading.Interlocked.Decrement(ref _applyingUndoDepth); }
        }, () => CanUndo);
        RedoCommand = new AsyncRelayCommand(async () =>
        {
            System.Threading.Interlocked.Increment(ref _applyingUndoDepth);
            try { await _undoService.RedoAsync(); }
            finally { System.Threading.Interlocked.Decrement(ref _applyingUndoDepth); }
        }, () => CanRedo);

        _undoService.StateChanged += OnUndoStateChanged;
        if (_editRecorder != null) _editRecorder.Context = this;   // 커맨드 적용 seam 연결(순환주입 회피)
    }

    private void OnUndoStateChanged(object? sender, EventArgs e)
    {
        void Refresh()
        {
            NotifyOfPropertyChange(nameof(CanUndo));
            NotifyOfPropertyChange(nameof(CanRedo));
            NotifyOfPropertyChange(nameof(NextUndoDescription));
            NotifyOfPropertyChange(nameof(NextRedoDescription));
            UndoCommand?.RaiseCanExecuteChanged();
            RedoCommand?.RaiseCanExecuteChanged();
        }
        var disp = Application.Current?.Dispatcher;
        if (disp != null && !disp.CheckAccess()) disp.BeginInvoke((Action)Refresh);
        else Refresh();
    }

    /// <summary>맵 전환/로그아웃/벌크 재조회 시 Undo 스택 비움(외부 상태와 충돌 방지, FR-12).</summary>
    private void ClearUndoStack() => _undoService?.Clear();
    #endregion

    #region - IUndoApplyContext 구현 (커맨드→앱 적용 seam) -
    IGMapDbSymbolService IUndoApplyContext.Db => _gMapDbSymbolService;

    public IEditableMarker? FindMarkerById(int id)
        => MainMap?.Markers?.OfType<IEditableMarker>().FirstOrDefault(m => !m.IsDisposed && m.Id == id);

    /// <summary>호출자가 이미 마커 모델을 세팅 → 타입별 DbUpdate 영속 + 시각 갱신. (기록 억제 하 실행)</summary>
    public async Task ApplyMarkerUpdateAsync(IEditableMarker marker, CancellationToken ct = default)
    {
        if (marker == null || marker.IsDisposed) return;
        try { await DbUpdateProcess(marker); MainMap?.InvalidateVisual(); }
        catch (Exception ex) { _log?.Error($"[Undo] 마커 업데이트 적용 실패: {ex.Message}"); }
    }

    /// <summary>스냅샷으로 삭제 심볼 복원(Id 보존 Restore → 실패 시 새 Id Insert) + 마커/트리 재구성.</summary>
    public async Task<IEditableMarker?> RestoreDeletedAsync(ISymbolSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot == null) return null;
        try
        {
            var model = snapshot.CloneModel();
            if (snapshot.IsImage && model is IImageModel img)
            {
                try { await _gMapDbSymbolService.RestoreImageAsync(img, ct); }
                catch (IdCollisionException) { await _gMapDbSymbolService.InsertImageAsync(img, ct); }
                if (img.Id > 0) snapshot.Id = img.Id;   // Id 충돌 폴백 시 새 Id 반영(FindMarkerById·이후 undo 정합, FIX 6)
                AddImageMarkerFromModel(img);
                ResyncTree();                            // 이미지 노드도 트리 반영(AddImageMarkerFromModel은 트리 리로드 안 함, FIX 5)
            }
            else if (model is ISymbolModel sym)
            {
                await RestoreOrInsertSymbolAsync(snapshot.MarkerTypeName, sym, ct);
                if (sym.Id > 0) snapshot.Id = sym.Id;   // Id 충돌 폴백 시 새 Id 반영(FIX 6)
                AddMarkerFromSymbol(sym);                // 마커 재생성 + _symbolProvider + 트리
            }
            return FindMarkerById(snapshot.Id);
        }
        catch (Exception ex) { _log?.Error($"[Undo] 복원 실패(Id={snapshot.Id}): {ex.Message}"); return null; }
    }

    /// <summary>타입별 Id 보존 Restore, Id 충돌 시 새 Id Insert 폴백.</summary>
    private async Task RestoreOrInsertSymbolAsync(string markerType, ISymbolModel sym, CancellationToken ct)
    {
        try
        {
            switch (markerType)
            {
                case nameof(GMapPidsGroupMarker): await _gMapDbSymbolService.RestorePidsGroupSymbolAsync((IPidsGroupSymbolModel)sym, ct); break;
                case nameof(GMapLineMarker): await _gMapDbSymbolService.RestoreLineSymbolAsync((ILineSymbolModel)sym, ct); break;
                case nameof(GMapGeometricMarker): await _gMapDbSymbolService.RestoreGeometrySymbolAsync((IGeometricSymbolModel)sym, ct); break;
                case nameof(GMapPidsMarker): await _gMapDbSymbolService.RestorePidsSymbolAsync((IPidsSymbolModel)sym, ct); break;
                case nameof(GMapMilitarySymbolMarker): await _gMapDbSymbolService.RestoreMilitarySymbolAsync((IMilitarySymbolModel)sym, ct); break;
                case nameof(GMapInfraMarker): await _gMapDbSymbolService.RestoreInfraSymbolAsync((IInfraSymbolModel)sym, ct); break;
                default: await _gMapDbSymbolService.RestoreSymbolAsync(sym, ct); break;
            }
        }
        catch (IdCollisionException)
        {
            _log?.Warning($"[Undo] Id={sym.Id} 점유 — 새 Id로 복원(폴백)");
            switch (markerType)
            {
                case nameof(GMapPidsGroupMarker): await _gMapDbSymbolService.InsertPidsGroupSymbolAsync((IPidsGroupSymbolModel)sym, ct); break;
                case nameof(GMapLineMarker): await _gMapDbSymbolService.InsertLineSymbolAsync((ILineSymbolModel)sym, ct); break;
                case nameof(GMapGeometricMarker): await _gMapDbSymbolService.InsertGeometrySymbolAsync((IGeometricSymbolModel)sym, ct); break;
                case nameof(GMapPidsMarker): await _gMapDbSymbolService.InsertPidsSymbolAsync((IPidsSymbolModel)sym, ct); break;
                case nameof(GMapMilitarySymbolMarker): await _gMapDbSymbolService.InsertMilitarySymbolAsync((IMilitarySymbolModel)sym, ct); break;
                case nameof(GMapInfraMarker): await _gMapDbSymbolService.InsertInfraSymbolAsync((IInfraSymbolModel)sym, ct); break;
                default: await _gMapDbSymbolService.InsertSymbolAsync(sym, ct); break;
            }
        }
    }

    /// <summary>마커 제거(DB 삭제 + 맵/provider/트리 제거). Add 취소 / Delete 재실행 시.</summary>
    public async Task RemoveMarkerAsync(IEditableMarker marker, CancellationToken ct = default)
    {
        if (marker == null || marker.IsDisposed) return;
        try
        {
            int id = marker.Id;
            if (SelectedMarker is IEditableMarker sm && sm.Id == id) HidePropertyPanel();   // 열린 패널이 dispose 마커 참조 방지(FIX 7)
            MainMap?.DeselectMarker(marker);
            if (marker is GMapMarker gm) MainMap?.Markers?.Remove(gm);
            await DbDeleteProcess(marker);
            var s = _symbolProvider.FirstOrDefault(x => x.Id == id);
            if (s != null) _symbolProvider.Remove(s);
            try { marker.Dispose(); } catch { /* 무시 */ }
            await LoadLayersFromDbAsync();
        }
        catch (Exception ex) { _log?.Error($"[Undo] 제거 실패: {ex.Message}"); }
    }

    /// <summary>ZOrder 일괄 적용((id,z) 페어) + 로컬 렌더순서 반영.</summary>
    public async Task ApplyZOrderAsync(IReadOnlyList<(int id, int zOrder)> pairs, CancellationToken ct = default)
    {
        if (pairs == null || pairs.Count == 0) return;
        try
        {
            await _gMapDbSymbolService.BatchUpdateZOrderAsync(pairs.Select(p => (p.id, p.zOrder)).ToList(), ct);
            foreach (var (id, z) in pairs)
            {
                if (FindMarkerById(id) is GMapMarker gm && gm.Shape is UIElement shape)
                {
                    ((IEditableMarker)gm).ZOrder = z;
                    System.Windows.Controls.Panel.SetZIndex(shape, z);
                    gm.ZIndex = z;
                }
            }
            MainMap?.InvalidateVisual();
            if (SelectedMarker is IEditableMarker sel && pairs.Any(p => p.id == sel.Id))
                RefreshPropertyPanelZOrder();   // 선택 마커 순서표시 갱신(FIX 8)
        }
        catch (Exception ex) { _log?.Error($"[Undo] ZOrder 적용 실패: {ex.Message}"); }
    }

    public void ResyncTree() => _ = LoadLayersFromDbAsync();

    /// <summary>단일 심볼 리프 노드만 라이브 마커 기준 갱신(이름/잠금/체크) — 전체 리로드 회피(열린 패널 dispose 방지, FIX 4).</summary>
    public void SyncMarkerNode(int id)
    {
        // 트리 노드 INotifyPropertyChanged는 UI 스레드에서만 발화(백그라운드 replay 안전, V6 하드닝)
        var disp = System.Windows.Application.Current?.Dispatcher;
        if (disp != null && !disp.CheckAccess()) { disp.Invoke(() => SyncMarkerNode(id)); return; }
        var m = FindMarkerById(id);
        if (m == null || _layerTreeNodes == null) return;
        bool matched = false;
        foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes).Where(n => n.IsSymbolLeaf && n.Symbol != null))
            if (leaf.Symbol!.Id == id)
            {
                leaf.Name = m.Title ?? string.Empty;
                leaf.InitIsLocked(m.IsLocked);
                leaf.SetCheckedSilently(m.ShowShape);
                matched = true;
            }
        // AREA 4: 심볼 리프에 없으면(이미지 마커=오버레이 이미지 노드) 전체 리로드로 트리 반영
        if (!matched && m is GMapSymbols.GMapImageMarker) ResyncTree();
    }

    // ── v2 커버리지 seam 구현 ──
    /// <summary>런타임 가시성 적용(DB 미영속) — 마커 상태 + 시각 + 트리 체크 갱신.</summary>
    public Task ApplyVisibilityAsync(int id, bool show, CancellationToken ct = default)
    {
        var m = FindMarkerById(id);
        if (m == null) return Task.CompletedTask;
        m.IsLayerEnabled = show;
        m.ShowShape = show;
        m.IsVisible = show && MainMap != null && MainMap.Zoom >= m.Zoom;   // 유효 가시성 = 토글 AND 줌
        MainMap?.InvalidateVisual();
        SyncMarkerNode(id);   // 트리 체크박스 반영(UI 스레드 마샬 내장)
        return Task.CompletedTask;
    }

    /// <summary>파일 오버레이 이미지 회전 적용 + 영속.</summary>
    public async Task ApplyCustomImageRotationAsync(int id, double rotation, CancellationToken ct = default)
    {
        try
        {
            var img = MainMap?.CustomImages?.FirstOrDefault(i => i.Id == id);
            if (img == null) return;
            img.UserRotation = rotation;   // EffectiveRotation 자동 갱신
            MainMap?.InvalidateVisual();
            if (img.Id > 0) await _gMapDbSymbolService.UpdateImageAsync(img.Model);
        }
        catch (Exception ex) { _log?.Error($"[Undo] 이미지 회전 적용 실패: {ex.Message}"); }
    }

    /// <summary>MapLayers 노드 필드 적용(이름/투명도/ZOrder) + OverlayImage 마커 동기화 + 트리 리로드.</summary>
    public async Task ApplyLayerFieldsAsync(int layerId, string? name, double? opacity, int? zOrder, CancellationToken ct = default)
    {
        try
        {
            var layers = await _gMapDbService.FetchMapLayersAsync();
            var layer = layers?.FirstOrDefault(l => l.Id == layerId);
            if (layer == null) return;
            if (name != null) layer.Name = name;
            if (opacity.HasValue) layer.Opacity = opacity.Value;
            if (zOrder.HasValue) layer.ZOrder = zOrder.Value;
            await _gMapDbService.UpdateMapLayerAsync(layer);
            // OverlayImage 이름/투명도 → 이미지 마커 동기화
            if (layer.LayerType == "OverlayImage" && !string.IsNullOrEmpty(layer.FilePath))
            {
                var marker = FindImageMarkerByFilePath(layer.FilePath);
                if (marker != null)
                {
                    if (name != null) { marker.Title = name; await _gMapDbSymbolService.UpdateImageAsync(marker.ImageModel); }
                    if (opacity.HasValue) marker.Opacity = opacity.Value;
                }
            }
            if (zOrder.HasValue) SyncMapRenderingOrder(layer);   // ZOrder 변경 시 렌더순서 동기화
            await LoadLayersFromDbAsync();
            MainMap?.InvalidateVisual();
        }
        catch (Exception ex) { _log?.Error($"[Undo] 레이어 필드 적용 실패: {ex.Message}"); }
    }
    #endregion
}
