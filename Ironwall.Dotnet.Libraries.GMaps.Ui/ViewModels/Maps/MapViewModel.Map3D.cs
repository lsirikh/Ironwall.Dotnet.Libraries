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
                  ★ 3차(2026-10-17, R5): 전환 안내 패널 — 들어갈 때마다 "3D 지도로 전환합니다" + 막대(만들어 둔 화면이어도 최소 0.6 s 뒤 펼침),
                    나올 때 "2D 지도로 돌아갑니다" 0.5 s, 로그인 화면 뒤에서 펼쳤으면 로그인 직후 한 번(판정 = Map3DGuide 순수 함수).
                    영역 표시 · 관리 · 칩은 MapViewModel.Map3DAreas.cs.
                  ★ 4차(2026-10-17, 전환 시간): ① 지도가 뜬 뒤 WebView2 미리 데우기 ② 줌 15 · 영역 안이면 장면 미리 불러오기(1×1)
                    ③ ready 직후 오버레이 스냅샷, 접을 때 Pause · 펼칠 때 Resume(바뀐 것만) ④ 3D 의 rendered 를 받은 뒤 펼침(1.5 s 넘으면 그냥),
                    이미 그려 둔 화면은 0.4 s ⑤ 진입마다 "[Map3D] 전환 시간 …" 한 줄 로그.
                  ★ 5차(2026-10-18, 영역마다 3D 줌): 게이트 · 미리 불러오기 · 2D 복귀 줌은 그 영역의 진입 줌(로컬 &gt; 서버 enter_zoom &gt; 세팅)으로
                    세운다(Map3DMath.AreaGate). 만들기 패널에서 고른 줌은 굽기 요청 enter_zoom 으로 보낸다.
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

    // ── 3D 전환 · 굽기 진행 표시(지도 가운데 패널, MapView.xaml "GMaps.Map3D.Progress") ──
    private bool _isMap3DBusy;
    private string _map3DBusyText = "";
    private double _map3DBusyPercent;
    private bool _map3DBusyIndeterminate = true;
    /// <summary>UIA 계측용 지금 화면 — "3D"(3D 표시 중) · "2D". 지도 루트의 ItemStatus 로 내보낸다(컴포지션 WebView2 는 UIA 트리에 없다).</summary>
    public string Map3DStatusText => _map3DState == Map3DState.Shown ? "3D" : "2D";
    public bool IsMap3DBusy { get => _isMap3DBusy; private set { _isMap3DBusy = value; NotifyOfPropertyChange(); } }
    public string Map3DBusyText { get => _map3DBusyText; private set { _map3DBusyText = value; NotifyOfPropertyChange(); } }
    public double Map3DBusyPercent { get => _map3DBusyPercent; private set { _map3DBusyPercent = value; NotifyOfPropertyChange(); } }
    public bool Map3DBusyIndeterminate { get => _map3DBusyIndeterminate; private set { _map3DBusyIndeterminate = value; NotifyOfPropertyChange(); } }
    private string _map3DBusyDetail = "";
    /// <summary>진행 패널 둘째 줄(단계 · %). 비면 줄을 접는다.</summary>
    public string Map3DBusyDetail { get => _map3DBusyDetail; private set { _map3DBusyDetail = value; NotifyOfPropertyChange(); } }

    /// <summary>진행 패널 — text(제목)가 null 이면 닫는다. percent 가 null 이면 끝을 모르는 진행(막대가 흐른다).
    /// 제목이 바뀌거나 새로 열 때만 표시 시작 시각을 다시 잡는다(최소 표시 시간 기준).</summary>
    private void SetMap3DBusy(string? text, double? percent = null, string detail = "")
    {
        _map3DBusyForBake = false;
        if (text == null) { StopMap3DGuideTimer(); IsMap3DBusy = false; return; }
        if (!IsMap3DBusy || text != Map3DBusyText) _map3DBusyShownAt = DateTime.UtcNow;
        Map3DBusyText = text;
        Map3DBusyDetail = detail;
        Map3DBusyIndeterminate = percent == null;
        Map3DBusyPercent = percent ?? 0;
        IsMap3DBusy = true;
    }

    // ── 전환 안내(R5) — 최소 표시 시간을 채운 뒤 3D 를 펼치거나(RevealWhenDue) 패널을 닫는다(HideWhenDue) ──
    private enum Map3DGuideMode { None, RevealWhenDue, HideWhenDue, RevealWhenRendered }
    private Map3DGuideMode _map3DGuideMode;
    private DateTime _map3DBusyShownAt;
    private TimeSpan _map3DGuideMin;
    /// <summary>막대를 시간으로 채운다(만들어 둔 3D 화면 펼치기).</summary>
    private bool _map3DGuideTimedBar;
    private DispatcherTimer? _map3DGuideTimer;
    /// <summary>지금 패널이 굽기 진행 표시인가(굽기가 끝나면 그 패널만 닫는다).</summary>
    private bool _map3DBusyForBake;
    /// <summary>로그인 화면 뒤에서 3D 를 펼쳤다 — 로그인 직후 한 번 안내한다.</summary>
    private bool _map3DRevealedBehindLogin;
    private bool _map3DLoginSubscribed;

    private void RunMap3DGuideTimer(Map3DGuideMode mode, TimeSpan minimum, bool timedBar)
    {
        _map3DGuideMode = mode;
        _map3DGuideMin = minimum;
        _map3DGuideTimedBar = timedBar;
        if (_map3DGuideTimer == null)
        {
            _map3DGuideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _map3DGuideTimer.Tick += (_, __) =>
            {
                try { OnMap3DGuideTick(); }
                catch (Exception ex) { StopMap3DGuideTimer(); _log?.Error($"[Map3D] 안내 처리 실패(격리): {ex.Message}"); }
            };
        }
        _map3DGuideTimer.Start();
    }

    private void StopMap3DGuideTimer()
    {
        _map3DGuideTimer?.Stop();
        _map3DGuideMode = Map3DGuideMode.None;
    }

    private void OnMap3DGuideTick()
    {
        if (_map3DGuideMode == Map3DGuideMode.RevealWhenRendered) { TryRevealWhenRendered(); return; }
        var elapsed = DateTime.UtcNow - _map3DBusyShownAt;
        if (_map3DGuideTimedBar) Map3DBusyPercent = Map3DGuide.TimedPercent(elapsed, _map3DGuideMin);
        if (Map3DGuide.Remaining(elapsed, _map3DGuideMin) > TimeSpan.Zero) return;
        var mode = _map3DGuideMode;
        StopMap3DGuideTimer();
        if (mode == Map3DGuideMode.RevealWhenDue) { if (_map3DState == Map3DState.Pending) Reveal3D(); }
        else SetMap3DBusy(null);
    }

    /// <summary>3D 가 준비됐다 — 안내가 최소 시간(0.6 s)만큼 보였으면 바로 펼치고, 아니면 막대를 채운 채 남은 시간 뒤 펼친다.</summary>
    private void RevealAfterGuide()
    {
        if (Map3DGuide.Remaining(DateTime.UtcNow - _map3DBusyShownAt, Map3DEnterShow) <= TimeSpan.Zero) { Reveal3D(); return; }
        SetMap3DBusy(Map3DGuide.EnterTitle, 100, Map3DGuide.StageText("ready"));
        RunMap3DGuideTimer(Map3DGuideMode.RevealWhenDue, Map3DEnterShow, timedBar: false);
    }

    /// <summary>2D → 3D 안내 최소 표시 시간 — 설정(GuideMinSeconds, 기본 0.6 s).</summary>
    private TimeSpan Map3DEnterShow => TimeSpan.FromSeconds(_map3DSettings.GuideMinSeconds);

    /// <summary>
    /// ready 뒤 — 3D 가 오버레이를 적용하고 한 프레임 그렸다는 rendered 를 받았으면 펼친다. 1.5 s 안에 안 오면 경고를 남기고 그냥 펼친다.
    /// (안내 타이머 50 ms 마다 · rendered 메시지를 받을 때 부른다)
    /// </summary>
    private void TryRevealWhenRendered()
    {
        if (_map3DState != Map3DState.Pending || _map3DJob is not { } job) { StopMap3DGuideTimer(); return; }
        var sinceReady = Map3DHost.ReadyAt is DateTime readyAt ? DateTime.UtcNow - readyAt : TimeSpan.Zero;
        var decision = Map3DGuide.DecideReveal(Map3DHost.IsReadyFor(job), Map3DHost.IsRenderedFor(job), sinceReady);
        if (decision == Map3DRevealDecision.Wait) return;
        if (decision == Map3DRevealDecision.TimedOut)
            _log?.Warning($"[Map3D] rendered 를 {Map3DGuide.RenderedTimeout.TotalSeconds:0.0} s 안에 받지 못함 → 그냥 펼침 job={job}");
        StopMap3DGuideTimer();
        RevealAfterGuide();
    }

    /// <summary>로그인 성공(배경 스레드 가능) — 로그인 화면 뒤에서 3D 로 들어갔고 지금도 3D 면 한 번 안내한다.</summary>
    private void OnMap3DLoginSucceeded()
    {
        _ = OnUiAsync(() =>
        {
            if (!Map3DGuide.ShouldGuideAfterLogin(_map3DRevealedBehindLogin, _map3DState == Map3DState.Shown)) return;
            _map3DRevealedBehindLogin = false;
            _log?.Info("[Map3D] 로그인 직후 3D 안내");
            SetMap3DBusy(Map3DGuide.EnterTitle, 100, Map3DGuide.StageText("login", Map3DCurrentGate.Exit));
            RunMap3DGuideTimer(Map3DGuideMode.HideWhenDue, Map3DGuide.AfterLoginShow, timedBar: false);
        });
    }
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

    // ── 전환 시간 계측(진입마다 한 줄 로그 — Map3DGuide.TransitionTimingText) ──
    private DateTime _map3DEnterAt;
    private bool _map3DEnterPrewarmed;
    private bool _map3DEnterPreloaded;
    /// <summary>지금 페이지에 ready 직후 전체 스냅샷을 다 보낸 시각(UTC).</summary>
    private DateTime? _map3DSnapshotAt;

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
        host.HostFallback += reason => _log?.Warning($"[Map3D] {reason}");
        return host;
    }

    // ── 설정 · 구독(ConfigureCommonMapSettings 에서 호출 — −=/+= 멱등) ──

    private void LoadMap3DFromSettings()
    {
        if (MainMap == null) return;
        string? oldServer = _map3DClient == null ? null : _map3DSettings.ServerUrl;
        _map3DSettings = MapSettingsHelper.LoadMap3D(_log);
        NotifyOfPropertyChange(nameof(Map3DChipText));
        if (oldServer != _map3DSettings.ServerUrl)
        {
            _map3DClient?.Dispose();
            _map3DClient = new Map3DClient(_map3DSettings.ServerUrl);
            if (oldServer != null) { _map3DScenes.Clear(); _map3DLastLookupCenter = null; }   // 다른 서버 — 옛 장면 캐시를 버린다
        }
        Map3DHost.UseComposition = _map3DSettings.UseCompositionHost;   // 3D 화면을 처음 만들 때만 본다(이미 만들었으면 다음 실행부터)

        // 설정 화면 「3D 지도」 저장 → 다시 불러와 바로 반영(MapSettingsHelper.Map3DSaved)
        MapSettingsHelper.Map3DSaved -= OnMap3DSettingsSaved;
        MapSettingsHelper.Map3DSaved += OnMap3DSettingsSaved;

        MainMap.OnPositionChanged -= OnMap3DPositionChanged;
        MainMap.OnPositionChanged += OnMap3DPositionChanged;
        MainMap.OnMapZoomChanged -= OnMap3DZoomChanged;
        MainMap.OnMapZoomChanged += OnMap3DZoomChanged;
        MainMap.DigitalZoomLevelChanged -= OnMap3DDigitalZoomChanged;
        MainMap.DigitalZoomLevelChanged += OnMap3DDigitalZoomChanged;
        if (!_map3DLoginSubscribed && ResolveSessionLifecycle() is { } session)
        {
            session.LoginSucceeded += OnMap3DLoginSucceeded;   // 로그인 화면 뒤에서 들어간 3D 를 로그인 직후 안내(R5)
            _map3DLoginSubscribed = true;
        }
        QueueMap3DGate();
        if (!_map3DSettings.IsEnabled) return;
        _ = RefreshMap3DAreasAsync();   // 2D 지도 3D 영역 표시(R4)
        // 첫 3D 진입의 WebView2 기동(실측 4.36 s)을 지도가 그려진 뒤 한가할 때 미리 해 둔다
        MainMap.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => _ = PrewarmMap3DAsync()));
    }

    /// <summary>WebView2 미리 데우기(1×1 대기 · 탐색 없음). 3D 설정이 꺼졌거나 이미 기동했으면 하지 않는다.</summary>
    private async Task PrewarmMap3DAsync()
    {
        if (!_map3DSettings.IsEnabled || Map3DHost.CoreReadyAt != null) return;
        var startedAt = DateTime.UtcNow;
        _log?.Info("[Map3D] WebView2 미리 데우기 시작");
        await Map3DHost.PrewarmAsync();
        if (Map3DHost.CoreReadyAt is DateTime readyAt)
            _log?.Info($"[Map3D] WebView2 미리 데우기 끝 {(readyAt - startedAt).TotalSeconds:0.00}s");
    }

    /// <summary>AppSettings.Map3D 가 저장됐다(배경 스레드 가능) — 다시 불러온다. 3D 를 끄고 저장했으면 보던 3D 를 접는다.</summary>
    private void OnMap3DSettingsSaved()
    {
        _ = OnUiAsync(() =>
        {
            try
            {
                LoadMap3DFromSettings();
                if (_map3DSettings.IsEnabled) return;
                if (_map3DState == Map3DState.Shown) Leave3D(_map3DLastCamera, "settings-off");
                else if (_map3DState == Map3DState.Pending) CancelMap3DPending();
            }
            catch (Exception ex) { _log?.Error($"[Map3D] 설정 다시 불러오기 실패(격리): {ex.Message}"); }
        });
    }

    private void OnMap3DPositionChanged(PointLatLng _) => QueueMap3DGate();
    private void OnMap3DZoomChanged() => QueueMap3DGate();
    private void OnMap3DDigitalZoomChanged(int _) => QueueMap3DGate();

    // ── 영역 그리기(앵커 러버밴드 재사용) ──

    private void EnterMap3DDrawMode()
    {
        if (MainMap == null) return;
        CloseMap3DBakeOptions();   // 열려 있던 굽기 방식 패널은 새 영역 그리기로 대신한다
        EnterAnchorDrawMode();   // 러버밴드 · 십자 커서 · ESC 후킹 · 포커스
        _isMap3DDrawMode = true;
        // 그리는 중 마퀴 옆 크기 표시 — 한 변 2 km 를 넘으면 경고색(R6)
        MainMap.AnchorDrawLabel = (nw, se) => Map3DGuide.AreaDrawLabel(Map3DMath.BuildBbox(nw.Lat, nw.Lng, se.Lat, se.Lng), _map3DSettings.MaxAreaSideM);
        SetAimStatus("지도에서 드래그해 3D 로 만들 영역을 그리세요 (Enter = 지금 화면 · ESC = 취소)");
    }

    /// <summary>앵커 그리기 완료가 3D 그리기였으면 굽기로 넘긴다(OnAnchorAreaDrawn 서두에서 호출).</summary>
    private bool TryTakeMap3DArea(PointLatLng nw, PointLatLng se)
    {
        if (!_isMap3DDrawMode) return false;
        ExitAnchorDrawMode();
        TakeMap3DArea(Map3DMath.BuildBbox(nw.Lat, nw.Lng, se.Lat, se.Lng));
        return true;
    }

    /// <summary>그린 영역(또는 Enter = 지금 화면) — 한 변 2 km 를 넘으면 이유를 알리고 다시 그리기 모드로, 아니면 굽는다.</summary>
    private void TakeMap3DArea(double[] bbox)
    {
        var (w, h) = Map3DGuide.AreaSizeMeters(bbox);
        if (Map3DGuide.IsAreaTooLarge(w, h, _map3DSettings.MaxAreaSideM))
        {
            _log?.Info($"[Map3D] 영역이 너무 큼 {Map3DGuide.FormatAreaSize(w, h)} → 다시 그리기");
            EnterMap3DDrawMode();
            SetAimStatus(Map3DGuide.AreaTooLargeText(w, h, _map3DSettings.MaxAreaSideM));
            return;
        }
        RequestMap3DBake(bbox);   // 만들기 패널 — 3D 줌 고르기(+ 영역과 겹치는 오버레이 이미지가 있으면 굽기 방식, MapViewModel.Map3DBakeOptions)
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
            TakeMap3DArea(Map3DMath.BuildBbox(cLat + hLat, cLng - hLng, cLat - hLat, cLng + hLng));
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

    /// <param name="enterZoom">그 영역의 3D 로 바뀌는 줌(만들기 패널에서 고른 값) — 굽기 요청 enter_zoom.</param>
    private async Task BakeMap3DAsync(double[] bbox, string overlayMode, IReadOnlyList<Map3DBakeOverlay>? overlays, double enterZoom)
    {
        if (_map3DClient == null || _isMap3DBaking) return;
        _isMap3DBaking = true;
        DrawMap3DAreaCommand.RaiseCanExecuteChanged();
        try
        {
            _log?.Info($"[Map3D] 굽기 요청 bbox=[{string.Join(", ", bbox)}] overlay={overlayMode} images={overlays?.Count ?? 0} enterZoom={enterZoom}");
            var job = await _map3DClient.StartBakeAsync(bbox, overlayMode, overlays, enterZoom);
            while (!job.IsDone && !job.IsError)
            {
                SetAimStatus($"3D 만드는 중… {job.Progress:P0}{(string.IsNullOrEmpty(job.Message) ? "" : $" — {job.Message}")}");
                if (_map3DState == Map3DState.Off && _map3DGuideMode == Map3DGuideMode.None)   // 전환 안내를 덮지 않는다
                {
                    SetMap3DBusy(Map3DGuide.WithPercent("3D 를 만드는 중…", job.Progress * 100), job.Progress * 100, job.Message ?? "");
                    _map3DBusyForBake = true;
                }
                await Task.Delay(1000);
                job = await _map3DClient.GetJobAsync(job.Id);
            }
            _isMap3DBaking = false;   // 아래 QueueMap3DGate 가 새 영역으로 들어갈 수 있게(finally 보다 먼저)
            if (job.IsError)
            {
                SetAimStatus($"3D 만들기 실패: {job.Message ?? "알 수 없는 오류"}", autoHide: true);
                return;
            }
            _map3DScenes.RemoveAll(sc => sc.Id == job.Id);
            _map3DScenes.Add(new Map3DScene(job.Id, bbox) { EnterZoom = enterZoom });
            _map3DEntrySuppressed = false;
            SetAimStatus($"3D 준비됨 — 줌 {Map3DGuide.Zoom(Map3DAreaEnterZoom(job.Id, enterZoom))} 이상에서 3D", autoHide: true);
            QueueMap3DGate();
            _ = RefreshMap3DAreasAsync();   // 새 영역 테두리 · 배지(만든 날짜 · 건물 수는 서버 목록에서)
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
            if (_map3DBusyForBake) SetMap3DBusy(null);   // 굽기 패널만 닫는다(전환 안내는 그대로)
            _isMap3DBaking = false;
            DrawMap3DAreaCommand.RaiseCanExecuteChanged();
        }
    }

    // ── 영역마다 3D 줌(2026-10-18) ──

    private Map3DAreaZoomStore? _map3DAreaZooms;
    /// <summary>GIS 로컬 영역 줌(map3d-areas.json) — 서버가 「3D 줌 바꾸기」를 모를 때 저장한 값.</summary>
    private Map3DAreaZoomStore Map3DAreaZooms => _map3DAreaZooms ??= new Map3DAreaZoomStore(Map3DAreaZoomStore.DefaultPath);

    /// <summary>영역 진입 줌 — 로컬 값 &gt; 서버 장면의 enter_zoom &gt; 세팅의 3D 최소 줌.</summary>
    private double Map3DAreaEnterZoom(string id, double? serverZoom)
        => Map3DMath.ResolveAreaEnterZoom(Map3DAreaZooms.Get(id), serverZoom, _map3DSettings.EnterZoom);

    /// <summary>영역 진입 줌으로 세운 게이트(이탈 · 미리 준비 줌은 세팅에서 옮겨 온다).</summary>
    private Map3DZoomGate Map3DGateFor(double areaEnterZoom)
        => Map3DMath.AreaGate(areaEnterZoom, _map3DSettings.EnterZoom, _map3DSettings.ExitZoom, _map3DSettings.PreloadZoom);

    /// <summary>장면의 게이트 — 장면이 없으면 세팅값.</summary>
    private Map3DZoomGate Map3DGateFor(Map3DScene? scene)
        => Map3DGateFor(scene == null ? _map3DSettings.EnterZoom : Map3DAreaEnterZoom(scene.Id, scene.EnterZoom));

    /// <summary>지금 3D 로 보는(또는 여는) 장면의 게이트.</summary>
    private Map3DZoomGate Map3DCurrentGate => Map3DGateFor(_map3DScenes.Find(sc => sc.Id == _map3DJob));

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
            try { UpdateMap3DAreaDisplay(); }   // 영역 테두리 크기 · 줌 13 표시 · "3D 가능" 칩(MapViewModel.Map3DAreas)
            catch (Exception ex) { _log?.Error($"[Map3D] 영역 표시 갱신 실패(격리): {ex.Message}"); }
        }));
    }

    private void EvaluateMap3DGate()
    {
        if (MainMap == null || !_map3DSettings.IsEnabled || _map3DState == Map3DState.Shown) return;
        var center = MainMap.Position;
        double zoom = MainMap.EffectiveZoom;
        var scene = _map3DScenes.Find(sc => Map3DMath.BboxContains(sc.Bbox, center.Lng, center.Lat));
        // 굽는 동안은 3D 로 들어가지 않는다 — 영역을 그리고 나면 곧바로 옛 3D 로 들어가 굽기 진행 패널이 가려졌다(2026-10-17 실기 S1)
        var gate = Map3DGateFor(scene);   // 그 영역의 진입 · 이탈 · 미리 준비 줌(영역마다 3D 줌)
        bool active = !_isMap3DBaking && !IsMap3DBlockedByInputMode() && Map3DMath.Decide(zoom, _map3DState == Map3DState.Pending, scene != null, gate.Enter, gate.Exit);

        if (!Map3DMath.Decide(zoom, false, scene != null, gate.Enter, gate.Exit)) _map3DEntrySuppressed = false;   // 한 번 꺼지면 보류 해제
        // 목록에 없는 장면은 가장 낮게 고를 수 있는 영역 줌의 이탈 줌부터 묻는다
        if (scene == null && zoom >= Map3DGateFor(Math.Min(Map3DGuide.EnterZoomChoiceMin, _map3DSettings.EnterZoom)).Exit) QueueMap3DSceneLookup(center);

        if (active && _map3DState == Map3DState.Off && !_map3DEntrySuppressed) Begin3D(scene!);
        else if (!active && _map3DState == Map3DState.Pending)
        {
            _log?.Info($"[Map3D] 로딩 중 게이트 이탈(zoom={zoom:F1}) → 취소");
            CancelMap3DPending();
        }
        else if (_map3DState == Map3DState.Off
                 && Map3DMath.ShouldPreload(zoom, scene?.Id, Map3DHost.Job, Map3DHost.IsLoading, _isMap3DBaking || IsMap3DBlockedByInputMode(),
                                            gate.Preload))
        {
            // 줌 17 진입 전에(줌 15 · 영역 안) 장면을 1×1 로 미리 열어 둔다 — 진입은 펼치기만. 벗어나도 연 장면은 그대로 둔다.
            string job = scene!.Id;
            _log?.Info($"[Map3D] 미리 불러오기 job={job} zoom={zoom:F1}");
            LoadMap3DScene(job);
        }
    }

    /// <summary>로딩 중(Pending) 진입 취소 — 패널을 닫고 1×1 로 접는다(불러오던 장면은 그대로 둬 다음 진입에 쓴다).</summary>
    private void CancelMap3DPending()
    {
        _map3DState = Map3DState.Off; NotifyOfPropertyChange(nameof(Map3DStatusText)); SetMap3DBusy(null);
        Map3DHost.Park();
        _map3DOverlay?.Pause();
    }

    /// <summary>장면 페이지를 1×1 대기 상태로 연다(지금 2D 와 같은 자리 · 축척, 위에서 내려다봄). 새 페이지라 오버레이 기록은 버린다.</summary>
    private void LoadMap3DScene(string job)
    {
        _map3DOverlay?.Stop();
        bool remote = IsRemoteSession();
        if (remote) _log?.Info("[Map3D] 원격 세션 — 3D 저사양(low=1)");
        // 위에서 내려다본 같은 축척으로 연다(animate=0) — 펼칠 때(Reveal3D) 기울기 애니메이션을 보낸다.
        _ = Map3DHost.LoadAsync(job, Map3DMath.ViewUrl(_map3DSettings.ServerUrl, job, Current2DCamera(0d), animate: false, lowQuality: remote));
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
            if (!_map3DAreaMarkers.ContainsKey(scene.Id)) _ = RefreshMap3DAreasAsync();   // 서버가 늦게 켜졌거나 다른 곳에서 만든 영역 — 영역 표시도 맞춘다
        }
        catch (OperationCanceledException) { }
        catch (Map3DServerUnavailableException ex)
        {
            _map3DLastLookupCenter = null;   // 서버가 다시 켜지면 같은 자리에서도 다시 묻는다
            _log?.Warning($"[Map3D] 장면 조회 — 서버 닿지 않음: {ex.Message}");
            if (MainMap.EffectiveZoom >= _map3DSettings.EnterZoom) SetAimStatus(Map3DUnavailableText, autoHide: true);
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
        _map3DEnterAt = DateTime.UtcNow;
        _map3DEnterPrewarmed = Map3DHost.CoreReadyAt != null;
        bool ready = Map3DHost.IsReadyFor(scene.Id);
        // 미리 불러온 장면(불러오는 중 포함)이면 다시 탐색하지 않는다
        _map3DEnterPreloaded = ready || (Map3DHost.Job == scene.Id && Map3DHost.IsLoading);
        var sinceReady = Map3DHost.ReadyAt is DateTime readyAt ? _map3DEnterAt - readyAt : TimeSpan.Zero;
        bool reuse = Map3DGuide.DecideReveal(ready, Map3DHost.IsRenderedFor(scene.Id), sinceReady) != Map3DRevealDecision.Wait;
        _log?.Info($"[Map3D] 진입 시작 job={scene.Id} zoom={MainMap.EffectiveZoom:F1} reuse={reuse} preloaded={_map3DEnterPreloaded} prewarmed={_map3DEnterPrewarmed}");
        // 안내 패널은 매번 — 이미 그려 둔 화면이어도 최소 0.4 s 동안 막대를 채운 뒤 펼친다(R5). 복귀 안내 타이머가 돌고 있으면 먼저 멈춘다.
        StopMap3DGuideTimer();
        SetMap3DBusy(Map3DGuide.EnterTitle, reuse ? 0 : null, Map3DGuide.StageText(reuse ? "reuse" : null));
        if (reuse)
        {
            // 만들어 둔 화면은 0.4 s — 설정의 안내 시간이 그보다 짧으면 그 시간
            var reuseShow = Map3DEnterShow < Map3DGuide.MinReuseShow ? Map3DEnterShow : Map3DGuide.MinReuseShow;
            RunMap3DGuideTimer(Map3DGuideMode.RevealWhenDue, reuseShow, timedBar: true);
            return;
        }
        if (ready)
        {
            // ready 는 왔고 rendered 를 기다리는 중(미리 불러오기 직후)
            SetMap3DBusy(Map3DGuide.EnterTitle, 100, Map3DGuide.StageText("rendering"));
            RunMap3DGuideTimer(Map3DGuideMode.RevealWhenRendered, Map3DGuide.RenderedTimeout, timedBar: false);
            return;
        }
        if (_map3DEnterPreloaded) return;   // 미리 불러오는 중 — ready(OnMap3DMessage)를 기다린다
        LoadMap3DScene(scene.Id);
    }

    private void Reveal3D()
    {
        if (MainMap == null) return;
        _map3DState = Map3DState.Shown;
        NotifyOfPropertyChange(nameof(Map3DStatusText));
        SetMap3DBusy(null);
        _map3DRevealedBehindLogin = Map3DGuide.IsBehindLogin(ResolveTokenStorage()?.IsAuthenticated);   // 로그인 뒤 한 번 안내(R5)
        _map3DLastCamera = null;
        Map3DOverlay.Resume(MainMap);                                                    // ready 때 보낸 스냅샷에 이어 바뀐 것만(기록이 없으면 전체 스냅샷)
        Map3DHost.PostCamera(Current2DCamera(0d), animate: false);                    // 지금 2D 와 같은 자리 · 축척(로딩 중 움직였을 수 있다)
        Map3DHost.Reveal();
        Map3DHost.PostCamera(Current2DCamera(Map3DMath.DefaultPitchDeg), animate: true); // 0.6 s 동안 눕힌다
        MainMap.IsHitTestVisible = false;                                               // 3D 가 보이는 동안 2D 입력 막음
        EnsureAimEscWindowHook();
        BeginMap3DWindows();                                                            // 카메라 상자 · 조립 카드가 3D 마커 자리를 따라간다(MapViewModel.Map3DWindows)
        _log?.Info($"[Map3D] 3D 표시 job={_map3DJob} host={Map3DHost.HostMode}");
        _log?.Info(Map3DGuide.TransitionTimingText(_map3DEnterAt, Map3DHost.CoreReadyAt, Map3DHost.ReadyAt, _map3DSnapshotAt,
                                                   Map3DHost.RenderedAt, DateTime.UtcNow, _map3DEnterPrewarmed, _map3DEnterPreloaded));
    }

    private void Leave3D(Map3DCamera? cam, string cause)
    {
        if (MainMap == null) return;
        _map3DState = Map3DState.Off;
        NotifyOfPropertyChange(nameof(Map3DStatusText));
        _map3DRevealedBehindLogin = false;
        // 짧은 복귀 안내(R5) — 패널은 2D 위에 0.5 s
        SetMap3DBusy(Map3DGuide.LeaveTitle, 100);
        RunMap3DGuideTimer(Map3DGuideMode.HideWhenDue, Map3DGuide.LeaveShow, timedBar: false);
        _map3DOverlay?.Pause();   // 페이지는 1×1 로 살아 있다 — 다시 펼칠 때 바뀐 것만 보낸다
        Map3DHost.Park();
        MainMap.IsHitTestVisible = true;
        if (cam is { } c) Apply3DCameraTo2D(c);
        EndMap3DWindows();   // 열린 창은 닫지 않고 같은 마커의 2D 자리로(MapViewModel.Map3DWindows)
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
        if (msg.Type == "progress")
        {
            if (_map3DState == Map3DState.Pending && _map3DGuideMode == Map3DGuideMode.None)
                SetMap3DBusy(Map3DGuide.EnterTitle, msg.Progress, Map3DGuide.WithPercent(Map3DGuide.StageText(msg.Reason), msg.Progress));
            return;
        }
        if (msg.Type == "ready")
        {
            _log?.Info($"[Map3D] ready job={msg.Job}");
            if (MainMap == null) return;
            // 새 페이지 — 곧바로 오버레이 전체 스냅샷(3D 가 적용하고 한 프레임 그린 뒤 rendered 를 보낸다)
            Map3DOverlay.Start(MainMap);
            _map3DSnapshotAt = DateTime.UtcNow;
            if (_map3DState == Map3DState.Pending && (msg.Job == null || msg.Job == _map3DJob))
            {
                SetMap3DBusy(Map3DGuide.EnterTitle, 100, Map3DGuide.StageText("rendering"));
                RunMap3DGuideTimer(Map3DGuideMode.RevealWhenRendered, Map3DGuide.RenderedTimeout, timedBar: false);
                TryRevealWhenRendered();
            }
            else if (_map3DState == Map3DState.Off) _map3DOverlay?.Pause();   // 미리 불러오기 — 2D 동안은 보내지 않는다
            return;
        }
        if (msg.Type == "rendered")
        {
            _log?.Info($"[Map3D] rendered job={msg.Job} items={msg.Items?.ToString() ?? "-"}");
            if (_map3DGuideMode == Map3DGuideMode.RevealWhenRendered) TryRevealWhenRendered();
            return;
        }
        if (msg.Type == "overlay.resync")
        {
            if (_map3DOverlay?.IsActive == true) _map3DOverlay.SendSnapshot();
            else _map3DOverlay?.Stop();   // 접혀 있으면 기록만 버린다 — 다음 펼침 때 전체 스냅샷
            return;
        }
        if (msg.Type == "overlay.screen")
        {
            try { if (msg.Screens is { } screens) OnMap3DScreens(screens); }
            catch (Exception ex) { _log?.Error($"[Map3D] 창 자리 처리 실패(격리): {ex.Message}"); }
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
        if (Map3DMath.ShouldLeave3D(cam.Zoom, Map3DCurrentGate.Exit)) { Leave3D(cam, $"zoom {cam.Zoom:F2}"); return; }
        // WPF 안 3D(CompositionControl)면 2D 오버레이(나침반 · 축척바 · 줌 · 좌표)가 3D 위에 그대로 보인다 — 뒤의 2D 지도를
        // 3D 카메라(위치 · 줌 · 방위)에 맞춰 오버레이가 3D 상태를 보이게 하고, 2D 로 돌아갈 때도 끊기지 않게 한다(2026-10-17 실기: 나침반이 0° 에 멈춰 있었다).
        if (Map3DHost.HostMode == "composition") Apply3DCameraTo2D(cam);
    }

    private void OnMap3DLoadFailed(string reason)
    {
        _log?.Warning($"[Map3D] {reason}");
        if (_map3DState == Map3DState.Off) return;
        _map3DEntrySuppressed = true;   // 같은 줌에서 계속 다시 시도하지 않게
        if (_map3DState == Map3DState.Shown) Leave3D(_map3DLastCamera, "load-failed");
        else CancelMap3DPending();
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
            CancelMap3DPending();
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
        if (!Map3DMath.ShouldStay3DAfterMove(MainMap.EffectiveZoom, inside, Map3DGateFor(scene).Exit))
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
        // WPF 안에서 그리는 방식이면 창이 3D 위에 그대로 보인다 — 상세 창은 고정 자리, 카드 · 카메라 상자는 3D 가 알려 주는 마커 화면 자리를 따라간다(2026-10-18).
        // 네이티브 창 3D 는 WPF 창을 덮으므로 2D 로 돌아온다.
        if (!Map3DGuide.ShouldLeave3DForWindow(Map3DHost.HostMode)) return;
        _map3DEntrySuppressed = true;
        Leave3D(_map3DLastCamera, cause);
    }

    private void OnMap3DPick(Map3DPick pick)
    {
        if (MainMap == null || _map3DState != Map3DState.Shown) return;
        Point? at = pick.ScreenX is double x && pick.ScreenY is double y ? Map3DHost.TranslatePoint(new Point(x, y), MainMap) : null;
        if (pick.Kind == Utils.Map3DOverlay.KindTrack)
        {
            // 2D 추적 마커는 클릭 동작이 없다(호버 툴팁만) — 기록만 남긴다
            _log?.Info($"[Map3D] pick {pick.Action} kind=track id={pick.TrackId} → 동작 없음(2D 와 같음)");
            return;
        }
        if (pick.Kind == Utils.Map3DOverlay.KindImage && pick.Id is int imageId && imageId < 0)
        {
            // 지도 CustomImages(TIF 등) — 다리가 붙인 음수 id. 2D 도 클릭만 받는다(OnImageClicked → 선택)
            var image = _map3DOverlay?.FindCustomImage(imageId);
            _log?.Info($"[Map3D] pick {pick.Action} kind=image id={imageId} → {(image != null ? "오버레이 이미지" : "없음")}");
            if (image != null && pick.Action == "click") OnMapImageClicked(image);
            return;
        }
        var marker = pick.Kind is { } kind && pick.Id is int id
            ? MainMap.Markers.FirstOrDefault(m => Map3DOverlayBridge.KindOf(m) == kind && ((IEditableMarker)m).Id == id)
            : null;
        _log?.Info($"[Map3D] pick {pick.Action} kind={pick.Kind ?? "-"} id={pick.Id?.ToString() ?? "-"} → {(marker != null ? "마커" : "빈 곳")}");

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
                    if (Services.ComponentCardService.CanShow(editable)) LeaveMap3DForPanel("component-card");   // 네이티브 창 3D 만 2D 로 — WPF 안 3D 는 3D 위 층에 카드

                    _componentCard.OnMarkerPressed(editable, at ?? default);
                    _componentCard.OnReleased(at ?? default);
                    SyncMap3DWatch();   // 3D 에 남았으면 열린 카드의 심볼 화면 자리를 받는다
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
