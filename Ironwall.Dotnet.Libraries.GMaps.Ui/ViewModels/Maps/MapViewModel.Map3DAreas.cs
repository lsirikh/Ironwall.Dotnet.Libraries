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
   Note         : 영역 마커는 편집 심볼이 아니다(DB 저장 없음). 판정 · 글은 순수 함수 Map3DGuide(헤드리스 시험).
                  갱신은 3D 게이트 큐(QueueMap3DGate — 줌 · 위치 변경을 프레임당 1회로 모은다)에서 함께 한다.
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

    public string Map3DChipText => Map3DGuide.ChipText;

    // ── 목록 ──

    /// <summary>서버 목록을 다시 받아 영역 표시 · 게이트 장면 캐시를 맞춘다. 서버에 닿지 않으면 그대로 둔다(로그만).</summary>
    private async Task RefreshMap3DAreasAsync()
    {
        if (_map3DClient == null || MainMap == null) return;
        try
        {
            var scenes = await _map3DClient.ListScenesAsync().ConfigureAwait(false);
            await OnUiAsync(() => ApplyMap3DAreas(scenes)).ConfigureAwait(false);
        }
        catch (Map3DServerUnavailableException ex) { _log?.Warning($"[Map3D] 영역 목록 — 서버 닿지 않음: {ex.Message}"); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 영역 목록 실패: {ex.Message}"); }
    }

    private void ApplyMap3DAreas(IReadOnlyList<Map3DSceneInfo> scenes)
    {
        if (MainMap == null) return;
        foreach (var gone in Map3DAreas.Where(a => scenes.All(s => s.Id != a.Id)).ToList())
            RemoveMap3DArea(gone.Id);

        foreach (var scene in scenes)
        {
            _map3DScenes.RemoveAll(sc => sc.Id == scene.Id);
            _map3DScenes.Add(new Map3DScene(scene.Id, scene.Bbox));

            var item = new Map3DAreaItem(scene, ViewMap3DArea, RebakeMap3DArea, area => _ = RequestDeleteMap3DAreaAsync(area));
            int index = Map3DAreas.ToList().FindIndex(a => a.Id == scene.Id);
            if (index >= 0) Map3DAreas[index] = item; else Map3DAreas.Add(item);

            if (_map3DAreaMarkers.TryGetValue(scene.Id, out var old)) MainMap.Markers.Remove(old);
            var marker = new Map3DAreaMarker(item);
            _map3DAreaMarkers[scene.Id] = marker;
            MainMap.Markers.Add(marker);
        }
        NotifyOfPropertyChange(nameof(HasMap3DAreas));
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

    /// <summary>줌 · 위치가 바뀐 뒤(게이트 큐) — 테두리 크기 · 줌 13 표시 · "3D 가능" 칩.</summary>
    private void UpdateMap3DAreaDisplay()
    {
        if (MainMap == null) return;
        double zoom = MainMap.EffectiveZoom;
        bool show = _map3DSettings.IsEnabled && Map3DGuide.ShouldShowAreaOutlines(zoom);
        foreach (var marker in _map3DAreaMarkers.Values) marker.Refresh(MainMap, show);

        var center = MainMap.Position;
        bool inside = _map3DScenes.Exists(sc => Map3DMath.BboxContains(sc.Bbox, center.Lng, center.Lat));
        IsMap3DChipVisible = _map3DSettings.IsEnabled && Map3DGuide.ShouldShowChip(zoom, inside, _map3DState == Map3DState.Off);
    }

    // ── 동작 ──

    /// <summary>「이 영역 3D 로 보기」 — 영역 가운데 · 줌 17.5 로 옮기면 게이트가 3D 로 들어간다(안내 패널과 함께).</summary>
    private void ViewMap3DArea(Map3DAreaItem item)
    {
        if (MainMap == null) return;
        if (_map3DState == Map3DState.Shown) Leave3D(null, "area-view");
        else if (_map3DState == Map3DState.Pending) CancelMap3DPending();

        var (lat, lon) = Map3DGuide.AreaCenter(item.Bbox);
        _map3DEntrySuppressed = false;
        MainMap.Position = new PointLatLng(lat, lon);
        MainMap.SetEffectiveZoom(Map3DGuide.AreaViewZoom);
        _log?.Info($"[Map3D] 영역 보기 id={item.Id} → ({lat:F6},{lon:F6}) zoom={Map3DGuide.AreaViewZoom}");
        if (IsMap3DBlockedByInputMode())
            SetAimStatus("편집 · 그리기 중에는 3D 로 들어가지 않습니다 — 모드를 끝내면 3D 로 바뀝니다", autoHide: true);
        QueueMap3DGate();
    }

    /// <summary>「다시 만들기」 — 같은 영역을 다시 굽는다.</summary>
    private void RebakeMap3DArea(Map3DAreaItem item)
    {
        if (_isMap3DBaking) { SetAimStatus("이미 3D 를 만드는 중입니다", autoHide: true); return; }
        _log?.Info($"[Map3D] 영역 다시 만들기 id={item.Id}");
        RequestMap3DBake(item.Bbox);   // 영역 그리기와 같은 굽기 방식 패널
    }

    /// <summary>「지우기」 — 표준 확인 팝업(EventAggregator). "확인" → HandleAsync(CallDeleteMap3DAreaProcessMessageModel).</summary>
    private async Task RequestDeleteMap3DAreaAsync(Map3DAreaItem item)
    {
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

    /// <summary>지우기 확인 콜백 — 보고 있던 영역이면 먼저 2D 로, 서버에서 지우고 표시를 뺀다.</summary>
    public async Task HandleAsync(CallDeleteMap3DAreaProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingMap3DAreaDelete;
        _pendingMap3DAreaDelete = null;
        try
        {
            if (item == null || _map3DClient == null) return;
            if (_map3DJob == item.Id)
            {
                if (_map3DState == Map3DState.Shown) Leave3D(_map3DLastCamera, "area-delete");
                else if (_map3DState == Map3DState.Pending) CancelMap3DPending();
            }
            bool removed = await _map3DClient.DeleteSceneAsync(item.Id, cancellationToken);
            RemoveMap3DArea(item.Id);
            _log?.Info($"[Map3D] 영역 지움 id={item.Id} removed={removed}");
            SetAimStatus(removed ? "3D 영역을 지웠습니다" : "이미 지워진 3D 영역입니다", autoHide: true);
            QueueMap3DGate();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 영역 지우기 — 서버 닿지 않음: {ex.Message}");
            SetAimStatus(Map3DUnavailableText, autoHide: true);
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
