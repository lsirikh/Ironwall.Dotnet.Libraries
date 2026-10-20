using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 2D 지도의 3D 영역 표시 · 관리(지도 3D 시나리오 S2 · S3, R4).
                  ① map-3d 서버 목록(GET /api/jobs 의 scenes) → 영역 테두리 + "3D" 배지(Map3DAreaMarker, 줌 13 이상)
                  ② 배지 우클릭 · 지도 메뉴 「3D 영역 목록」: 이 영역 3D 로 보기(가운데 · 줌 17.5) / 다시 만들기 / 지우기(확인 → DELETE)
                  ③ 화면 가운데가 3D 영역 안이고 줌 &lt; 17 이면 줌 컨트롤 옆 "3D 가능 — 줌 17 이상" 칩
                  ④ 영역마다 3D 줌(2026-10-18) — 배지 툴팁 · 목록 · 칩에 그 영역의 줌, 배지 우클릭 「3D 줌 바꾸기…」
                    (PATCH /api/jobs/{id}, 서버가 모르면 GIS 로컬 map3d-areas.json — 그 값이 앞선다)
   Note         : 영역 마커는 편집 심볼이 아니다(DB 저장 없음). 판정 · 글은 순수 함수 Map3DGuide(헤드리스 시험).
                  갱신은 3D 게이트 큐(QueueMap3DGate — 줌 · 위치 · 회전 변경을 프레임당 1회로 모은다)에서 함께 한다.
                  ⑤ 지움 · 다시 만들기 · 목록에서 빠짐 · 3D 화면 scene-missing → RetireMap3DJob 이 그 job 을 모든 상태에서 뺀다(2026-10-19).
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel : IHandle<CallDeleteMap3DAreaProcessMessageModel>
{
    private readonly Dictionary<string, Map3DAreaMarker> _map3DAreaMarkers = new();
    private Map3DAreaItem? _pendingMap3DAreaDelete;

    /// <summary>지도 메뉴 「3D 영역 목록」 항목.</summary>
    public ObservableCollection<Map3DAreaItem> Map3DAreas { get; } = new();

    /// <summary>3D 영역이 하나라도 있는가(메뉴 활성).</summary>
    public bool HasMap3DAreas => Map3DAreas.Count > 0;

    private bool _isMap3DChipVisible;
    /// <summary>줌 컨트롤 옆 "3D 가능 — 줌 17 이상" 칩.</summary>
    public bool IsMap3DChipVisible { get => _isMap3DChipVisible; private set { if (_isMap3DChipVisible == value) return; _isMap3DChipVisible = value; NotifyOfPropertyChange(); } }

    private double? _map3DChipEnterZoom;
    /// <summary>칩 글 — 화면 가운데 영역의 3D 줌(영역 밖이면 세팅값).</summary>
    public string Map3DChipText => Map3DGuide.ChipTextFor(_map3DChipEnterZoom ?? _map3DSettings.EnterZoom);

    // ── 목록 ──

    /// <summary>서버 목록을 다시 받아 영역 표시 · 게이트 장면 캐시를 맞춘다. 서버에 닿지 않으면 그대로 둔다(로그만).</summary>
    private async Task RefreshMap3DAreasAsync()
    {
        if (_map3DClient == null || MainMap == null) return;
        int seq = Interlocked.Increment(ref _map3DAreasRequestSeq);
        try
        {
            var scenes = await _map3DClient.ListScenesAsync().ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                // 늦게 보낸 요청의 목록을 이미 달았다 — 먼저 보낸 요청이 늦게 온 옛 목록이 새 영역을 "목록에서 빠짐"으로 내리지 않게 버린다
                if (seq < _map3DAreasAppliedSeq) { _log?.Info($"[Map3D] 영역 목록 — 늦게 온 옛 응답 버림(요청 {seq} < 단 것 {_map3DAreasAppliedSeq})"); return; }
                _map3DAreasAppliedSeq = seq;
                ApplyMap3DAreas(scenes);
            }).ConfigureAwait(false);
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 영역 목록 — 서버 닿지 않음: {ex.Message}");
            await OnUiAsync(() => NoteMap3DEngineDown("area-list"));   // 다시 켜지면 영역 목록을 다시 받는다(MapViewModel.Map3DReconnect)
        }
        catch (Exception ex) { _log?.Warning($"[Map3D] 영역 목록 실패: {ex.Message}"); }
    }

    /// <summary>목록 요청 순번 · 마지막으로 단 응답의 순번 — 응답이 보낸 순서와 다르게 오면 늦게 온 옛 목록을 버린다.</summary>
    private int _map3DAreasRequestSeq, _map3DAreasAppliedSeq;

    /// <summary>마지막으로 받은 서버 목록 — 굽기 시작 · 끝에 항목 메뉴(「만들기 취소」)를 다시 짠다.</summary>
    private IReadOnlyList<Map3DSceneInfo>? _map3DLastAreaScenes;

    /// <summary>마지막 서버 목록으로 영역 항목 · 배지를 다시 만든다(다시 만드는 영역의 「만들기 취소」 달기 · 떼기).</summary>
    private void ReapplyMap3DAreas()
    {
        if (_map3DLastAreaScenes is { } scenes) ApplyMap3DAreas(scenes);
    }

    private void ApplyMap3DAreas(IReadOnlyList<Map3DSceneInfo> scenes)
    {
        if (MainMap == null) return;
        _map3DLastAreaScenes = scenes.Where(s => !Map3DGoneJobs.Contains(s.Id)).ToList();   // 지운 영역은 다시 짜기에서도 빼 둔다
        // 서버 목록에서 빠진 영역(다른 곳에서 지움 등) — 표시만이 아니라 그 job 을 잡고 있는 모든 상태에서 뺀다(보던 3D 면 2D 로)
        foreach (var gone in Map3DAreas.Where(a => scenes.All(s => s.Id != a.Id)).ToList())
            RetireMap3DJob(gone.Id, "list-missing");

        foreach (var scene in scenes)
        {
            if (Map3DGoneJobs.Contains(scene.Id)) continue;   // 지우기 전에 보낸 목록 요청의 늦은 응답 — 지운 영역을 되살리지 않는다
            _map3DScenes.RemoveAll(sc => sc.Id == scene.Id);
            _map3DScenes.Add(new Map3DScene(scene.Id, scene.Bbox) { EnterZoom = scene.EnterZoom, Rect = scene.Rect });

            var item = new Map3DAreaItem(scene, Map3DAreaEnterZoom(scene.Id, scene.EnterZoom), ViewMap3DArea, OpenMap3DAreaZoomEditor,
                                         area => _ = RequestResetMap3DHeightsAsync(area), RebakeMap3DArea, area => _ = RequestDeleteMap3DAreaAsync(area),
                                         // 이 영역을 다시 만드는 중이면 「만들기 취소」(진행 판 단추와 같은 동작)
                                         _isMap3DBaking && _map3DBakeJob != null && scene.Id == _map3DBakeSourceId ? area => _ = CancelMap3DBakeAsync() : null,
                                         // 직접 그린 건물(MapViewModel.UserBuilding) — 「건물 그리기」 · 「직접 그린 건물 n개 — 모두 지우기…」
                                         StartUserBuildingDraw, area => _ = RequestClearUserBuildingsAsync(area));
            int index = Map3DAreas.ToList().FindIndex(a => a.Id == scene.Id);
            if (index >= 0) Map3DAreas[index] = item; else Map3DAreas.Add(item);

            if (_map3DAreaMarkers.TryGetValue(scene.Id, out var old)) MainMap.Markers.Remove(old);
            var marker = new Map3DAreaMarker(item);
            _map3DAreaMarkers[scene.Id] = marker;
            MainMap.Markers.Add(marker);
        }
        NotifyOfPropertyChange(nameof(HasMap3DAreas));
        NotifyMap3DUnmatchedHeights(Map3DAreas);   // 다시 굽기에서 못 붙인 직접 입력 건물 높이 알림(MapViewModel.BuildingHeight)
        _log?.Info($"[Map3D] 영역 목록 {scenes.Count}개");
        QueueMap3DGate();   // 테두리 크기 · 칩 · 게이트(새 장면)
    }

    private void RemoveMap3DArea(string id)
    {
        _map3DScenes.RemoveAll(sc => sc.Id == id);
        var item = Map3DAreas.FirstOrDefault(a => a.Id == id);
        if (item != null) Map3DAreas.Remove(item);
        if (_map3DAreaMarkers.Remove(id, out var marker)) MainMap?.Markers.Remove(marker);
        NotifyOfPropertyChange(nameof(HasMap3DAreas));
    }

    private HashSet<string>? _map3DGoneJobs;
    /// <summary>지운 · 3D 화면이 "지워졌다"고 알린 job — 늦게 온 목록 · 장면 조회 응답이 되살리지 않게(지연 초기화 — 생성자 없는 시험 VM).</summary>
    private HashSet<string> Map3DGoneJobs => _map3DGoneJobs ??= new();

    /// <summary>
    /// 서버에서 사라진(지움 · 다시 만들기로 바뀜 · 목록에서 빠짐 · 3D 화면이 scene-missing) job 을 GIS 모든 상태에서 뺀다(2026-10-19 사용자 보고 —
    /// 지운 옛 장면 scene.json 을 다시 불러 "불러오지 못했습니다: TypeError" 와 옛 장면 「건물 높이」 판이 남았다).
    /// 보던 3D(또는 준비 중)면 2D 로 돌아오고 같은 자리에서 곧바로 다시 들어가지 않는다 · 3D 호스트가 그 장면을 열어 두었으면 내려놓는다(재사용 · 미리 불러오기 ·
    /// 네이티브 창 전환 때 다시 열기 막기) · 오버레이 기록 · 「건물 높이」 판 · 「영역 보기」 진입 · 화면 가운데 영역 · 장면 캐시 · 목록 · 배지.
    /// remember = 지운 것이 확실할 때(늦은 응답이 되살리지 않게). 보던 3D 에서 나왔으면 true.
    /// </summary>
    private bool RetireMap3DJob(string job, string cause, bool remember = false)
    {
        if (remember) Map3DGoneJobs.Add(job);
        bool wasShowing = false;
        if (_map3DJob == job)
        {
            wasShowing = _map3DState != Map3DState.Off;
            if (_map3DState == Map3DState.Shown) Leave3D(Map3DExitCamera, cause);
            else if (_map3DState == Map3DState.Pending) CancelMap3DPending();
            if (wasShowing)
            {
                _map3DEntrySuppressed = true;
                SetAimStatus(Map3DGuide.SceneMissingText, autoHide: true);   // 짧은 안내 — 보던 장면이 없어졌다
            }
            _map3DJob = null;
        }
        if (_map3DAreaViewJob == job) _map3DAreaViewJob = null;
        if (_bhJob == job)
        {
            CloseBuildingHeightPanel(clearUndo: false);
            _bhJob = null;
        }
        BhUndo.RemoveAll(u => u.Job == job);   // 판 안 되돌리기 — 없는 장면에 PUT 하지 않게
        if (_ubArea?.Id == job) ExitUserBuildingDraw(cause);   // 그 영역에서 건물을 그리던 중
        if (_decoArea?.Id == job) ExitDecoration(cause);   // 그 영역을 꾸미던 중
        // 그 영역의 「다시 만들기」 · 「3D 줌 바꾸기」 판 — 남기면 [만들기] 가 사라진 영역을 다시 구워 지운 영역이 되살아났다(2026-10-20)
        if (_map3DBakeOptionsSourceId == job || _map3DBakeReplaces == job || _map3DZoomEditArea?.Id == job)
        {
            _map3DBakeReplaces = null; _map3DBakeOptionsSourceId = null;
            CloseMap3DBakeOptions();
        }
        if (_map3DHost?.Job == job)
        {
            _map3DHost.Unload();
            _map3DOverlay?.Stop();   // 그 페이지에 보낸 기록 — 다음 페이지는 전체 스냅샷부터
        }
        RemoveMap3DArea(job);
        // 마지막 서버 목록(「만들기 취소」 달기 · 떼기용 다시 짜기)이 내려놓은 영역을 되살리지 않게
        if (_map3DLastAreaScenes?.Any(s => s.Id == job) == true) _map3DLastAreaScenes = _map3DLastAreaScenes.Where(s => s.Id != job).ToList();
        if (_map3DCenterScene?.Id == job) { _map3DCenterScene = null; NotifyOfPropertyChange(nameof(IsMap3DEnterButtonVisible)); }
        _log?.Info($"[Map3D] 장면 내려놓음({cause}) job={job} 보던중={wasShowing}");
        QueueMap3DGate();
        return wasShowing;
    }

    private double? _map3DAreaBearing;

    /// <summary>
    /// 지도 방위가 바뀌었다(뷰포트 스냅샷 — 키 · 나침반 · 3D 동기 · 회전 토글 등 모든 회전 경로, 프레임당 1회) — 테두리 · 배지를 이동 · 줌과 같은
    /// 게이트 큐(프레임당 1회)로 다시 그린다. 예전에는 회전만 하면 테두리가 옛 방위로 남고 팬해야 고쳐졌다(2026-10-19 사용자 보고).
    /// </summary>
    private void OnMap3DAreaBearingChanged(double canonicalBearing)
    {
        if (_map3DAreaBearing == canonicalBearing) return;
        _map3DAreaBearing = canonicalBearing;
        if (_map3DAreaMarkers is { Count: > 0 }) QueueMap3DGate();
    }

    /// <summary>줌 · 위치 · 회전이 바뀐 뒤(게이트 큐) — 테두리 모양 · 줌 13 표시 · "3D 가능" 칩.</summary>
    private void UpdateMap3DAreaDisplay()
    {
        if (MainMap == null) return;
        double zoom = MainMap.EffectiveZoom;
        bool show = _map3DSettings.IsEnabled && Map3DGuide.ShouldShowAreaOutlines(zoom, _map3DSettings.AreaShowZoom);
        foreach (var marker in _map3DAreaMarkers.Values) marker.Refresh(MainMap, show);

        var center = MainMap.Position;
        var scene = FindMap3DSceneAt(center);   // 회전 영역은 회전 사각형 안
        double enter = Map3DGateFor(scene).Enter;   // 그 영역의 3D 줌
        if (_map3DChipEnterZoom != enter) { _map3DChipEnterZoom = enter; NotifyOfPropertyChange(nameof(Map3DChipText)); }
        // "줌 17 이상" 칩은 자동(줌)일 때만 — 수동(단추)이면 왼쪽 위 「3D 로 보기」 단추가 대신한다
        IsMap3DChipVisible = _map3DSettings.IsEnabled && Map3DMath.IsZoomEntry(_map3DSettings.EntryMode)
                             && Map3DGuide.ShouldShowChip(zoom, scene != null, _map3DState == Map3DState.Off, enter);
        if (_map3DCenterScene != scene) { _map3DCenterScene = scene; NotifyOfPropertyChange(nameof(IsMap3DEnterButtonVisible)); }
    }

    // ── 동작 ──

    /// <summary>
    /// 「이 영역 3D 로 보기」 — 영역 가운데 · 줌 17.5 로 옮기고 줌 게이트와 상관없이 바로 3D 로 들어간다(안내 패널과 함께, 2026-10-19 수동(단추) 방식).
    /// 이렇게 들어간 3D 는 영역 전체가 들어오는 시점으로 열고, 줌으로 나가지 않으며, 나가면 지금(들어오기 전) 2D 자리로 돌아간다.
    /// </summary>
    private void ViewMap3DArea(Map3DAreaItem item)
    {
        if (MainMap == null) return;
        // 이미 3D 중(또는 준비 중)이면 처음 들어오기 전 자리를 그대로 둔다(3D 를 따라 움직인 2D 자리 · 앞 「영역 보기」가 옮긴 자리가 아니라)
        var before = _map3DState != Map3DState.Off ? _map3DReturn : null;
        if (_map3DState == Map3DState.Shown) Leave3D(null, "area-view");
        else if (_map3DState == Map3DState.Pending) CancelMap3DPending();

        _map3DReturn = before ?? Current2DCamera(0d);
        _map3DAreaViewJob = item.Id;
        var (lat, lon) = item.Area.Center;   // 회전 영역은 사각형 가운데
        _map3DEntrySuppressed = false;
        MainMap.Position = new PointLatLng(lat, lon);
        // 회전 영역 — 2D 도 그릴 때 방위로 돌려 사각형이 반듯하게(회전이 막혀 있으면 2D 는 그대로, 3D 는 Reveal3D 가 그 방위로 연다)
        if (item.Area.Rect is { } rect) MainMap.MapRotation = Map3DMath.ToGMapBearing(rect.RotationDeg);
        double viewZoom = Map3DGuide.AreaViewZoomFor(item.EnterZoom);   // 그 영역 진입 줌을 높였으면 그 줌까지
        MainMap.SetEffectiveZoom(viewZoom);
        _log?.Info($"[Map3D] 영역 보기 id={item.Id} → ({lat:F6},{lon:F6}) zoom={viewZoom}");
        // 줌 게이트에 기대지 않고 바로 들어간다(수동(단추)이면 줌으로는 들어가지 않는다). 목록에 장면이 아직 없으면 게이트가 찾아 들어간다(자동).
        if (_map3DScenes.Find(sc => sc.Id == item.Id) is { } scene)
        {
            EnterMap3DDirect(scene, "area-view");
            // 거절(편집 · 그리기 · 만드는 중 등) — 이 누름이 잡은 돌아갈 자리 · 영역 보기 표지를 지운다(남으면 다음 진입이 이 자리로 돌아갔다)
            if (_map3DState == Map3DState.Off) { _map3DReturn = null; _map3DAreaViewJob = null; }
        }
        QueueMap3DGate();
    }

    /// <summary>「다시 만들기」 — 같은 영역을 다시 굽는다.</summary>
    private void RebakeMap3DArea(Map3DAreaItem item)
    {
        if (_isMap3DBaking) { SetAimStatus("이미 3D 를 만드는 중입니다", autoHide: true); return; }
        _log?.Info($"[Map3D] 영역 다시 만들기 id={item.Id}");
        RequestMap3DBake(item.Area, item.EnterZoom);   // 회전 영역은 같은 회전 사각형으로   // 영역 그리기와 같은 만들기 패널(줌은 그 영역의 지금 줌)
        _map3DBakeOptionsSourceId = item.Id;   // 만드는 동안 이 영역 항목에 「만들기 취소」 — 옛 장면은 새로 다 만들 때까지 그대로
        _map3DBakeReplaces = item.Id;   // 다 만들면 이 job 을 새 job 으로 바꾼다(ReplaceMap3DJobAsync)
    }

    /// <summary>「다시 만들기」의 옛 job — 만들기 패널 확정(BakeMap3DAsync)이 가져간다. 영역 그리기는 null.</summary>
    private string? _map3DBakeReplaces;

    /// <summary>
    /// 「다시 만들기」가 끝났다 — 옛 job 을 GIS 모든 상태에서 빼고(보던 3D 면 2D 로) 서버에서도 지운다. 남겨 두면 겹친 옛 영역이 목록 · 배지에 남고
    /// 3D 호스트에 열어 둔 옛 장면을 다시 쓰다가 지워지면 404 로 깨졌다(2026-10-19). 서버가 지웠다고 답할 때만 내려놓는다 —
    /// 지우지 못했거나(견본 · 서버) 서버가 모르는 장면이면(404) 그대로 두고 서버 목록이 판단한다(새 장면이 앞에 있어 먼저 잡힌다).
    /// </summary>
    private async Task ReplaceMap3DJobAsync(string oldJob, string newJob)
    {
        try
        {
            if (!await _map3DClient!.DeleteSceneAsync(oldJob))
            {
                _log?.Info($"[Map3D] 다시 만들기 — 옛 장면 {oldJob} 은 서버에 없음(목록이 판단) → {newJob}");
                return;
            }
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Map3D] 다시 만들기 — 옛 장면 {oldJob} 지우기 실패(새 장면 {newJob} 이 앞선다): {ex.Message}");
            return;
        }
        RetireMap3DJob(oldJob, "rebake", remember: true);
        try { Map3DAreaZooms.Remove(oldJob); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 영역 줌 로컬 설정 정리 실패: {ex.Message}"); }
        _log?.Info($"[Map3D] 다시 만들기 — 옛 장면 지움 {oldJob} → {newJob}");
    }

    /// <summary>
    /// 「3D 줌 바꾸기…」 확정 — 다시 굽지 않고 서버에 PATCH. 서버가 그 기능을 모르면(404 · 405) GIS 로컬 설정에 저장하고(그 값이 앞선다),
    /// 서버가 받으면 로컬 값은 지운다. 목록을 다시 받아 배지 · 목록 · 칩 · 게이트에 반영한다.
    /// </summary>
    private async Task ChangeMap3DAreaZoomAsync(Map3DAreaItem item, double enterZoom)
    {
        if (_map3DClient == null) return;
        try
        {
            bool stored = await _map3DClient.UpdateSceneEnterZoomAsync(item.Id, enterZoom);
            if (stored) Map3DAreaZooms.Remove(item.Id);
            else Map3DAreaZooms.Set(item.Id, enterZoom);
            _log?.Info($"[Map3D] 영역 줌 바꿈 id={item.Id} zoom={enterZoom} → {(stored ? "서버" : "GIS 로컬(서버가 모름)")}");
            int index = _map3DScenes.FindIndex(sc => sc.Id == item.Id);
            if (index >= 0) _map3DScenes[index] = _map3DScenes[index] with { EnterZoom = enterZoom };
            SetAimStatus($"3D 줌을 {Map3DGuide.Zoom(enterZoom)} 이상으로 바꿨습니다", autoHide: true);
            QueueMap3DGate();
            _ = RefreshMap3DAreasAsync();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 영역 줌 바꾸기 — 서버 닿지 않음: {ex.Message}");
            SetAimStatus(Map3DUnavailableText, autoHide: true);
            NoteMap3DEngineDown("area-zoom");
        }
        catch (Map3DRequestException ex) { SetAimStatus($"3D 줌을 바꿀 수 없습니다: {ex.Message}", autoHide: true); }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 영역 줌 바꾸기 실패: {ex.Message}");
            SetAimStatus("3D 줌 바꾸기에 실패했습니다.", autoHide: true);
        }
    }

    /// <summary>「지우기」 — 표준 확인 팝업(EventAggregator). "확인" → HandleAsync(CallDeleteMap3DAreaProcessMessageModel).</summary>
    private async Task RequestDeleteMap3DAreaAsync(Map3DAreaItem item)
    {
        if (RefuseDeleteWhileRebaking(item)) return;   // 굽기 전에 연 메뉴 — 확인 창도 띄우지 않는다
        try
        {
            _pendingMap3DAreaDelete = item;
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = "3D 영역 지우기",
                Explain = $"3D 영역 '{item.Title}' 을(를) 지우시겠습니까?\n지운 영역은 다시 만들어야 3D 로 볼 수 있습니다.",
                MessageModel = new CallDeleteMap3DAreaProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 영역 지우기 확인 창 실패: {ex.Message}"); }
    }

    /// <summary>
    /// 다시 만드는 중인 그 영역은 지우지 않는다(안내만) — 지우면 굽기가 끝난 뒤 새 장면이 그 자리에 들어가 지운 영역이 되살아났다(2026-10-19 D1).
    /// 메뉴에서는 이미 빠지지만(Map3DAreaItem) 굽기 전에 연 메뉴 · 확인 창으로 와도 막는다. 막았으면 true.
    /// </summary>
    private bool RefuseDeleteWhileRebaking(Map3DAreaItem item)
    {
        if (!_isMap3DBaking || item.Id != _map3DBakeSourceId) return false;
        _log?.Info($"[Map3D] 다시 만드는 중 — 영역 지우기 막음 id={item.Id}");
        SetAimStatus("만드는 중에는 지울 수 없습니다 — 「만들기 취소」 뒤 지우세요", autoHide: true);
        return true;
    }

    /// <summary>지우기 확인 콜백 — 보고 있던 영역이면 먼저 2D 로, 서버에서 지우고 표시를 뺀다.</summary>
    public async Task HandleAsync(CallDeleteMap3DAreaProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingMap3DAreaDelete;
        _pendingMap3DAreaDelete = null;
        try
        {
            if (item == null || _map3DClient == null) return;
            if (RefuseDeleteWhileRebaking(item)) return;
            if (_map3DJob == item.Id)
            {
                if (_map3DState == Map3DState.Shown) Leave3D(Map3DExitCamera, "area-delete");
                else if (_map3DState == Map3DState.Pending) CancelMap3DPending();
            }
            bool removed = await _map3DClient.DeleteSceneAsync(item.Id, cancellationToken);
            RetireMap3DJob(item.Id, "area-delete", remember: true);   // 목록 · 배지만이 아니라 3D 호스트에 열어 둔 장면 · 판 · 진입 대상까지
            try { Map3DAreaZooms.Remove(item.Id); }   // 지운 영역의 로컬 줌도 지운다
            catch (Exception ex) { _log?.Warning($"[Map3D] 영역 줌 로컬 설정 정리 실패: {ex.Message}"); }
            _log?.Info($"[Map3D] 영역 지움 id={item.Id} removed={removed}");
            SetAimStatus(removed ? "3D 영역을 지웠습니다" : "이미 지워진 3D 영역입니다", autoHide: true);
            QueueMap3DGate();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 영역 지우기 — 서버 닿지 않음: {ex.Message}");
            SetAimStatus(Map3DUnavailableText, autoHide: true);
            NoteMap3DEngineDown("area-delete");
        }
        catch (Map3DRequestException ex) { SetAimStatus($"3D 영역을 지울 수 없습니다: {ex.Message}", autoHide: true); }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 영역 지우기 실패: {ex.Message}");
            SetAimStatus("3D 영역 지우기에 실패했습니다.", autoHide: true);
        }
        finally
        {
            // 확인 팝업은 콜백 쪽이 닫는다(ClickOk 는 메시지만 발행 — 카메라 전체 닫기 확인과 같은 패턴)
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }
}
