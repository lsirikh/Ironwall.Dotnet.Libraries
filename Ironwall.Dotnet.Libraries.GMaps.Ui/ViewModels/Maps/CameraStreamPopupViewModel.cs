using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/// <summary>화면에 보이는 영상 상태(FR-26 · FR-29) — 상자 안 안내 문구 · 단추를 가른다.</summary>
public enum CameraPopupVideoStatus
{
    /// <summary>아직 열지 않음 · 닫힘.</summary>
    Idle = 0,
    /// <summary>연결 중(주소 조회 · RTSP 연결 · 호스트 재시작 중).</summary>
    Connecting = 1,
    /// <summary>재생.</summary>
    Playing = 2,
    /// <summary>연결 안 됨 — [다시 시도].</summary>
    Failed = 3,
    /// <summary>제공자가 지원하지 않음(외부 VMS 자리 등).</summary>
    Unsupported = 4,
    /// <summary>팝업 호스트 없음 · 일시 중지 — "영상 기능을 사용할 수 없습니다 · [다시 시작]". GIS 는 정상.</summary>
    HostUnavailable = 5,
}

/// <summary>
/// 맵 위 카메라 팝업 1개의 런타임 상태(비영속).
/// <para>
/// 영상(camera-popup-modes T-02 · FR-24): GIS 는 LibVLC 를 부르지 않는다. 팝업 호스트 프로세스가 제공자(ONVIF · RTSP 주소)로
/// 주소를 얻어 디코딩하고, 상자 크기(물리 픽셀)의 프레임을 공유 메모리로 넘긴다 — 여기서는 그 <see cref="FrameSource"/> 를
/// <c>HostedVideoView</c> 가 그리기만 한다. 상자 크기가 바뀌면 잠깐 기다렸다가(<see cref="ResizeDebounce"/>) 새 크기로 다시 연다.
/// 호스트가 죽으면 감시자가 다시 띄우고 같은 스트림을 복원한다 — 그동안 "연결 중", 일시 중지면 [다시 시작].
/// </para>
/// </summary>
public class CameraStreamPopupViewModel : PropertyChangedBase, IAsyncDisposable
{
    public const double DefaultWidth = 384;
    public const double DefaultHeight = 300;
    public const double LargeWidth = 640;
    public const double LargeHeight = 380;
    /// <summary>헤더(창 이동) 높이 — 영상 상자 = 팝업 높이 − 이 값(템플릿 Row0 과 같다).</summary>
    public const double HeaderHeight = 42;
    /// <summary>상자 크기 변화 뒤 다시 열기까지 기다리는 시간 — 크게보기 토글 · 창 크기 변화가 한 번에 모이게.</summary>
    public static readonly TimeSpan ResizeDebounce = TimeSpan.FromMilliseconds(250);
    /// <summary>이만큼(px) 이하로 바뀌면 다시 열지 않는다(반올림 · DPI 떨림).</summary>
    public const int ResizeTolerancePx = 8;

    private readonly ICameraPopupHost? _host;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _videoGate = new();
    private double _canvasLeft;
    private double _canvasTop;
    private double _popupWidth = DefaultWidth;
    private double _popupHeight = DefaultHeight;
    private double _cameraScreenX;
    private double _cameraScreenY;
    private double _lineX1, _lineY1, _lineX2, _lineY2;
    private bool _isLarge;
    private ICommand? _closeCommand;
    private ICommand? _toggleSizeCommand;

    /// <summary>닫기 요청 — MapViewModel이 컬렉션에서 제거 + DisposeAsync.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>드래그 종료 — MapViewModel이 CanvasLeft/Top→AnchorGeo 재계산 + DB 저장.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>컨트롤이 드래그 종료 시 호출 → DragCompleted 발화.</summary>
    internal void RaiseDragCompleted() => DragCompleted?.Invoke(this, EventArgs.Empty);

    /// <summary>좌클릭 선택 요청 — MapViewModel이 SelectedCameraPopup 설정 + 맨앞 이동. (FR-SEL-01)</summary>
    public event EventHandler? SelectRequested;

    /// <summary>컨트롤 좌클릭 시 호출 → 선택 요청.</summary>
    internal void RaiseSelectRequested() => SelectRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>우버튼 드래그-PTZ 완료 — MapViewModel이 호스트로 연속 이동 → 정지(ICameraPopupControl). (FR-DRAG-03)</summary>
    public event EventHandler<PtzDragEventArgs>? PtzDragRequested;

    /// <summary>컨트롤이 영상 위 좌버튼 드래그 종료(8px 초과) 시 호출. 델타·영상 치수를 전달.</summary>
    internal void RaisePtzDrag(double dx, double dy, double imageW, double imageH)
        => PtzDragRequested?.Invoke(this, new PtzDragEventArgs(dx, dy, imageW, imageH));

    /// <summary>영상 위 휠 → PTZ 줌(+1=줌인 / -1=줌아웃). MapViewModel이 호스트로 줌 펄스(ICameraPopupControl). (FR-PTZCTL-03)</summary>
    public event EventHandler<int>? PtzZoomRequested;

    /// <summary>컨트롤이 영상 위 휠 회전 시 호출(방향 ±1).</summary>
    internal void RaisePtzZoom(int direction) => PtzZoomRequested?.Invoke(this, direction);

    // 줌 +/- 버튼 — press-hold(누르면 연속 줌·뗌 정지). 컨트롤이 PreviewMouseDown/Up에서 Hold/Stop 발화.
    // 줌 release는 PtzStopRequested 재사용(StopAsync가 PanTilt+Zoom 정지). 휠은 별도 PtzZoomRequested 펄스 경로 유지.
    /// <summary>줌 버튼 누름 → 연속 줌 시작(+1=줌인/-1=줌아웃). 컨트롤 PreviewMouseDown. 뗌은 PtzStopRequested.</summary>
    public event EventHandler<int>? ZoomHoldRequested;
    internal void RaiseZoomHold(int direction) => ZoomHoldRequested?.Invoke(this, direction);

    /// <summary>포커스 버튼 누름 → 연속 포커스 시작(+1=far/-1=near). 컨트롤 PreviewMouseDown. IsImagingCapable일 때만.</summary>
    public event EventHandler<int>? FocusHoldRequested;
    internal void RaiseFocusHold(int direction) => FocusHoldRequested?.Invoke(this, direction);
    /// <summary>포커스 버튼 뗌/캡처분실/닫기 → 포커스 모터 정지(ImagingClient Stop — PTZ StopAsync와 별개 경로).</summary>
    public event EventHandler? FocusStopRequested;
    internal void RaiseFocusStop() => FocusStopRequested?.Invoke(this, EventArgs.Empty);

    /// <param name="host">팝업 호스트(없으면 영상은 "사용할 수 없음", 나머지 팝업 기능은 그대로).</param>
    /// <param name="provider">영상 · PTZ 제공자 정보(<c>CameraPopupProviderFactory</c>). 계정 포함 — 로그에 찍을 땐 ToString(가림).</param>
    /// <param name="delay">디바운스 대기(시험용 주입). 기본 <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    public CameraStreamPopupViewModel(int cameraId, string? title, PointLatLng anchorGeo,
        ICameraPopupHost? host = null, VideoProviderInfo? provider = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        CameraId = cameraId;
        Title = string.IsNullOrWhiteSpace(title) ? $"카메라 {cameraId}" : title!;
        AnchorGeo = anchorGeo;
        _host = host;
        Provider = provider ?? new VideoProviderInfo();
        _delay = delay ?? ((t, ct) => Task.Delay(t, ct));
        StreamId = $"map-cam-{cameraId}";
        if (_host is not null) _host.StateChanged += OnHostStateChanged;
        RefreshVideoStatus();
    }

    /// <summary>카메라 장비 Id(= PidsSymbol.LinkedDeviceId). 팝업 식별/위치 키.</summary>
    public int CameraId { get; }
    public string Title { get; }

    /// <summary>영상 · PTZ 제공자 정보(호스트로 가는 명령에 싣는다).</summary>
    public VideoProviderInfo Provider { get; }

    /// <summary>호스트 오버레이 스트림 id(카메라당 하나 — 다시 열면 같은 id 로 교체).</summary>
    public string StreamId { get; }

    private bool _isResolvingSource;
    /// <summary>ONVIF 로 영상 주소를 얻는 중(호스트 상태 Opening "resolving") — "영상 주소 조회 중…" 배지. (FR-05)</summary>
    public bool IsResolvingSource { get => _isResolvingSource; set { if (_isResolvingSource == value) return; _isResolvingSource = value; NotifyOfPropertyChange(nameof(IsResolvingSource)); } }

    /// <summary>팝업 좌상단 코너의 위경도 앵커(드래그 완료 시 갱신 → DB 저장).</summary>
    public PointLatLng AnchorGeo { get; set; }

    // ── 영상(호스트 프레임) ────────────────────────────────────────────────
    private IFrameSource? _frameSource;
    private (int Width, int Height) _viewport = ((int)DefaultWidth, (int)(DefaultHeight - HeaderHeight));
    private (int Width, int Height) _openedSize;
    private bool _videoStarted;
    private bool _openFailed;
    private int _openVersion;
    private CameraPopupVideoStatus _videoStatus;

    /// <summary>호스트가 쓰는 공유 메모리 프레임(HostedVideoView.FrameSource 바인딩). 다시 열 때마다 바뀐다.</summary>
    public IFrameSource? FrameSource
    {
        get => _frameSource;
        private set { if (ReferenceEquals(_frameSource, value)) return; _frameSource = value; NotifyOfPropertyChange(nameof(FrameSource)); }
    }

    /// <summary>지금 열려 있는(또는 요청한) 상자 크기(물리 픽셀). 시험 · 진단용.</summary>
    public (int Width, int Height) OpenedVideoSize => _openedSize;

    public CameraPopupVideoStatus VideoStatus
    {
        get => _videoStatus;
        private set
        {
            if (_videoStatus == value) return;
            _videoStatus = value;
            NotifyOfPropertyChange(nameof(VideoStatus));
            NotifyOfPropertyChange(nameof(VideoStatusText));
            NotifyOfPropertyChange(nameof(IsVideoMessageVisible));
            NotifyOfPropertyChange(nameof(IsVideoPlaying));
            NotifyOfPropertyChange(nameof(CanRetryVideo));
            NotifyOfPropertyChange(nameof(CanRestartHost));
        }
    }

    /// <summary>상자 안 안내 문구(연결 중… · 재생 · 연결 안 됨 · 지원 안 함 · 영상 기능을 사용할 수 없습니다).</summary>
    public string VideoStatusText => StatusText(VideoStatus);

    /// <summary>가운데 안내판을 보일까(재생 · 대기 아닐 때).</summary>
    public bool IsVideoMessageVisible => VideoStatus is CameraPopupVideoStatus.Connecting or CameraPopupVideoStatus.Failed
        or CameraPopupVideoStatus.Unsupported or CameraPopupVideoStatus.HostUnavailable;

    public bool IsVideoPlaying => VideoStatus == CameraPopupVideoStatus.Playing;

    /// <summary>[다시 시도] — 그 상자만 다시 연다(FR-26).</summary>
    public bool CanRetryVideo => VideoStatus == CameraPopupVideoStatus.Failed;

    /// <summary>[다시 시작] — 호스트 재시작(FR-29 · 재시작 예산을 비운다).</summary>
    public bool CanRestartHost => VideoStatus == CameraPopupVideoStatus.HostUnavailable && _host is not null;

    private ICommand? _retryVideoCommand, _restartHostCommand;
    public ICommand RetryVideoCommand => _retryVideoCommand ??= new RelayCommand(() => OpenVideoNow());
    public ICommand RestartHostCommand => _restartHostCommand ??= new RelayCommand(() =>
    {
        try { _host?.Restart(); }
        catch { /* 감시자는 던지지 않는다 — 방어 */ }
    });

    /// <summary>상태 → 문구(순수).</summary>
    public static string StatusText(CameraPopupVideoStatus status) => status switch
    {
        CameraPopupVideoStatus.Connecting => "연결 중…",
        CameraPopupVideoStatus.Playing => "재생",
        CameraPopupVideoStatus.Failed => "연결 안 됨",
        CameraPopupVideoStatus.Unsupported => "지원 안 함",
        CameraPopupVideoStatus.HostUnavailable => "영상 기능을 사용할 수 없습니다",
        _ => string.Empty,
    };

    /// <summary>
    /// 호스트 상태 · 스트림 상태 → 화면 상태(순수, FR-26/29). 호스트가 없거나 멈춰 있으면 스트림과 상관없이 HostUnavailable.
    /// </summary>
    public static CameraPopupVideoStatus ComputeStatus(CameraPopupHostState? host, bool opened, StreamState? stream, string? detail)
    {
        if (host is null or CameraPopupHostState.Unavailable or CameraPopupHostState.Suspended
            or CameraPopupHostState.Disposed or CameraPopupHostState.NotStarted)
            return CameraPopupVideoStatus.HostUnavailable;
        if (host is CameraPopupHostState.Starting or CameraPopupHostState.Restarting) return CameraPopupVideoStatus.Connecting;
        if (!opened) return CameraPopupVideoStatus.Connecting;
        return stream switch
        {
            null or StreamState.Opening => CameraPopupVideoStatus.Connecting,
            StreamState.Playing => CameraPopupVideoStatus.Playing,
            StreamState.Closed => CameraPopupVideoStatus.Idle,
            _ when detail == CameraErrorCodes.NotSupported => CameraPopupVideoStatus.Unsupported,
            _ => CameraPopupVideoStatus.Failed,
        };
    }

    /// <summary>영상 시작 — 잠깐 기다렸다가(컨트롤이 실제 픽셀 크기를 알려줄 시간) 연다. 여러 번 불러도 한 번.</summary>
    public void StartVideo()
    {
        if (_videoStarted || _disposed) return;
        _videoStarted = true;
        RefreshVideoStatus();
        ScheduleOpen();
    }

    /// <summary>컨트롤이 알려주는 영상 상자 크기(물리 픽셀). 열린 뒤 크게 바뀌면 디바운스 후 새 크기로 다시 연다.</summary>
    public void UpdateVideoViewport(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return;
        var size = ClampSize(pixelWidth, pixelHeight);
        lock (_videoGate) { _viewport = size; }
        if (!_videoStarted || _disposed || _openedSize == default) return;
        var opened = _openedSize;
        if (Math.Abs(opened.Width - size.Width) <= ResizeTolerancePx && Math.Abs(opened.Height - size.Height) <= ResizeTolerancePx) return;
        ScheduleOpen();
    }

    /// <summary>상자 크기 정리(순수) — 공유 메모리 한도 안 · 짝수 · 최소 16.</summary>
    public static (int Width, int Height) ClampSize(int width, int height)
    {
        int w = Math.Clamp(width, 16, SharedFrameLayout.MaxWidth) & ~1;
        int h = Math.Clamp(height, 16, SharedFrameLayout.MaxHeight) & ~1;
        return (w, h);
    }

    private void ScheduleOpen()
    {
        int version = Interlocked.Increment(ref _openVersion);
        _ = DelayThenOpenAsync(version);
    }

    private async Task DelayThenOpenAsync(int version)
    {
        try
        {
            await _delay(ResizeDebounce, _lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }
        if (version != Volatile.Read(ref _openVersion) || _disposed) return;
        OpenVideoNow();
    }

    /// <summary>지금 크기로 (다시) 연다. 같은 스트림 id 라 감시자 · 호스트가 이전 스트림을 닫고 교체한다.</summary>
    internal void OpenVideoNow()
    {
        if (_disposed) return;
        Interlocked.Increment(ref _openVersion);   // 대기 중인 디바운스는 무효
        _videoStarted = true;
        if (_host is null) { RefreshVideoStatus(); return; }
        (int Width, int Height) size;
        lock (_videoGate) { size = _viewport; }
        try
        {
            var old = _frameSource;
            if (old is not null) old.StateChanged -= OnFrameSourceStateChanged;
            var source = _host.OpenOverlay(new OverlayStreamRequest
            {
                StreamId = StreamId,
                Camera = new CameraRef { CameraId = CameraId.ToString(), Name = Title },
                Provider = Provider,
                Width = size.Width,
                Height = size.Height,
            });
            _openedSize = size;
            if (source is not null) source.StateChanged += OnFrameSourceStateChanged;
            FrameSource = source;
            _openFailed = source is null;
        }
        catch
        {
            _openFailed = true;   // 감시자는 던지지 않는다 — 방어(FR-27)
        }
        RefreshVideoStatus();
    }

    private void OnFrameSourceStateChanged(object? sender, EventArgs e) => RefreshVideoStatus();

    private void OnHostStateChanged(object? sender, CameraPopupHostStateChangedEventArgs e) => RefreshVideoStatus();

    /// <summary>호스트 · 스트림 상태를 다시 읽어 화면 상태를 정한다(어느 스레드에서 불러도 된다 — 알림은 CM 이 UI 로).</summary>
    internal void RefreshVideoStatus()
    {
        try
        {
            var source = _frameSource;
            var host = _host?.State;
            CameraPopupVideoStatus status;
            if (_openFailed && host == CameraPopupHostState.Running) status = CameraPopupVideoStatus.Failed;
            else status = ComputeStatus(host, source is not null, source?.State, source?.StateDetail);
            if (!_videoStarted && status != CameraPopupVideoStatus.HostUnavailable) status = CameraPopupVideoStatus.Idle;
            VideoStatus = status;
            IsResolvingSource = status == CameraPopupVideoStatus.Connecting && source?.State == StreamState.Opening
                && source.StateDetail == "resolving";
        }
        catch { /* 상태 표시는 GIS 를 흔들지 않는다 */ }
    }

    public double CanvasLeft { get => _canvasLeft; set { _canvasLeft = value; NotifyOfPropertyChange(nameof(CanvasLeft)); RecomputeLine(); } }
    // FR-A1: 세터 클램프 금지 — 맵 팬/줌 추종(RefreshCameraPopupPositions)이 이 세터를 경유하므로 여기서
    // 하한을 물면 앵커가 화면 위로 나갈 때 팝업이 상단에 붙어 "딸려오는" 버그가 된다(CanvasLeft와 비대칭).
    // 타이틀바 침범 방지는 드래그 경로(CameraStreamPopupControl.OnMouseMove)의 경계 클램프가 전담한다.
    public double CanvasTop { get => _canvasTop; set { _canvasTop = value; NotifyOfPropertyChange(nameof(CanvasTop)); RecomputeLine(); } }
    public double PopupWidth { get => _popupWidth; set { _popupWidth = value; NotifyOfPropertyChange(nameof(PopupWidth)); RecomputeLine(); } }
    public double PopupHeight { get => _popupHeight; set { _popupHeight = value; NotifyOfPropertyChange(nameof(PopupHeight)); NotifyOfPropertyChange(nameof(ControlHeight)); RecomputeLine(); } }

    /// <summary>컨트롤 패널(아코디언) 높이 — 펼치면 팝업이 이만큼 아래로 커져 영상이 가려지지 않음. (PTZ 탭 줌·포커스 +/- 버튼 2행 추가로 188→230)</summary>
    public const double PanelHeight = 230;
    /// <summary>드래그 시 팝업 상단 하한(PropertyPanelCanvas 기준 0=맵 영역 상단) — 헤더 드래그가 상단 툴바/
    /// MahApps 윈도우 타이틀바를 침범하지 않게 <see cref="GMapControls.CameraStreamPopupControl"/>의 드래그 경계
    /// 클램프에서만 사용(FR-A2/OQ-1b). 맵 팬/줌 추종엔 적용하지 않는다(세터 클램프 시 상단 딸려옴 버그 — FR-A1).</summary>
    public const double MinCanvasTop = 0;
    /// <summary>컨트롤 실제 높이 = 영상 높이(PopupHeight) + 패널(펼침 시). MapView Height에 바인딩. (FR-UI-01)</summary>
    public double ControlHeight => _popupHeight + (_isPanelExpanded ? PanelHeight : 0);

    private int _zIndex;
    /// <summary>팝업 z-order(선택/오픈 시 최상위). Panel.ZIndex 바인딩 — 컬렉션 Move 대신 사용해 RTSP 컨테이너 재생성(영상 끊김) 방지. (FR-SEL-02)</summary>
    public int ZIndex { get => _zIndex; set { if (_zIndex == value) return; _zIndex = value; NotifyOfPropertyChange(nameof(ZIndex)); } }

    // ── 연결선(Leader Line): 카메라 심볼 중점 → 팝업 경계 (빨간 점선) ──────────
    /// <summary>카메라 심볼 중점의 위경도(팬/줌 시 화면점 재계산용). MapViewModel이 설정.</summary>
    public PointLatLng CameraGeo { get; set; }

    /// <summary>카메라 심볼 중점의 화면(Canvas) 좌표 = 연결선 끝점1. MapViewModel이 팬/줌 시 갱신.</summary>
    public double CameraScreenX { get => _cameraScreenX; set { _cameraScreenX = value; RecomputeLine(); } }
    public double CameraScreenY { get => _cameraScreenY; set { _cameraScreenY = value; RecomputeLine(); } }

    public double LineX1 { get => _lineX1; private set { _lineX1 = value; NotifyOfPropertyChange(nameof(LineX1)); } }
    public double LineY1 { get => _lineY1; private set { _lineY1 = value; NotifyOfPropertyChange(nameof(LineY1)); } }
    public double LineX2 { get => _lineX2; private set { _lineX2 = value; NotifyOfPropertyChange(nameof(LineX2)); } }
    public double LineY2 { get => _lineY2; private set { _lineY2 = value; NotifyOfPropertyChange(nameof(LineY2)); } }

    /// <summary>끝점1=카메라 중점, 끝점2=카메라→팝업중심 선분이 팝업 사각형 경계와 만나는 점(좌/우/상/하 자동).</summary>
    private void RecomputeLine()
    {
        var cx = _canvasLeft + _popupWidth / 2;
        var cy = _canvasTop + _popupHeight / 2;
        var dx = _cameraScreenX - cx;
        var dy = _cameraScreenY - cy;

        LineX1 = _cameraScreenX;
        LineY1 = _cameraScreenY;

        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) { LineX2 = cx; LineY2 = cy; return; }

        var scaleX = Math.Abs(dx) > 1e-6 ? (_popupWidth / 2) / Math.Abs(dx) : double.PositiveInfinity;
        var scaleY = Math.Abs(dy) > 1e-6 ? (_popupHeight / 2) / Math.Abs(dy) : double.PositiveInfinity;
        var scale = Math.Min(Math.Min(scaleX, scaleY), 1.0);   // 팝업 경계까지(카메라가 안쪽이면 중심)

        LineX2 = cx + dx * scale;
        LineY2 = cy + dy * scale;
    }

    /// <summary>"크게보기" 토글 상태(런타임만, 영속 안 함 — Q4).</summary>
    public bool IsLarge { get => _isLarge; private set { _isLarge = value; NotifyOfPropertyChange(nameof(IsLarge)); } }

    // ── PTZ 제어 상태(CameraPopup_PTZ_Control) ────────────────────────────────
    private bool _isSelected;
    private bool _isPtzCapable;
    private bool _isPtzLoading;
    private bool _isPanelExpanded;

    /// <summary>단일 선택 상태(MapViewModel.SelectedCameraPopup이 상호배타 설정). (FR-SEL-01)</summary>
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; NotifyOfPropertyChange(nameof(IsSelected)); } }

    /// <summary>PTZ 제어 가능 여부(MapViewModel이 호스트 PreparePtz 응답으로 설정). false면 우버튼 입력 차단. (FR-GATE-01)</summary>
    public bool IsPtzCapable { get => _isPtzCapable; set { if (_isPtzCapable == value) return; _isPtzCapable = value; NotifyOfPropertyChange(nameof(IsPtzCapable)); } }

    /// <summary>ONVIF PTZ 준비(InitializeFull+GetNode, 수 초 소요) 진행 중 — "PTZ 준비 중…" 배지 표시용. 끝나면 IsPtzCapable로 결정.</summary>
    public bool IsPtzLoading { get => _isPtzLoading; set { if (_isPtzLoading == value) return; _isPtzLoading = value; NotifyOfPropertyChange(nameof(IsPtzLoading)); } }

    // ── PTZ 속도(ContinuousMove 속도, 사용자 조절) — PTZ 탭 슬라이더/텍스트박스. [0.1, 1.0]. ────────────
    private double _panTiltSpeed = 0.6;   // 팬/틸트 ContinuousMove 속도(패드/드래그)
    private double _zoomSpeed = 0.6;       // 줌 ContinuousMove 속도(휠)

    /// <summary>패드/드래그 PanTilt 속도(ContinuousMove 속도 크기 [0.1,1.0]). MapViewModel이 ContinuousMove에 전달. 슬라이더+직접입력.</summary>
    public double PanTiltSpeed
    {
        get => _panTiltSpeed;
        set { var v = Math.Round(Math.Clamp(value, 0.1, 1.0), 1, MidpointRounding.AwayFromZero); if (Math.Abs(_panTiltSpeed - v) < 1e-6) return; _panTiltSpeed = v; NotifyOfPropertyChange(nameof(PanTiltSpeed)); }
    }

    /// <summary>휠 줌 속도(ContinuousMove 줌 속도 크기 [0.1,1.0]). MapViewModel이 ContinuousMove(zoomVel)에 전달. 슬라이더+직접입력.</summary>
    public double ZoomSpeed
    {
        get => _zoomSpeed;
        set { var v = Math.Round(Math.Clamp(value, 0.1, 1.0), 1, MidpointRounding.AwayFromZero); if (Math.Abs(_zoomSpeed - v) < 1e-6) return; _zoomSpeed = v; NotifyOfPropertyChange(nameof(ZoomSpeed)); }
    }

    /// <summary>하단 [PTZ][프리셋][옵션] 탭 패널 펼침 여부(우버튼 짧은클릭 토글). (FR-UI-01)</summary>
    public bool IsPanelExpanded { get => _isPanelExpanded; set { if (_isPanelExpanded == value) return; _isPanelExpanded = value; NotifyOfPropertyChange(nameof(IsPanelExpanded)); NotifyOfPropertyChange(nameof(ControlHeight)); } }

    /// <summary>컨트롤 패널(아코디언) 토글.</summary>
    internal void TogglePanel() => IsPanelExpanded = !IsPanelExpanded;

    private ICommand? _togglePanelCommand;
    /// <summary>헤더 옵션 버튼 → 컨트롤 패널(PTZ/프리셋/옵션) 펼침/접기.</summary>
    public ICommand TogglePanelCommand => _togglePanelCommand ??= new RelayCommand(() => TogglePanel());

    private int _activeTab;   // 0=PTZ, 1=프리셋, 2=옵션
    /// <summary>활성 탭(0=PTZ / 1=프리셋 / 2=옵션). (FR-UI-02)</summary>
    public int ActiveTab { get => _activeTab; set { if (_activeTab == value) return; _activeTab = value; NotifyOfPropertyChange(nameof(ActiveTab)); } }

    private ICommand? _selectTabCommand;
    public ICommand SelectTabCommand => _selectTabCommand ??= new RelayCommand(p =>
    {
        if (!int.TryParse(p?.ToString(), out var i)) return;
        ActiveTab = i;
        IsPanelExpanded = true;
        if (i == 1) RaisePresetsReload();   // 프리셋 탭 진입 시 DB 재조회
        else if (i == 2) RaiseOptionsReload();   // 옵션 탭 진입 시 영상 옵션 조회
    });

    /// <summary>PTZ 탭 방향 버튼(8방향) → MapViewModel이 호스트로 연속 이동. (FR-UI-02)</summary>
    public event EventHandler<PtzNudgeEventArgs>? PtzNudgeRequested;
    /// <summary>PTZ 정지 버튼.</summary>
    public event EventHandler? PtzStopRequested;

    private ICommand? _nudgeCommand;
    public ICommand NudgeCommand => _nudgeCommand ??= new RelayCommand(p => RaiseNudge(p?.ToString()));

    private ICommand? _stopCommand;
    public ICommand StopCommand => _stopCommand ??= new RelayCommand(() => PtzStopRequested?.Invoke(this, EventArgs.Empty));

    /// <summary>방향 패드 버튼 누름 → 해당 방향 연속 이동 시작(컨트롤이 PreviewMouseDown에서 호출). dx/dy ∈ {-1,0,1}.</summary>
    internal void RaisePadPress(int dx, int dy) => PtzNudgeRequested?.Invoke(this, new PtzNudgeEventArgs(dx, dy));
    /// <summary>방향 패드 버튼 뗌/정지 → 이동 중지(컨트롤이 PreviewMouseUp에서 호출).</summary>
    internal void RaisePtzStop() => PtzStopRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseNudge(string? dir)
    {
        var (dx, dy) = dir switch
        {
            "UL" => (-1d, -1d), "U" => (0d, -1d), "UR" => (1d, -1d),
            "L" => (-1d, 0d), "R" => (1d, 0d),
            "DL" => (-1d, 1d), "D" => (0d, 1d), "DR" => (1d, 1d),
            _ => (0d, 0d)
        };
        if (dx != 0 || dy != 0) PtzNudgeRequested?.Invoke(this, new PtzNudgeEventArgs(dx, dy));
    }

    // ── 프리셋 탭(ONVIF — 카메라 저장 프리셋, FR-C2) ─────────────────────────
    private readonly BindableCollection<IPtzPresetModel> _presets = new();
    /// <summary>카메라 프리셋 목록(MapViewModel이 ONVIF GetPresets 결과를 어댑터로 주입). (FR-C2)</summary>
    public BindableCollection<IPtzPresetModel> Presets => _presets;

    private string _presetStatusText = "등록된 프리셋이 없습니다";
    /// <summary>프리셋 탭 빈 목록 시 상태 문구(FR-C3) — "조회 중/미지원/없음/조회 실패"를 구분해 무음 빈 목록을 없앤다.</summary>
    public string PresetStatusText { get => _presetStatusText; set { if (_presetStatusText == value) return; _presetStatusText = value; NotifyOfPropertyChange(nameof(PresetStatusText)); } }

    /// <summary>MapViewModel이 프리셋 로드 결과를 주입(UI 스레드). 빈 목록일 때 표시할 사유를 함께 주입(FR-C3).</summary>
    internal void SetPresets(IEnumerable<IPtzPresetModel> presets, string? statusWhenEmpty = null)
    {
        _presets.Clear();
        _presets.AddRange(presets);
        if (statusWhenEmpty != null) PresetStatusText = statusWhenEmpty;
    }

    public event EventHandler? PresetsReloadRequested;       // 탭 진입/변경 후 재조회 요청
    public event EventHandler<IPtzPresetModel>? PresetGotoRequested;
    public event EventHandler<string>? PresetSaveRequested;  // 인라인 이름 확정
    public event EventHandler<IPtzPresetModel>? PresetDeleteRequested;
    /// <summary>[Home 지정] — 현재 위치를 카메라 Home으로(ONVIF SetHomePosition). per-preset Home 개념 폐기(OQ-6). (FR-C2)</summary>
    public event EventHandler? PresetHomeSetRequested;
    /// <summary>[Home 이동] — 카메라 Home 위치로(ONVIF GotoHomePosition). (FR-C2)</summary>
    public event EventHandler? PresetHomeGotoRequested;

    internal void RaisePresetsReload() => PresetsReloadRequested?.Invoke(this, EventArgs.Empty);

    private bool _isSavingPreset;
    /// <summary>프리셋 저장 인라인 이름 입력 표시 여부. (FR-PRESET-03)</summary>
    public bool IsSavingPreset { get => _isSavingPreset; set { if (_isSavingPreset == value) return; _isSavingPreset = value; NotifyOfPropertyChange(nameof(IsSavingPreset)); } }

    private bool _isLoadingPresets;
    /// <summary>프리셋 ONVIF 조회 진행 중 여부(FR-C3) — 조회 동안 스피너 표시·빈목록 문구 숨김(빈 목록 오해 방지). MapViewModel.LoadPresetsAsync가 토글.</summary>
    public bool IsLoadingPresets { get => _isLoadingPresets; set { if (_isLoadingPresets == value) return; _isLoadingPresets = value; NotifyOfPropertyChange(nameof(IsLoadingPresets)); } }

    private string _newPresetName = string.Empty;
    public string NewPresetName { get => _newPresetName; set { _newPresetName = value; NotifyOfPropertyChange(nameof(NewPresetName)); } }

    private ICommand? _gotoPresetCommand, _deletePresetCommand, _setHomeCommand, _gotoHomeCommand;
    private ICommand? _savePresetCommand, _confirmSaveCommand, _cancelSaveCommand;

    public ICommand GotoPresetCommand => _gotoPresetCommand ??= new RelayCommand(p => { if (p is IPtzPresetModel m) PresetGotoRequested?.Invoke(this, m); });
    public ICommand DeletePresetCommand => _deletePresetCommand ??= new RelayCommand(p => { if (p is IPtzPresetModel m) PresetDeleteRequested?.Invoke(this, m); });
    /// <summary>[Home 지정] — 현재 위치를 카메라 Home으로. 행 파라미터 불필요(ONVIF Home=전용 슬롯). (FR-C2/OQ-6)</summary>
    public ICommand SetHomeCommand => _setHomeCommand ??= new RelayCommand(() => PresetHomeSetRequested?.Invoke(this, EventArgs.Empty));
    /// <summary>[Home 이동] — 카메라 Home 위치로(미지정 카메라는 무동작 — MapViewModel이 로그). (FR-C2/OQ-6)</summary>
    public ICommand GotoHomeCommand => _gotoHomeCommand ??= new RelayCommand(() => PresetHomeGotoRequested?.Invoke(this, EventArgs.Empty));

    /// <summary>[현재위치 저장] → 인라인 이름 입력 시작.</summary>
    public ICommand SavePresetCommand => _savePresetCommand ??= new RelayCommand(() => { NewPresetName = $"Preset_{_presets.Count + 1}"; IsSavingPreset = true; });
    public ICommand ConfirmSavePresetCommand => _confirmSaveCommand ??= new RelayCommand(() =>
    {
        var name = NewPresetName?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        PresetSaveRequested?.Invoke(this, name);
        IsSavingPreset = false;
    });
    public ICommand CancelSavePresetCommand => _cancelSaveCommand ??= new RelayCommand(() => IsSavingPreset = false);

    // ── 옵션 탭(영상: 주야간/포커스) ──────────────────────────────────────────
    private bool _isImagingCapable;
    /// <summary>영상 옵션 지원 여부(미지원이면 안내). (FR-OPT-03)</summary>
    public bool IsImagingCapable { get => _isImagingCapable; set { if (_isImagingCapable == value) return; _isImagingCapable = value; NotifyOfPropertyChange(nameof(IsImagingCapable)); } }

    private string _irCutFilterMode = "AUTO";
    /// <summary>주야간(IrCutFilter): "ON"=주간 / "OFF"=야간 / "AUTO". (FR-OPT-01)</summary>
    public string IrCutFilterMode { get => _irCutFilterMode; set { if (_irCutFilterMode == value) return; _irCutFilterMode = value; NotifyOfPropertyChange(nameof(IrCutFilterMode)); } }

    private bool _isAutoFocus = true;
    /// <summary>오토포커스 여부(false=수동). (FR-OPT-02)</summary>
    public bool IsAutoFocus { get => _isAutoFocus; set { if (_isAutoFocus == value) return; _isAutoFocus = value; NotifyOfPropertyChange(nameof(IsAutoFocus)); } }

    /// <summary>MapViewModel이 ONVIF 조회 결과를 주입(UI 스레드).</summary>
    internal void SetImagingState(string irCutFilter, bool autoFocus)
    {
        IrCutFilterMode = string.IsNullOrEmpty(irCutFilter) ? "AUTO" : irCutFilter.ToUpperInvariant();
        IsAutoFocus = autoFocus;
    }

    public event EventHandler? OptionsReloadRequested;
    public event EventHandler<string>? IrCutFilterRequested;
    public event EventHandler<bool>? AutoFocusRequested;

    internal void RaiseOptionsReload() => OptionsReloadRequested?.Invoke(this, EventArgs.Empty);

    private ICommand? _setIrCutFilterCommand, _setAutoFocusCommand;
    public ICommand SetIrCutFilterCommand => _setIrCutFilterCommand ??= new RelayCommand(p => { var m = p?.ToString(); if (!string.IsNullOrEmpty(m)) IrCutFilterRequested?.Invoke(this, m); });
    public ICommand SetAutoFocusCommand => _setAutoFocusCommand ??= new RelayCommand(p => { if (bool.TryParse(p?.ToString(), out var b)) AutoFocusRequested?.Invoke(this, b); });

    public ICommand CloseCommand =>
        _closeCommand ??= new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

    public ICommand ToggleSizeCommand =>
        _toggleSizeCommand ??= new RelayCommand(ToggleSize);

    private void ToggleSize()
    {
        IsLarge = !IsLarge;
        PopupWidth = IsLarge ? LargeWidth : DefaultWidth;
        PopupHeight = IsLarge ? LargeHeight : DefaultHeight;
    }

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;   // 멱등(타이머 Tick + 수동 Close 동시 진입 방어)
        _disposed = true;

        // 영상 닫기 — 공유 메모리 해제 + 호스트에 CloseStream(감시자가 복원 목록에서도 뺀다). 던지지 않는다.
        try { _lifetime.Cancel(); } catch (ObjectDisposedException) { }
        if (_host is not null) _host.StateChanged -= OnHostStateChanged;
        var source = _frameSource;
        if (source is not null)
        {
            source.StateChanged -= OnFrameSourceStateChanged;
            try { source.Dispose(); } catch { /* 종료 경로 — 무해 */ }
        }
        FrameSource = null;
        CloseRequested = null;
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>우버튼 드래그-PTZ 완료 이벤트 인자 — 픽셀 델타 + 영상 영역 치수(정규화·변환 기준).</summary>
public sealed class PtzDragEventArgs : EventArgs
{
    public PtzDragEventArgs(double dx, double dy, double imageW, double imageH)
    {
        Dx = dx; Dy = dy; ImageWidth = imageW; ImageHeight = imageH;
    }

    public double Dx { get; }
    public double Dy { get; }
    public double ImageWidth { get; }
    public double ImageHeight { get; }
}

/// <summary>PTZ 탭 방향 패드 nudge 인자 — 방향 단위벡터(-1/0/1).</summary>
public sealed class PtzNudgeEventArgs : EventArgs
{
    public PtzNudgeEventArgs(double dx, double dy) { Dx = dx; Dy = dy; }
    public double Dx { get; }
    public double Dy { get; }
}

/// <summary>
/// ONVIF 프리셋 표시 어댑터(FR-C2) — 기존 프리셋 탭 XAML 바인딩(PresetName)과 이벤트 시그니처(IPtzPresetModel)를
/// 유지한 채 소스만 로컬 DB→카메라(ONVIF)로 교체하기 위한 경량 아이템. 좌표(Pan/Tilt/Zoom)는 카메라 내부
/// 상태라 미보유(0) — 이동/삭제는 <see cref="Token"/>(GotoPreset/RemovePreset)으로 수행한다.
/// IsHome은 ONVIF에 per-preset 개념이 없어 항상 false(Home은 SetHome/GotoHome 전용 슬롯 — OQ-6).
/// </summary>
public sealed class OnvifPresetDisplayModel : IPtzPresetModel
{
    public OnvifPresetDisplayModel(int cameraId, string token, string? name)
    {
        CameraId = cameraId;
        Token = token;
        PresetName = string.IsNullOrWhiteSpace(name) ? $"프리셋 {token}" : name!;   // 빈 이름 폴백(FR-C2)
    }

    /// <summary>ONVIF 프리셋 토큰 — 이동(GotoPreset)/삭제(RemovePreset) 키.</summary>
    public string Token { get; }

    public int Id { get; set; }
    public int CameraId { get; set; }
    public string PresetName { get; set; }
    public bool IsHome { get; set; }
    public double Pan { get; set; }
    public double Tilt { get; set; }
    public double Zoom { get; set; }
    public string? PanTiltSpace { get; set; }
    public string? ZoomSpace { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
