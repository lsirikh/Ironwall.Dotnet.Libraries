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

    private void OnUndoStateChanged(object? sender, EventArgs e) => RaiseUndoRedoState();

    /// <summary>Undo/Redo 가시상태 갱신(CanUndo/CanRedo·설명·Command CanExecute). 스택 변경뿐 아니라
    /// CanUndo/CanRedo 조건에 포함된 IsEditModeEnabled·CanEditMap() 변경(편집모드 토글·권한 회수) 시에도
    /// 호출해야 버튼 시각상태가 실제 조건과 동기화됨(C#2 유령 활성 방지). UI 스레드 마샬.</summary>
    internal void RaiseUndoRedoState()
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

    /// <summary>타입 인지 조회 — 이미지(Images.Id)와 심볼(Symbols.Id)의 Id가 같은 Markers 컬렉션서 충돌할 때
    /// 올바른 대상만 선택(undo가 엉뚱한 마커에 이미지 크기 등 적용=데이터손상 차단). isImage=true→이미지, false→심볼.</summary>
    public IEditableMarker? FindMarkerById(int id, bool isImage)
        => MainMap?.Markers?.OfType<IEditableMarker>()
            .FirstOrDefault(m => !m.IsDisposed && m.Id == id && (m is GMapSymbols.GMapImageMarker) == isImage);

    /// <summary>호출자가 이미 마커 모델을 세팅 → 타입별 DbUpdate 영속 + 시각 갱신. (기록 억제 하 실행)
    /// UI 스레드 보장(v2 seam과 동일 — 그룹 replay 크로스스레드 방지, THREAD-02).</summary>
    public Task ApplyMarkerUpdateAsync(IEditableMarker marker, CancellationToken ct = default)
        => RunOnUiAsync(async () =>
        {
            if (marker == null || marker.IsDisposed) return;
            try
            {
                await DbUpdateProcess(marker);
                // 이미지 마커: undo/redo로 ImageBounds(크기)가 바뀌어도 Shape 컨트롤의 화면 픽셀크기는
                // 어도너 드래그 경로만 갱신함 → 프로그램적 변경(undo)에선 컨트롤이 stale로 남아 화면상 이미지가 "튐".
                // 줌 리프레시와 동일 경로(RefreshFromMarker→UpdateGeometryFromBounds)로 바운즈 기준 재계산해 동기화.
                if (marker is GMapSymbols.GMapImageMarker img)
                {
                    img.UpdateScreenPosition(MainMap);   // 컨트롤 화면 크기 + Offset(=-크기/2) 바운즈 기준 재계산
                    // ★ 리사이즈 undo 위치 튐 근본차단: 순수 리사이즈는 Position(Center) 불변이라 GMap이 마커를
                    //   재배치하지 않음 → UpdateSize로 바뀐 Offset(=-크기/2)이 캔버스에 반영 안 됨(이동 undo는 Position이
                    //   바뀌어 자동 재배치되므로 정상). 마커 로컬위치를 명시 재투영해 새 Offset을 확실히 적용.
                    if (MainMap != null) img.ForceUpdateLocalPosition(MainMap);
                    // 라이브 rename은 마커 Title(Images)과 MapLayers.Name을 함께 바꾸지만(SyncOverlayImageLayer),
                    // undo(PropertyChangeCommand Title)는 마커 Title만 되돌림 → MapLayers.Name이 이전 이름에 고착돼
                    // 레이어 트리가 desync. 마커 제목 기준으로 MapLayers.Name을 재동기(이름이 실제 바뀐 경우만 트리 재로드).
                    await SyncOverlayImageLayerNameAsync(img);
                }
                MainMap?.InvalidateVisual();
                // 그룹 선택 중이면 점선박스도 마커 복귀/변경 위치로 재계산(undo 후 박스 미추종 방지, 이슈①).
                if (_groupSelection?.HasSelection ?? false) _groupSelection.RefreshAdorner();
            }
            catch (Exception ex) { _log?.Error($"[Undo] 마커 업데이트 적용 실패: {ex.Message}"); }
        });

    /// <summary>이미지 undo/redo 후 MapLayers.Name을 마커 제목과 동기 — 이름이 실제 다를 때만 갱신+트리 재로드
    /// (리사이즈/투명도 undo에선 재로드 생략). 라이브 rename의 MapLayers 변경이 undo에 안 잡혀 생기던 desync 차단.</summary>
    private async Task SyncOverlayImageLayerNameAsync(GMapSymbols.GMapImageMarker img)
    {
        if (string.IsNullOrEmpty(img.FilePath)) return;
        try
        {
            var layers = await _gMapDbService.FetchMapLayersAsync();
            var layer = layers?.FirstOrDefault(l => l.LayerType == "OverlayImage"
                && string.Equals(l.FilePath, img.FilePath, System.StringComparison.OrdinalIgnoreCase));
            if (layer == null || string.Equals(layer.Name, img.Title, System.StringComparison.Ordinal)) return;   // 변경 없으면 생략
            layer.Name = img.Title ?? layer.Name;
            await _gMapDbService.UpdateMapLayerAsync(layer);
            await LoadLayersFromDbAsync();
        }
        catch (System.Exception ex) { _log?.Error($"[Undo] 오버레이 이미지 레이어 이름 동기 실패: {ex.Message}"); }
    }

    /// <summary>스냅샷으로 삭제 심볼 복원(Id 보존 Restore → 실패 시 새 Id Insert) + 마커/트리 재구성.
    /// UI 스레드 보장 — 복원 후 AddMarkerFromSymbol/AddImageMarkerFromModel(WPF)가 DB await 이후 UI에서 실행(THREAD-02).</summary>
    public Task<IEditableMarker?> RestoreDeletedAsync(ISymbolSnapshot snapshot, CancellationToken ct = default)
        => RunOnUiAsync(async () =>
        {
            if (snapshot == null) return (IEditableMarker?)null;
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
                return FindMarkerById(snapshot.Id, snapshot.IsImage);   // 타입인지 — 복원 반환이 같은 Id 반대타입 마커로 새지 않게
            }
            catch (Exception ex) { _log?.Error($"[Undo] 복원 실패(Id={snapshot.Id}): {ex.Message}"); return (IEditableMarker?)null; }
        });

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

    /// <summary>마커 제거(DB 삭제 + 맵/provider/트리 제거). Add 취소 / Delete 재실행 시.
    /// UI 스레드 보장 — DeselectMarker/Markers.Remove/Dispose 등 동기 WPF 접근 크로스스레드 방지(THREAD-02).</summary>
    public Task RemoveMarkerAsync(IEditableMarker marker, CancellationToken ct = default)
        => RunOnUiAsync(async () =>
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
        });

    /// <summary>ZOrder 일괄 적용((id,z) 페어) + 로컬 렌더순서 반영.
    /// UI 스레드 보장 — Panel.SetZIndex/InvalidateVisual이 DB await 이후 UI에서 실행(THREAD-02).</summary>
    public Task ApplyZOrderAsync(IReadOnlyList<(bool isImage, int id, int zOrder)> pairs, CancellationToken ct = default)
        => RunOnUiAsync(async () =>
        {
            if (pairs == null || pairs.Count == 0) return;
            try
            {
                // ★ [P0-2] DB 배치는 심볼만 — 이미지 Id가 Symbols 테이블로 가면 동일 Id 심볼 ZOrder 오염(분석문서 §5).
                var symbolPairs = pairs.Where(p => !p.isImage).Select(p => (p.id, p.zOrder)).ToList();
                if (symbolPairs.Count > 0) await _gMapDbSymbolService.BatchUpdateZOrderAsync(symbolPairs, ct);
                foreach (var (isImage, id, z) in pairs)
                {
                    if (FindMarkerById(id, isImage) is GMapMarker gm && gm.Shape is UIElement shape)   // 타입인지 — 같은 Id 반대타입 마커 ZIndex 오적용 차단(D1)
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
        });

    public void ResyncTree() => _ = LoadLayersFromDbAsync();

    /// <summary>UI 스레드 보장 실행 — Undo/Redo가 세마포어 경합(급속 Ctrl+Z)으로 스레드풀에서 재개돼도
    /// WPF 마커/맵/트리 접근이 크로스스레드가 되지 않게 본문 전체를 디스패처로 마샬. 예외는 호출자로 전파(BUG-05~09).</summary>
    private Task RunOnUiAsync(Func<Task> body)
    {
        var disp = System.Windows.Application.Current?.Dispatcher;
        if (disp == null || disp.CheckAccess()) return body();
        return disp.InvokeAsync(body).Task.Unwrap();
    }

    /// <summary>RunOnUiAsync의 반환값 버전 — RestoreDeletedAsync 등 Task&lt;T&gt; seam용.</summary>
    private Task<T> RunOnUiAsync<T>(Func<Task<T>> body)
    {
        var disp = System.Windows.Application.Current?.Dispatcher;
        if (disp == null || disp.CheckAccess()) return body();
        return disp.InvokeAsync(body).Task.Unwrap();
    }

    /// <summary>단일 심볼 리프 노드만 라이브 마커 기준 갱신(이름/잠금/체크) — 전체 리로드 회피(열린 패널 dispose 방지, FIX 4).</summary>
    public void SyncMarkerNode(int id) => SyncMarkerNode(id, false);

    /// <summary>타입 인지 노드 동기화 — 이미지↔심볼 Id 충돌 시 올바른 마커로 패널 제목/트리 리프 갱신.
    /// 비타입 조회가 같은 Id의 반대타입 마커(제어기1)를 잡아 패널에 엉뚱한 이름을 표시/심볼리프를 오염시키던 손상 차단.</summary>
    public void SyncMarkerNode(int id, bool isImage)
    {
        // 트리 노드 INotifyPropertyChanged는 UI 스레드에서만 발화(백그라운드 replay 안전, V6 하드닝)
        var disp = System.Windows.Application.Current?.Dispatcher;
        if (disp != null && !disp.CheckAccess()) { disp.Invoke(() => SyncMarkerNode(id, isImage)); return; }
        var m = FindMarkerById(id, isImage);   // 타입인지 — 같은 Id의 반대타입 마커로 새지 않게
        if (m == null) return;
        // 열린 속성패널이 이 마커면 제목 표시도 동기화 — 심볼 이름 undo 시 패널 stale 방지(BUG-02 대칭, P7).
        // 패널 선택 마커도 타입까지 일치해야 함(같은 Id의 이미지↔심볼이 동시 존재 → 타입 불일치면 스킵).
        if (PropertyPanel?.SelectedMarker is IEditableMarker psel && psel.Id == id
            && (psel is GMapSymbols.GMapImageMarker) == isImage)
            PropertyPanel.MarkerTitle = m.Title ?? string.Empty;
        if (_layerTreeNodes == null) return;
        bool matched = false;
        // 이미지는 심볼 리프가 아님 — 심볼 리프 동기화는 심볼 대상일 때만(이미지가 같은 Id 심볼리프를 오염하는 것 차단).
        if (!isImage)
            foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes).Where(n => n.IsSymbolLeaf && n.Symbol != null))
                if (leaf.Symbol!.Id == id)
                {
                    leaf.Name = m.Title ?? string.Empty;
                    leaf.InitIsLocked(m.IsLocked);
                    leaf.SetCheckedSilently(m.ShowShape);
                    matched = true;
                }
        // AREA 4: 심볼 리프에 없으면(이미지 마커=오버레이 이미지 노드 OR 트리 누락 심볼) 전체 리로드로 트리 반영(BUG-01)
        if (!matched) ResyncTree();
    }

    // ── v2 커버리지 seam 구현 ──
    /// <summary>런타임 가시성 적용(DB 미영속) — 마커 상태 + 시각 + 트리 체크 갱신.</summary>
    public Task ApplyVisibilityAsync(int id, bool show, bool isImage = false, CancellationToken ct = default)
        => RunOnUiAsync(() =>
        {
            var m = FindMarkerById(id, isImage);   // 타입인지 — 같은 Id의 반대타입 마커 가시성 오토글 차단
            if (m == null) return Task.CompletedTask;
            m.IsLayerEnabled = show;
            m.ShowShape = show;
            m.IsVisible = show && MainMap != null && MainMap.Zoom >= m.Zoom;   // 유효 가시성 = 토글 AND 줌
            MainMap?.InvalidateVisual();
            SyncMarkerNode(id, isImage);   // 트리 체크박스 반영
            return Task.CompletedTask;
        });

    /// <summary>파일 오버레이 이미지 회전 적용 + 영속. 예외는 UndoService로 전파(실패 시 redo 오이동 방지, BUG-03/04).</summary>
    public Task ApplyCustomImageRotationAsync(int id, double rotation, CancellationToken ct = default)
        => RunOnUiAsync(async () =>
        {
            var img = MainMap?.CustomImages?.FirstOrDefault(i => i.Id == id);
            if (img == null) return;
            img.UserRotation = rotation;   // EffectiveRotation 자동 갱신
            MainMap?.InvalidateVisual();
            if (img.Id > 0) await _gMapDbSymbolService.UpdateImageAsync(img.Model);
        });

    /// <summary>파일 오버레이 이미지 이동/크기/회전 적용 + 영속(D1 핸들 편집). 예외는 UndoService로 전파(BUG-03/04).</summary>
    public Task ApplyCustomImageEditAsync(int id, GMap.NET.RectLatLng bounds, double rotation, CancellationToken ct = default)
        => RunOnUiAsync(async () =>
        {
            var img = MainMap?.CustomImages?.FirstOrDefault(i => i.Id == id);
            if (img == null) return;
            img.ImageBounds = bounds;      // Model(Deconstruct) 동기화
            img.UserRotation = rotation;
            MainMap?.InvalidateVisual();
            if (img.Id > 0) await _gMapDbSymbolService.UpdateImageAsync(img.Model);
        });

    /// <summary>MapLayers 노드 필드 적용(이름/투명도/ZOrder) + OverlayImage 마커 동기화 + 트리 리로드.
    /// UI스레드 마샬(BUG-06/08/09), 예외는 UndoService로 전파(BUG-04).</summary>
    public Task ApplyLayerFieldsAsync(int layerId, string? name, double? opacity, int? zOrder, CancellationToken ct = default)
        => RunOnUiAsync(async () =>
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
                    if (name != null)
                    {
                        marker.Title = name;
                        await _gMapDbSymbolService.UpdateImageAsync(marker.ImageModel);
                        // 열린 속성패널이 이 마커 선택 중이면 제목 반영(forward OnLayerRenameRequested 미러, BUG-02)
                        if (PropertyPanel?.SelectedMarker == marker) PropertyPanel.MarkerTitle = name;
                    }
                    if (opacity.HasValue) marker.Opacity = opacity.Value;
                }
            }
            if (zOrder.HasValue) SyncMapRenderingOrder(layer);   // ZOrder 변경 시 렌더순서 동기화
            await LoadLayersFromDbAsync();
            MainMap?.InvalidateVisual();
        });
    #endregion
}
