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

    private VideoView _videoView;
    private ProgressBar _loadingIndicator;
    private StackPanel _errorPanel;
    private TextBlock _statusText;
    private IRtspStreamingService _streamingService;
    private ILogService? _log;
    private string? _contextId;
    private DispatcherTimer _statusTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private WeakReference<IRtspStreamingService> _weakStreamingService;
    private bool _disposed;
    private readonly object _lockObject = new object();

    static RtspPlayer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RtspPlayer),
            new FrameworkPropertyMetadata(typeof(RtspPlayer)));
    }

    public RtspPlayer()
    {
        _contextId = Guid.NewGuid().ToString();
        _cancellationTokenSource = new CancellationTokenSource();

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

        _log?.Info($"[RtspPlayer] Control created: {_contextId}");
    }

    #region - Dependency Properties -

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

    private ICommand _playCommand;
    public ICommand PlayCommand =>
    _playCommand ??= new AsyncRelayCommand(PlayOrConnectAsync, CanPlayOrConnect);

    private ICommand _stopCommand;
    public ICommand StopCommand =>
    _stopCommand ??= new AsyncRelayCommand(DisconnectAsync, CanDisconnect);

    private ICommand _pauseCommand;
    public ICommand PauseCommand =>
        _pauseCommand ??= new RelayCommand(Pause, () => PlaybackState == PlaybackState.Playing);

    private ICommand _resumeCommand;
    public ICommand ResumeCommand =>
        _resumeCommand ??= new RelayCommand(Resume, () => PlaybackState == PlaybackState.Paused);

    private ICommand _snapshotCommand;
    public ICommand SnapshotCommand =>
        _snapshotCommand ??= new AsyncRelayCommand(() => TakeSnapshotAsync(), () => PlaybackState == PlaybackState.Playing);

    #endregion

    #region - Events -

    public event EventHandler<StreamingStateChangedEventArgs> StateChanged;
    public event EventHandler<StreamingErrorEventArgs> ErrorOccurred;
    public event EventHandler<StreamingProgressEventArgs> ProgressUpdated;

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
        if (e.ContextId == _contextId)
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
        if (e.ContextId == _contextId || string.IsNullOrEmpty(e.ContextId))
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
        if (e.ContextId == _contextId)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ProgressUpdated?.Invoke(this, e);
            });
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _log?.Info($"[RtspPlayer] Control loaded: {_contextId}");

        if (AutoPlay && ConnectionInfo != null)
        {
            await ConnectAsync();
        }
    }

    private async void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _log?.Info($"[RtspPlayer] Control unloading: {_contextId}");

        await CleanupAsync();
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
                service.SetVolume(player._contextId, volume);
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
                service.ToggleMute(player._contextId);
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

        lock (_lockObject)
        {
            if (PlaybackState == PlaybackState.Connecting ||
                PlaybackState == PlaybackState.Playing)
            {
                return false;
            }
        }

        try
        {
            if (ConnectionInfo == null)
            {
                _log?.Warning("[RtspPlayer] No connection info provided");
                StatusMessage = "No connection info";
                return false;
            }

            _log?.Info($"[RtspPlayer] Connecting: {_contextId} to {ConnectionInfo.GetFullUrl()}");

            PlaybackState = PlaybackState.Connecting;
            StatusMessage = "Connecting...";
            UpdateUI(PlaybackState.Connecting);
            _statusTimer?.Start();

            if (_weakStreamingService?.TryGetTarget(out var service) == true)
            {
                //// 1. 스트리밍 서비스로부터 MediaPlayer 인스턴스를 먼저 가져옵니다.
                //// (이 시점에는 아직 스트림이 시작되지 않았습니다.)
                //var mediaPlayer = service.GetMediaPlayer(_contextId);

                //if (_videoView != null && mediaPlayer != null)
                //{
                //    // 2. MediaPlayer를 VideoView에 할당하여, 비디오를 렌더링할 캔버스를 미리 지정합니다.
                //    _videoView.MediaPlayer = mediaPlayer;
                //}

                //_videoView = GetTemplateChild("PART_VideoView") as VideoView;


                //// 3. 이제 스트리밍 서비스에 연결을 시작하도록 명령합니다.
                //// 이때 VLC는 이미 VideoView와 연결되어 있으므로, 영상은 컨트롤 내부에 표시됩니다.
                //var success = await service.ConnectAsync(
                //                                _contextId,
                //                                ConnectionInfo,
                //                                StreamingOptions,
                //                                _videoView);

                //if (success)
                //{
                //    StatusMessage = "Connected";
                //    _log?.Info($"[RtspPlayer] Connected successfully: {_contextId}");
                //}
                //else
                //{
                //    PlaybackState = PlaybackState.Error;
                //    StatusMessage = "Connection failed";
                //    UpdateUI(PlaybackState.Error);
                //    _log?.Error($"[RtspPlayer] Connection failed: {_contextId}");
                //}

                // videoView 파라미터 없이 호출 (인터페이스 수정됨)
                var success = await service.ConnectAsync(
                    _contextId,
                    ConnectionInfo,
                    StreamingOptions);

                if (success)
                {
                    // 연결 성공 후 MediaPlayer를 VideoView에 연결
                    var mediaPlayer = service.GetMediaPlayer(_contextId);
                    if (_videoView != null && mediaPlayer != null)
                    {
                        _videoView.MediaPlayer = mediaPlayer;
                    }

                    StatusMessage = "Connected";
                    _log?.Info($"[RtspPlayer] Connected successfully: {_contextId}");
                }
                else
                {
                    PlaybackState = PlaybackState.Error;
                    StatusMessage = "Connection failed";
                    UpdateUI(PlaybackState.Error);
                    _log?.Error($"[RtspPlayer] Connection failed: {_contextId}");
                }

                return success;
            }

            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspPlayer] Connect error: {ex.Message}");
            PlaybackState = PlaybackState.Error;
            StatusMessage = $"Error: {ex.Message}";
            UpdateUI(PlaybackState.Error);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_disposed) return;

        try
        {
            _log?.Info($"[RtspPlayer] Disconnecting: {_contextId}");

            _statusTimer?.Stop();

            if (_videoView != null)
            {
                _videoView.MediaPlayer = null;
            }

            if (_weakStreamingService?.TryGetTarget(out var service) == true)
            {
                await service.DisconnectAsync(_contextId);
            }

            PlaybackState = PlaybackState.Stopped;
            StatusMessage = "Disconnected";
            UpdateUI(PlaybackState.Stopped);
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspPlayer] Disconnect error: {ex.Message}");
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
            service.Pause(_contextId);
            PlaybackState = PlaybackState.Paused;  // 상태 직접 업데이트
            StatusMessage = "Paused";
        }
    }

    public void Resume()
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            service.Play(_contextId);
            PlaybackState = PlaybackState.Playing;  // 상태 직접 업데이트
            StatusMessage = "Playing";
        }
    }

    public async Task<bool> TakeSnapshotAsync(string filePath = null)
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            filePath ??= $"snapshot_{_contextId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var result = await service.TakeSnapshotAsync(_contextId, filePath);

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
            service.SetFrameSkip(_contextId, skipFrames);
        }
    }

    public StreamingStatistics GetStatistics()
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            return service.GetStatistics(_contextId);
        }
        return null;
    }

    private void UpdateStatus(object sender, EventArgs e)
    {
        if (_weakStreamingService?.TryGetTarget(out var service) == true)
        {
            var state = service.GetPlaybackState(_contextId);
            if (state != PlaybackState)
            {
                PlaybackState = state;
                UpdateUI(state);
            }

            // 통계 업데이트
            var stats = service.GetStatistics(_contextId);
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
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _log?.Info($"[RtspPlayer] Disposing control: {_contextId}");

            var cleanupTask = CleanupAsync();
            cleanupTask.Wait(TimeSpan.FromSeconds(5));

            _log?.Info($"[RtspPlayer] Control disposed: {_contextId}");
        }

        _disposed = true;
    }

    ~RtspPlayer()
    {
        Dispose(false);
    }

    #endregion
}