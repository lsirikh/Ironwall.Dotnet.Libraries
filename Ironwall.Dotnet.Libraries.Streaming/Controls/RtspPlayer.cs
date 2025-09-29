using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Commands;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.Serivces;
using LibVLCSharp.WPF;
using log4net;
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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Streaming.Controls;
/// <summary>
/// RTSP 스트림 재생 커스텀 컨트롤
/// </summary>
[TemplatePart(Name = PART_VideoView, Type = typeof(VideoView))]
[TemplatePart(Name = PART_LoadingIndicator, Type = typeof(ProgressBar))]
[TemplatePart(Name = PART_ErrorPanel, Type = typeof(StackPanel))]
[TemplatePart(Name = PART_StatusText, Type = typeof(TextBlock))]
public class RtspPlayer : Control, IDisposable
{
    private const string PART_VideoView = "PART_VideoView";
    private const string PART_LoadingIndicator = "PART_LoadingIndicator";
    private const string PART_ErrorPanel = "PART_ErrorPanel";
    private const string PART_StatusText = "PART_StatusText";

    private VideoView? _videoView;
    private ProgressBar? _loadingIndicator;
    private StackPanel? _errorPanel;
    private TextBlock? _statusText;
    private IRtspStreamingService? _streamingService;
    private ILogService? _log;
    private DispatcherTimer? _statusTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private WeakReference<IRtspStreamingService>? _weakStreamingService;
    private bool _disposed;
    private readonly object _disposeLock = new object();

    private readonly object _lockObject = new object();
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

    private int _connectionRetryCount = 0;

    // ContextId 캐시 필드 추가
    private string? _cachedContextId;

    static RtspPlayer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RtspPlayer),
            new FrameworkPropertyMetadata(typeof(RtspPlayer)));
    }

    public RtspPlayer()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        // 기본 ContextId 생성 및 캐시
        var defaultId = Guid.NewGuid().ToString();
        _cachedContextId = defaultId;  // 먼저 캐시에 저장


        // DI Container에서 서비스 획득
        try
        {
            _streamingService = IoC.Get<IRtspStreamingService>();
            _log = IoC.Get<ILogService>();

            // WeakReference로 메모리 누수 방지
            _weakStreamingService = new WeakReference<IRtspStreamingService>(_streamingService);
        }
        catch (Exception ex)
        {
            // DI가 설정되지 않은 경우 (디자인 타임 등)
            _log?.Error($"[RtspPlayer] Failed to get services: {ex.Message}");
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // 캐시된 값 사용
        _log?.Info($"[RtspPlayer] Control created with default ID: {_cachedContextId}");

    }

    #region - Dependency Properties -
    // 새로운 DP 추가
    public static readonly DependencyProperty ContextIdProperty =
        DependencyProperty.Register(
            nameof(ContextId),
            typeof(string),
            typeof(RtspPlayer),
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
            typeof(RtspPlayer),
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
            typeof(RtspPlayer),
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
            typeof(RtspPlayer),
            new FrameworkPropertyMetadata(
                PlaybackState.None,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPlaybackStateChanged));

    public PlaybackState PlaybackState
    {
        get => (PlaybackState)GetValue(PlaybackStateProperty);
        private set => SetValue(PlaybackStateProperty, value);
    }

    public static readonly DependencyProperty AutoPlayProperty =
        DependencyProperty.Register(
            nameof(AutoPlay),
            typeof(bool),
            typeof(RtspPlayer),
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
            typeof(RtspPlayer),
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
            typeof(RtspPlayer),
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
            typeof(RtspPlayer),
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
            typeof(RtspPlayer),
            new PropertyMetadata(string.Empty));

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        private set => SetValue(StatusMessageProperty, value);
    }

    #endregion

    #region - Commands -

    private ICommand? _playCommand;
    public ICommand PlayCommand =>
    _playCommand ??= new AsyncRelayCommand(PlayOrConnectAsync, CanPlayOrConnect);

    private ICommand? _stopCommand;
    public ICommand StopCommand =>
    _stopCommand ??= new AsyncRelayCommand(DisconnectAsync, CanDisconnect);

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

    #region - Overrides -

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _videoView = GetTemplateChild(PART_VideoView) as VideoView;
        _loadingIndicator = GetTemplateChild(PART_LoadingIndicator) as ProgressBar;
        _errorPanel = GetTemplateChild(PART_ErrorPanel) as StackPanel;
        _statusText = GetTemplateChild(PART_StatusText) as TextBlock;

        InitializeStatusTimer();
        SubscribeToServiceEvents();
    }

    #endregion

    #region - Private Methods -

    private void InitializeStatusTimer()
    {
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _statusTimer.Tick += UpdateStatus;
    }

    private void SubscribeToServiceEvents()
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            // WeakEventManager 사용으로 메모리 누수 방지
            WeakEventManager<IRtspStreamingService, StreamingStateChangedEventArgs>
                .AddHandler(service, nameof(service.StateChanged), OnServiceStateChanged);

            WeakEventManager<IRtspStreamingService, StreamingErrorEventArgs>
                .AddHandler(service, nameof(service.ErrorOccurred), OnServiceErrorOccurred);

            WeakEventManager<IRtspStreamingService, StreamingProgressEventArgs>
                .AddHandler(service, nameof(service.ProgressUpdated), OnServiceProgressUpdated);
        }
    }

    private void UnsubscribeFromServiceEvents()
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            WeakEventManager<IRtspStreamingService, StreamingStateChangedEventArgs>
                .RemoveHandler(service, nameof(service.StateChanged), OnServiceStateChanged);

            WeakEventManager<IRtspStreamingService, StreamingErrorEventArgs>
                .RemoveHandler(service, nameof(service.ErrorOccurred), OnServiceErrorOccurred);

            WeakEventManager<IRtspStreamingService, StreamingProgressEventArgs>
                .RemoveHandler(service, nameof(service.ProgressUpdated), OnServiceProgressUpdated);
        }
    }

    private void OnServiceStateChanged(object? sender, StreamingStateChangedEventArgs e)
    {
        if (e.ContextId == _cachedContextId)
        {
            Dispatcher.BeginInvoke(() =>
            {
                PlaybackState = e.NewState;
                UpdateUI(e.NewState);
                StateChanged?.Invoke(this, e);
            });
        }
    }

    private void OnServiceErrorOccurred(object? sender, StreamingErrorEventArgs e)
    {
        if (e.ContextId == _cachedContextId || string.IsNullOrEmpty(e.ContextId))
        {
            Dispatcher.BeginInvoke(() =>
            {
                StatusMessage = e.ErrorMessage;
                ShowError(true);
                ErrorOccurred?.Invoke(this, e);
            });
        }
    }

    private void OnServiceProgressUpdated(object? sender, StreamingProgressEventArgs e)
    {
        if (e.ContextId == _cachedContextId)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ProgressUpdated?.Invoke(this, e);
            });
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // XAML에서 설정된 ContextId가 있다면 캐시 업데이트
        if (!string.IsNullOrEmpty(ContextId) && ContextId != _cachedContextId)
        {
            _cachedContextId = ContextId;
            _log?.Info($"[RtspPlayer] Updated cached ContextId on load: {_cachedContextId}");
        }

        _log?.Info($"[RtspPlayer] Control loaded: {_cachedContextId}");


        if (AutoPlay && ConnectionInfo != null)
        {
            // 로드 직후가 아닌 약간의 지연 후 연결
            // _contextId를 사용하여 각기 다른 지연 시간 적용
            var randomDelay = Math.Abs(_cachedContextId.GetHashCode() % 1500) + 500; // 500-2000ms
            await Task.Delay(randomDelay);

            // 이미 Unload되지 않았는지 확인
            if (!_disposed && IsLoaded)
            {
                await ConnectAsync();
            }
        }
    }

    private async void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _log?.Info($"[RtspPlayer] Unloading: {_cachedContextId}");

        // 이벤트 핸들러 즉시 해제
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        await CleanupAsync();

        // 강한 참조 해제
        _streamingService = null;
    }

    private static void OnContextIdChanged(DependencyObject? d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RtspPlayer player)
        {
            var newId = e.NewValue as string;
            var oldId = e.OldValue as string;

            // 캐시 업데이트
            player._cachedContextId = newId;

            player._log?.Info($"[RtspPlayer] ContextId changed from '{oldId}' to '{newId}'");

            // 이미 서비스에 연결된 상태라면 경고
            if (!string.IsNullOrEmpty(oldId) && player.PlaybackState != PlaybackState.None)
            {
                player._log?.Warning($"[RtspPlayer] ContextId changed while active! Old: {oldId}, New: {newId}");
            }

            CommandManager.InvalidateRequerySuggested();
        }
    }


    private static async void OnConnectionInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RtspPlayer player && player.AutoPlay && e.NewValue != null)
        {
            await player.ConnectAsync();
        }
    }

    private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RtspPlayer player && e.NewValue is int volume)
        {
            if (player._weakStreamingService?.TryGetTarget(out var service) == true)
            {
                service.SetVolume(player._cachedContextId, volume);
            }
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
        if (d is RtspPlayer player)
        {
            if (player._weakStreamingService?.TryGetTarget(out var service) == true)
            {
                service.ToggleMute(player._cachedContextId);
            }
        }
    }

    private static void OnPlaybackStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RtspPlayer player && e.NewValue is PlaybackState state)
        {
            player.UpdateUI(state);
        }
    }

  
    public async Task<bool> ConnectAsync()
    {
        if (_disposed) return false;
        if (string.IsNullOrEmpty(_cachedContextId)) return false;

        await _connectionSemaphore.WaitAsync();

        try
        {
            // 현재 상태 확인
            var currentState = PlaybackState;
            if (currentState == PlaybackState.Connecting ||
                currentState == PlaybackState.Playing ||
                currentState == PlaybackState.Buffering ||
                currentState == PlaybackState.Reconnecting)
            {
                _log?.Warning($"[RtspPlayer] Already in progress: {_cachedContextId}, State: {currentState}");
                return false;
            }

            // 재시도 카운트 초기화
            _connectionRetryCount = 0;

            // 재시도 로직으로 연결 시도
            return await ConnectWithRetryAsync();
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspPlayer] Connect error: {ex.Message}");
            PlaybackState = PlaybackState.Error;
            StatusMessage = $"Error: {ex.Message}";
            UpdateUI(PlaybackState.Error);
            return false;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private async Task<bool> ConnectWithRetryAsync()
    {
        const int MAX_ATTEMPTS = 3;

        while (_connectionRetryCount < MAX_ATTEMPTS)
        {
            _connectionRetryCount++;

            _log?.Info($"[RtspPlayer] Connection attempt {_connectionRetryCount}/{MAX_ATTEMPTS} for {_cachedContextId}");

            // 재시도 시 상태 메시지 업데이트
            StatusMessage = _connectionRetryCount > 1
                ? $"Connecting... (Attempt {_connectionRetryCount}/{MAX_ATTEMPTS})"
                : "Connecting...";

            PlaybackState = PlaybackState.Connecting;
            UpdateUI(PlaybackState.Connecting);

            // 실제 연결 시도
            var success = await AttemptConnectionAsync();

            if (success)
            {
                _log?.Info($"[RtspPlayer] Successfully connected on attempt {_connectionRetryCount} for {_cachedContextId}");
                _connectionRetryCount = 0; // 성공 시 카운터 리셋
                return true;
            }

            // 마지막 시도가 아니면 대기 후 재시도
            if (_connectionRetryCount < MAX_ATTEMPTS)
            {
                var delay = CalculateRetryDelay(_connectionRetryCount);
                _log?.Warning($"[RtspPlayer] Connection attempt {_connectionRetryCount} failed, retrying in {delay}ms for {_cachedContextId}");

                StatusMessage = $"Connection failed, retrying in {delay / 1000}s...";
                // Error가 아닌 Reconnecting 상태 사용
                PlaybackState = PlaybackState.Reconnecting;
                UpdateUI(PlaybackState.Reconnecting);

                await Task.Delay(delay);
            }
        }

        // 모든 재시도 실패
        _log?.Error($"[RtspPlayer] All {MAX_ATTEMPTS} connection attempts failed for {_cachedContextId}");
        PlaybackState = PlaybackState.Error;
        StatusMessage = $"Connection failed after {MAX_ATTEMPTS} attempts";
        UpdateUI(PlaybackState.Error);
        _connectionRetryCount = 0;

        return false;
    }

    private async Task<bool> AttemptConnectionAsync()
    {
        try
        {
            // VideoView 준비 확인
            if (_videoView == null)
            {
                _log?.Error($"[RtspPlayer] VideoView not ready for {_cachedContextId}");

                // VideoView가 아직 준비되지 않았다면 잠시 대기
                var delay = Random.Shared.Next(100, 300);
                await Task.Delay(delay);
                // 재확인
                if (_videoView == null)
                {
                    StatusMessage = "VideoView not ready";
                    return false;
                }
            }

            // UI 스레드에서 이전 MediaPlayer 정리
            var tcs = new TaskCompletionSource<bool>();
            await Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (_videoView.MediaPlayer != null)
                    {
                        _log?.Info($"[RtspPlayer] Clearing existing MediaPlayer for {_cachedContextId}");
                        _videoView.MediaPlayer = null;
                    }
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task;

            if (ConnectionInfo == null)
            {
                _log?.Warning("[RtspPlayer] No connection info provided");
                StatusMessage = "No connection info";
                return false;
            }

            _log?.Info($"[RtspPlayer] Starting connection: {_cachedContextId} to {ConnectionInfo.GetFullUrl()}");
            _statusTimer?.Start();

            if (_weakStreamingService?.TryGetTarget(out var service) == true)
            {
                var success = await service.ConnectAsync(
                    _cachedContextId,
                    ConnectionInfo,
                    StreamingOptions,
                    _videoView);

                if (!success)
                {
                    // 실패하더라도 여기서는 에러 상태로 설정하지 않음 (재시도 로직에서 처리)
                    _log?.Warning($"[RtspPlayer] Service connection failed for {_cachedContextId}");
                    StatusMessage = "Stream connection failed";
                }

                return success;
            }

            _log?.Warning("[RtspPlayer] Streaming service not available");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspPlayer] Connection attempt error: {ex.Message}");
            return false;
        }
    }

    private int CalculateRetryDelay(int attemptNumber)
    {
        // 고정 2초 지연
        return 2000;

        // 또는 점진적 증가
        // return attemptNumber * 1000;  // 1초, 2초, 3초...

        // 또는 지수 백오프
        // return (int)Math.Pow(2, attemptNumber - 1) * 1000;  // 1초, 2초, 4초...
    }



    public async Task DisconnectAsync()
    {
        if (_disposed) return;

        await _connectionSemaphore.WaitAsync();

        try
        {
            if (_cachedContextId == null) return;

            _log?.Info($"[RtspPlayer] Disconnecting: {_cachedContextId} - {ConnectionInfo?.Description}");

            _statusTimer?.Stop();

            // UI 스레드에서 VideoView 정리
            var tcs = new TaskCompletionSource<bool>();
            await Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (_videoView != null)
                    {
                        _videoView.MediaPlayer = null;
                    }
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task;

            if (_weakStreamingService?.TryGetTarget(out var service) == true)
            {
                await service.DisconnectAsync(_cachedContextId);
            }

            PlaybackState = PlaybackState.Stopped;
            StatusMessage = "Disconnected";
            UpdateUI(PlaybackState.Stopped);
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspPlayer] Disconnect error: {ex.Message}");
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }


    /// <summary>
    /// Play 버튼 동작 - 상태에 따라 연결 또는 재생
    /// </summary>
    public async Task PlayOrConnectAsync()
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

    private bool CanPlayOrConnect()
    {
        return !_disposed &&
               PlaybackState != PlaybackState.Playing &&
               PlaybackState != PlaybackState.Connecting &&
               PlaybackState != PlaybackState.Buffering &&
               PlaybackState != PlaybackState.Reconnecting;
    }

    public void Pause()
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            service.Pause(_cachedContextId);
            StatusMessage = "Paused";
        }
    }

    public void Resume()
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            service.Play(_cachedContextId);
            StatusMessage = "Playing";
        }
    }

    public async Task<bool> TakeSnapshotAsync(string filePath = null)
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            filePath ??= $"snapshot_{_cachedContextId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var result = await service.TakeSnapshotAsync(_cachedContextId, filePath);

            if (result)
            {
                StatusMessage = $"Snapshot saved: {filePath}";
                _log?.Info($"[RtspPlayer] Snapshot saved: {filePath}");
            }

            return result;
        }
        return false;
    }

    public void SetFrameSkip(int skipFrames)
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            service.SetFrameSkip(_cachedContextId, skipFrames);
        }
    }

    public StreamingStatistics GetStatistics()
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            return service.GetStatistics(_cachedContextId);
        }
        return null;
    }

    private void UpdateStatus(object sender, EventArgs e)
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            var state = service.GetPlaybackState(_cachedContextId);
            if (state != PlaybackState)
            {
                PlaybackState = state;
                UpdateUI(state);
            }

            // 통계 업데이트
            var stats = service.GetStatistics(_cachedContextId);
            if (stats != null && _statusText != null)
            {
                _statusText.Text = $"{stats.GetFormattedBitrate()} | {stats.TotalPlayTime:hh\\:mm\\:ss}";
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

            // Command 상태 업데이트
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

    private bool CanConnect()
    {
        return !_disposed &&
               PlaybackState != PlaybackState.Playing &&
               PlaybackState != PlaybackState.Connecting &&
               ConnectionInfo != null;
    }

    private bool CanDisconnect()
    {
        return !_disposed &&
               (PlaybackState == PlaybackState.Playing ||
                PlaybackState == PlaybackState.Paused ||
                PlaybackState == PlaybackState.Buffering);
    }

    private async Task CleanupAsync()
    {
        _statusTimer?.Stop();
        _statusTimer = null;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        UnsubscribeFromServiceEvents();

        if (_videoView != null)
        {
            _videoView.MediaPlayer = null;
            _videoView = null;
        }

        await DisconnectAsync();
    }

    #endregion

    #region - IDisposable -

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);  // 소멸자 호출 방지
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_disposeLock)  // 동시 Dispose 방지
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 관리 리소스 정리 (순서 중요!)
                DisposeInOrder();
            }

            // 비관리 리소스 정리 (있다면)
            _disposed = true;
        }
    }

    private void DisposeInOrder()
    {
        try
        {
            // 1. 타이머 먼저 정지
            _statusTimer?.Stop();
            _statusTimer = null;

            // 2. 진행 중인 작업 취소
            _cancellationTokenSource?.Cancel();

            // 3. 이벤트 구독 해제 (메모리 누수 방지)
            UnsubscribeFromServiceEvents();

            // 4. 비동기 정리 작업 (동기 대기 with timeout)
            var cleanupTask = CleanupAsync();
            if (!cleanupTask.Wait(TimeSpan.FromSeconds(3)))
            {
                _log?.Warning($"[RtspPlayer] Cleanup timeout for {_cachedContextId}");
            }

            // 5. 동기화 객체 해제
            try
            {
                _connectionSemaphore?.Dispose();
            }
            catch { }

            // 6. CancellationTokenSource 해제
            _cancellationTokenSource?.Dispose();

            // 7. WeakReference 정리
            _weakStreamingService = null;

            _log?.Info($"[RtspPlayer] Disposed: {_cachedContextId}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspPlayer] Dispose error: {ex.Message}");
        }
    }

    // 소멸자는 비관리 리소스만 처리
    ~RtspPlayer()
    {
        Dispose(false);
    }

    #endregion
}