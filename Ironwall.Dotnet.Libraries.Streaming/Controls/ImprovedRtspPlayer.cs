using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Commands;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.Serivces;
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

    private VideoView? _videoView;
    private ProgressBar? _loadingIndicator;
    private StackPanel? _errorPanel;
    private TextBlock? _statusText;

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

        _log?.Info($"[ImprovedRtspPlayer] Control created with ID: {_cachedContextId}");
    }

    #region - Dependency Properties -

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
    public VideoView GetVideoView()
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
    }

    #endregion

    #region - Private Methods -

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // XAML에서 설정된 ContextId가 있다면 캐시 업데이트
        if (!string.IsNullOrEmpty(ContextId) && ContextId != _cachedContextId)
        {
            _cachedContextId = ContextId;
        }

        // Player Registry에 등록
        RegisterToService();

        _log?.Info($"[ImprovedRtspPlayer] Control loaded: {_cachedContextId}");

        if (AutoPlay && ConnectionInfo != null)
        {
            // 지연 후 연결 (서비스를 통해)
            var randomDelay = Math.Abs(_cachedContextId.GetHashCode() % 1500) + 500; // 500-2000ms
            await Task.Delay(randomDelay);

            if (!_disposed && IsLoaded)
            {
                await ConnectAsync();
            }
        }
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
            _log?.Info($"[ImprovedRtspPlayer] Registered to service: {_cachedContextId}, Success: {_isRegistered}");
        }
    }

    private void UnregisterFromService()
    {
        if (_isRegistered && _playerRegistry != null && !string.IsNullOrEmpty(_cachedContextId))
        {
            _playerRegistry.UnregisterPlayer(_cachedContextId);
            _isRegistered = false;
            _log?.Info($"[ImprovedRtspPlayer] Unregistered from service: {_cachedContextId}");
        }
    }

    private async Task ConnectAsync()
    {
        if (_streamingService == null || ConnectionInfo == null)
        {
            _log?.Warning("[ImprovedRtspPlayer] Cannot connect: Service or ConnectionInfo is null");
            return;
        }

        _log?.Info($"[ImprovedRtspPlayer] Requesting connection through service: {_cachedContextId}");

        // 서비스를 통해 연결 요청 (VideoView는 서비스가 알아서 가져감)
        await _streamingService.ConnectAsync(
            _cachedContextId,
            ConnectionInfo,
            StreamingOptions,
            null);  // VideoView는 서비스가 Player에서 직접 가져감
    }

    private async Task DisconnectAsync()
    {
        if (_streamingService == null)
            return;

        _log?.Info($"[ImprovedRtspPlayer] Requesting disconnection through service: {_cachedContextId}");
        await _streamingService.DisconnectAsync(_cachedContextId);
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
        if (_streamingService == null)
            return false;

        filePath ??= $"snapshot_{_cachedContextId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        return await _streamingService.TakeSnapshotAsync(_cachedContextId, filePath);
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

            player._log?.Info($"[ImprovedRtspPlayer] ContextId changed from '{oldId}' to '{newId}'");
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static async void OnConnectionInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImprovedRtspPlayer player && player.AutoPlay && e.NewValue != null)
        {
            await player.ConnectAsync();
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

                    _log?.Info($"[ImprovedRtspPlayer] Disposed: {_cachedContextId}");
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