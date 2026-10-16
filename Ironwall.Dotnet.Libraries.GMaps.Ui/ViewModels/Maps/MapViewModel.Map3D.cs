using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 지도 3D 바꿔 끼우기(map-3d POC) 배선 — 영역 그리기 → 굽기 → 줌 게이트 → WebView2 3D 화면 ↔ 2D 복귀.
   Note         : ★ 영역 그리기는 새 드래그를 붙이지 않고 앵커 "영역 그리기 모드"(IsAnchorDrawMode · AnchorAreaDrawn · ESC 취소)를
                    그대로 쓴다 — _isMap3DDrawMode 가 켜져 있으면 OnAnchorAreaDrawn 이 결과를 3D 굽기로 넘긴다. Enter = 지금 화면 전체.
                  ★ 게이트 판정(Map3DMath.Decide)은 줌 · 디지털줌 · 위치 변경을 Render 우선순위로 모아 프레임당 1회(틸트 코얼레싱과 같은 이유 —
                    휠/스텝은 정수 줌 → 디지털 줌으로 두 번 발화한다).
                  ★ 장면 유무(/api/scenes/at)는 지도 중심이 바뀔 때만 400 ms 디바운스로 묻고, 받은 장면은 목록에 캐시한다.
                  ★ 상태: Off → Pending(3D 로딩 — 2D 그대로 보이고 조작 가능, 줌 16.5 밑이면 취소) → Shown(ready 뒤 펼침, 2D 입력 막음).
                    Shown 에서 3D 가 보고한 줌 &lt; 16.5 또는 ESC → 2D 로 돌아오며 위치 · 줌 · 방위를 2D 에 되돌린다.
                  ★ 서버에 닿지 않으면 "3D 서버(map-3d)가 꺼져 있습니다" 배너만 띄우고 아무것도 바꾸지 않는다.
                  ★ 2차(2026-10-17): 편집 · 독점 입력 모드(ExclusiveInputModeGate) 중에는 3D 에 들어가지 않고, 3D 중 그 모드에 들어가면 먼저 2D 로 돌아온다.
                    3D 가 보이는 동안 마커를 오버레이 다리(Map3DOverlayBridge)로 보내고, 3D 의 pick 을 지도 마커 클릭 경로로 넘긴다.
   Created On   : 2026-10-16 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private enum Map3DState { Off, Pending, Shown }

    private const string Map3DUnavailableText = "3D 서버(map-3d)가 꺼져 있습니다";

    private Map3DModel _map3DSettings = new();
    private Map3DClient? _map3DClient;
    private Map3DViewHost? _map3DHost;
    private Map3DState _map3DState;
    private string? _map3DJob;
    private bool _isMap3DDrawMode;
    private bool _isMap3DBaking;
    private bool _map3DGateQueued;
    /// <summary>ESC · 로딩 실패 뒤 같은 줌에서 곧바로 다시 들어가지 않게 — 게이트가 한 번 꺼질(줌 &lt; 17 · 장면 밖) 때까지 진입 보류.</summary>
    private bool _map3DEntrySuppressed;
    private Map3DCamera? _map3DLastCamera;
    private readonly List<Map3DScene> _map3DScenes = new();
    private DispatcherTimer? _map3DLookupTimer;
    private CancellationTokenSource? _map3DLookupCts;
    private PointLatLng? _map3DLastLookupCenter;
    private Map3DOverlayBridge? _map3DOverlay;
    /// <summary>3D 우클릭 메뉴 자리(지도 컨트롤 좌표) — ShowMarkerContextMenu 가 MousePoint 대신 쓴다(3D pick 처리 중에만 값이 있다).</summary>
    private Point? _map3DMenuAt;

    private Map3DOverlayBridge Map3DOverlay => _map3DOverlay ??= new Map3DOverlayBridge(json => Map3DHost.PostJson(json), _log);

    /// <summary>지도 위 3D 화면 호스트(MapView.xaml 의 ContentControl). WebView2 는 처음 3D 에 들어갈 때 1회 만든다.</summary>
    public Map3DViewHost Map3DHost => _map3DHost ??= CreateMap3DHost();

    private RelayCommand? _drawMap3DAreaCommand;
    /// <summary>3D 영역 그리기 모드 진입(툴바 "기준" 묶음).</summary>
    public RelayCommand DrawMap3DAreaCommand
        => _drawMap3DAreaCommand ??= new RelayCommand(_ => EnterMap3DDrawMode(), _ => MainMap != null && !_isMap3DBaking);

    private Map3DViewHost CreateMap3DHost()
    {
        var host = new Map3DViewHost();
        host.MessageReceived += OnMap3DMessage;
        host.LoadFailed += OnMap3DLoadFailed;
        return host;
    }

    // ── 설정 · 구독(ConfigureCommonMapSettings 에서 호출 — −=/+= 멱등) ──

    private void LoadMap3DFromSettings()
    {
        if (MainMap == null) return;
        _map3DSettings = MapSettingsHelper.LoadMap3D(_log);
        _map3DClient?.Dispose();
        _map3DClient = new Map3DClient(_map3DSettings.ServerUrl);

        MainMap.OnPositionChanged -= OnMap3DPositionChanged;
        MainMap.OnPositionChanged += OnMap3DPositionChanged;
        MainMap.OnMapZoomChanged -= OnMap3DZoomChanged;
        MainMap.OnMapZoomChanged += OnMap3DZoomChanged;
        MainMap.DigitalZoomLevelChanged -= OnMap3DDigitalZoomChanged;
        MainMap.DigitalZoomLevelChanged += OnMap3DDigitalZoomChanged;
        QueueMap3DGate();
    }

    private void OnMap3DPositionChanged(PointLatLng _) => QueueMap3DGate();
    private void OnMap3DZoomChanged() => QueueMap3DGate();
    private void OnMap3DDigitalZoomChanged(int _) => QueueMap3DGate();

    // ── 영역 그리기(앵커 러버밴드 재사용) ──

    private void EnterMap3DDrawMode()
    {
        if (MainMap == null) return;
        EnterAnchorDrawMode();   // 러버밴드 · 십자 커서 · ESC 후킹 · 포커스
        _isMap3DDrawMode = true;
        SetAimStatus("지도에서 드래그해 3D 로 만들 영역을 그리세요 — (Enter = 지금 화면 전체 · ESC = 취소)");
    }

    /// <summary>앵커 그리기 완료가 3D 그리기였으면 굽기로 넘긴다(OnAnchorAreaDrawn 서두에서 호출).</summary>
    private bool TryTakeMap3DArea(PointLatLng nw, PointLatLng se)
    {
        if (!_isMap3DDrawMode) return false;
        ExitAnchorDrawMode();
        _ = BakeMap3DAsync(Map3DMath.BuildBbox(nw.Lat, nw.Lng, se.Lat, se.Lng));
        return true;
    }

    /// <summary>창 PreviewKeyDown(OnMapPreviewKeyDownForAim) 서두 — 3D 그리기 중 Enter = 지금 화면 전체, 3D 화면 중 ESC = 2D 복귀.</summary>
    private bool TryHandleMap3DKey(System.Windows.Input.KeyEventArgs e)
    {
        if (MainMap == null) return false;
        if (e.Key == System.Windows.Input.Key.Enter && _isMap3DDrawMode && MainMap.IsAnchorDrawMode)
        {
            // 보이는 영역 = ViewArea(코어 정수줌)를 디지털 줌 배율로 줄인 것(SetAnchorFromCurrentView 와 같은 보정)
            var v = MainMap.ViewArea;
            double s = MainMap.DigitalZoomScale;
            double cLat = v.Lat - v.HeightLat / 2.0, cLng = v.Lng + v.WidthLng / 2.0;
            double hLat = v.HeightLat / 2.0 / s, hLng = v.WidthLng / 2.0 / s;
            ExitAnchorDrawMode();
            _ = BakeMap3DAsync(Map3DMath.BuildBbox(cLat + hLat, cLng - hLng, cLat - hLat, cLng + hLng));
            e.Handled = true;
            return true;
        }
        if (e.Key == System.Windows.Input.Key.Escape && _map3DState == Map3DState.Shown)
        {
            _map3DEntrySuppressed = true;
            Leave3D(_map3DLastCamera, "esc");
            e.Handled = true;
            return true;
        }
        return false;
    }

    // ── 굽기 ──

    private async Task BakeMap3DAsync(double[] bbox)
    {
        if (_map3DClient == null || _isMap3DBaking) return;
        _isMap3DBaking = true;
        DrawMap3DAreaCommand.RaiseCanExecuteChanged();
        try
        {
            _log?.Info($"[Map3D] 굽기 요청 bbox=[{string.Join(", ", bbox)}]");
            var job = await _map3DClient.StartBakeAsync(bbox);
            while (!job.IsDone && !job.IsError)
            {
                SetAimStatus($"3D 만드는 중… {job.Progress:P0}{(string.IsNullOrEmpty(job.Message) ? "" : $" — {job.Message}")}");
                await Task.Delay(1000);
                job = await _map3DClient.GetJobAsync(job.Id);
            }
            if (job.IsError)
            {
                SetAimStatus($"3D 만들기 실패: {job.Message ?? "알 수 없는 오류"}", autoHide: true);
                return;
            }
            _map3DScenes.RemoveAll(sc => sc.Id == job.Id);
            _map3DScenes.Add(new Map3DScene(job.Id, bbox));
            _map3DEntrySuppressed = false;
            SetAimStatus("3D 준비됨 — 줌 17 이상에서 3D", autoHide: true);
            QueueMap3DGate();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 서버 닿지 않음: {ex.Message}");
            SetAimStatus(Map3DUnavailableText, autoHide: true);
        }
        catch (Map3DRequestException ex)
        {
            SetAimStatus($"3D 를 만들 수 없습니다: {ex.Message}", autoHide: true);
        }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 굽기 실패: {ex.Message}");
            SetAimStatus("3D 만들기에 실패했습니다.", autoHide: true);
        }
        finally
        {
            _isMap3DBaking = false;
            DrawMap3DAreaCommand.RaiseCanExecuteChanged();
        }
    }

    // ── 게이트(코얼레싱 · 장면 조회) ──

    private void QueueMap3DGate()
    {
        if (_map3DGateQueued || MainMap == null) return;
        _map3DGateQueued = true;
        MainMap.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _map3DGateQueued = false;
            try { EvaluateMap3DGate(); }
            catch (Exception ex) { _log?.Error($"[Map3D] 게이트 판정 실패(격리): {ex.Message}"); }
        }));
    }

    private void EvaluateMap3DGate()
    {
        if (MainMap == null || !_map3DSettings.IsEnabled || _map3DState == Map3DState.Shown) return;
        var center = MainMap.Position;
        double zoom = MainMap.EffectiveZoom;
        var scene = _map3DScenes.Find(sc => Map3DMath.BboxContains(sc.Bbox, center.Lng, center.Lat));
        bool active = !IsMap3DBlockedByInputMode() && Map3DMath.Decide(zoom, _map3DState == Map3DState.Pending, scene != null);

        if (!Map3DMath.Decide(zoom, false, scene != null)) _map3DEntrySuppressed = false;   // 한 번 꺼지면 보류 해제
        if (scene == null && zoom >= Map3DMath.ExitZoom) QueueMap3DSceneLookup(center);

        if (active && _map3DState == Map3DState.Off && !_map3DEntrySuppressed) Begin3D(scene!);
        else if (!active && _map3DState == Map3DState.Pending)
        {
            _log?.Info($"[Map3D] 로딩 중 게이트 이탈(zoom={zoom:F1}) → 취소");
            _map3DState = Map3DState.Off;
            Map3DHost.Park();
        }
    }

    /// <summary>지도 중심이 바뀌었을 때만 400 ms 뒤 /api/scenes/at 을 묻는다(같은 중심은 다시 묻지 않음).</summary>
    private void QueueMap3DSceneLookup(PointLatLng center)
    {
        if (_map3DLastLookupCenter is { } last && Math.Abs(last.Lat - center.Lat) < 1e-7 && Math.Abs(last.Lng - center.Lng) < 1e-7) return;
        if (_map3DLookupTimer == null)
        {
            _map3DLookupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _map3DLookupTimer.Tick += (_, __) => { _map3DLookupTimer!.Stop(); _ = LookupMap3DSceneAsync(); };
        }
        _map3DLookupTimer.Stop();
        _map3DLookupTimer.Start();
    }

    private async Task LookupMap3DSceneAsync()
    {
        if (MainMap == null || _map3DClient == null) return;
        var center = MainMap.Position;
        _map3DLastLookupCenter = center;
        _map3DLookupCts?.Cancel();
        var cts = _map3DLookupCts = new CancellationTokenSource();
        try
        {
            var scene = await _map3DClient.FindSceneAtAsync(center.Lng, center.Lat, cts.Token);
            if (scene == null) return;
            _map3DScenes.RemoveAll(sc => sc.Id == scene.Id);
            _map3DScenes.Add(scene);
            _log?.Info($"[Map3D] 장면 찾음 id={scene.Id}");
            QueueMap3DGate();
        }
        catch (OperationCanceledException) { }
        catch (Map3DServerUnavailableException ex)
        {
            _map3DLastLookupCenter = null;   // 서버가 다시 켜지면 같은 자리에서도 다시 묻는다
            _log?.Warning($"[Map3D] 장면 조회 — 서버 닿지 않음: {ex.Message}");
            if (MainMap.EffectiveZoom >= Map3DMath.EnterZoom) SetAimStatus(Map3DUnavailableText, autoHide: true);
        }
        catch (Exception ex) { _log?.Warning($"[Map3D] 장면 조회 실패: {ex.Message}"); }
    }

    // ── 바꿔 끼우기 ──

    private Map3DCamera Current2DCamera(double pitch)
        => new(MainMap!.Position.Lng, MainMap.Position.Lat,
               Map3DMath.ToMercatorZoom(Math.Min(MainMap.Zoom, MainMap.MaxZoom), MainMap.DigitalZoomScale),
               Map3DMath.ToMap3DBearing(MainMap.Bearing), pitch);

    private void Begin3D(Map3DScene scene)
    {
        if (MainMap == null || IsMap3DBlockedByInputMode()) return;
        _map3DState = Map3DState.Pending;
        _map3DJob = scene.Id;
        _log?.Info($"[Map3D] 진입 시작 job={scene.Id} zoom={MainMap.EffectiveZoom:F1}");
        if (Map3DHost.IsReadyFor(scene.Id)) { Reveal3D(); return; }
        // 위에서 내려다본 같은 축척으로 연다(animate=0) — 펼칠 때(Reveal3D) 기울기 애니메이션을 보낸다.
        bool remote = IsRemoteSession();
        if (remote) _log?.Info("[Map3D] 원격 세션 — 3D 저사양(low=1)");
        _ = Map3DHost.LoadAsync(scene.Id, Map3DMath.ViewUrl(_map3DSettings.ServerUrl, scene.Id, Current2DCamera(0d), animate: false, lowQuality: remote));
    }

    private void Reveal3D()
    {
        if (MainMap == null) return;
        _map3DState = Map3DState.Shown;
        _map3DLastCamera = null;
        Map3DOverlay.Start(MainMap);                                                     // 오버레이 전체 스냅샷(ready 직후 · 재사용 펼침)
        Map3DHost.PostCamera(Current2DCamera(0d), animate: false);                    // 지금 2D 와 같은 자리 · 축척(로딩 중 움직였을 수 있다)
        Map3DHost.Reveal();
        Map3DHost.PostCamera(Current2DCamera(Map3DMath.DefaultPitchDeg), animate: true); // 0.6 s 동안 눕힌다
        MainMap.IsHitTestVisible = false;                                               // 3D 가 보이는 동안 2D 입력 막음
        EnsureAimEscWindowHook();
        _log?.Info($"[Map3D] 3D 표시 job={_map3DJob}");
    }

    private void Leave3D(Map3DCamera? cam, string cause)
    {
        if (MainMap == null) return;
        _map3DState = Map3DState.Off;
        _map3DOverlay?.Stop();
        Map3DHost.Park();
        MainMap.IsHitTestVisible = true;
        if (cam is { } c) Apply3DCameraTo2D(c);
        MainMap.Focus();
        _log?.Info($"[Map3D] 2D 복귀({cause}) zoom={MainMap.EffectiveZoom:F1} bearing={MainMap.Bearing:F1}");
        QueueMap3DGate();
    }

    private void Apply3DCameraTo2D(Map3DCamera c)
    {
        MainMap!.SetEffectiveZoom(Map3DMath.ToEffectiveZoom(c.Zoom, MainMap.MinZoom, MainMap.MaxZoom, GMapCustomControl.DigitalScaleTable));
        MainMap.Position = new PointLatLng(c.Lat, c.Lon);
        MainMap.MapRotation = Map3DMath.ToGMapBearing(c.Bearing);   // 회전 게이트(kill-switch · 앵커)가 막으면 2D 방위는 그대로
    }

    private void OnMap3DMessage(Map3DMessage msg)
    {
        if (msg.Type == "ready")
        {
            _log?.Info($"[Map3D] ready job={msg.Job}");
            if (_map3DState == Map3DState.Pending && (msg.Job == null || msg.Job == _map3DJob)) Reveal3D();
            return;
        }
        if (msg.Type == "overlay.resync")
        {
            if (_map3DState == Map3DState.Shown) Map3DOverlay.SendSnapshot();
            return;
        }
        if (msg.Pick is { } pick)
        {
            try { OnMap3DPick(pick); }
            catch (Exception ex) { _log?.Error($"[Map3D] pick 처리 실패(격리): {ex.Message}"); }
            return;
        }
        if (_map3DState != Map3DState.Shown || msg.Camera is not { } cam) return;
        _map3DLastCamera = cam;
        if (Map3DMath.ShouldLeave3D(cam.Zoom)) Leave3D(cam, $"zoom {cam.Zoom:F2}");
    }

    private void OnMap3DLoadFailed(string reason)
    {
        _log?.Warning($"[Map3D] {reason}");
        if (_map3DState == Map3DState.Off) return;
        _map3DEntrySuppressed = true;   // 같은 줌에서 계속 다시 시도하지 않게
        if (_map3DState == Map3DState.Shown) Leave3D(_map3DLastCamera, "load-failed");
        else { _map3DState = Map3DState.Off; Map3DHost.Park(); }
        SetAimStatus(reason.Contains("ConnectionAborted") || reason.Contains("CannotConnect") ? Map3DUnavailableText : reason, autoHide: true);
    }

    // ── 입력 모드 충돌(1단계) ──

    /// <summary>편집 모드 · 독점 입력 모드 중인가 — 이때는 3D 에 들어가지 않는다.</summary>
    private bool IsMap3DBlockedByInputMode()
        => MainMap != null && Map3DMath.IsBlockedByInputMode(IsEditModeEnabled, ExclusiveInputModeGate.IsActive(MainMap));

    /// <summary>
    /// 입력 모드가 켜지거나 꺼진 직후 호출 — 모드 중이면 3D 를 접고(표시 중이면 3D 마지막 카메라를 2D 에 적용),
    /// 모드가 모두 끝났으면 게이트를 다시 판정한다.
    /// </summary>
    private void SyncMap3DWithInputModes()
    {
        if (MainMap == null) return;
        if (!IsMap3DBlockedByInputMode()) { QueueMap3DGate(); return; }
        if (_map3DState == Map3DState.Shown) Leave3D(_map3DLastCamera, "input-mode");
        else if (_map3DState == Map3DState.Pending)
        {
            _log?.Info("[Map3D] 로딩 중 입력 모드 시작 → 취소");
            _map3DState = Map3DState.Off;
            Map3DHost.Park();
        }
    }

    // ── 2D 이동 → 3D(이벤트 시 지도 이동 · 지도에서 보기) ──

    /// <summary>3D 표시 중이면 3D 에서 움직인 마지막 카메라를 2D 에 먼저 맞춘다(이동 계획이 지금 보이는 자리에서 출발하도록).</summary>
    private void Sync2DFrom3DCamera()
    {
        if (MainMap != null && _map3DState == Map3DState.Shown && _map3DLastCamera is { } c) Apply3DCameraTo2D(c);
    }

    /// <summary>
    /// 3D 표시 중 2D 이동이 끝났다 — 그 위치 · 줌을 3D 에 보낸다(animate). 지금 장면 밖이거나 줌 &lt; 16.5 면 2D 로 돌아온다
    /// (2D 가 이미 목표 자리라 카메라는 되돌리지 않는다 — 게이트가 다른 장면을 다시 판정한다).
    /// </summary>
    private void Sync3DAfter2DMove(string cause)
    {
        if (MainMap == null || _map3DState != Map3DState.Shown) return;
        var center = MainMap.Position;
        var scene = _map3DScenes.Find(sc => sc.Id == _map3DJob);
        bool inside = scene != null && Map3DMath.BboxContains(scene.Bbox, center.Lng, center.Lat);
        if (!Map3DMath.ShouldStay3DAfterMove(MainMap.EffectiveZoom, inside))
        {
            Leave3D(null, cause);
            return;
        }
        var last = _map3DLastCamera;
        var cam = Current2DCamera(last?.Pitch ?? Map3DMath.DefaultPitchDeg);
        if (last is { } l) cam = cam with { Bearing = l.Bearing };   // 3D 에서 돌려 둔 방위 · 기울기는 그대로
        Map3DHost.PostCamera(cam, animate: true);
        _log?.Info($"[Map3D] 2D 이동({cause}) → 3D 카메라 zoom={cam.Zoom:F2}");
    }

    // ── 3D 클릭(3단계) ──

    /// <summary>
    /// 3D pick → 지도 마커 클릭 경로. 편집 모드에서는 3D 가 열리지 않으므로 운영 모드 동작만 한다.
    /// 화면 좌표(3D 화면 DIP)는 지도 컨트롤 좌표로 바꿔 우클릭 메뉴 · 조립 카드에 쓴다.
    /// </summary>
    /// <summary>
    /// 3D 위에서는 지도 칸 안 창(상세 창 · 카메라 상자 · 조립 카드)이 WebView2(네이티브 창) 뒤에 가려진다 — 그런 창을 열기 전에
    /// 같은 위치 · 줌 · 방위로 2D 에 돌아온다. 창을 보는 동안 곧바로 다시 3D 로 들어가지 않게, 줌을 17 아래로 한 번 내릴 때까지 보류한다.
    /// (WebView2CompositionControl 은 net8.0-windows7.0 대상에 Windows SDK 투영이 없어 로드 순간 앱을 죽였다 — 2026-10-17 실기, 되돌림)
    /// </summary>
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    /// <summary>원격 데스크톱 세션인가(SM_REMOTESESSION) — 3D 화면을 저사양으로 연다(원격 화면 전송량 · 끊김 줄이기).</summary>
    private static bool IsRemoteSession() => GetSystemMetrics(0x1000) != 0;

    private void LeaveMap3DForPanel(string cause)
    {
        if (_map3DState != Map3DState.Shown) return;
        _map3DEntrySuppressed = true;
        Leave3D(_map3DLastCamera, cause);
    }

    private void OnMap3DPick(Map3DPick pick)
    {
        if (MainMap == null || _map3DState != Map3DState.Shown) return;
        Point? at = pick.ScreenX is double x && pick.ScreenY is double y ? Map3DHost.TranslatePoint(new Point(x, y), MainMap) : null;
        var marker = pick.Kind is { } kind && pick.Id is int id
            ? MainMap.Markers.FirstOrDefault(m => Map3DOverlayBridge.KindOf(m) == kind && ((IEditableMarker)m).Id == id)
            : null;

        if (marker is not IEditableMarker editable)
        {
            if (pick.Action != "click") return;
            // 빈 곳 클릭 — "지도에서 보기" 강조 해제 · 조립 카드 닫기만
            if (_locateHighlight.ActiveRequestId != null) ClearLocateHighlight();
            _componentCard?.OnEmptyPressed(at ?? default);
            _componentCard?.OnReleased(at ?? default);
            return;
        }

        switch (pick.Action)
        {
            case "click":
                MainMap.TriggerMarkerClicked(marker);
                // 지도 위 누름 · 뗌이 없으므로 같은 자리로 바로 끝낸다(OnMapMarkerClicked 와 같은 조건)
                if (_componentCard != null && !IsEditModeEnabled && !editable.IsLocked)
                {
                    if (Services.ComponentCardService.CanShow(editable)) LeaveMap3DForPanel("component-card");   // 카드는 2D 심볼 위 어도너

                    _componentCard.OnMarkerPressed(editable, at ?? default);
                    _componentCard.OnReleased(at ?? default);
                }
                break;
            case "dblclick":
                if (marker is GMapPidsGroupMarker) MainMap.TriggerGroupActionReport(marker);
                else MainMap.TriggerMarkerDoubleClicked(marker);
                break;
            case "context":
                _map3DMenuAt = at;
                try { MainMap.TriggerMarkerRightClicked(marker); }
                finally { _map3DMenuAt = null; }
                break;
        }
    }
}
