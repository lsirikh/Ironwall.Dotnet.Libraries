using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 에서 지도 심볼(오버레이 마커) 고르기 · 고치기(2026-10-22 사용자 보고 — "3D 에서 심볼을 선택하면 Adorner 가 다 안 보이고 사라진다").
                  2D 편집 어도너(옮기기 · 돌리기 · 그룹 상자)는 2D 지도에 붙어 3D 화면(WebView2, 별도 창 핸들) 밑에 깔린다(에어스페이스) —
                  고름 표시와 손잡이를 3D 엔진이 그리게 한다.
   Note         : ★ 고름 = 2D 와 같은 집합(묶음 선택 ∪ 단일 선택)을 overlay.select 로. 2D 고름이 바뀔 때마다(SelectedMarker · 묶음 SelectionChanged)
                    한 박자 모아 보낸다 — 클릭 한 번이 「모두 풀기 → 새로 고르기」 두 단계라 사이의 빈 고름을 보내지 않게. 같은 내용이면 다시 보내지 않는다.
                  ★ 손잡이 = 편집 모드 · 심볼 하나 · 잠기지 않음 · 3D 위 판(건물 만들기 · 꾸미기)이 없을 때만 edit.start(target kind "overlay").
                    돌리기 원호는 2D 어도너가 돌리기 손잡이를 그리는 종류만(빌보드 아님 — MarkerEditAdorner.IsBillboardTarget 과 같은 판정).
                  ★ edit.commit(overlay) = 2D 어도너가 끝낼 때와 같은 경로(OnMarkerEditCompleted → DbUpdateProcess · 편집 기록) — 새 저장 경로 없음.
                    받지 않으면(편집 모드 꺼짐 · 잠김 · 권한 없음) edit.revert. 모르는 심볼은 무시.
                  ★ 3D 를 나가도 페이지는 1×1 로 남아 고름을 기억한다 — 다시 펼칠 때 그 사이 바뀐 고름만 보내고 손잡이는 다시 세운다.
                    새 페이지(ready)는 고름 없이 시작한다. 고른 것이 없으면 3D 에 아무것도 보내지 않는다.
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    /// <summary>3D 페이지에 마지막으로 보낸 overlay.select — 같으면 다시 보내지 않는다. 새 페이지면 null(= 고름 없음).</summary>
    private string? _map3DSelectSent;
    private bool _map3DSelectQueued;
    /// <summary>3D 손잡이를 띄운 지도 심볼(edit.start overlay) — 없으면 null.</summary>
    private (string Kind, int Id)? _overlayEdit3D;

    /// <summary>2D 고름이 바뀌었다 — 3D 표시 중이면 한 박자 뒤 한 번에 맞춘다.</summary>
    private void QueueMap3DSelectionSync()
    {
        if (_map3DState != Map3DState.Shown || _map3DSelectQueued || MainMap == null) return;
        _map3DSelectQueued = true;
        MainMap.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            _map3DSelectQueued = false;
            try { SyncMap3DSelection(); }
            catch (Exception ex) { _log?.Error($"[Map3D] 심볼 고름 맞추기 실패(격리): {ex.Message}"); }
        }));
    }

    /// <summary>
    /// 3D 를 펼칠 때 · 새 페이지(ready) — 3D 표시 중이면 지금 고름을 맞추고 손잡이는 다시 세운다(2D 에서 고쳤을 수 있다).
    /// 새 페이지는 아무것도 고르지 않은 채 시작하므로 보낸 기록을 비운다. 접어 둔 페이지(1×1)는 고름을 기억하므로 기록을 둔다 —
    /// 그 사이 2D 에서 바뀐 것만 보낸다(빈 고름을 그냥 보내지 않는다 — 고르기 없이는 3D 에 아무것도 보내지 않는다).
    /// </summary>
    private void ResyncMap3DSelection(bool newPage)
    {
        if (newPage) { _map3DSelectSent = null; _overlayEdit3D = null; }
        SyncMap3DSelection(rebuildHandles: true);
    }

    private void SyncMap3DSelection(bool rebuildHandles = false)
    {
        if (_map3DState != Map3DState.Shown || MainMap == null) return;
        var items = Map3DSelectionItems();
        string json = Map3DEdit.OverlaySelectJson(items);
        if (json != (_map3DSelectSent ?? Map3DEdit.OverlaySelectJson(Array.Empty<(string, int)>())))
        {
            _map3DSelectSent = json;
            PostOverlay3D(json);
            _log?.Info($"[Map3D] 심볼 고름 → 3D {items.Count}개");
        }

        if (Overlay3DHandleTarget() is { } t)
        {
            if (!rebuildHandles && _overlayEdit3D == (t.Kind, t.Id)) return;
            _overlayEdit3D = (t.Kind, t.Id);
            PostOverlay3D(Map3DEdit.OverlayEditStartJson(t.Kind, t.Id, t.Rotatable));
            _log?.Info($"[Map3D] 심볼 손잡이 kind={t.Kind} id={t.Id} 돌리기={t.Rotatable}");
        }
        else if (_overlayEdit3D != null)
        {
            _overlayEdit3D = null;
            PostOverlay3D(Map3DEdit.EditStopJson());
        }
    }

    /// <summary>2D 와 같은 고름 집합(묶음 선택이 있으면 그것, 없으면 단일 선택) — 3D 에 그리는 종류만.</summary>
    private List<(string Kind, int Id)> Map3DSelectionItems()
    {
        IEnumerable<IEditableMarker> selected = (_groupSelection?.HasSelection ?? false) ? _groupSelection.Selection
                                              : SelectedMarker != null ? new[] { SelectedMarker } : Array.Empty<IEditableMarker>();
        var items = new List<(string, int)>();
        foreach (var m in selected)
            if (m is GMapMarker gm && !m.IsDisposed && Map3DOverlayBridge.KindOf(gm) is { } kind) items.Add((kind, m.Id));
        return items;
    }

    /// <summary>손잡이를 띄울 심볼 — 편집 모드 · 단일 선택 · 잠기지 않음 · 3D 위 판 없음. 오버레이 이미지는 고름 표시만.</summary>
    private (string Kind, int Id, bool Rotatable)? Overlay3DHandleTarget()
    {
        if (!IsEditModeEnabled || IsEdit3DMode || (_groupSelection?.HasSelection ?? false)) return null;
        if (SelectedMarker is not GMapMarker gm || SelectedMarker.IsDisposed || SelectedMarker.IsLocked) return null;
        if (Map3DOverlayBridge.KindOf(gm) is not { } kind || kind == Utils.Map3DOverlay.KindImage) return null;
        bool rotatable = !Utils.Map3DOverlay.IsLineKind(kind) && gm.Shape is not IMapRotationAwareShape { IsBillboard: true };
        return (kind, SelectedMarker.Id, rotatable);
    }

    /// <summary>overlay.* · 심볼 edit.* 는 장면(job)에 매이지 않는다 — 지금 3D 페이지로 바로.</summary>
    private void PostOverlay3D(string json)
    {
        try { PostBuilding3D(json); }
        catch (Exception ex) { _log?.Error($"[Map3D] 심볼 고름 보내기 실패(격리): {ex.Message}"); }
    }

    /// <summary>3D 의 종류 · id → 지도 마커(없으면 null).</summary>
    private IEditableMarker? FindMap3DMarker(string kind, int id)
        => MainMap?.Markers.FirstOrDefault(m => Map3DOverlayBridge.KindOf(m) == kind && ((IEditableMarker)m).Id == id) as IEditableMarker;

    // ── 3D → GIS ──

    /// <summary>
    /// 3D {Shift}+끌기 상자 — 편집 모드에서만. 2D 러버밴드처럼 잠긴 심볼 · 오버레이 이미지는 빼고, add = 지금 고름에 더하기/빼기(겹치면 뺀다),
    /// add=false = 상자 안 것으로 바꾸기.
    /// </summary>
    private void OnMap3DOverlayBox(Map3DOverlayBox box)
    {
        if (MainMap == null || _map3DState != Map3DState.Shown || !IsEditModeEnabled) return;
        var hits = box.Items.Select(i => FindMap3DMarker(i.Kind, i.Id))
                            .Where(m => m is { IsDisposed: false, IsLocked: false } && m is not GMapImageMarker).Cast<IEditableMarker>().ToList();
        _log?.Info($"[Map3D] 심볼 상자 고르기 {hits.Count}/{box.Items.Count}개 add={box.Add}");
        if (box.Add) { OnMarkersRubberBandSelected(hits); return; }
        var ids = hits.Select(h => (false, h.Id)).ToHashSet();
        ApplyGroupSelectionByIds(ids);
        SetAimStatus(ids.Count > 0 ? $"{ids.Count}개 선택" : "선택 해제", autoHide: true);
    }

    /// <summary>
    /// 3D 손잡이로 고친 심볼 저장 — 마커 값을 바꾼 뒤 2D 어도너 끝과 같은 OnMarkerEditCompleted(DB 저장 · 편집 기록 1단위).
    /// 점 = 자리(+ 돌리기 되는 종류만 방향), 선 · 구역 = 점 전체(가운데는 점 상자 가운데 — 2D 크기 바꾸기와 같은 규칙).
    /// </summary>
    private void CommitOverlayEdit3D(Map3DOverlayEdit c)
    {
        if (FindMap3DMarker(c.OverlayKind, c.Id) is not { IsDisposed: false } marker || marker is not GMapMarker gm)
        {
            _log?.Info($"[Map3D] 심볼 edit.commit kind={c.OverlayKind} id={c.Id} — 지도에 없음(무시)");
            return;
        }
        var line = marker as ILineEditableMarker;
        int min = line?.IsClosedPath == true ? 3 : 2;
        string? why = !IsEditModeEnabled ? "편집 모드 꺼짐" : marker.IsLocked ? "잠김" : !CanEditMap() ? "권한 없음"
                    : line != null ? (c.Points is { } p && p.Count >= min ? null : "점 부족")
                    : c.Lon == null || c.Lat == null ? "자리 없음" : null;
        if (why != null)
        {
            if (why == "권한 없음") ShowNoMapEditPermissionInfo();
            PostOverlay3D(Map3DEdit.OverlayEditRevertJson(c.OverlayKind, c.Id));
            _log?.Info($"[Map3D] 심볼 edit.revert kind={c.OverlayKind} id={c.Id} — {why}");
            return;
        }

        var (pos, w, h, bearing) = (marker.Position, marker.Width, marker.Height, marker.Bearing);
        List<PointLatLng>? beforePoints = null;
        if (line != null)
        {
            beforePoints = new List<PointLatLng>(line.RuntimePoints);
            var pts = c.Points!.Select(q => new PointLatLng(q.Y, q.X)).ToList();
            line.ApplyGeometry(pts, LineGeometryUtils.BoundsCenter(pts));
        }
        else
        {
            marker.UpdateLocation(new PointLatLng(c.Lat!.Value, c.Lon!.Value));
            if (c.HeadingDeg is double hd && gm.Shape is not IMapRotationAwareShape { IsBillboard: true }) marker.UpdateRotation(hd);
        }
        _log?.Info($"[Map3D] 심볼 edit.commit kind={c.OverlayKind} id={c.Id} 점={c.Points?.Count.ToString() ?? "-"} 방향={c.HeadingDeg?.ToString() ?? "-"}");
        OnMarkerEditCompleted(this, new MarkerEditCompletedEventArgs(marker, pos, w, h, bearing) { OriginalLinePoints = beforePoints });
        // 엔진 계약(2026-10-22): 저장 뒤 새 값을 overlay.patch 로 반드시 — 엔진이 고치는 중 모양을 그 값으로 맞춘다
        try { _map3DOverlay?.PatchNow(gm); }
        catch (Exception ex) { _log?.Error($"[Map3D] 심볼 patch 보내기 실패(격리): {ex.Message}"); }
    }
}
