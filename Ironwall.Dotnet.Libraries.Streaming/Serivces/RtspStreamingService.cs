using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Helpers;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using LibVLCSharp.Shared;
using Polly;
using Polly.Retry;
using System;
using System.Buffers;
using System.Collections.Concurrent;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 2:33:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// RTSP 스트리밍 서비스 구현 (Autofac DI 사용)
/// </summary>
public class RtspStreamingService : IRtspStreamingService
{
    #region - Ctors -
    public RtspStreamingService(
        ILogService log,
        StreamingSetupModel setupModel,
        IStreamingContextPool contextPool)
    {
        _log = log;
        _setupModel = setupModel;
        _contextPool = contextPool;
        _contexts = new ConcurrentDictionary<string, StreamingContext>();
        _weakContexts = new ConcurrentDictionary<string, WeakReference<StreamingContext>>();
        _memoryPool = ArrayPool<byte>.Create();
        _disposed = false;

        // 재시도 정책 생성
        _retryPolicy = CreateRetryPolicy();

        // 메모리 모니터링 타이머
        _memoryMonitorTimer = new Timer(CheckMemoryPressure, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        // LibVLC 초기화
        InitializeLibVLC();

        _log?.Info($"[RtspStreamingService] Service initialized - MaxConnections: {_setupModel.MaxConnections}, PoolSize: {_setupModel.ContextPoolSize}");
    }
    #endregion

    #region - Implementation of IRtspStreamingService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public async Task<bool> ConnectAsync(
        string contextId,
        RtspConnectionInfo connectionInfo,
        StreamingOptions options = null,
        LibVLCSharp.WPF.VideoView videoView = null)
    {
        if (_disposed)
        {
            _log?.Warning("[RtspStreamingService] Service is disposed");
            return false;
        }

        if (string.IsNullOrEmpty(contextId))
        {
            _log?.Error("[RtspStreamingService] Invalid contextId");
            return false;
        }

        try
        {
            _log?.Info($"[RtspStreamingService] Connecting stream: {contextId} to {connectionInfo?.IpAddress}");

            // 최대 연결 수 체크
            if (_contexts.Count >= _setupModel.MaxConnections)
            {
                _log?.Warning($"[RtspStreamingService] Max connections reached: {_setupModel.MaxConnections}");
                OnErrorOccurred(contextId, "Maximum connections limit reached", ErrorSeverity.Warning);
                return false;
            }

            // 기존 컨텍스트 정리
            await CleanupContextAsync(contextId);

            // 새 컨텍스트 생성 (Pool에서 가져오기)
            var context = _contextPool.Get();
            context.Initialize(contextId, connectionInfo, options ?? new StreamingOptions(), _libVLC);

            if (!_contexts.TryAdd(contextId, context))
            {
                _contextPool.Return(context);
                _log?.Error($"[RtspStreamingService] Failed to add context: {contextId}");
                return false;
            }

            // Weak Reference 추가 (메모리 압박 시 자동 해제)
            _weakContexts[contextId] = new WeakReference<StreamingContext>(context);

            // URL 유효성 검사
            var url = connectionInfo.GetFullUrl();
            if (!RtspUrlValidator.IsValidUrl(url))
            {
                _log?.Error($"[RtspStreamingService] Invalid RTSP URL: {url}");
                await CleanupContextAsync(contextId);
                OnErrorOccurred(contextId, "Invalid RTSP URL", ErrorSeverity.Error);
                return false;
            }

            // 재시도 정책으로 연결
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await ConnectInternalAsync(context, url, videoView);
            });
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspStreamingService] Connect failed for {contextId}: {ex.Message}");
            OnErrorOccurred(contextId, ex.Message, ErrorSeverity.Error);
            await CleanupContextAsync(contextId);
            return false;
        }
    }

    private Task<bool> ConnectInternalAsync(StreamingContext context, string url, LibVLCSharp.WPF.VideoView videoView)
    {
        try
        {
            UpdateState(context.Id, PlaybackState.Connecting);
            _log?.Info($"[RtspStreamingService] Creating media for {context.Id}: {url}");

            // Media 생성 및 옵션 설정
            var media = CreateOptimizedMedia(url, context.Options);
            context.SetMedia(media);

            if (context.MediaPlayer == null)
            {
                _log?.Error($"[RtspStreamingService] MediaPlayer is null for {context.Id}");
                return Task.FromResult(false);
            }

            // 핵심 수정: VideoView를 MediaPlayer에 할당
            if (videoView != null)
            {
                // This line is the solution. It tells the MediaPlayer where to render the video.
                videoView.MediaPlayer = context.MediaPlayer;
                _log?.Info($"[RtspStreamingService] VideoView assigned to MediaPlayer for {context.Id}");
            }
            else
            {
                _log?.Warning($"[RtspStreamingService] VideoView is null for {context.Id}. The video may appear in a separate window.");
            }

            // 이벤트 핸들러 등록
            RegisterEventHandlers(context);

            // 재생 시작
            var result = context.MediaPlayer.Play(media);

            if (result)
            {
                _log?.Info($"[RtspStreamingService] Stream {context.Id} started successfully");
                context.LastConnectionTime = DateTime.Now;
                context.ReconnectAttempts = 0;
            }
            else
            {
                _log?.Error($"[RtspStreamingService] Failed to start stream {context.Id}");
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspStreamingService] ConnectInternal error for {context.Id}: {ex.Message}");
            UpdateState(context.Id, PlaybackState.Error);
            throw;
        }
       
    }

    public async Task DisconnectAsync(string contextId)
    {
        try
        {
            _log?.Info($"[RtspStreamingService] Disconnecting stream: {contextId}");
            await CleanupContextAsync(contextId);
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspStreamingService] Disconnect error for {contextId}: {ex.Message}");
        }
    }

    public async Task DisconnectAllAsync()
    {
        _log?.Info("[RtspStreamingService] Disconnecting all streams");

        var tasks = _contexts.Keys.Select(DisconnectAsync).ToArray();
        await Task.WhenAll(tasks);

        _log?.Info("[RtspStreamingService] All streams disconnected");
    }

    public void Play(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            context.MediaPlayer.Play();
            UpdateState(contextId, PlaybackState.Playing);
            _log?.Info($"[RtspStreamingService] Stream {contextId} playing");
        }
    }

    public void Pause(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            context.MediaPlayer.SetPause(true);
            UpdateState(contextId, PlaybackState.Paused);
            _log?.Info($"[RtspStreamingService] Stream {contextId} paused");
        }
    }

    public void Stop(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            context.MediaPlayer.Stop();
            UpdateState(contextId, PlaybackState.Stopped);
            _log?.Info($"[RtspStreamingService] Stream {contextId} stopped");
        }
    }

    public void SetVolume(string contextId, int volume)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            var clampedVolume = Math.Clamp(volume, 0, 100);
            context.MediaPlayer.Volume = clampedVolume;
            context.Options.Volume = clampedVolume;
            _log?.Info($"[RtspStreamingService] Volume set to {clampedVolume} for {contextId}");
        }
    }

    public void ToggleMute(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            context.MediaPlayer.Mute = !context.MediaPlayer.Mute;
            context.Options.IsMuted = context.MediaPlayer.Mute;
            _log?.Info($"[RtspStreamingService] Mute toggled for {contextId}: {context.MediaPlayer.Mute}");
        }
    }

    public MediaPlayer GetMediaPlayer(string contextId)
    {
        return TryGetContext(contextId, out var context) ? context.MediaPlayer : null;
    }

    public PlaybackState GetPlaybackState(string contextId)
    {
        return TryGetContext(contextId, out var context) ? context.State : PlaybackState.None;
    }

    public StreamingStatistics GetStatistics(string contextId)
    {
        return TryGetContext(contextId, out var context) ? context.Statistics : null;
    }

    public bool IsConnected(string contextId)
    {
        return TryGetContext(contextId, out var context) &&
               context.State == PlaybackState.Playing;
    }

    public async Task<bool> TakeSnapshotAsync(string contextId, string filePath)
    {
        try
        {
            if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
            {
                _log?.Info($"[RtspStreamingService] Taking snapshot for {contextId}: {filePath}");
                return await Task.Run(() => context.MediaPlayer.TakeSnapshot(0, filePath, 0, 0));
            }
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspStreamingService] Snapshot failed for {contextId}: {ex.Message}");
            return false;
        }
    }

    public void SetFrameSkip(string contextId, int skipFrames)
    {
        if (TryGetContext(contextId, out var context))
        {
            context.FrameSkipCount = Math.Max(0, skipFrames);
            _log?.Info($"[RtspStreamingService] Frame skip set to {skipFrames} for {contextId}");
        }
    }

    public void SetQuality(string contextId, int quality)
    {
        if (TryGetContext(contextId, out var context))
        {
            context.Options.VideoQuality = Math.Clamp(quality, 0, 100);
            _log?.Info($"[RtspStreamingService] Quality set to {quality} for {contextId}");
        }
    }

    #endregion

    #region - Events -

    public event EventHandler<StreamingStateChangedEventArgs> StateChanged;
    public event EventHandler<StreamingErrorEventArgs> ErrorOccurred;
    public event EventHandler<StreamingProgressEventArgs> ProgressUpdated;

    #endregion

    #region - Private Methods -

    private void InitializeLibVLC()
    {
        try
        {
            _libVLC = LibVLCInitializer.Initialize(_setupModel);
            _log?.Info("[RtspStreamingService] LibVLC initialized successfully");
        }
        catch (Exception ex)
        {
            _log?.Error($"[RtspStreamingService] LibVLC initialization failed: {ex.Message}");
            throw;
        }
    }

    private Media CreateOptimizedMedia(string url, StreamingOptions options)
    {
        var media = new Media(_libVLC, url, FromType.FromLocation);

        // 네트워크 캐싱 (적응형)
        var caching = CalculateOptimalCaching(options);
        media.AddOption($":network-caching={caching}");
        media.AddOption($":live-caching={caching}");

        // 프로토콜 설정
        if (options.UseTcp)
        {
            media.AddOption(":rtsp-tcp");
        }
        else
        {
            media.AddOption(":rtsp-udp");
            media.AddOption(":rtp-max-misorder=100");
        }

        // 버퍼 설정
        media.AddOption($":rtsp-frame-buffer-size={options.FrameBufferSize}");

        // 하드웨어 가속
        if (options.UseHardwareAcceleration)
        {
            media.AddOption(":avcodec-hw=any");
            media.AddOption(":avcodec-threads=0"); // 자동 스레드 수
        }

        // 성능 최적화 옵션
        if (options.AllowFrameSkip)
        {
            media.AddOption(":avcodec-fast");
            media.AddOption(":avcodec-skip-frame=1"); // Non-ref frames skip
            media.AddOption(":avcodec-skip-idct=1");
            media.AddOption(":avcodec-hurry-up");
        }

        // 타임아웃 설정
        media.AddOption($":ipv4-timeout={options.ConnectionTimeoutSeconds * 1000}");

        // 로깅 레벨
        if (options.EnableDebugLogging || _setupModel.EnableDebugLogging)
        {
            media.AddOption(":verbose=2");
        }

        return media;
    }

    private int CalculateOptimalCaching(StreamingOptions options)
    {
        var baseCaching = options.NetworkCaching;

        // TCP는 더 많은 버퍼링 필요
        if (options.UseTcp)
        {
            baseCaching = (int)(baseCaching * 1.5);
        }

        return Math.Min(baseCaching, 2000); // 최대 2초
    }

    private void RegisterEventHandlers(StreamingContext context)
    {
        if (context.MediaPlayer == null) return;

        // WeakReference를 사용하여 메모리 누수 방지
        var weakThis = new WeakReference<RtspStreamingService>(this);

        context.MediaPlayer.Playing += (s, e) =>
        {
            if (weakThis.TryGetTarget(out var service))
            {
                service.OnStreamPlaying(context);
            }
        };

        context.MediaPlayer.Buffering += (s, e) =>
        {
            if (weakThis.TryGetTarget(out var service))
            {
                service.OnStreamBuffering(context, e.Cache);
            }
        };

        context.MediaPlayer.EncounteredError += (s, e) =>
        {
            if (weakThis.TryGetTarget(out var service))
            {
                service.OnStreamError(context);
            }
        };

        context.MediaPlayer.EndReached += (s, e) =>
        {
            if (weakThis.TryGetTarget(out var service))
            {
                service.OnStreamEnded(context);
            }
        };

        // 통계 업데이트 (1초마다)
        context.MediaPlayer.TimeChanged += (s, e) =>
        {
            if (weakThis.TryGetTarget(out var service))
            {
                service.UpdateStatistics(context);
            }
        };
    }

    private void OnStreamPlaying(StreamingContext context)
    {
        context.State = PlaybackState.Playing;
        context.LastConnectionTime = DateTime.Now;
        context.ReconnectAttempts = 0;
        UpdateState(context.Id, PlaybackState.Playing);
        _log?.Info($"[RtspStreamingService] Stream {context.Id} is playing");
    }

    private void OnStreamBuffering(StreamingContext context, float cache)
    {
        if (cache < 100)
        {
            context.State = PlaybackState.Buffering;
            _log?.Info($"[RtspStreamingService] Stream {context.Id} buffering: {cache:F1}%");
        }
        else
        {
            context.State = PlaybackState.Playing;
        }
        UpdateState(context.Id, context.State);

        // Progress 이벤트 발생
        var progressArgs = new StreamingProgressEventArgs(
            context.Id,
            cache,
            TimeSpan.Zero,
            TimeSpan.Zero);
        ProgressUpdated?.Invoke(this, progressArgs);
    }

    private void OnStreamError(StreamingContext context)
    {
        context.State = PlaybackState.Error;
        context.Statistics.ErrorCount++;
        UpdateState(context.Id, PlaybackState.Error);
        _log?.Error($"[RtspStreamingService] Stream {context.Id} encountered error");

        if (context.Options.EnableAutoReconnect && context.ReconnectAttempts < context.Options.MaxReconnectAttempts)
        {
            _ = Task.Run(() => HandleReconnectAsync(context.Id));
        }
    }

    private void OnStreamEnded(StreamingContext context)
    {
        context.State = PlaybackState.Stopped;
        UpdateState(context.Id, PlaybackState.Stopped);
        _log?.Info($"[RtspStreamingService] Stream {context.Id} ended");
    }

    private void UpdateStatistics(StreamingContext context)
    {
        if (context?.MediaPlayer == null || context.Statistics == null)
            return;

        // 통계 업데이트
        context.Statistics.TotalPlayTime = DateTime.Now - context.Statistics.StartTime;

        // 메모리 사용량
        context.Statistics.MemoryUsageBytes = GC.GetTotalMemory(false);
    }

    private async Task HandleReconnectAsync(string contextId)
    {
        if (!TryGetContext(contextId, out var context))
            return;

        context.ReconnectAttempts++;
        context.Statistics.ReconnectCount++;
        UpdateState(contextId, PlaybackState.Reconnecting);

        _log?.Info($"[RtspStreamingService] Reconnect attempt {context.ReconnectAttempts}/{context.Options.MaxReconnectAttempts} for {contextId}");

        // Exponential backoff
        var delay = TimeSpan.FromSeconds(Math.Pow(2, context.ReconnectAttempts - 1) * context.Options.ReconnectDelaySeconds);
        await Task.Delay(delay);

        await ConnectAsync(contextId, context.ConnectionInfo, context.Options);
    }

    private bool TryGetContext(string contextId, out StreamingContext context)
    {
        // 강한 참조에서 먼저 찾기
        if (_contexts.TryGetValue(contextId, out context))
            return true;

        // Weak Reference에서 복구 시도
        if (_weakContexts.TryGetValue(contextId, out var weakRef) &&
            weakRef.TryGetTarget(out context))
        {
            // 강한 참조로 복구
            _contexts.TryAdd(contextId, context);
            return true;
        }

        context = null;
        return false;
    }

    private async Task CleanupContextAsync(string contextId)
    {
        if (_contexts.TryRemove(contextId, out var context))
        {
            UpdateState(contextId, PlaybackState.Disconnected);

            await Task.Run(() =>
            {
                try
                {
                    context.MediaPlayer?.Stop();
                    context.Cleanup();
                    _contextPool.Return(context);
                    _log?.Info($"[RtspStreamingService] Context {contextId} cleaned up");
                }
                catch (Exception ex)
                {
                    _log?.Error($"[RtspStreamingService] Cleanup error for {contextId}: {ex.Message}");
                }
            });
        }

        _weakContexts.TryRemove(contextId, out _);
    }

    private void UpdateState(string contextId, PlaybackState newState)
    {
        if (TryGetContext(contextId, out var context))
        {
            var oldState = context.State;
            context.State = newState;

            StateChanged?.Invoke(this, new StreamingStateChangedEventArgs(contextId, oldState, newState));
            _log?.Info($"[RtspStreamingService] State changed for {contextId}: {oldState} -> {newState}");
        }
    }

    private void OnErrorOccurred(string contextId, string errorMessage, ErrorSeverity severity)
    {
        var args = new StreamingErrorEventArgs(errorMessage, null, contextId, severity);
        ErrorOccurred?.Invoke(this, args);

        switch (severity)
        {
            case ErrorSeverity.Warning:
                _log?.Warning($"[RtspStreamingService] {contextId}: {errorMessage}");
                break;
            case ErrorSeverity.Error:
                _log?.Error($"[RtspStreamingService] {contextId}: {errorMessage}");
                break;
            case ErrorSeverity.Critical:
                _log?.Error($"[RtspStreamingService] {contextId}: {errorMessage}");
                break;
            default:
                _log?.Info($"[RtspStreamingService] {contextId}: {errorMessage}");
                break;
        }
    }

    private void CheckMemoryPressure(object state)
    {
        var memoryInfo = GC.GetTotalMemory(false);

        if (memoryInfo > _setupModel.MaxMemoryUsageBytes)
        {
            _log?.Warning($"[RtspStreamingService] Memory pressure detected: {memoryInfo / 1024 / 1024}MB / {_setupModel.MaxMemoryUsageBytes / 1024 / 1024}MB");

            // 강제 GC
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Playing 상태가 아닌 컨텍스트를 Weak Reference로 이동
            foreach (var kvp in _contexts.ToList())
            {
                if (kvp.Value.State != PlaybackState.Playing &&
                    kvp.Value.State != PlaybackState.Buffering)
                {
                    _weakContexts[kvp.Key] = new WeakReference<StreamingContext>(kvp.Value);
                    _contexts.TryRemove(kvp.Key, out _);
                }
            }

            _log?.Info($"[RtspStreamingService] Memory cleanup completed. New size: {GC.GetTotalMemory(false) / 1024 / 1024}MB");
        }
    }

    private AsyncRetryPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _setupModel.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _log?.Warning($"[RtspStreamingService] Retry {retryCount}/{_setupModel.MaxRetryAttempts} after {timeSpan.TotalSeconds:F1}s: {exception.Message}");
                });
    }

    #endregion

    #region - Dispose -

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
            _log?.Info("[RtspStreamingService] Disposing service...");

            // 메모리 모니터 타이머 정지
            _memoryMonitorTimer?.Dispose();

            // 모든 컨텍스트 정리
            var disconnectTask = DisconnectAllAsync();
            disconnectTask.Wait(TimeSpan.FromSeconds(10));

            // Pool 정리
            _contextPool?.Clear();

            // LibVLC 정리
            _libVLC?.Dispose();

            _log?.Info("[RtspStreamingService] Service disposed");
        }

        _disposed = true;
    }

   
    ~RtspStreamingService()
    {
        Dispose(false);
    }

    #endregion

    #region - Attributes -

    private readonly ILogService _log;
    private readonly StreamingSetupModel _setupModel;
    private readonly IStreamingContextPool _contextPool;
    private readonly ConcurrentDictionary<string, StreamingContext> _contexts;
    private readonly ConcurrentDictionary<string, WeakReference<StreamingContext>> _weakContexts;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ArrayPool<byte> _memoryPool;
    private readonly Timer _memoryMonitorTimer;
    private LibVLC _libVLC;
    private bool _disposed;

    #endregion
}