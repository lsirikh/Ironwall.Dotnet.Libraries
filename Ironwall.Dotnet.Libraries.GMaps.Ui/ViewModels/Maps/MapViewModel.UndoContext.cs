using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
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

    /// <summary>Undo/Redo 재적용 중 — SelectMarkerForEditing의 deselect 암묵저장을 스킵(이미 명시 영속).</summary>
    private bool _isApplyingUndo;

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
            _isApplyingUndo = true;
            try { await _undoService.UndoAsync(); }
            finally { _isApplyingUndo = false; }
        }, () => CanUndo);
        RedoCommand = new AsyncRelayCommand(async () =>
        {
            _isApplyingUndo = true;
            try { await _undoService.RedoAsync(); }
            finally { _isApplyingUndo = false; }
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
        _isApplyingUndo = true;
        try { await DbUpdateProcess(marker); MainMap?.InvalidateVisual(); }
        catch (Exception ex) { _log?.Error($"[Undo] 마커 업데이트 적용 실패: {ex.Message}"); }
        finally { _isApplyingUndo = false; }
    }

    /// <summary>스냅샷으로 삭제 심볼 복원(Id 보존 Restore → 실패 시 새 Id Insert) + 마커/트리 재구성.</summary>
    public async Task<IEditableMarker?> RestoreDeletedAsync(ISymbolSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot == null) return null;
        _isApplyingUndo = true;
        try
        {
            var model = snapshot.CloneModel();
            if (snapshot.IsImage && model is IImageModel img)
            {
                try { await _gMapDbSymbolService.RestoreImageAsync(img, ct); }
                catch (IdCollisionException) { await _gMapDbSymbolService.InsertImageAsync(img, ct); }
                AddImageMarkerFromModel(img);
            }
            else if (model is ISymbolModel sym)
            {
                await RestoreOrInsertSymbolAsync(snapshot.MarkerTypeName, sym, ct);
                AddMarkerFromSymbol(sym);   // 마커 재생성 + _symbolProvider + 트리
            }
            return FindMarkerById(snapshot.Id);
        }
        catch (Exception ex) { _log?.Error($"[Undo] 복원 실패(Id={snapshot.Id}): {ex.Message}"); return null; }
        finally { _isApplyingUndo = false; }
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
        _isApplyingUndo = true;
        try
        {
            int id = marker.Id;
            MainMap?.DeselectMarker(marker);
            if (marker is GMapMarker gm) MainMap?.Markers?.Remove(gm);
            await DbDeleteProcess(marker);
            var s = _symbolProvider.FirstOrDefault(x => x.Id == id);
            if (s != null) _symbolProvider.Remove(s);
            try { marker.Dispose(); } catch { /* 무시 */ }
            await LoadLayersFromDbAsync();
        }
        catch (Exception ex) { _log?.Error($"[Undo] 제거 실패: {ex.Message}"); }
        finally { _isApplyingUndo = false; }
    }

    /// <summary>ZOrder 일괄 적용((id,z) 페어) + 로컬 렌더순서 반영.</summary>
    public async Task ApplyZOrderAsync(IReadOnlyList<(int id, int zOrder)> pairs, CancellationToken ct = default)
    {
        if (pairs == null || pairs.Count == 0) return;
        _isApplyingUndo = true;
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
        }
        catch (Exception ex) { _log?.Error($"[Undo] ZOrder 적용 실패: {ex.Message}"); }
        finally { _isApplyingUndo = false; }
    }

    public void ResyncTree() => _ = LoadLayersFromDbAsync();
    #endregion
}
