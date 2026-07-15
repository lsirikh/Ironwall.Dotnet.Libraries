using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Commands;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.Serivces;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;
using LibVLCSharp.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.Streaming.Controls;

/// <summary>
/// 개선된 RTSP 스트림 재생 커스텀 컨트롤 (수동적 View)
/// </summary>
[TemplatePart(Name = PART_VideoView, Type = typeof(VideoView))]
[TemplatePart(Name = PART_LoadingIndicator, Type = typeof(ProgressBar))]
[TemplatePart(Name = PART_ErrorPanel, Type = typeof(StackPanel))]
[TemplatePart(Name = PART_StatusText, Type = typeof(TextBlock))]
public class ImprovedRtspPlayer : Control, IDisposable
{
    private const string PART_VideoView = "PART_VideoView";
    private const string PART_LoadingIndicator = "PART_LoadingIndicator";
    private const string PART_ErrorPanel = "PART_ErrorPanel";
    private const string PART_StatusText = "PART_StatusText";
    private const string PART_SnapshotFlash = "PART_SnapshotFlash";
    private const string PART_SnapshotOsd = "PART_SnapshotOsd";

    private VideoView? _videoView;
    private ProgressBar? _loadingIndicator;
    private StackPanel? _errorPanel;
    private TextBlock? _statusText;
    private FrameworkElement? _snapshotFlash;
    private FrameworkElement? _snapshotOsd;

    // Service references
    private IImprovedRtspStreamingService? _streamingService;
    private IPlayerRegistry? _playerRegistry;
    private ILogService? _log;

    private bool _disposed;
    private bool _isRegistered;
    private readonly object _disposeLock = new object();
    private string? _cachedContextId;

    static ImprovedRtspPlayer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ImprovedRtspPlayer),
            new FrameworkPropertyMetadata(typeof(ImprovedRtspPlayer)));
    }

    public ImprovedRtspPlayer()
    {
        // 기본 ContextId 생성
        _cachedContextId = Guid.NewGuid().ToString();

        // DI Container에서 서비스 획득
        try
        {
            _streamingService = IoC.Get<IImprovedRtspStreamingService>();
            _playerRegistry = _streamingService as IPlayerRegistry;
            _log = IoC.Get<ILogService>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspPlayer] Failed to get services: {ex.Message}");
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        //_log?.Info($"[ImprovedRtspPlayer] Control created with ID: {_cachedContextId}");
    }

    #region - Dependency Properties -
    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(
            nameof(DisplayName), 
            typeof(string), 
            typeof(ImprovedRtspPlayer), 
            new PropertyMetadata(null));

    public string DisplayName
    {
        get { return (string)GetValue(DisplayNameProperty); }
        set { SetValue(DisplayNameProperty, value); }
    }

    public static readonly DependencyProperty ContextIdProperty =
        DependencyProperty.Register(
            nameof(ContextId),
            typeof(string),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(null, OnContextIdChanged));

    public string ContextId
    {
        get => (string)GetValue(ContextIdProperty);
        set => SetValue(ContextIdProperty, value);
    }

    public static readonly DependencyProperty ConnectionInfoProperty =
        DependencyProperty.Register(
            nameof(ConnectionInfo),
            typeof(RtspConnectionInfo),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(null, OnConnectionInfoChanged));

    public RtspConnectionInfo ConnectionInfo
    {
        get => (RtspConnectionInfo)GetValue(ConnectionInfoProperty);
        set => SetValue(ConnectionInfoProperty, value);
    }

    public static readonly DependencyProperty StreamingOptionsProperty =
        DependencyProperty.Register(
            nameof(StreamingOptions),
            typeof(StreamingOptions),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(null));

    public StreamingOptions StreamingOptions
    {
        get => (StreamingOptions)GetValue(StreamingOptionsProperty) ?? new StreamingOptions();
        set => SetValue(StreamingOptionsProperty, value);
    }

    public static readonly DependencyProperty PlaybackStateProperty =
        DependencyProperty.Register(
            nameof(PlaybackState),
            typeof(PlaybackState),
            typeof(ImprovedRtspPlayer),
            new FrameworkPropertyMetadata(
                PlaybackState.None,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPlaybackStateChanged));

    public PlaybackState PlaybackState
    {
        get => (PlaybackState)GetValue(PlaybackStateProperty);
        set => SetValue(PlaybackStateProperty, value);
    }

    public static readonly DependencyProperty AutoPlayProperty =
        DependencyProperty.Register(
            nameof(AutoPlay),
            typeof(bool),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(true));

    public bool AutoPlay
    {
        get => (bool)GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    public static readonly DependencyProperty ShowControlsProperty =
        DependencyProperty.Register(
            nameof(ShowControls),
            typeof(bool),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(true));

    public bool ShowControls
    {
        get => (bool)GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register(
            nameof(Volume),
            typeof(int),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(50, OnVolumeChanged, CoerceVolume));

    public int Volume
    {
        get => (int)GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly DependencyProperty IsMutedProperty =
        DependencyProperty.Register(
            nameof(IsMuted),
            typeof(bool),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(false, OnIsMutedChanged));

    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(
            nameof(StatusMessage),
            typeof(string),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(string.Empty));

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    // 타임아웃 표시용 DependencyProperty 추가
    public static readonly DependencyProperty TimeoutDisplayProperty =
        DependencyProperty.Register(
            nameof(TimeoutDisplay),
            typeof(string),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(string.Empty));

    public string TimeoutDisplay
    {
        get => (string)GetValue(TimeoutDisplayProperty);
        set => SetValue(TimeoutDisplayProperty, value);
    }

    public static readonly DependencyProperty SharedFrameProperty =
        DependencyProperty.Register(
            nameof(SharedFrame),
            typeof(System.Windows.Media.Imaging.BitmapSource),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(null));

    /// <summary>Hub SharedFrameBuffer의 공유 BitmapSource. Hub 모드에서 Image.Source에 바인딩된다.</summary>
    public System.Windows.Media.Imaging.BitmapSource? SharedFrame
    {
        get => (System.Windows.Media.Imaging.BitmapSource?)GetValue(SharedFrameProperty);
        set => SetValue(SharedFrameProperty, value);
    }

    public static readonly DependencyProperty IsHubModeProperty =
        DependencyProperty.Register(
            nameof(IsHubMode),
            typeof(bool),
            typeof(ImprovedRtspPlayer),
            new PropertyMetadata(false, OnIsHubModeChanged));

    /// <summary>true이면 VideoView 대신 SharedFrame Image를 표시한다.</summary>
    public bool IsHubMode
    {
        get => (bool)GetValue(IsHubModeProperty);
        set => SetValue(IsHubModeProperty, value);
    }

    #endregion

    #region - Commands -

    private ICommand? _playCommand;
    public ICommand PlayCommand =>
        _playCommand ??= new AsyncRelayCommand(PlayAsync, CanPlay);

    private ICommand? _stopCommand;
    public ICommand StopCommand =>
        _stopCommand ??= new AsyncRelayCommand(StopAsync, CanStop);

    private ICommand? _pauseCommand;
    public ICommand PauseCommand =>
        _pauseCommand ??= new RelayCommand(Pause, () => PlaybackState == PlaybackState.Playing);

    private ICommand? _resumeCommand;
    public ICommand ResumeCommand =>
        _resumeCommand ??= new RelayCommand(Resume, () => PlaybackState == PlaybackState.Paused);

    private ICommand? _snapshotCommand;
    public ICommand SnapshotCommand =>
        _snapshotCommand ??= new AsyncRelayCommand(() => TakeSnapshotAsync(), () => PlaybackState == PlaybackState.Playing);

    #endregion

    #region - Events -

    public event EventHandler<StreamingStateChangedEventArgs>? StateChanged;
    public event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<StreamingProgressEventArgs>? ProgressUpdated;

    #endregion

    #region - Public Methods -

    /// <summary>
    /// VideoView 가져오기 (서비스에서 사용)
    /// </summary>
    public VideoView? GetVideoView()
    {
        return _videoView;
    }

    #endregion

    #region - Overrides -

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _videoView = GetTemplateChild(PART_VideoView) as VideoView;
        _loadingIndicator = GetTemplateChild(PART_LoadingIndicator) as ProgressBar;
        _errorPanel = GetTemplateChild(PART_ErrorPanel) as StackPanel;
        _statusText = GetTemplateChild(PART_StatusText) as TextBlock;
        _snapshotFlash = GetTemplateChild(PART_SnapshotFlash) as FrameworkElement;
        _snapshotOsd = GetTemplateChild(PART_SnapshotOsd) as FrameworkElement;

        // OnApplyTemplate 시점의 IsHubMode 값을 VideoView에 즉시 반영
        // (OnIsHubModeChanged 콜백이 _videoView 초기화 전 호출된 경우 대비)
        if (_videoView != null)
            _videoView.Visibility = IsHubMode ? Visibility.Collapsed : Visibility.Visible;
    }

    #endregion

    #region - Private Methods -

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // XAML에서 설정된 ContextId가 있다면 캐시 업데이트
        if (!string.IsNullOrEmpty(ContextId) && ContextId != _cachedContextId)
        {
            _cachedContextId = ContextId;
        }

        // Player Registry에 등록
        RegisterToService();

        // AutoPlay 처리 - 모든 바인딩 완료 후 여기서!
        // ConnectionInfo가 아직 없으면(Onvif조회 모드 late-bind) OnConnectionInfoChanged가 이어받는다.
        if (AutoPlay && ConnectionInfo != null)
            StartAutoConnect();
    }

    /// <summary>
    /// AutoPlay 연결 시작(Hub/직결 분기). 호출 경로 2곳 — ① OnLoaded(오픈 시 ConnectionInfo 보유, 현행)
    /// ② OnConnectionInfoChanged(Loaded 이후 late-bind — Onvif조회 모드가 URL 확보 후 주입, FR-05).
    /// WPF 렌더 파이프라인 지연 후에도 Row 취소 여부를 재확인.
    /// 재진입 방어(M-4)는 호출측이 담당 — OnLoaded는 로드당 1회, DP change는 PlaybackState 미연결 가드 +
    /// 주입 경로 단일(Onvif 조회 1회 + VM 세터 멱등). 새 호출자를 추가하면 이 가정을 재검토할 것.
    /// </summary>
    private void StartAutoConnect()
    {
        if (ConnectionInfo == null) return;
        if (DataContext is CameraViewModel vm)
        {
            if (vm.IsConnectCancelled)
            {
                _log?.Info($"[ImprovedRtspPlayer] Skipping connect — row cancelled: {_cachedContextId}");
                return;
            }
            // Hub 경로: relay URL 경유 재생 (sout 재생성 없음)
            if (vm.IsHubManaged)
            {
                // AllowsTransparency 창에서 VideoView HWND가 airspace hole을 만들지 않도록
                // ConnectViaHubAsync 시작 전 동기로 IsHubMode=true 설정.
                // 이렇게 하면 OnApplyTemplate 이후 VideoView가 로드되어도 즉시 Collapsed.
                IsHubMode = true;
                _ = ConnectViaHubAsync(vm);
            }
            else
                _ = ConnectAsync();
        }
        else
        {
            _ = ConnectAsync();
        }
    }

    /// <summary>
    /// ConnectionInfo가 Loaded 이후 뒤늦게 설정되면(Onvif조회 모드 late-bind) 여기서 연결을 시작한다.
    /// 오픈 시 이미 값이 있는 경우 DP는 Loaded 전에 바인딩되므로 !IsLoaded로 스킵 — OnLoaded 경로가
    /// 담당(이중 연결 없음). 이미 연결(중) 상태면 무시(최초 연결 전용). (FR-05)
    /// </summary>
    private static void OnConnectionInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ImprovedRtspPlayer player || e.NewValue is not RtspConnectionInfo) return;
        if (!player.IsLoaded || !player.AutoPlay || player._disposed) return;
        if (player.PlaybackState is not (PlaybackState.None or PlaybackState.Stopped
            or PlaybackState.Disconnected or PlaybackState.Error)) return;
        player.StartAutoConnect();
    }

    private async void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _log?.Info($"[ImprovedRtspPlayer] Unloading: {_cachedContextId}");

        // Registry에서 제거
        UnregisterFromService();

        // 이벤트 핸들러 해제
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        // 연결 해제
        await DisconnectAsync();
    }

    private void RegisterToService()
    {
        if (!_isRegistered && _playerRegistry != null && !string.IsNullOrEmpty(_cachedContextId))
        {
            _isRegistered = _playerRegistry.RegisterPlayer(_cachedContextId, this);
            //_log?.Info($"[ImprovedRtspPlayer] Registered to service: {_cachedContextId}, Success: {_isRegistered}");
        }
    }

    private void UnregisterFromService()
    {
        if (_isRegistered && _playerRegistry != null && !string.IsNullOrEmpty(_cachedContextId))
        {
            _playerRegistry.UnregisterPlayer(_cachedContextId);
            _isRegistered = false;
            //_log?.Info($"[ImprovedRtspPlayer] Unregistered from service: {_cachedContextId}");
        }
    }

    private async Task ConnectAsync()
    {
        if (_streamingService == null || ConnectionInfo == null)
        {
            _log?.Warning("[ImprovedRtspPlayer] Cannot connect: Service or ConnectionInfo is null");
            return;
        }

        // DataContext의 Row CancellationToken 사용 (Row 취소 시 연결 중단)
        var ct = DataContext is CameraViewModel vm ? vm.RowCancellationToken : default;

        await _streamingService.ConnectAsync(
            _cachedContextId,
            ConnectionInfo,
            StreamingOptions,
            null,
            ct);
    }

    private async Task ConnectViaHubAsync(CameraViewModel vm)
    {
        if (ConnectionInfo == null) return;

        var ct = vm.RowCancellationToken;
        // Cache DP values on UI thread before any ConfigureAwait(false) await
        var connInfo = ConnectionInfo;
        var streamOpts = StreamingOptions;
        var cameraId = connInfo.GetCameraKey();

        _log?.Info($"[Player][HubPath] START  contextId={_cachedContextId}  src={connInfo.GetFullUrl()}");

        await Dispatcher.InvokeAsync(() =>
        {
            PlaybackState = PlaybackState.Connecting;
            IsHubMode = true;
        });

        try
        {
            // 1. Hub Lease 획득 — Entry가 없으면 VLC MediaPlayer 생성 + Playing 대기
            _log?.Info($"[Player][HubPath] AcquireAsync  camera={connInfo.IpAddress}:{connInfo.Port}/{connInfo.StreamPath}");
            await vm.OwnerRow!.StartStreamAsync(connInfo, ct).ConfigureAwait(false);

            // 2. 공유 BitmapSource 획득 후 UI에 바인딩 (ConnectAsync 직접 호출 금지 — H-01)
            // Playing 이벤트 직후 OnVideoFormat이 아직 호출 안 됐을 수 있으므로 최대 2s 재시도
            var frame = vm.OwnerRow.GetLeaseFrame(cameraId);
            if (frame == null)
            {
                for (int i = 0; i < 15 && !ct.IsCancellationRequested; i++)
                {
                    await Task.Delay(200, ct).ConfigureAwait(false);
                    frame = vm.OwnerRow.GetLeaseFrame(cameraId);
                    if (frame != null) break;
                }
                if (frame != null)
                    _log?.Info($"[Player][HubPath] Frame arrived after retry  cameraId={cameraId}");
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (frame != null)
                {
                    SharedFrame = frame;
                    PlaybackState = PlaybackState.Playing;
                    _log?.Info($"[Player][HubPath] Frame bound  cameraId={cameraId}  contextId={_cachedContextId}");
                }
                else
                {
                    // 메타데이터 전용 엔트리(테스트용)거나 VLC 초기화 중 — 레거시 경로로 폴백
                    _log?.Info($"[Player][HubPath] No frame available, falling back to direct connect  contextId={_cachedContextId}");
                    IsHubMode = false;
                    PlaybackState = PlaybackState.None;
                }
            });

            // Hub 경로 타임아웃 등록 (IsAutoDiscard=false면 CheckTimeouts가 무시)
            if (frame != null)
                _streamingService?.RegisterConnectionStartTime(_cachedContextId);

            // VLC는 연결 직후 OnVideoFormat을 두 번 호출하는 경우가 있다 (첫 번째: 예비 해상도, 두 번째: 실제 해상도).
            // 두 번째 호출 시 새 WriteableBitmap이 생성되어 SharedFrame이 구버전을 가리키는 문제를 방지.
            // 800ms 후 최신 Frame으로 재바인딩한다.
            if (frame != null)
            {
                try
                {
                    await Task.Delay(800, ct).ConfigureAwait(false);
                    var latestFrame = vm.OwnerRow.GetLeaseFrame(cameraId);
                    if (latestFrame != null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (IsHubMode && latestFrame != SharedFrame)
                            {
                                SharedFrame = latestFrame;
                                _log?.Info($"[Player][HubPath] WB replaced — SharedFrame rebind  cameraId={cameraId}");
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { /* Row 취소 — 정상 */ }
            }

            if (frame == null && _streamingService != null)
                await _streamingService.ConnectAsync(_cachedContextId, connInfo, streamOpts, null, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _log?.Info($"[Player][HubPath] Cancelled  contextId={_cachedContextId}");
            await Dispatcher.InvokeAsync(() => { IsHubMode = false; });
        }
        catch (Exception ex)
        {
            _log?.Error($"[Player][HubPath] Exception → fallback  contextId={_cachedContextId}  err={ex.Message}");
            await Dispatcher.InvokeAsync(() => { IsHubMode = false; });
            if (_streamingService != null)
                await ConnectAsync().ConfigureAwait(false);
        }
    }

    private async Task DisconnectAsync()
    {
        // Hub 모드: SharedFrame + IsHubMode 초기화만 수행 (Lease 해제는 CameraRowViewModel.DisposeAsync 담당)
        if (IsHubMode)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                SharedFrame = null;
                IsHubMode = false;
                PlaybackState = PlaybackState.Disconnected;
            });
            return;
        }

        if (_streamingService == null)
            return;

        await _streamingService.DisconnectAsync(_cachedContextId);
    }

    /// <summary>
    /// Timeout 만료 시 CheckTimeouts에서 호출 — Hub-mode 플레이어를 정리하고 Row 종료를 요청한다.
    /// 반드시 UI 스레드에서 실행되도록 Dispatcher.InvokeAsync를 사용한다.
    /// </summary>
    internal void RequestHubStop()
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            SharedFrame = null;
            IsHubMode = false;
            PlaybackState = PlaybackState.Stopped;

            // Row 종료 — CloseRowCommand는 CameraRowViewModel이 소유
            if (DataContext is CameraViewModel vm)
                vm.OwnerRow?.CloseRowCommand?.Execute(null);
        });
    }

    private async Task PlayAsync()
    {
        // 연결되지 않은 상태면 연결
        if (PlaybackState == PlaybackState.None ||
            PlaybackState == PlaybackState.Stopped ||
            PlaybackState == PlaybackState.Error ||
            PlaybackState == PlaybackState.Disconnected)
        {
            await ConnectAsync();
        }
        // Paused 상태면 재생 재개
        else if (PlaybackState == PlaybackState.Paused)
        {
            Resume();
        }
    }

    private async Task StopAsync()
    {
        await DisconnectAsync();
    }

    private void Pause()
    {
        _streamingService?.Pause(_cachedContextId);
    }

    private void Resume()
    {
        _streamingService?.Play(_cachedContextId);
    }

    private async Task<bool> TakeSnapshotAsync(string filePath = null)
    {
        try
        {
            if (_streamingService == null)
                return false;

            filePath ??= $"snapshot_{_cachedContextId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            // Hub 모드: 로컬 LibVLC MediaPlayer가 없으므로 표시 중인 공유 프레임(SharedFrame)을 직접 PNG로 저장.
            var ok = IsHubMode
                ? await SaveSharedFrameSnapshotAsync(filePath)
                : await _streamingService.TakeSnapshotAsync(_cachedContextId ?? throw new NullReferenceException($"CachedContextId가 설정되어있지 않습니다."), filePath);

            if (ok)
                await Dispatcher.InvokeAsync(PlaySnapshotFeedback);   // 흰 플래시 + 우상단 OSD
            return ok;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            return false;
        }
    }

    /// <summary>Hub 모드 스냅샷 — 표시 중인 공유 프레임(SharedFrame)의 동결 복사본을 PNG로 저장(로컬 MediaPlayer 없음 대응).</summary>
    private async Task<bool> SaveSharedFrameSnapshotAsync(string fileName)
    {
        // UI 스레드에서 현재 공유 프레임의 픽셀 복사본(동결) 생성 — 라이브 WriteableBitmap 갱신과 분리.
        var frozen = await Dispatcher.InvokeAsync(() =>
        {
            var f = SharedFrame;
            if (f == null) return (BitmapSource?)null;
            var copy = new WriteableBitmap(f);
            copy.Freeze();
            return (BitmapSource)copy;
        });
        if (frozen == null)
        {
            _log?.Warning("[ImprovedRtspPlayer] Hub 스냅샷 실패 — 공유 프레임 없음");
            return false;
        }

        var fullPath = _streamingService!.ResolveSnapshotPath(fileName);
        await Task.Run(() =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frozen));
            using var fs = System.IO.File.Create(fullPath);
            encoder.Save(fs);
        });
        _log?.Info($"[ImprovedRtspPlayer] Hub snapshot saved: {fullPath}");
        return true;
    }

    /// <summary>스냅샷 촬영 피드백: 흰 플래시 번쩍(250ms) + 우상단 OSD "스냅샷 저장"(1초 유지 후 사라짐).</summary>
    private void PlaySnapshotFeedback()
    {
        try
        {
            if (_snapshotFlash != null)
            {
                var flash = new DoubleAnimation
                {
                    From = 0.85,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(250),
                    FillBehavior = FillBehavior.Stop,
                };
                _snapshotFlash.BeginAnimation(UIElement.OpacityProperty, flash);
            }

            if (_snapshotOsd != null)
            {
                // 0→1(120ms) → 1초 유지 → 0(300ms). FillBehavior.Stop으로 종료 후 Opacity=0 복귀.
                var osd = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                osd.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
                osd.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1120))));
                osd.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1420))));
                _snapshotOsd.BeginAnimation(UIElement.OpacityProperty, osd);
            }
        }
        catch (Exception ex) { _log?.Error($"[ImprovedRtspPlayer] Snapshot feedback failed: {ex.Message}"); }
    }

    private bool CanPlay()
    {
        return !_disposed &&
               PlaybackState != PlaybackState.Playing &&
               PlaybackState != PlaybackState.Connecting &&
               PlaybackState != PlaybackState.Buffering &&
               PlaybackState != PlaybackState.Reconnecting;
    }

    private bool CanStop()
    {
        return !_disposed &&
               (PlaybackState == PlaybackState.Playing ||
                PlaybackState == PlaybackState.Paused ||
                PlaybackState == PlaybackState.Buffering);
    }

    private static void OnIsHubModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImprovedRtspPlayer player && e.NewValue is bool isHub && player._videoView != null)
        {
            // LibVLCSharp ForegroundWindow (sibling HWND)은 VideoView.Visibility 변경에 반응한다.
            // VideoContainer collapse만으로는 ForegroundWindow가 갱신되지 않을 수 있으므로
            // VideoView를 직접 숨겨 투명 airspace hole을 방지한다.
            player._videoView.Visibility = isHub ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnContextIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImprovedRtspPlayer player)
        {
            var newId = e.NewValue as string;
            var oldId = e.OldValue as string;

            // 기존 등록 해제
            if (!string.IsNullOrEmpty(oldId) && player._isRegistered)
            {
                player.UnregisterFromService();
            }

            // 캐시 업데이트
            player._cachedContextId = newId;

            // 새로 등록
            if (!string.IsNullOrEmpty(newId))
            {
                player.RegisterToService();
            }

            //player._log?.Info($"[ImprovedRtspPlayer] ContextId changed from '{oldId}' to '{newId}'");
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImprovedRtspPlayer player && e.NewValue is int volume)
        {
            player._streamingService?.SetVolume(player._cachedContextId, volume);
        }
    }

    private static object CoerceVolume(DependencyObject d, object value)
    {
        if (value is int volume)
        {
            return Math.Clamp(volume, 0, 100);
        }
        return 50;
    }

    private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImprovedRtspPlayer player)
        {
            player._streamingService?.ToggleMute(player._cachedContextId);
        }
    }

    private static void OnPlaybackStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImprovedRtspPlayer player && e.NewValue is PlaybackState state)
        {
            // UI 스레드 확인
            if (!player.Dispatcher.CheckAccess())
            {
                player.Dispatcher.BeginInvoke((System.Action)(() => player.UpdateUI(state)));
            }
            else
            {
                player.UpdateUI(state);
            }

        }
    }

    private void UpdateUI(PlaybackState state)
    {
        if (_disposed) return;

        Dispatcher.BeginInvoke(() =>
        {
            switch (state)
            {
                case PlaybackState.Connecting:
                case PlaybackState.Buffering:
                case PlaybackState.Reconnecting:
                    ShowLoading(true);
                    ShowError(false);
                    break;

                case PlaybackState.Playing:
                    ShowLoading(false);
                    ShowError(false);
                    break;

                case PlaybackState.Error:
                case PlaybackState.Disconnected:
                    ShowLoading(false);
                    ShowError(true);
                    break;

                default:
                    ShowLoading(false);
                    ShowError(false);
                    break;
            }

            CommandManager.InvalidateRequerySuggested();
        });
    }

    private void ShowLoading(bool show)
    {
        if (_loadingIndicator != null)
        {
            _loadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _loadingIndicator.IsIndeterminate = show;
        }
    }

    private void ShowError(bool show)
    {
        if (_errorPanel != null)
        {
            _errorPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    #endregion

    #region - IDisposable -

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    // Registry에서 제거
                    UnregisterFromService();

                    // VideoView 정리
                    if (_videoView != null)
                    {
                        _videoView.MediaPlayer = null;
                        _videoView = null;
                    }

                    //_log?.Info($"[ImprovedRtspPlayer] Disposed: {_cachedContextId}");
                }
                catch (Exception ex)
                {
                    _log?.Error($"[ImprovedRtspPlayer] Dispose error: {ex.Message}");
                }
            }

            _disposed = true;
        }
    }

    ~ImprovedRtspPlayer()
    {
        Dispose(false);
    }

    #endregion
}