using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 화면 위 GIS 창 — 카메라 상자 · 조립 카드가 3D 에서 마커가 보이는 자리를 따라가고, 3D ↔ 2D 전환 때 닫히지 않고 옮겨 간다.
   Note         : ★ WPF 안 3D(composition)가 펼쳐진 동안만 — 네이티브 창 3D(hwnd)는 WPF 창을 덮으므로 창을 열 때 2D 로 돌아온다(Map3DGuide.ShouldLeave3DForWindow).
                  ★ 3D 에 지켜볼 목록(overlay.watch: 열린 카메라 상자의 카메라 마커 · 열린 조립 카드의 심볼)을 보내면, 3D 가 그린 뒤 바뀐 화면 좌표만
                    overlay.screen 으로 돌려준다(마우스 이동마다가 아니라 카메라 · 렌더가 바뀔 때만). 목록은 펼칠 때 · 상자를 열고 닫을 때 · 카드를 열 때 보낸다.
                  ★ 카메라 상자 = 마커 3D 화면 자리 + 2D 화면 오프셋(앵커 − 마커)을 3D 줌 · 방위에 맞춰 키우고 돌린 것(Map3DScreen).
                    3D 에서 끌어다 놓으면 그 오프셋을 2D 로 되돌려 위경도 앵커로 저장한다 — 2D 로 돌아가도 같은 자리 관계다.
                    마커가 카메라 뒤로 가면(좌표 없음) 상자는 마지막 자리에 둔다(2D 와 같이 감추거나 화면 안에 가두지 않는다).
                  ★ 조립 카드 = 같은 VM 을 3D 위 층(Map3DViewHost.OverlayLayer)으로 옮겨 2D 와 같은 배치 규칙(ComponentCardPlacement)으로 붙인다.
                  ★ 2D 로 돌아오면 2D 카메라를 맞춘 뒤 2D 규칙(RefreshCameraPopupPositions · 어도너)으로 같은 마커의 새 자리에 놓는다.
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    /// <summary>3D 가 마지막으로 알려 준 화면 좌표(CSS px) — key → 점. 카메라 뒤로 가면 지운다.</summary>
    private readonly Dictionary<string, Point> _map3DScreens = new();
    /// <summary>마지막 overlay.screen 의 그 프레임 3D 줌 · 방위 — 상자 오프셋을 마커 자리와 같은 프레임 값으로 셈한다(옛 3D 는 null).</summary>
    private Map3DScreenView? _map3DScreenView;
    /// <summary>카메라 id → 줌 0 · 방위 0 기준 상자 오프셋(Map3DScreen.BaseOffset) — 앵커 · 마커 위경도가 같으면 3D 동안 다시 재지 않는다(2D 정수 반올림 떨림).</summary>
    private readonly Dictionary<int, (PointLatLng Anchor, PointLatLng Camera, (double X, double Y) Base)> _map3DPopupOffsets = new();
    private bool _map3DWindowsHooked;

    /// <summary>WPF 안 3D 가 펼쳐져 있어 창이 3D 마커 자리를 따라가는가.</summary>
    private bool IsMap3DWindowFollow => _map3DState == Map3DState.Shown && Map3DHost.HostMode == "composition";

    private static string Map3DCardKey(int markerId) => $"component-card:{markerId}";

    /// <summary>3D 를 펼쳤다(Reveal3D 끝) — 카드를 3D 위 층으로 옮기고 지켜볼 목록을 보낸다.</summary>
    private void BeginMap3DWindows()
    {
        if (!IsMap3DWindowFollow) return;
        try
        {
            if (!_map3DWindowsHooked)
            {
                CameraPopups.CollectionChanged += OnCameraPopupsChangedFor3D;
                _map3DWindowsHooked = true;
            }
            _componentCard?.UseOverlay(Map3DHost.OverlayLayer, Map3DCardAnchor);
            SyncMap3DWatch();
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 3D 창 따라가기 시작 실패(격리 — 창만): {ex.Message}"); }
    }

    /// <summary>2D 로 돌아왔다(Leave3D — 2D 카메라를 맞춘 뒤) — 창을 닫지 않고 2D 규칙으로 같은 마커의 2D 자리에 놓는다.</summary>
    private void EndMap3DWindows()
    {
        if (_map3DWindowsHooked)
        {
            CameraPopups.CollectionChanged -= OnCameraPopupsChangedFor3D;
            _map3DWindowsHooked = false;
        }
        _map3DScreens.Clear();
        _map3DScreenView = null;
        _map3DPopupOffsets.Clear();
        try
        {
            _componentCard?.UseOverlay(null, null);
            RefreshCameraPopupPositions();
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 창 2D 자리로 옮기기 실패(격리 — 창만): {ex.Message}"); }
    }

    private void OnCameraPopupsChangedFor3D(object? sender, NotifyCollectionChangedEventArgs e) => SyncMap3DWatch();

    /// <summary>지켜볼 목록(열린 카메라 상자의 카메라 마커 · 열린 조립 카드의 심볼)을 3D 에 보낸다 — 통째로 바꾸기.</summary>
    private void SyncMap3DWatch()
    {
        if (MainMap == null || !IsMap3DWindowFollow) return;
        var items = new List<Map3DWatchItem>();
        foreach (var vm in CameraPopups)
        {
            var marker = MainMap.Markers.OfType<GMapPidsMarker>().FirstOrDefault(p => p.LinkedDeviceId == vm.CameraId);
            items.Add(new Map3DWatchItem(Map3DScreen.CameraPopupKey(vm.CameraId),
                marker != null ? Map3DOverlayBridge.KindOf(marker) : null, marker?.Id, vm.CameraGeo.Lat, vm.CameraGeo.Lng, 0d));
        }
        if (_componentCard?.Current is { } card)
            items.Add(new Map3DWatchItem(Map3DCardKey(card.Id), Map3DOverlayBridge.KindOf(card), card.Id, card.Position.Lat, card.Position.Lng, 0d));
        foreach (var gone in _map3DScreens.Keys.Where(k => items.All(i => i.Key != k)).ToList()) _map3DScreens.Remove(gone);   // 다시 열 때 옛 좌표를 쓰지 않게
        try { Map3DHost.PostJson(Map3DScreen.WatchJson(items)); }
        catch (Exception ex) { _log?.Error($"[Map3D] 지켜볼 목록 보내기 실패(격리): {ex.Message}"); }
    }

    /// <summary>3D 의 overlay.screen — 좌표(와 그 프레임 줌 · 방위)를 기록하고 따라가는 중이면 창 자리를 다시 잡는다.</summary>
    private void OnMap3DScreens(IReadOnlyList<Map3DScreenPoint> screens, Map3DScreenView? view)
    {
        foreach (var s in screens)
        {
            if (s.X is double x && s.Y is double y) _map3DScreens[s.Key] = new Point(x, y);
            else _map3DScreens.Remove(s.Key);   // 카메라 뒤 — 창은 마지막 자리에
        }
        if (view != null) _map3DScreenView = view;
        if (!IsMap3DWindowFollow) return;
        TryPlaceCameraPopupsIn3D(fromScreens: true);
        _componentCard?.Relayout();
    }

    /// <summary>
    /// 3D 를 따라가는 중이면 카메라 상자 자리를 3D 화면 기준으로 잡고 true(2D 규칙을 건너뛴다). 화면 좌표를 아직 모르는 상자 · 끄는 중인 상자는 그대로 둔다.
    /// (RefreshCameraPopupPositions 서두 — 3D 카메라를 2D 에 맞추며 생기는 2D 팬 · 줌 통지도 여기로 온다. 같은 프레임 값(view)을 받는 동안은
    ///  그 통지로는 놓지 않는다 — overlay.screen 한 출처만, Map3DScreen.PlaceOn2DChange)
    /// </summary>
    private bool TryPlaceCameraPopupsIn3D(bool fromScreens = false)
    {
        if (MainMap == null || _cameraPopups == null || !IsMap3DWindowFollow) return false;
        if (!fromScreens && !Map3DScreen.PlaceOn2DChange(_map3DScreenView)) return true;
        var view = Map3DViewPair();
        foreach (var vm in _cameraPopups.ToList())
        {
            if (vm.IsWindowDragging) continue;   // 끄는 중 — 놓으면 끌기와 번갈아 자리를 정해 떨린다(놓으면 DragCompleted 가 새 앵커로 저장)
            if (Map3DScreenOuter(Map3DScreen.CameraPopupKey(vm.CameraId)) is not Point cam) continue;
            if (!_map3DPopupOffsets.TryGetValue(vm.CameraId, out var off) || off.Anchor != vm.AnchorGeo || off.Camera != vm.CameraGeo)
            {
                var c = Outer2D(vm.CameraGeo);
                var a = Outer2D(vm.AnchorGeo);
                off = (vm.AnchorGeo, vm.CameraGeo, Map3DScreen.BaseOffset((c.X, c.Y), (a.X, a.Y), view.Zoom2D, view.Bearing2D));
                _map3DPopupOffsets[vm.CameraId] = off;
            }
            var (left, top) = Map3DScreen.PopupTopLeft3DFromBase((cam.X, cam.Y), off.Base, view.Zoom3D, view.Bearing3D);
            vm.CameraScreenX = cam.X;
            vm.CameraScreenY = cam.Y;
            vm.CanvasLeft = left;
            vm.CanvasTop = top;
        }
        return true;
    }

    /// <summary>3D 에서 끌어다 놓은 카메라 상자 → 2D 위경도 앵커(3D 오프셋을 2D 로 되돌린 것). 3D 를 따라가는 중이 아니거나 마커 자리를 모르면 null.</summary>
    private PointLatLng? Map3DDraggedAnchor(CameraStreamPopupViewModel vm)
    {
        if (MainMap == null || !IsMap3DWindowFollow || Map3DScreenOuter(Map3DScreen.CameraPopupKey(vm.CameraId)) is not Point cam) return null;
        var view = Map3DViewPair();
        var c = Outer2D(vm.CameraGeo);
        var (x, y) = Map3DScreen.DraggedAnchor2D((vm.CanvasLeft, vm.CanvasTop), (cam.X, cam.Y), (c.X, c.Y), view.Zoom2D, view.Zoom3D, view.Bearing2D, view.Bearing3D);
        var ip = MainMap.OuterToInner(new Point(x, y));
        return MainMap.FromLocalToLatLng((int)ip.X, (int)ip.Y);
    }

    /// <summary>조립 카드가 붙을 자리(3D 위 층 = 3D 호스트 좌표). 모르면 null.</summary>
    private Point? Map3DCardAnchor(GMapPidsMarker marker)
    {
        if (!_map3DScreens.TryGetValue(Map3DCardKey(marker.Id), out var css)) return null;
        var (x, y) = Map3DScreen.ToOuter(css.X, css.Y, Map3DHost.ZoomFactor, 0d, 0d);
        return new Point(x, y);
    }

    /// <summary>3D 화면 좌표(CSS px) → 지도 칸(outer — 카메라 상자 캔버스) 좌표. 모르면 null.</summary>
    private Point? Map3DScreenOuter(string key)
    {
        if (MainMap == null || !_map3DScreens.TryGetValue(key, out var css)) return null;
        var origin = MainMap.InnerToOuter(Map3DHost.TranslatePoint(new Point(0, 0), MainMap));   // 3D 호스트 왼쪽 위(지도 칸 기준)
        var (x, y) = Map3DScreen.ToOuter(css.X, css.Y, Map3DHost.ZoomFactor, origin.X, origin.Y);
        return new Point(x, y);
    }

    /// <summary>위경도 → 2D 지도 칸(outer) 좌표(카메라 상자 2D 규칙과 같은 투영).</summary>
    private Point Outer2D(PointLatLng p)
    {
        var g = MainMap!.FromLatLngToLocal(p);
        return MainMap.InnerToOuter(new Point(g.X, g.Y));
    }

    /// <summary>지금 2D(메르카토르 줌 · 방위)와 3D — 3D 는 마커 자리와 같은 프레임 값이 먼저(Map3DScreen.OffsetView3D), 보고가 아직 없으면(펼친 직후) 2D 와 같다고 본다.</summary>
    private (double Zoom2D, double Zoom3D, double Bearing2D, double Bearing3D) Map3DViewPair()
    {
        var cam2 = Current2DCamera(0d);
        var (zoom3, bearing3) = Map3DScreen.OffsetView3D(_map3DScreenView, _map3DLastCamera, cam2.Zoom, cam2.Bearing);
        return (cam2.Zoom, zoom3, cam2.Bearing, bearing3);
    }
}
