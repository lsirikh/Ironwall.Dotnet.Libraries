//using Caliburn.Micro;
//using Ironwall.Dotnet.Libraries.Base.Services;
//using Ironwall.Dotnet.Libraries.Streaming.Events;
//using Ironwall.Dotnet.Libraries.Streaming.Helpers;
//using Ironwall.Dotnet.Libraries.Streaming.Models;
//using LibVLCSharp.Shared;
//using Polly;
//using Polly.Retry;
//using System;
//using System.Buffers;
//using System.Collections.Concurrent;

//namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
///****************************************************************************
//   Purpose      :                                                          
//   Created By   : GHLee                                                
//   Created On   : 9/24/2025 2:33:00 PM                                                    
//   Department   : SW Team                                                   
//   Company      : Sensorway Co., Ltd.                                       
//   Email        : lsirikh@naver.com                                         
//****************************************************************************/
///// <summary>
///// RTSP 스트리밍 서비스 구현 (Autofac DI 사용)
///// </summary>
//public class RtspStreamingService : IRtspStreamingService
//{
//    #region - Ctors -
//    public RtspStreamingService(
//        ILogService log,
//        StreamingSetupModel setupModel,
//        IStreamingContextPool contextPool)
//    {
//        _log = log;
//        _setupModel = setupModel;
//        _contextPool = contextPool;
//        _contexts = new ConcurrentDictionary<string, StreamingContext>();
//        _weakContexts = new ConcurrentDictionary<string, WeakReference<StreamingContext>>();



//        _memoryPool = ArrayPool<byte>.Create();
//        _disposed = false;

//        // 재시도 정책 생성
//        _retryPolicy = CreateRetryPolicy();

//        // 메모리 모니터링 타이머
//        _memoryMonitorTimer = new Timer(CheckMemoryPressure, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

//        // 이벤트 핸들러 딕셔너리 초기화
//        EventHandlers = new Dictionary<string, Delegate>();

//        // LibVLC 초기화
//        InitializeLibVLC();

//        _log?.Info($"[RtspStreamingService] Service initialized - MaxConnections: {_setupModel.MaxConnections}, PoolSize: {_setupModel.ContextPoolSize}");
//    }
//    #endregion

//    #region - Implementation of IRtspStreamingService -
//    public Task ExecuteAsync(CancellationToken token = default)
//    {
//        return Task.CompletedTask;
//    }

//    public Task StopAsync(CancellationToken token = default)
//    {
//        return Task.CompletedTask;
//    }

//    public async Task<bool> ConnectAsync(
//        string contextId,
//        RtspConnectionInfo connectionInfo,
//        StreamingOptions options = null,
//        LibVLCSharp.WPF.VideoView videoView = null)
//    {
//        if (_disposed)
//        {
//            _log?.Warning("[RtspStreamingService] Service is disposed");
//            return false;
//        }

//        if (string.IsNullOrEmpty(contextId))
//        {
//            _log?.Error("[RtspStreamingService] Invalid contextId");
//            return false;
//        }

//        // Context별 Lock 획득
//        var contextLock = _contextLocks.GetOrAdd(contextId, _ => new SemaphoreSlim(1, 1));
//        await contextLock.WaitAsync();

//        try
//        {
//            _log?.Info($"[RtspStreamingService] Connecting stream: {contextId} to {connectionInfo?.IpAddress}");

//            // 최대 연결 수 체크
//            if (_contexts.Count >= _setupModel.MaxConnections)
//            {
//                _log?.Warning($"[RtspStreamingService] Max connections reached: {_setupModel.MaxConnections}");
//                OnErrorOccurred(contextId, "Maximum connections limit reached", ErrorSeverity.Warning);
//                return false;
//            }

//            // 기존 컨텍스트 완전 정리
//            if (_contexts.ContainsKey(contextId))
//            {
//                _log?.Warning($"[RtspStreamingService] Context already exists, cleaning up: {contextId}");
//                await CleanupContextAsync(contextId);

//                // 추가 확인: CleanupContextAsync 후에도 남아있는지 체크
//                var verifyWait = 10;
//                while ((_contexts.ContainsKey(contextId) ||
//                        _weakContexts.ContainsKey(contextId)) && verifyWait-- > 0)
//                {
//                    await Task.Delay(100);
//                    GC.Collect(); // 강제 GC로 WeakReference 정리
//                    GC.WaitForPendingFinalizers();
//                }

//                if (_contexts.ContainsKey(contextId))
//                {
//                    // 강제로 제거
//                    _contexts.TryRemove(contextId, out _);
//                    _weakContexts.TryRemove(contextId, out _);
//                    _log?.Warning($"[RtspStreamingService] Force removed lingering context: {contextId}");
//                }
//            }


//            // 새 컨텍스트 생성 (Pool에서 가져오기)
//            var context = _contextPool.Get();

//            // Initialize 전에 contextId 유효성 재확인
//            if (string.IsNullOrEmpty(contextId))
//            {
//                _log?.Error("[RtspStreamingService] ContextId became null before initialization");
//                _contextPool.Return(context);
//                return false;
//            }

//            context.Initialize(contextId, connectionInfo, options ?? new StreamingOptions(), _libVLC);
//            // context.Id가 제대로 설정되었는지 확인
//            if (string.IsNullOrEmpty(context.Id))
//            {
//                _log?.Error($"[RtspStreamingService] Context initialization failed - Id is null");
//                context.Cleanup();
//                _contextPool.Return(context);
//                return false;
//            }

//            // Context 추가
//            if (!_contexts.TryAdd(contextId, context))
//            {
//                _log?.Error($"[RtspStreamingService] Failed to add context: {contextId}");
//                context.Cleanup();
//                _contextPool.Return(context);
//                return false;
//            }

//            // Weak Reference 추가
//            _weakContexts[contextId] = new WeakReference<StreamingContext>(context);

//            // URL 유효성 검사
//            var url = connectionInfo.GetFullUrl();
//            if (!RtspUrlValidator.IsValidUrl(url))
//            {
//                _log?.Error($"[RtspStreamingService] Invalid RTSP URL: {url}");
//                await CleanupContextAsync(contextId);
//                OnErrorOccurred(contextId, "Invalid RTSP URL", ErrorSeverity.Error);
//                return false;
//            }

//            // 재시도 정책으로 연결
//            return await _retryPolicy.ExecuteAsync(async () =>
//            {
//                return await ConnectInternalAsync(context, url, videoView);
//            });
//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] Connect failed for {contextId}: {ex.Message}");
//            OnErrorOccurred(contextId, ex.Message, ErrorSeverity.Error);
//            if (!string.IsNullOrEmpty(contextId))
//            {
//                await CleanupContextAsync(contextId);
//            }
//            return false;
//        }
//        finally
//        {
//            contextLock.Release();
//        }
//    }

//    private async Task<bool> ConnectInternalAsync(StreamingContext context, string url, LibVLCSharp.WPF.VideoView videoView)
//    {
//        try
//        {
//            // Context와 Id 유효성 확인
//            if (context == null || string.IsNullOrEmpty(context.Id))
//            {
//                _log?.Error($"[RtspStreamingService] Invalid context or context.Id is null");
//                return false;
//            }
//            var contextId = context.Id; // 로컬 변수에 저장

//            // 핵심 수정: VideoView를 MediaPlayer에 할당
//            if (videoView != null)
//            {
//                UpdateState(context.Id!, PlaybackState.Connecting);
//                _log?.Info($"[RtspStreamingService] Creating media for {context.Id}: {url}");

//                // Media 생성 및 옵션 설정
//                var media = CreateOptimizedMedia(url, context.Options!);
//                context.SetMedia(media);

//                if (context.MediaPlayer == null)
//                {
//                    _log?.Error($"[RtspStreamingService] MediaPlayer is null for {context.Id}");
//                    UpdateState(context.Id, PlaybackState.Error);
//                    return false;
//                }

//                // UI 스레드에서 실행 (완료 대기)
//                var tcs = new TaskCompletionSource<bool>();
//                await DispatcherService.BeginInvoke(() =>
//                {
//                    try
//                    {
//                        // 기존 MediaPlayer 해제
//                        if (videoView.MediaPlayer != null)
//                        {
//                            _log?.Info($"[RtspStreamingService] Clearing existing MediaPlayer from VideoView for {context.Id}");
//                            videoView.MediaPlayer = null;
//                        }

//                        // 새 MediaPlayer 할당
//                        videoView.MediaPlayer = context.MediaPlayer;
//                        _log?.Info($"[RtspStreamingService] MediaPlayer assigned to VideoView for {context.Id}");
//                        tcs.SetResult(true);
//                    }
//                    catch (Exception ex)
//                    {
//                        _log?.Error($"[RtspStreamingService] Failed to assign MediaPlayer to VideoView: {ex.Message}");
//                        tcs.SetException(ex);
//                    }

//                });

//                // UI 작업 완료 대기
//                await tcs.Task;

//                if (videoView.MediaPlayer != context.MediaPlayer)
//                {
//                    _log?.Error($"[RtspStreamingService] MediaPlayer assignment verification failed for {context.Id}");
//                    UpdateState(context.Id!, PlaybackState.Error);
//                    return false;
//                }
//            }
//            else
//            {
//                _log?.Warning($"[RtspStreamingService] VideoView is null for {context.Id}. The video may appear in a separate window.");
//            }

//            // 이벤트 핸들러 등록 (중복 등록 방지)
//            RegisterEventHandlers(context);   // 새로 등록

//            // 재생 시작
//            var result = context.MediaPlayer.Play();

//            if (result)
//            {
//                _log?.Info($"[RtspStreamingService] Stream {contextId} started successfully");
//                context.LastConnectionTime = DateTime.Now;
//                context.ReconnectAttempts = 0;
//            }
//            else
//            {
//                _log?.Error($"[RtspStreamingService] Failed to start stream {contextId}");
//                UnregisterEventHandlers(context);
//                UpdateState(contextId, PlaybackState.Error);
//            }

//            return result;
//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] ConnectInternal error for {context.Id}: {ex.Message}");
//            UpdateState(context.Id!, PlaybackState.Error);
//            throw;
//        }
       
//    }

//    public async Task DisconnectAsync(string contextId)
//    {
//        try
//        {
//            _log?.Info($"[RtspStreamingService] Disconnecting stream: {contextId}");
//            await CleanupContextAsync(contextId);
//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] Disconnect error for {contextId}: {ex.Message}");
//        }
//    }

//    public async Task DisconnectAllAsync()
//    {
//        _log?.Info("[RtspStreamingService] Disconnecting all streams");

//        var tasks = _contexts.Keys.Select(DisconnectAsync).ToArray();
//        await Task.WhenAll(tasks);

//        _log?.Info("[RtspStreamingService] All streams disconnected");
//    }

//    public void Play(string contextId)
//    {
//        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
//        {
//            context.MediaPlayer.Play();
//            UpdateState(contextId, PlaybackState.Playing);
//            _log?.Info($"[RtspStreamingService] Stream {contextId} playing");
//        }
//    }

//    public void Pause(string contextId)
//    {
//        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
//        {
//            context.MediaPlayer.SetPause(true);
//            UpdateState(contextId, PlaybackState.Paused);
//            _log?.Info($"[RtspStreamingService] Stream {contextId} paused");
//        }
//    }

//    public void Stop(string contextId)
//    {
//        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
//        {
//            context.MediaPlayer.Stop();
//            UpdateState(contextId, PlaybackState.Stopped);
//            _log?.Info($"[RtspStreamingService] Stream {contextId} stopped");
//        }
//    }

//    public void SetVolume(string contextId, int volume)
//    {
//        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
//        {
//            var clampedVolume = Math.Clamp(volume, 0, 100);
//            context.MediaPlayer.Volume = clampedVolume;
//            context.Options.Volume = clampedVolume;
//            _log?.Info($"[RtspStreamingService] Volume set to {clampedVolume} for {contextId}");
//        }
//    }

//    public void ToggleMute(string contextId)
//    {
//        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
//        {
//            context.MediaPlayer.Mute = !context.MediaPlayer.Mute;
//            context.Options.IsMuted = context.MediaPlayer.Mute;
//            _log?.Info($"[RtspStreamingService] Mute toggled for {contextId}: {context.MediaPlayer.Mute}");
//        }
//    }

//    public MediaPlayer GetMediaPlayer(string contextId)
//    {
//        return TryGetContext(contextId, out var context) ? context.MediaPlayer : null;
//    }

//    public PlaybackState GetPlaybackState(string contextId)
//    {
//        return TryGetContext(contextId, out var context) ? context.State : PlaybackState.None;
//    }

//    public StreamingStatistics GetStatistics(string contextId)
//    {
//        return TryGetContext(contextId, out var context) ? context.Statistics : null;
//    }

//    public bool IsConnected(string contextId)
//    {
//        return TryGetContext(contextId, out var context) &&
//               context.State == PlaybackState.Playing;
//    }

//    public async Task<bool> TakeSnapshotAsync(string contextId, string filePath)
//    {
//        try
//        {
//            if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
//            {
//                _log?.Info($"[RtspStreamingService] Taking snapshot for {contextId}: {filePath}");
//                return await Task.Run(() => context.MediaPlayer.TakeSnapshot(0, filePath, 0, 0));
//            }
//            return false;
//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] Snapshot failed for {contextId}: {ex.Message}");
//            return false;
//        }
//    }

//    public void SetFrameSkip(string contextId, int skipFrames)
//    {
//        if (TryGetContext(contextId, out var context))
//        {
//            context.FrameSkipCount = Math.Max(0, skipFrames);
//            _log?.Info($"[RtspStreamingService] Frame skip set to {skipFrames} for {contextId}");
//        }
//    }

//    public void SetQuality(string contextId, int quality)
//    {
//        if (TryGetContext(contextId, out var context))
//        {
//            context.Options.VideoQuality = Math.Clamp(quality, 0, 100);
//            _log?.Info($"[RtspStreamingService] Quality set to {quality} for {contextId}");
//        }
//    }

//    #endregion

//    #region - Events -

//    public event EventHandler<StreamingStateChangedEventArgs> StateChanged;
//    public event EventHandler<StreamingErrorEventArgs> ErrorOccurred;
//    public event EventHandler<StreamingProgressEventArgs> ProgressUpdated;

//    #endregion

//    #region - Private Methods -

//    private void InitializeLibVLC()
//    {
//        try
//        {
//            _libVLC = LibVLCInitializer.Initialize(_setupModel);
//            _log?.Info("[RtspStreamingService] LibVLC initialized successfully");
//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] LibVLC initialization failed: {ex.Message}");
//            throw;
//        }
//    }

//    private Media CreateOptimizedMedia(string url, StreamingOptions options)
//    {
//        var media = new Media(_libVLC, url, FromType.FromLocation);

//        // 네트워크 캐싱 (적응형)
//        var caching = CalculateOptimalCaching(options);
//        media.AddOption($":network-caching={caching}");
//        media.AddOption($":live-caching={caching}");

//        // 프로토콜 설정
//        if (options.UseTcp)
//        {
//            media.AddOption(":rtsp-tcp");
//        }
//        else
//        {
//            media.AddOption(":rtsp-udp");
//            media.AddOption(":rtp-max-misorder=100");
//        }

//        // 버퍼 설정
//        media.AddOption($":rtsp-frame-buffer-size={options.FrameBufferSize}");

//        // 하드웨어 가속
//        if (options.UseHardwareAcceleration)
//        {
//            media.AddOption(":avcodec-hw=any");
//            media.AddOption(":avcodec-threads=0"); // 자동 스레드 수
//        }

//        // 성능 최적화 옵션
//        if (options.AllowFrameSkip)
//        {
//            media.AddOption(":avcodec-fast");
//            media.AddOption(":avcodec-skip-frame=1"); // Non-ref frames skip
//            media.AddOption(":avcodec-skip-idct=1");
//            media.AddOption(":avcodec-hurry-up");
//        }

//        // 타임아웃 설정
//        media.AddOption($":ipv4-timeout={options.ConnectionTimeoutSeconds * 1000}");

//        // 로깅 레벨
//        if (options.EnableDebugLogging || _setupModel.EnableDebugLogging)
//        {
//            media.AddOption(":verbose=2");
//        }

//        return media;
//    }

//    private int CalculateOptimalCaching(StreamingOptions options)
//    {
//        var baseCaching = options.NetworkCaching;

//        // TCP는 더 많은 버퍼링 필요
//        if (options.UseTcp)
//        {
//            baseCaching = (int)(baseCaching * 1.5);
//        }

//        return Math.Min(baseCaching, 2000); // 최대 2초
//    }

//    private void RegisterEventHandlers(StreamingContext context)
//    {
//        if (context?.MediaPlayer == null)
//        {
//            _log?.Warning($"[RtspStreamingService] Cannot register handlers - MediaPlayer is null for {context?.Id}");
//            return;
//        }

//        try
//        {
//            // 이미 핸들러가 등록되어 있으면 먼저 제거
//            if (context.EventHandlers != null && context.EventHandlers.Count > 0)
//            {
//                UnregisterEventHandlers(context);
//            }

//            // WeakReference를 사용하여 서비스 참조
//            var weakThis = new WeakReference<RtspStreamingService>(this);
//            var contextId = context.Id; // 로컬 변수로 캡처

//            // Playing 핸들러
//            EventHandler<EventArgs> playingHandler = (s, e) =>
//            {
//                if (weakThis.TryGetTarget(out var service))
//                {
//                    service.OnStreamPlaying(context);
//                }
//            };

//            // Buffering 핸들러
//            EventHandler<MediaPlayerBufferingEventArgs> bufferingHandler = (s, e) =>
//            {
//                if (weakThis.TryGetTarget(out var service))
//                {
//                    service.OnStreamBuffering(context, e.Cache);
//                }
//            };

//            // Error 핸들러
//            EventHandler<EventArgs> errorHandler = (s, e) =>
//            {
//                if (weakThis.TryGetTarget(out var service))
//                {
//                    service.OnStreamError(context);
//                }
//            };

//            // EndReached 핸들러
//            EventHandler<EventArgs> endReachedHandler = (s, e) =>
//            {
//                if (weakThis.TryGetTarget(out var service))
//                {
//                    service.OnStreamEnded(context);
//                }
//            };

//            // TimeChanged 핸들러 (통계 업데이트)
//            EventHandler<MediaPlayerTimeChangedEventArgs> timeChangedHandler = (s, e) =>
//            {
//                if (weakThis.TryGetTarget(out var service))
//                {
//                    service.UpdateStatistics(context);
//                }
//            };

//            // 핸들러를 딕셔너리에 저장
//            context.EventHandlers = new Dictionary<string, Delegate>
//            {
//                ["Playing"] = playingHandler,
//                ["Buffering"] = bufferingHandler,
//                ["EncounteredError"] = errorHandler,
//                ["EndReached"] = endReachedHandler,
//                ["TimeChanged"] = timeChangedHandler
//            };

//            // 이벤트에 핸들러 등록
//            context.MediaPlayer.Playing += playingHandler;
//            context.MediaPlayer.Buffering += bufferingHandler;
//            context.MediaPlayer.EncounteredError += errorHandler;
//            context.MediaPlayer.EndReached += endReachedHandler;
//            context.MediaPlayer.TimeChanged += timeChangedHandler;

//            _log?.Info($"[RtspStreamingService] Event handlers registered for context: {contextId}");
//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] Failed to register event handlers for {context.Id}: {ex.Message}");
//        }
//    }

//    private void UnregisterEventHandlers(StreamingContext context)
//    {
//        if (context?.MediaPlayer == null || context.EventHandlers == null)
//        {
//            return;
//        }

//        try
//        {
//            // Playing 핸들러 제거
//            if (context.EventHandlers.TryGetValue("Playing", out var playingHandler))
//            {
//                context.MediaPlayer.Playing -= (EventHandler<EventArgs>)playingHandler;
//            }

//            // Buffering 핸들러 제거
//            if (context.EventHandlers.TryGetValue("Buffering", out var bufferingHandler))
//            {
//                context.MediaPlayer.Buffering -= (EventHandler<MediaPlayerBufferingEventArgs>)bufferingHandler;
//            }

//            // EncounteredError 핸들러 제거
//            if (context.EventHandlers.TryGetValue("EncounteredError", out var errorHandler))
//            {
//                context.MediaPlayer.EncounteredError -= (EventHandler<EventArgs>)errorHandler;
//            }

//            // EndReached 핸들러 제거
//            if (context.EventHandlers.TryGetValue("EndReached", out var endReachedHandler))
//            {
//                context.MediaPlayer.EndReached -= (EventHandler<EventArgs>)endReachedHandler;
//            }

//            // TimeChanged 핸들러 제거
//            if (context.EventHandlers.TryGetValue("TimeChanged", out var timeChangedHandler))
//            {
//                context.MediaPlayer.TimeChanged -= (EventHandler<MediaPlayerTimeChangedEventArgs>)timeChangedHandler;
//            }

//            // 딕셔너리 클리어
//            context.EventHandlers.Clear();

//            _log?.Info($"[RtspStreamingService] Event handlers unregistered for context: {context.Id}");
//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] Failed to unregister event handlers for {context.Id}: {ex.Message}");
//        }
//    }

//    private void OnStreamPlaying(StreamingContext context)
//    {
//        context.State = PlaybackState.Playing;
//        context.LastConnectionTime = DateTime.Now;
//        context.ReconnectAttempts = 0;
//        UpdateState(context.Id, PlaybackState.Playing);
//        _log?.Info($"[RtspStreamingService] Stream {context.Id} is playing");
//    }

//    private void OnStreamBuffering(StreamingContext context, float cache)
//    {
//        if (cache < 100)
//        {
//            context.State = PlaybackState.Buffering;
//            _log?.Info($"[RtspStreamingService] Stream {context.Id} buffering: {cache:F1}%");
//        }
//        else
//        {
//            context.State = PlaybackState.Playing;
//        }
//        UpdateState(context.Id, context.State);

//        // Progress 이벤트 발생
//        var progressArgs = new StreamingProgressEventArgs(
//            context.Id,
//            cache,
//            TimeSpan.Zero,
//            TimeSpan.Zero);
//        ProgressUpdated?.Invoke(this, progressArgs);
//    }

//    private void OnStreamError(StreamingContext context)
//    {
//        context.State = PlaybackState.Error;
//        context.Statistics.ErrorCount++;
//        UpdateState(context.Id, PlaybackState.Error);
//        _log?.Error($"[RtspStreamingService] Stream {context.Id} encountered error");

//        if (context.Options.EnableAutoReconnect && context.ReconnectAttempts < context.Options.MaxReconnectAttempts)
//        {
//            _ = Task.Run(() => HandleReconnectAsync(context.Id));
//        }
//    }

//    private void OnStreamEnded(StreamingContext context)
//    {
//        context.State = PlaybackState.Stopped;
//        UpdateState(context.Id, PlaybackState.Stopped);
//        _log?.Info($"[RtspStreamingService] Stream {context.Id} ended");
//    }

//    private void UpdateStatistics(StreamingContext context)
//    {
//        if (context?.MediaPlayer == null || context.Statistics == null)
//            return;

//        // 통계 업데이트
//        context.Statistics.TotalPlayTime = DateTime.Now - context.Statistics.StartTime;

//        // 메모리 사용량
//        context.Statistics.MemoryUsageBytes = GC.GetTotalMemory(false);
//    }

//    private async Task HandleReconnectAsync(string contextId)
//    {
//        if (!TryGetContext(contextId, out var context))
//            return;

//        context.ReconnectAttempts++;
//        context.Statistics.ReconnectCount++;
//        UpdateState(contextId, PlaybackState.Reconnecting);

//        _log?.Info($"[RtspStreamingService] Reconnect attempt {context.ReconnectAttempts}/{context.Options.MaxReconnectAttempts} for {contextId}");

//        // Exponential backoff
//        var delay = TimeSpan.FromSeconds(Math.Pow(2, context.ReconnectAttempts - 1) * context.Options.ReconnectDelaySeconds);
//        await Task.Delay(delay);

//        await ConnectAsync(contextId, context.ConnectionInfo, context.Options);
//    }

//    private bool TryGetContext(string contextId, out StreamingContext context)
//    {
//        // 강한 참조에서 먼저 찾기
//        if (_contexts.TryGetValue(contextId, out context))
//            return true;

//        // Weak Reference에서 복구 시도
//        if (_weakContexts.TryGetValue(contextId, out var weakRef) &&
//            weakRef.TryGetTarget(out context))
//        {
//            // 강한 참조로 복구
//            _contexts.TryAdd(contextId, context);
//            return true;
//        }

//        context = null;
//        return false;
//    }

//    private async Task CleanupContextAsync(string contextId)
//    {
//        if (string.IsNullOrEmpty(contextId))
//        {
//            _log?.Warning("[RtspStreamingService] CleanupContextAsync called with null/empty contextId");
//            return;
//        }

//        try
//        {
//            _log?.Info($"[RtspStreamingService] Starting cleanup for context: {contextId}");

//            if (!_contexts.TryRemove(contextId, out var context))
//            {
//                _log?.Warning($"[RtspStreamingService] Context not found for cleanup: {contextId}");
//                return;
//            }

//            UpdateState(contextId, PlaybackState.Disconnected);

//            var cleanupTask = Task.Run(async () =>
//            {
//                try
//                {
//                    if (context.MediaPlayer != null)
//                    {
//                        if (context.MediaPlayer != null)
//                        {
//                            // MediaPlayer 정지
//                            if (context.MediaPlayer.IsPlaying)
//                            {
//                                context.MediaPlayer.Stop();
//                                await Task.Delay(50); // 정지 완료 대기
//                            }

//                            // 이벤트 핸들러 제거
//                            UnregisterEventHandlers(context);
//                        }

//                        // Context 정리
//                        context.Cleanup();

//                        // Pool 반환
//                        _contextPool.Return(context);

//                        _log?.Info($"[RtspStreamingService] Context {contextId} cleaned up");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _log?.Error($"[RtspStreamingService] Cleanup error for {contextId}: {ex.Message}");
//                }
//            });

//            // Cleanup 완료 대기 (최대 5초)
//            try
//            {
//                await cleanupTask.WaitAsync(TimeSpan.FromSeconds(5));
//            }
//            catch (TimeoutException)
//            {
//                _log?.Warning($"[RtspStreamingService] Cleanup timeout for {contextId}");
//            }

//            // 모든 정리가 완료된 후에만 Dictionary에서 제거
//            _contexts.TryRemove(contextId, out _);
//            _weakContexts.TryRemove(contextId, out _);

//            _log?.Info($"[RtspStreamingService] Context {contextId} cleanup completed");

//        }
//        catch (Exception ex)
//        {
//            _log?.Error($"[RtspStreamingService] CleanupContextAsync error for {contextId}: {ex.Message}");
//        }
//    }

//    private void UpdateState(string contextId, PlaybackState newState)
//    {
//        if (TryGetContext(contextId, out var context))
//        {
//            var oldState = context.State;
//            context.State = newState;

//            StateChanged?.Invoke(this, new StreamingStateChangedEventArgs(contextId, oldState, newState));
//            _log?.Info($"[RtspStreamingService] State changed for {contextId}: {oldState} -> {newState}");
//        }
//    }

//    private void OnErrorOccurred(string contextId, string errorMessage, ErrorSeverity severity)
//    {
//        var args = new StreamingErrorEventArgs(errorMessage, null, contextId, severity);
//        ErrorOccurred?.Invoke(this, args);

//        switch (severity)
//        {
//            case ErrorSeverity.Warning:
//                _log?.Warning($"[RtspStreamingService] {contextId}: {errorMessage}");
//                break;
//            case ErrorSeverity.Error:
//                _log?.Error($"[RtspStreamingService] {contextId}: {errorMessage}");
//                break;
//            case ErrorSeverity.Critical:
//                _log?.Error($"[RtspStreamingService] {contextId}: {errorMessage}");
//                break;
//            default:
//                _log?.Info($"[RtspStreamingService] {contextId}: {errorMessage}");
//                break;
//        }
//    }

//    private void CheckMemoryPressure(object state)
//    {
//        var memoryInfo = GC.GetTotalMemory(false);

//        if (memoryInfo > _setupModel.MaxMemoryUsageBytes)
//        {
//            _log?.Warning($"[RtspStreamingService] Memory pressure detected: {memoryInfo / 1024 / 1024}MB / {_setupModel.MaxMemoryUsageBytes / 1024 / 1024}MB");

//            // 강제 GC
//            GC.Collect(2, GCCollectionMode.Forced);
//            GC.WaitForPendingFinalizers();
//            GC.Collect();

//            // Playing 상태가 아닌 컨텍스트를 Weak Reference로 이동
//            foreach (var kvp in _contexts.ToList())
//            {
//                if (kvp.Value.State != PlaybackState.Playing &&
//                    kvp.Value.State != PlaybackState.Buffering)
//                {
//                    _weakContexts[kvp.Key] = new WeakReference<StreamingContext>(kvp.Value);
//                    _contexts.TryRemove(kvp.Key, out _);
//                }
//            }

//            _log?.Info($"[RtspStreamingService] Memory cleanup completed. New size: {GC.GetTotalMemory(false) / 1024 / 1024}MB");
//        }
//    }

//    private AsyncRetryPolicy CreateRetryPolicy()
//    {
//        return Policy
//            .Handle<Exception>()
//            .WaitAndRetryAsync(
//                _setupModel.MaxRetryAttempts,
//                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
//                (exception, timeSpan, retryCount, context) =>
//                {
//                    _log?.Warning($"[RtspStreamingService] Retry {retryCount}/{_setupModel.MaxRetryAttempts} after {timeSpan.TotalSeconds:F1}s: {exception.Message}");
//                });
//    }

//    #endregion

//    #region - Dispose -

//    public void Dispose()
//    {
//        Dispose(true);
//        GC.SuppressFinalize(this);
//    }

//    protected virtual void Dispose(bool disposing)
//    {
//        if (_disposed)
//            return;

//        if (disposing)
//        {
//            _log?.Info("[RtspStreamingService] Disposing service...");

//            // 메모리 모니터 타이머 정지
//            _memoryMonitorTimer?.Dispose();

//            // 모든 컨텍스트 정리
//            var disconnectTask = DisconnectAllAsync();
//            disconnectTask.Wait(TimeSpan.FromSeconds(10));

//            // Context Locks 정리
//            foreach (var lockItem in _contextLocks)
//            {
//                lockItem.Value?.Dispose();
//            }
//            _contextLocks.Clear();

//            // Pool 정리
//            _contextPool?.Clear();

//            // LibVLC 정리
//            _libVLC?.Dispose();

//            _log?.Info("[RtspStreamingService] Service disposed");
//        }

//        _disposed = true;
//    }

   
//    ~RtspStreamingService()
//    {
//        Dispose(false);
//    }

//    #endregion

//    #region - Properties -
//    // 이벤트 핸들러 저장용 딕셔너리
//    public Dictionary<string, Delegate> EventHandlers { get; private set; }
//    #endregion
//    #region - Attributes -

//    private readonly ILogService _log;
//    private readonly StreamingSetupModel _setupModel;
//    private readonly IStreamingContextPool _contextPool;
//    private readonly ConcurrentDictionary<string, StreamingContext> _contexts;
//    private readonly ConcurrentDictionary<string, WeakReference<StreamingContext>> _weakContexts;
//    private readonly ConcurrentDictionary<string, SemaphoreSlim> _contextLocks = new();

//    private readonly AsyncRetryPolicy _retryPolicy;
//    private readonly ArrayPool<byte> _memoryPool;
//    private readonly Timer _memoryMonitorTimer;
//    private LibVLC _libVLC;
//    private bool _disposed;

//    #endregion
//}