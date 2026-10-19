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
                    Shown 에서 ESC · 「나가기」 → 2D 로 돌아오며 위치 · 줌 · 방위를 2D 에 되돌린다(7차부터 줌으로는 나가지 않는다).
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
                  ★ 6차(2026-10-18, 나가기): 3D 가 보이는 동안 지도 위 「나가기」 단추(= ESC, ExitMap3D). 「이 영역 3D 로 보기」로 들어온 3D 는
                    영역 전체가 들어오는 시점으로 열고(3D 의 home 메시지) 줌으로는 나가지 않으며, 나가면 들어오기 전 2D 자리로 돌아간다.
                  ★ 7차(2026-10-18): 6차 규칙을 모든 진입으로 — 줌 게이트로 들어와도 영역 전체 시점으로 열고, 3D 에서는 줌으로 나가지 않으며
                    (나가기 · ESC 만), 나가면 들어오기 직전 2D 자리(_map3DReturn). 상태는 SetMap3DState 하나로 바꿔 "3D" 표시와 「나가기」 보임이 늘 같다.
                  ★ 8차(2026-10-19, 들어가는 방식): 설정 Map3D.EntryMode — 자동(줌, 기본)은 지금처럼 줌 게이트, 수동(단추)은 줌으로 들어가지 않는다.
                    어느 방식이든 화면 가운데가 3D 영역 안이면 왼쪽 위 「3D 로 보기」 단추 · 「이 영역 3D 로 보기」로 바로 들어간다(EnterMap3DDirect).
                  ★ 9차(2026-10-19, 건물 높이): 3D 의 building 메시지 → 「건물 높이」 판(MapViewModel.BuildingHeight). 판이 열려 있으면 ESC 는 판만 취소하고,
                    3D 를 나가면 판을 닫으며 미리보기를 원래대로 한다.
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
    public string Map3DStatusText => Map3DMath.StatusText(_map3DState == Map3DState.Shown);
    /// <summary>3D 가 보이는 동안 — 지도 위 「나가기」 단추(MapView.xaml "GMaps.Map3D.ExitButton"). <see cref="Map3DStatusText"/> 에서만 파생한다.</summary>
    public bool IsMap3DShown => Map3DMath.IsExitButtonVisible(Map3DStatusText);
    /// <summary>오른쪽 아래 줌 컨트롤 보임 — 3D 에는 줌 개념이 없어 3D 동안 숨긴다. <see cref="Map3DStatusText"/> 에서만 파생한다.</summary>
    public bool IsMap3DZoomControlVisible => Map3DMath.IsZoomControlVisible(Map3DStatusText);
    private string? _currentAltitudeText;
    /// <summary>좌표 칩 고도 줄 — 3D 에서 마우스가 가리키는 땅 높이("58.3 m"), 2D 는 null(줄 숨김).</summary>
    public string? CurrentAltitudeText { get => _currentAltitudeText; private set { _currentAltitudeText = value; NotifyOfPropertyChange(); } }

    /// <summary>3D 상태는 여기서만 바꾼다 — "3D" 표시와 「나가기」 보임을 함께 알린다.</summary>
    private void SetMap3DState(Map3DState state)
    {
        _map3DState = state;
        NotifyOfPropertyChange(nameof(Map3DStatusText));
        NotifyOfPropertyChange(nameof(IsMap3DShown));
        NotifyOfPropertyChange(nameof(IsMap3DZoomControlVisible));
        NotifyOfPropertyChange(nameof(IsMap3DEnterButtonVisible));
    }

    private RelayCommand? _exitMap3DCommand;
    /// <summary>「나가기」 — ESC 와 같은 경로(ExitMap3D)로 2D 에 돌아온다.</summary>
    public RelayCommand ExitMap3DCommand => _exitMap3DCommand ??= new RelayCommand(_ => ExitMap3D("exit-button"));

    /// <summary>화면 가운데가 들어 있는 만들어 둔 3D 영역(없으면 null) — 게이트 큐의 UpdateMap3DAreaDisplay 가 맞춘다.</summary>
    private Map3DScene? _map3DCenterScene;
    /// <summary>지도 위 「3D 로 보기」 단추(MapView.xaml "Map.Map3D.EnterButton") — 2D 이고 화면 가운데가 3D 영역 안일 때(Map3DMath.IsEnterButtonVisible).</summary>
    public bool IsMap3DEnterButtonVisible => Map3DMath.IsEnterButtonVisible(_map3DSettings.IsEnabled, _map3DState == Map3DState.Off, _map3DCenterScene != null);

    private RelayCommand? _enterMap3DCommand;
    /// <summary>「3D 로 보기」 — 화면 가운데 3D 영역으로 줌과 상관없이 바로 들어간다(EnterMap3DDirect).</summary>
    public RelayCommand EnterMap3DCommand => _enterMap3DCommand ??= new RelayCommand(_ =>
    {
        if (_map3DCenterScene is { } scene) EnterMap3DDirect(scene, "enter-button");
    });
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
        NotifyMap3DCancelBake();   // 「만들기 취소」는 굽기 진행을 보일 때만
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
            SetMap3DBusy(Map3DGuide.EnterTitle, 100, Map3DGuide.ShownText(Map3DHost.HostMode));   // 3D 위 안내 — 줌 표기 없이 · Esc 는 네이티브 창 3D 만
            RunMap3DGuideTimer(Map3DGuideMode.HideWhenDue, Map3DGuide.AfterLoginShow, timedBar: false);
        });
    }
    private string? _map3DJob;
    private bool _isMap3DDrawMode;
    private bool _isMap3DBaking;
    private bool _map3DGateQueued;
    /// <summary>ESC · 로딩 실패 뒤 같은 줌에서 곧바로 다시 들어가지 않게 — 게이트가 한 번 꺼질(줌 &lt; 17 · 장면 밖) 때까지 진입 보류.</summary>
    private bool _map3DEntrySuppressed;
    /// <summary>「이 영역 3D 로 보기」로 여는(또는 보는) 영역 id — 그 진입은 「영역 보기」 누르기 전 2D 자리로 돌아간다. 줌 게이트 진입이면 null.</summary>
    private string? _map3DAreaViewJob;
    /// <summary>3D 로 들어오기 직전 2D 자리(위치 · 줌 · 방위) — 나가기 · ESC 때 되돌린다. 「영역 보기」는 누르기 전 자리, 줌 게이트는 펼친 순간 자리.</summary>
    private Map3DCamera? _map3DReturn;
    /// <summary>나가며 2D 에 맞출 카메라 — 들어오기 직전 2D 자리, 없으면 3D 마지막 카메라.</summary>
    private Map3DCamera? Map3DExitCamera => Map3DMath.ExitCamera(_map3DReturn, _map3DLastCamera);
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
    /// <summary>3D 표시 중 WPF 프레임 간격 · 2D 따라가기(Apply3DCameraTo2D) 시간 — perf.stats 를 받을 때 로그 한 줄로 꺼낸다.</summary>
    private readonly Map3DPerfSamples _map3DWpfFrames = new(), _map3DSync2D = new();

    private Map3DOverlayBridge Map3DOverlay => _map3DOverlay ??= new Map3DOverlayBridge(json => Map3DHost.PostJson(json), _log, ZoneDetectionSection);

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
        host.HostFallback += reason =>
        {
            _log?.Warning($"[Map3D] {reason}");
            // 네이티브 창으로 바꾸며 호스트가 접히고 다시 연다 — 3D 가 안 보이는데 "3D"(「나가기」)로 남지 않게 2D 로(다시 열리면 게이트가 다시 들어간다)
            if (_map3DState == Map3DState.Shown) Leave3D(Map3DExitCamera, "host-fallback");
        };
        return host;
    }

    // ── 설정 · 구독(ConfigureCommonMapSettings 에서 호출 — −=/+= 멱등) ──

    private void LoadMap3DFromSettings()
    {
        if (MainMap == null) return;
        string? oldServer = _map3DClient == null ? null : _map3DSettings.ServerUrl;
        _map3DSettings = MapSettingsHelper.LoadMap3D(_log);
        NotifyOfPropertyChange(nameof(Map3DChipText));
        NotifyOfPropertyChange(nameof(IsMap3DEnterButtonVisible));   // 3D 끄기 · 켜기
        if (oldServer != _map3DSettings.ServerUrl)
        {
            _map3DClient?.Dispose();
            _map3DClient = new Map3DClient(_map3DSettings.ServerUrl);
            if (oldServer != null) { _map3DScenes.Clear(); _map3DLastLookupCenter = null; }   // 다른 서버 — 옛 장면 캐시를 버린다
        }
        // 3D 화면을 처음 만들 때만 본다(이미 만들었으면 다음 실행부터). 원격(RDP)이면 네이티브 창(HwndWhenRemote) — WPF 안 그리기는 원격에서 매 프레임 CPU 복사
        bool remote = IsRemoteSession();
        Map3DHost.UseComposition = Map3DPerf.UseComposition(_map3DSettings.UseCompositionHost, _map3DSettings.HwndWhenRemote, remote);
        if (remote && _map3DSettings.UseCompositionHost && !Map3DHost.UseComposition) _log?.Info("[Map3D] 원격 세션 — 3D 를 네이티브 창으로(HwndWhenRemote)");

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
                if (_map3DState == Map3DState.Shown) Leave3D(Map3DExitCamera, "settings-off");
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
        // 그리는 중 마퀴 옆 크기 표시 — 한 변 2 km 를 넘으면 경고색(R6). 지도가 돌아가 있으면 화면 사각형의 실제 가로 · 세로(회전 영역, 2026-10-19)
        MainMap.AnchorDrawCornersLabel = corners => Map3DGuide.AreaDrawLabel(Map3DAreaOf(corners), _map3DSettings.MaxAreaSideM);
        SetAimStatus("지도에서 드래그해 3D 로 만들 영역을 그리세요 (Enter = 지금 화면 · ESC = 취소)");
    }

    /// <summary>화면 사각형 네 모서리(좌상 · 우상 · 우하 · 좌하) → 3D 영역 — 지도가 돌아가 있으면 그 사각형 그대로(회전 영역), 아니면 북쪽 정렬 bbox.</summary>
    private Map3DArea Map3DAreaOf(IReadOnlyList<PointLatLng> corners)
        => Map3DMath.AreaFromScreenCorners(corners.Select(c => (c.Lat, c.Lng)).ToList(), MainMap?.Bearing ?? 0d);

    /// <summary>앵커 그리기 완료가 3D 그리기였으면 굽기로 넘긴다(OnAnchorAreaDrawn 서두에서 호출).</summary>
    private bool TryTakeMap3DArea(PointLatLng nw, PointLatLng se)
    {
        if (!_isMap3DDrawMode) return false;
        var corners = MainMap?.LastAnchorCorners;
        ExitAnchorDrawMode();
        TakeMap3DArea(corners is { Count: 4 } ? Map3DAreaOf(corners) : new Map3DArea(Map3DMath.BuildBbox(nw.Lat, nw.Lng, se.Lat, se.Lng), null));
        return true;
    }

    /// <summary>그린 영역(또는 Enter = 지금 화면) — 한 변 2 km 를 넘으면 이유를 알리고 다시 그리기 모드로, 아니면 굽는다.
    /// 크기는 회전 영역이면 회전 사각형의 실제 변 길이.</summary>
    private void TakeMap3DArea(Map3DArea area)
    {
        var (w, h) = area.SizeMeters;
        if (Map3DGuide.IsAreaTooLarge(w, h, _map3DSettings.MaxAreaSideM))
        {
            _log?.Info($"[Map3D] 영역이 너무 큼 {Map3DGuide.FormatAreaSize(w, h)} → 다시 그리기");
            EnterMap3DDrawMode();
            SetAimStatus(Map3DGuide.AreaTooLargeText(w, h, _map3DSettings.MaxAreaSideM));
            return;
        }
        _map3DBakeReplaces = null;   // 새 영역 — 바꿀 옛 job 없음
        RequestMap3DBake(area);   // 만들기 패널 — 3D 줌 고르기(+ 영역과 겹치는 오버레이 이미지가 있으면 굽기 방식, MapViewModel.Map3DBakeOptions)
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
            // 지도가 돌아가 있으면 ViewArea 는 돌아간 화면을 감싸는 상자라 — 보이는 화면 네 모서리(디지털 줌 배율은 가운데 기준)를 그대로 영역으로
            var area = Map3DMath.ToMap3DBearing(MainMap.Bearing) == 0d
                ? new Map3DArea(Map3DMath.BuildBbox(cLat + hLat, cLng - hLng, cLat - hLat, cLng + hLng), null)
                : Map3DAreaOf(VisibleScreenCorners(MainMap.ActualWidth, MainMap.ActualHeight, s));
            ExitAnchorDrawMode();
            TakeMap3DArea(area);
            e.Handled = true;
            return true;
        }
        if (e.Key == System.Windows.Input.Key.Escape && _map3DState == Map3DState.Shown)
        {
            // 「건물 높이」 판이 열려 있으면 3D 를 나가지 않고 판만 취소(끄는 중이면 끌기만)
            if (TryCancelBuildingHeightByEscape()) { e.Handled = true; return true; }
            // 3D 는 Esc 로 나가지 않는다 — 「나가기」 단추로만(2026-10-19). 단, 네이티브 창 3D 는 그 창이 단추를 가리므로 Esc 를 남긴다.
            if (!Map3DGuide.EscExitsMap3D(Map3DHost.HostMode)) return false;
            ExitMap3D("esc");
            e.Handled = true;
            return true;
        }
        return false;
    }

    /// <summary>보이는 화면 네 모서리 위경도(좌상 · 우상 · 우하 · 좌하) — 디지털 줌은 컨트롤 가운데 기준 배율이라 로컬 좌표에서 그만큼 안쪽.</summary>
    private IReadOnlyList<PointLatLng> VisibleScreenCorners(double width, double height, double digitalScale)
    {
        double hw = width / 2d / digitalScale, hh = height / 2d / digitalScale, cx = width / 2d, cy = height / 2d;
        return new[] { (cx - hw, cy - hh), (cx + hw, cy - hh), (cx + hw, cy + hh), (cx - hw, cy + hh) }
            .Select(p => MainMap!.FromLocalToLatLng((int)p.Item1, (int)p.Item2)).ToArray();
    }

    /// <summary>
    /// 사용자가 3D 를 나간다(ESC · 「나가기」 단추) — 같은 줌에서 곧바로 다시 들어가지 않게 진입을 보류하고 2D 로 돌아온다.
    /// 진입 방식과 상관없이 3D 로 들어오기 직전 2D 자리로(없으면 3D 마지막 카메라, Map3DMath.ExitCamera).
    /// </summary>
    private void ExitMap3D(string cause)
    {
        if (_map3DState != Map3DState.Shown) return;
        _map3DEntrySuppressed = true;
        Leave3D(Map3DExitCamera, cause);
    }

    /// <summary>
    /// 줌 게이트에 기대지 않고 그 영역 3D 로 바로 들어간다(「3D 로 보기」 단추 · 「이 영역 3D 로 보기」, 들어가는 방식과 상관없이).
    /// 돌아올 자리를 지금 2D 로 잡고(이미 잡아 뒀으면 그대로) 준비를 시작한다 — 준비 중에는 게이트가 줌으로 취소하지 않는다.
    /// 편집 · 그리기 중이면 들어가지 않고 알린다.
    /// </summary>
    private void EnterMap3DDirect(Map3DScene scene, string cause)
    {
        if (MainMap == null || !_map3DSettings.IsEnabled || _map3DState != Map3DState.Off) return;
        // 만드는 중 · 이벤트 지도 이동 중에는 게이트가 곧바로 취소한다 — 안내가 깜빡이지 않게 처음부터 들어가지 않는다
        if (_isMap3DBaking) { SetAimStatus("3D 를 만드는 중입니다 — 다 만든 뒤 다시 누르세요", autoHide: true); return; }
        if (_flyAnimation != null) return;
        if (IsMap3DBlockedByInputMode())
        {
            SetAimStatus("편집 · 그리기 중에는 3D 로 들어가지 않습니다 — 모드를 끝낸 뒤 다시 누르세요", autoHide: true);
            return;
        }
        _map3DReturn ??= Current2DCamera(0d);
        _map3DAreaViewJob = scene.Id;
        _map3DEntrySuppressed = false;
        _log?.Info($"[Map3D] 바로 들어가기({cause}) job={scene.Id} mode={_map3DSettings.EntryMode}");
        Begin3D(scene);
    }

    // ── 굽기 ──

    // ── 만들기 취소(2026-10-19) — 진행 판 「만들기 취소」 · 다시 만드는 영역의 배지 우클릭 · 3D 영역 목록 ──
    /// <summary>굽는 작업 id(시작 응답 뒤) — 취소 요청 대상.</summary>
    private string? _map3DBakeJob;
    /// <summary>「다시 만들기」면 그 영역(옛 장면) id — 그 영역 항목에 「만들기 취소」를 단다.</summary>
    private string? _map3DBakeSourceId;
    /// <summary>만들기 판을 연 「다시 만들기」 영역 id(판 [만들기] 때 굽기로 넘긴다).</summary>
    private string? _map3DBakeOptionsSourceId;
    /// <summary>취소 요청을 보내는 중(연타 막기).</summary>
    private bool _map3DBakeCancelling;
    /// <summary>이번 굽기에서 서버가 취소를 모른다고 했다(옛 map-3d) — 단추를 숨긴다.</summary>
    private bool _map3DBakeCancelUnsupported;
    /// <summary>이번 굽기의 진행 조회를 끊는다(취소 요청이 받아지면) — 이 굽기가 아직 지금 것인지도 이것으로 가린다.</summary>
    private CancellationTokenSource? _map3DBakeCts;

    /// <summary>진행 판 「만들기 취소」 보임 — 굽기 진행을 보이는 중이고 작업 id 를 받았을 때(보내는 중 · 옛 서버면 숨김).</summary>
    public bool IsMap3DCancelBakeVisible => Map3DGuide.IsCancelBakeVisible(_isMap3DBaking, _map3DBakeJob != null, _map3DBusyForBake,
                                                                            _map3DBakeCancelling, _map3DBakeCancelUnsupported);

    private RelayCommand? _cancelMap3DBakeCommand;
    /// <summary>「만들기 취소」 — 확인 없이 바로 서버에 취소 요청(되돌릴 것이 없다).</summary>
    public RelayCommand CancelMap3DBakeCommand => _cancelMap3DBakeCommand ??= new RelayCommand(_ => _ = CancelMap3DBakeAsync());

    private void NotifyMap3DCancelBake() => NotifyOfPropertyChange(nameof(IsMap3DCancelBakeVisible));

    /// <summary>
    /// 만들기 취소 — 서버가 받으면(cancelled · requested · missing) 진행 조회를 끊고 곧바로 "취소함" 알림 · 만들기 전 상태로(다시 「만들기」 가능).
    /// 서버는 1~2초 안에 멈추고 만들다 만 폴더를 지운다 — 같은 영역의 이전 장면은 그대로. 옛 서버(취소 모름)면 안내하고 단추를 숨긴다.
    /// 거의 다 만든 경우(too_late) · 이미 끝난 경우(already)는 진행 조회를 그대로 둬 실제 결과(완료 · 실패)를 보인다.
    /// </summary>
    private async Task CancelMap3DBakeAsync()
    {
        if (!_isMap3DBaking || _map3DBakeJob is not { } job || _map3DClient == null || _map3DBakeCancelling) return;
        var cts = _map3DBakeCts;
        _map3DBakeCancelling = true;
        NotifyMap3DCancelBake();
        _log?.Info($"[Map3D] 만들기 취소 요청 job={job}");
        try
        {
            var r = await _map3DClient.CancelJobAsync(job);
            _log?.Info($"[Map3D] 만들기 취소 응답 job={job} result={r.Result}");
            if (!ReferenceEquals(cts, _map3DBakeCts)) return;   // 그 사이 굽기가 끝났다
            if (r.IsUnsupported)
            {
                _map3DBakeCancelUnsupported = true;
                SetAimStatus(Map3DGuide.CancelBakeUnsupportedText, autoHide: true);
                return;
            }
            if (r.IsTooLate) { SetAimStatus(Map3DGuide.CancelBakeTooLateText, autoHide: true); return; }
            if (r.Result == "already") return;   // 막 끝났다 — 진행 조회가 결과를 보인다
            cts?.Cancel();
            EndMap3DBake();
            SetAimStatus(Map3DGuide.CancelledBakeText, autoHide: true);
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 만들기 취소 — 서버 닿지 않음: {ex.Message}");
            SetAimStatus(Map3DUnavailableText, autoHide: true);
        }
        catch (Map3DRequestException ex) { SetAimStatus($"3D 만들기를 취소할 수 없습니다: {ex.Message}", autoHide: true); }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 만들기 취소 실패: {ex.Message}");
            SetAimStatus("3D 만들기 취소에 실패했습니다.", autoHide: true);
        }
        finally
        {
            _map3DBakeCancelling = false;
            NotifyMap3DCancelBake();
        }
    }

    /// <summary>굽기 끝(완료 · 실패 · 취소) — 진행 판을 닫고 만들기 전 상태로. 다시 만들던 영역이면 그 항목의 「만들기 취소」를 뗀다.</summary>
    private void EndMap3DBake()
    {
        if (_map3DBusyForBake) SetMap3DBusy(null);   // 굽기 패널만 닫는다(전환 안내는 그대로)
        bool hadSource = _map3DBakeSourceId != null;
        _isMap3DBaking = false;
        _map3DBakeJob = null;
        _map3DBakeSourceId = null;
        _map3DBakeCts = null;
        DrawMap3DAreaCommand.RaiseCanExecuteChanged();
        NotifyMap3DCancelBake();
        if (hadSource) ReapplyMap3DAreas();
        // 만드는 동안 막았던 게이트를 다시 판정 — 취소 · 실패로 끝나도 지도를 움직이기 전에 지금 자리의 옛 영역으로 들어간다(자동)
        QueueMap3DGate();
    }

    /// <param name="enterZoom">그 영역의 3D 로 바뀌는 줌(만들기 패널에서 고른 값) — 굽기 요청 enter_zoom.</param>
    private async Task BakeMap3DAsync(Map3DArea area, string overlayMode, IReadOnlyList<Map3DBakeOverlay>? overlays, double enterZoom)
    {
        if (_map3DClient == null || _isMap3DBaking) return;
        string? replaces = _map3DBakeReplaces;   // 「다시 만들기」면 바꿀 옛 job(영역 그리기는 null)
        _map3DBakeReplaces = null;
        _isMap3DBaking = true;
        var cts = new CancellationTokenSource();
        _map3DBakeCts = cts;
        _map3DBakeSourceId = _map3DBakeOptionsSourceId;
        _map3DBakeOptionsSourceId = null;
        _map3DBakeCancelUnsupported = false;
        DrawMap3DAreaCommand.RaiseCanExecuteChanged();
        var bbox = area.Bbox;
        try
        {
            _log?.Info($"[Map3D] 굽기 요청 bbox=[{string.Join(", ", bbox)}] rect={area.Rect?.ToJson().ToString(Newtonsoft.Json.Formatting.None) ?? "-"} overlay={overlayMode} images={overlays?.Count ?? 0} enterZoom={enterZoom}");
            var job = await _map3DClient.StartBakeAsync(bbox, overlayMode, overlays, enterZoom, area.Rect);
            _map3DBakeJob = job.Id;
            if (_map3DBakeSourceId != null) ReapplyMap3DAreas();   // 다시 만드는 영역 항목에 「만들기 취소」
            while (!job.IsDone && !job.IsError && !job.IsCancelled)
            {
                SetAimStatus($"3D 만드는 중… {job.Progress:P0}{(string.IsNullOrEmpty(job.Message) ? "" : $" — {job.Message}")}");
                if (_map3DState == Map3DState.Off && _map3DGuideMode == Map3DGuideMode.None)   // 전환 안내를 덮지 않는다
                {
                    SetMap3DBusy(Map3DGuide.WithPercent("3D 를 만드는 중…", job.Progress * 100), job.Progress * 100, job.Message ?? "");
                    _map3DBusyForBake = true;
                }
                NotifyMap3DCancelBake();
                await Task.Delay(1000, cts.Token);
                job = await _map3DClient.GetJobAsync(job.Id, cts.Token);
            }
            _isMap3DBaking = false;   // 아래 QueueMap3DGate 가 새 영역으로 들어갈 수 있게(finally 보다 먼저)
            if (job.IsCancelled)   // 다른 곳(map-3d 화면 등)에서 취소됨 — 이전 장면은 그대로
            {
                SetAimStatus(Map3DGuide.CancelledBakeText, autoHide: true);
                return;
            }
            if (job.IsError)
            {
                SetAimStatus($"3D 만들기 실패: {job.Message ?? "알 수 없는 오류"}", autoHide: true);
                return;
            }
            _map3DScenes.RemoveAll(sc => sc.Id == job.Id);
            // 맨 앞에 — 옛 영역과 겹치면 새 장면이 먼저 잡힌다(FindMap3DSceneAt 은 앞부터 · 서버 목록도 최신이 앞)
            _map3DScenes.Insert(0, new Map3DScene(job.Id, bbox) { EnterZoom = enterZoom, Rect = area.Rect });
            if (replaces != null && replaces != job.Id) await ReplaceMap3DJobAsync(replaces, job.Id);   // 「다시 만들기」 — 옛 job 을 새 job 으로
            _map3DEntrySuppressed = false;
            SetAimStatus(Map3DMath.IsZoomEntry(_map3DSettings.EntryMode)
                ? $"3D 준비됨 — 줌 {Map3DGuide.Zoom(Map3DAreaEnterZoom(job.Id, enterZoom))} 이상에서 3D"
                : "3D 준비됨 — 지도 왼쪽 위 「3D 로 보기」로 들어갑니다", autoHide: true);
            QueueMap3DGate();
            _ = RefreshMap3DAreasAsync();   // 새 영역 테두리 · 배지(만든 날짜 · 건물 수는 서버 목록에서)
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _log?.Info("[Map3D] 굽기 진행 조회 끊음(만들기 취소)");   // 알림 · 상태 정리는 취소 쪽(CancelMap3DBakeAsync)이 이미 했다
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
            // 취소한 뒤 곧바로 다시 만들기를 시작했으면 그 굽기 상태는 건드리지 않는다
            if (ReferenceEquals(_map3DBakeCts, cts)) EndMap3DBake();
            cts.Dispose();
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
        var scene = FindMap3DSceneAt(center);
        // 굽는 동안은 3D 로 들어가지 않는다 — 영역을 그리고 나면 곧바로 옛 3D 로 들어가 굽기 진행 패널이 가려졌다(2026-10-17 실기 S1)
        var gate = Map3DGateFor(scene);   // 그 영역의 진입 · 이탈 · 미리 준비 줌(영역마다 3D 줌)
        // 이벤트 2D 비행 중에는 출발 자리에서 3D 로 다시 들어가지 않는다 — 끝난 자리에서 판정한다(마지막 프레임의 위치 변경이 게이트를 다시 부른다)
        // 수동(단추)이면 줌으로는 들어가지 않는다 — 단추 · 「이 영역 3D 로 보기」로 직접 들어가는 중이면 줌과 상관없이 유지(EnterMap3DDirect)
        bool pending = _map3DState == Map3DState.Pending;
        bool direct = pending && _map3DAreaViewJob != null && _map3DAreaViewJob == _map3DJob && scene?.Id == _map3DJob;
        bool active = !_isMap3DBaking && _flyAnimation == null && !IsMap3DBlockedByInputMode()
                      && Map3DMath.DecideEntry(zoom, pending, scene != null, gate.Enter, gate.Exit, Map3DMath.IsZoomEntry(_map3DSettings.EntryMode), direct);

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

    /// <summary>지점(화면 가운데)을 덮는 만들어 둔 3D 영역 — 회전 영역은 회전 사각형 안, 옛 영역은 bbox 안(Map3DMath.SceneContains).</summary>
    private Map3DScene? FindMap3DSceneAt(PointLatLng p) => _map3DScenes.Find(sc => Map3DMath.SceneContains(sc, p.Lng, p.Lat));

    /// <summary>로딩 중(Pending) 진입 취소 — 패널을 닫고 1×1 로 접는다(불러오던 장면은 그대로 둬 다음 진입에 쓴다).</summary>
    private void CancelMap3DPending()
    {
        SetMap3DState(Map3DState.Off); SetMap3DBusy(null);
        _map3DAreaViewJob = null; _map3DReturn = null;   // 「영역 보기」 진입도 함께 취소
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
            if (scene == null || Map3DGoneJobs.Contains(scene.Id)) return;   // 지운 장면(지우기 전에 보낸 조회의 늦은 응답)은 되살리지 않는다
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
        if (_map3DAreaViewJob != scene.Id) { _map3DAreaViewJob = null; _map3DReturn = null; }   // 다른 영역은 줌 게이트 진입(돌아갈 자리는 펼칠 때 잡는다)
        SetMap3DState(Map3DState.Pending);
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
        _map3DReturn ??= Current2DCamera(0d);   // 나가면 돌아올 자리 — 줌 게이트 진입은 지금 2D(「영역 보기」는 누르기 전 자리를 이미 잡았다)
        SetMap3DState(Map3DState.Shown);
        SetMap3DBusy(null);
        _map3DRevealedBehindLogin = Map3DGuide.IsBehindLogin(ResolveTokenStorage()?.IsAuthenticated);   // 로그인 뒤 한 번 안내(R5)
        _map3DLastCamera = null;
        Map3DOverlay.Resume(MainMap);                                                    // ready 때 보낸 스냅샷에 이어 바뀐 것만(기록이 없으면 전체 스냅샷)
        Map3DHost.PostCamera(Current2DCamera(0d), animate: false);                    // 지금 2D 와 같은 자리 · 축척(로딩 중 움직였을 수 있다)
        Map3DHost.Reveal();
        Map3DHost.PostCamera(Current2DCamera(Map3DMath.DefaultPitchDeg), animate: true); // 0.6 s 동안 눕힌다
        // 모든 진입 — 영역 전체가 화면에 다 들어오는 비스듬한 시점으로(거리는 3D 가 화면 비율 · 시야각으로 셈한다). 옛 3D 서버는 모르는 메시지라 눕히기만 남는다.
        // 「이 영역 3D 로 보기」로 연 회전 영역은 그 회전각(사각형이 반듯하게) — 그 밖은 지금 2D 방위
        var areaViewRect = _map3DAreaViewJob != null && _map3DAreaViewJob == _map3DJob ? _map3DScenes.Find(sc => sc.Id == _map3DJob)?.Rect : null;
        Map3DHost.PostJson(Map3DMath.EnterViewMessageJson(Map3DMath.EnterBearing(MainMap.Bearing, areaViewRect)));
        HoldMap2DInput();                                                               // 3D 가 보이는 동안 2D 입력 막음(나올 때 RestoreMap2DInput)
        EnsureAimEscWindowHook();
        BeginMap3DWindows();                                                            // 카메라 상자 · 조립 카드가 3D 마커 자리를 따라간다(MapViewModel.Map3DWindows)
        _map3DWpfFrames.Reset(); _map3DSync2D.Reset();                                  // 계측 — 3D 의 perf.stats 가 올 때 WPF 프레임 간격과 함께 한 줄([Map3D][perf])
        System.Windows.Media.CompositionTarget.Rendering -= OnMap3DWpfFrame;
        System.Windows.Media.CompositionTarget.Rendering += OnMap3DWpfFrame;
        _log?.Info($"[Map3D] 3D 표시 job={_map3DJob} host={Map3DHost.HostMode}");
        _log?.Info(Map3DGuide.TransitionTimingText(_map3DEnterAt, Map3DHost.CoreReadyAt, Map3DHost.ReadyAt, _map3DSnapshotAt,
                                                   Map3DHost.RenderedAt, DateTime.UtcNow, _map3DEnterPrewarmed, _map3DEnterPreloaded));
    }

    private void Leave3D(Map3DCamera? cam, string cause)
    {
        SetMap3DState(Map3DState.Off);   // 지도가 없어도 상태는 내린다 — "3D"(「나가기」)로 남지 않게
        // 3D 화면을 접고 2D 입력을 먼저 되살린다 — 아래 단계가 예외를 던져도 "2D 인데 클릭 · 휠이 안 먹는" 지도로 남지 않게(2026-10-19 사용자 보고)
        if (MainMap != null) { Map3DHost.Park(); RestoreMap2DInput(); }
        CloseBuildingHeightPanel(clearUndo: true);   // 「건물 높이」 판 — 미리보기 원래대로 · 판 안 되돌리기 비움
        if (MainMap == null) return;
        _map3DAreaViewJob = null; _map3DReturn = null;   // 다음 진입은 다시 줌 게이트 또는 새 「영역 보기」
        _map3DRevealedBehindLogin = false;
        // 짧은 복귀 안내(R5) — 패널은 2D 위에 0.5 s
        SetMap3DBusy(Map3DGuide.LeaveTitle, 100);
        RunMap3DGuideTimer(Map3DGuideMode.HideWhenDue, Map3DGuide.LeaveShow, timedBar: false);
        _map3DOverlay?.Pause();   // 페이지는 1×1 로 살아 있다 — 다시 펼칠 때 바뀐 것만 보낸다
        System.Windows.Media.CompositionTarget.Rendering -= OnMap3DWpfFrame;
        if (cam is { } c) Apply3DCameraTo2D(c);
        CreateScaleBar();              // 축척 막대를 2D 값으로(줌이 그대로면 줌 변경 이벤트가 오지 않는다)
        CurrentAltitudeText = null;    // 좌표 칩 고도 줄은 3D 에서만
        EndMap3DWindows();   // 열린 창은 닫지 않고 같은 마커의 2D 자리로(MapViewModel.Map3DWindows)
        MainMap.Focus();
        _log?.Info($"[Map3D] 2D 복귀({cause}) zoom={MainMap.EffectiveZoom:F1} bearing={MainMap.Bearing:F1}");
        LogMap2DInputState(cause);
        QueueMap3DGate();
    }

    // ── 2D 입력 막기 · 되살리기(2026-10-19 "3D 들어갔다 나왔더니 좌 · 우 클릭 · 휠이 다 안 먹는다") ──

    /// <summary>3D 를 펼치며 막기 직전 지도 IsHitTestVisible 의 로컬 값(없었으면 UnsetValue). 막고 있지 않으면 null — 두 번 펼쳐도 처음 값만 적는다.</summary>
    private object? _map3DHeldHitTest;

    /// <summary>3D 가 보이는 동안 2D 지도 입력을 막는다 — 막기 전 값을 적어 둔다(RestoreMap2DInput 이 되돌린다).</summary>
    private void HoldMap2DInput()
    {
        _map3DHeldHitTest ??= MainMap!.ReadLocalValue(UIElement.IsHitTestVisibleProperty);
        MainMap!.IsHitTestVisible = false;
    }

    /// <summary>
    /// 3D 동안 막은 2D 입력을 막기 전 값으로 되돌린다(3D 중 다른 코드가 바꿨어도). 3D 화면 안에 남은 마우스 캡처도 푼다 —
    /// 캡처가 접힌 3D 화면에 남으면 지도 누름 · 휠이 그쪽으로 간다.
    /// </summary>
    private void RestoreMap2DInput()
    {
        var map = MainMap!;
        if (_map3DHeldHitTest is bool held) map.IsHitTestVisible = held;
        else map.ClearValue(UIElement.IsHitTestVisibleProperty);   // 원래 로컬 값 없음 = 기본(누름 받음)
        _map3DHeldHitTest = null;
        if (System.Windows.Input.Mouse.Captured is System.Windows.Media.Visual captured && Map3DHost.IsAncestorOf(captured))
            System.Windows.Input.Mouse.Capture(null);
        // 3D 동안 뗌을 못 받아 남은 일시 제스처(이미지 끌기 · 마퀴 · 팬 억제 등) — 사용자 증언(줌 단추를 누르니 풀렸다)의 줌 변경 정리와 같은 것을 나갈 때 한다
        var cleared = map.ResetTransientInput();
        if (cleared.Length > 0) _log?.Warning($"[Map3D] 나가며 남은 2D 입력 상태 정리: {cleared}");
    }

    /// <summary>나간 직후와 한가해진 뒤 2D 입력 상태 한 줄씩 — 「나가기 뒤 지도가 입력을 안 받는다」 원인 판별용(무엇이 막는지 이름으로).</summary>
    private void LogMap2DInputState(string cause)
    {
        if (_log == null || MainMap == null) return;
        _log.Info(Map2DInputStateText(cause, "now"));
        MainMap.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try { if (MainMap != null) _log?.Info(Map2DInputStateText(cause, "idle")); }
            catch (Exception ex) { _log?.Warning($"[Map3D] 2D 입력 상태 기록 실패(격리): {ex.Message}"); }
        }));
    }

    /// <summary>
    /// 2D 지도 입력 상태 — 누름 판정 · 사용 가능 · 보임, 막는 조상(첫 하나), 지도 가운데를 실제로 누르면 받는 요소(지도 밖이면 덮은 것),
    /// 마우스 캡처 · 키보드 포커스, 3D 화면 펼침, 지도 처리기를 일찍 끝내는 상태(GMapCustomControl.InputGateText).
    /// </summary>
    private string Map2DInputStateText(string cause, string when)
    {
        var map = MainMap!;
        string? blocker = null;
        for (var d = System.Windows.Media.VisualTreeHelper.GetParent(map); d != null && blocker == null; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            if (d is UIElement u && (!u.IsHitTestVisible || !u.IsEnabled)) blocker = DescribeInput(u);
        string center = "창 밖";
        if (PresentationSource.FromVisual(map)?.RootVisual is UIElement root)
        {
            var hit = root.InputHitTest(map.TranslatePoint(new Point(map.ActualWidth / 2, map.ActualHeight / 2), root)) as DependencyObject;
            center = hit == null ? "없음" : hit is System.Windows.Media.Visual v && (ReferenceEquals(v, map) || map.IsAncestorOf(v)) ? "지도" : DescribeInput(hit);
        }
        return $"[Map3D] 2D 입력 상태({cause} · {when}) hit={map.IsHitTestVisible} enabled={map.IsEnabled} visible={map.IsVisible} 막는조상={blocker ?? "없음"} " +
               $"가운데={center} 캡처={DescribeInput(System.Windows.Input.Mouse.Captured as DependencyObject)} 포커스={DescribeInput(System.Windows.Input.Keyboard.FocusedElement as DependencyObject)} " +
               $"마우스아래={DescribeInput(System.Windows.Input.Mouse.DirectlyOver as DependencyObject)} 3D펼침={Map3DHost.IsRevealed} 비행={_flyAnimation != null} " +
               $"줌={map.EffectiveZoom:F1}(tile={map.Zoom:F0},dzl={map.DigitalZoomLevel}) 변환={map.RenderTransform?.Value.ToString() ?? "없음"} 회전={map.MapRotation:F1} 지도상태={map.InputGateText()}";
    }

    private static string DescribeInput(DependencyObject? d)
    {
        if (d == null) return "없음";
        var id = System.Windows.Automation.AutomationProperties.GetAutomationId(d);
        return string.IsNullOrEmpty(id) ? d.GetType().Name : $"{d.GetType().Name}#{id}";
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
        if (msg.Type == "error")
        {
            // 3D 화면이 장면을 불러오지 못했다(map-3d sceneload.js) — 지워진 장면이면 그 job 을 모든 상태에서 빼고 2D 로 · 영역 목록을 새로 고친다
            _log?.Warning($"[Map3D] 3D 화면 불러오기 실패 job={msg.Job ?? "-"} reason={msg.Reason ?? "-"}");
            if (msg.Job is not { } failed) return;
            if (msg.Reason == Map3DGuide.SceneMissingReason)
            {
                RetireMap3DJob(failed, "scene-missing", remember: true);
                SetAimStatus(Map3DGuide.SceneMissingText, autoHide: true);
                _ = RefreshMap3DAreasAsync();
            }
            else if (_map3DJob == failed) OnMap3DLoadFailed(Map3DGuide.SceneLoadFailedText(msg.Reason));
            return;
        }
        if (msg.Type == "overlay.screen")
        {
            try { if (msg.Screens is { } screens) OnMap3DScreens(screens, msg.ScreenView); }
            catch (Exception ex) { _log?.Error($"[Map3D] 창 자리 처리 실패(격리): {ex.Message}"); }
            return;
        }
        if (msg.Type == "perf.stats")
        {
            // 3D 프레임 단계 요약(몇 초마다) + GIS 쪽 WPF 프레임 간격 · 2D 따라가기 시간 — 병목 판정은 .harness PLAN 의 표
            if (msg.PerfLines is { } lines)
                _log?.Info(Map3DPerf.LogLine(lines, Map3DHost.HostMode, Map3DHost.ActualWidth, Map3DHost.ActualHeight, IsRemoteSession(),
                                             System.Windows.Media.RenderCapability.Tier >> 16, _map3DWpfFrames.Take(), _map3DSync2D.Take()));
            return;
        }
        if (msg.Type == "cursor")
        {
            if (_map3DState == Map3DState.Shown && msg.Cursor is { } cursor) SetMap3DCursor(cursor);
            return;
        }
        if (msg.Type == "building")
        {
            try { if (msg.Building is { } building) OnMap3DBuildingPicked(building); }   // 「건물 높이」 판(MapViewModel.BuildingHeight)
            catch (Exception ex) { _log?.Error($"[Map3D] 건물 고르기 처리 실패(격리): {ex.Message}"); }
            return;
        }
        if (msg.Type == "building.none")
        {
            // 3D 빈 땅(건물 · 마커 아님) 클릭 — 3D 는 윤곽을 지웠다. 판도 닫는다(미리보기 중이면 원래 높이로 = 취소, 2026-10-19 사용자 결정)
            if (_map3DState != Map3DState.Shown || !IsBuildingHeightPanelOpen) return;
            _log?.Info("[Map3D] 빈 땅 클릭 → 「건물 높이」 판 닫기");
            try { CloseBuildingHeightPanel(clearUndo: false); }
            catch (Exception ex) { _log?.Error($"[Map3D] 빈 땅 클릭 처리 실패(격리): {ex.Message}"); }
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
        NoteMap3DCameraForFlyTo(msg.Reason);   // 3D 조작 중이면 이벤트 이동이 양보한다(MapViewModel.EventFlyTo)
        // 3D 에서는 줌으로 나가지 않는다(진입 방식과 상관없이 나가기 · ESC 만 — Map3DMath.ShouldSync2DToCamera).
        // WPF 안 3D(CompositionControl)면 2D 오버레이(나침반 · 축척바 · 줌 · 좌표)가 3D 위에 그대로 보인다 — 뒤의 2D 지도를
        // 3D 카메라(위치 · 줌 · 방위)에 맞춰 오버레이가 3D 상태를 보이게 하고, 2D 로 돌아갈 때도 끊기지 않게 한다(2026-10-17 실기: 나침반이 0° 에 멈춰 있었다).
        if (Map3DMath.ShouldSync2DToCamera(Map3DHost.HostMode, cam))
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            Apply3DCameraTo2D(cam);
            _map3DSync2D.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        CreateScaleBar();   // 축척 막대 = 3D 화면 가운데 1 px 의 실제 거리(TryCreateMap3DScaleBar)
    }

    /// <summary>좌표 칩 = 3D 마우스가 가리키는 땅(2D 마우스 이동과 같은 형식 — WGS-84 · MGRS · UTM, 고도는 덧붙임).</summary>
    private void SetMap3DCursor(Map3DCursor c)
    {
        CurrentCoordinatePosition = new CoordinateModel(c.Lat, c.Lon, c.Alt ?? 0.0);
        var coordinate = new CoordinateSharp.Coordinate(c.Lat, c.Lon);
        CurrentMGRS = coordinate.MGRS.ToString();
        CurrentUTM = coordinate.UTM.ToString();
        CurrentAltitudeText = Map3DMath.AltitudeText(c.Alt);
    }

    /// <summary>
    /// 3D 표시 중이고 3D 카메라를 받았으면 축척 막대를 3D 화면 가운데 m/px 로 세운다(CreateScaleBar 서두) — 뒤의 2D 줌은 0.5 단위로 맞춰지고
    /// 2D 막대는 정수 줌 표라 3D 화면과 맞지 않는다. 세웠으면 true.
    /// </summary>
    private bool TryCreateMap3DScaleBar()
    {
        if (_map3DState != Map3DState.Shown || _map3DLastCamera is not { } cam) return false;
        if (Map3DMath.ScaleBar(Map3DMath.MetersPerPixel(cam.Zoom, cam.Lat)) is not { } bar) return false;
        Scale = bar.Label;
        ScalePoints = new System.Windows.Media.PointCollection { new Point(0.0, 0.0), new Point(0.0, 5.0), new Point(bar.Px, 5.0), new Point(bar.Px, 0.0) };
        NotifyOfPropertyChange(() => ScalePoints);
        return true;
    }

    /// <summary>WPF 프레임 하나(3D 표시 중만 구독) — 간격을 모아 perf 로그 줄에 싣는다.</summary>
    private void OnMap3DWpfFrame(object? sender, EventArgs e)
    {
        if (e is System.Windows.Media.RenderingEventArgs r) _map3DWpfFrames.AddFrame(r.RenderingTime);
    }

    private void OnMap3DLoadFailed(string reason)
    {
        _log?.Warning($"[Map3D] {reason}");
        if (_map3DState == Map3DState.Off) return;
        _map3DEntrySuppressed = true;   // 같은 줌에서 계속 다시 시도하지 않게
        if (_map3DState == Map3DState.Shown) Leave3D(Map3DExitCamera, "load-failed");
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
        if (_map3DState == Map3DState.Shown)
        {
            // 모드를 끝낸 그 자리에서 곧바로 다시 3D 로 들어가지 않는다 — 「나가기」와 같은 진입 보류(지도를 움직여 진입 줌 · 영역을 새로 만족할 때까지, 2026-10-19 사용자 결정)
            _map3DEntrySuppressed = true;
            Leave3D(Map3DExitCamera, "input-mode");
        }
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
        if (!Map3DStaysAt(MainMap.Position, MainMap.EffectiveZoom))
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

    /// <summary>그 자리 · 실효줌에서 지금 3D 장면을 유지하는가(장면 안이고 줌 ≥ 그 장면의 이탈 줌).</summary>
    private bool Map3DStaysAt(PointLatLng center, double effectiveZoom)
    {
        var scene = _map3DScenes.Find(sc => sc.Id == _map3DJob);
        bool inside = scene != null && Map3DMath.SceneContains(scene, center.Lng, center.Lat);
        return Map3DMath.ShouldStay3DAfterMove(effectiveZoom, inside, Map3DGateFor(scene).Exit);
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
