using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Controls;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Helpers;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Polly;
using Polly.Retry;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;

/// <summary>
/// 개선된 RTSP 스트리밍 서비스 - RtspPlayer 통합 관리
/// </summary>
public class ImprovedRtspStreamingService : IPlayerRegistry, IImprovedRtspStreamingService
{
    #region - Ctors -
    public ImprovedRtspStreamingService(
        ILogService log,
        StreamingSetupModel setupModel,
        IStreamingContextPool contextPool)
    {
        _log = log;
        _setupModel = setupModel;
        _contextPool = contextPool;
        _contexts = new ConcurrentDictionary<string, ImprovedStreamingContext>();
        _players = new ConcurrentDictionary<string, ImprovedRtspPlayer>();
        _weakContexts = new ConcurrentDictionary<string, WeakReference<ImprovedStreamingContext>>();

        _disposed = false;

        // 재시도 정책 생성
        _retryPolicy = CreateRetryPolicy();

        // 메모리 모니터링 타이머
        _memoryMonitorTimer = new Timer(CheckMemoryPressure, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _operationTimer = new Timer(CheckTimeouts, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        // 이벤트 핸들러 딕셔너리 초기화
        EventHandlers = new Dictionary<string, Delegate>();

        // LibVLC 초기화
        InitializeLibVLC();

        _log?.Info($"[ImprovedRtspStreamingService] Service initialized - MaxConnections: {_setupModel.MaxConnections}");
    }

    
    #endregion

    #region - IPlayerRegistry Implementation -

    public bool RegisterPlayer(string contextId, ImprovedRtspPlayer player)
    {
        if (string.IsNullOrEmpty(contextId) || player == null)
        {
            _log?.Error("[ImprovedRtspStreamingService] Invalid parameters for RegisterPlayer");
            return false;
        }

        if (_players.TryAdd(contextId, player))
        {
            _log?.Info($"[ImprovedRtspStreamingService] Player registered: {contextId}");

            // 이미 존재하는 Context가 있으면 연결
            if (_contexts.TryGetValue(contextId, out var context))
            {
                context.AttachPlayer(player);
                // Player 상태 동기화
                context.UpdatePlayerState(context.State);
            }

            return true;
        }

        _log?.Warning($"[ImprovedRtspStreamingService] Player already registered: {contextId}");
        return false;
    }

    public bool UnregisterPlayer(string contextId)
    {
        if (_players.TryRemove(contextId, out var player))
        {
            _log?.Info($"[ImprovedRtspStreamingService] Player unregistered: {contextId}");

            // Context에서도 분리
            if (_contexts.TryGetValue(contextId, out var context))
            {
                context.DetachPlayer();
            }

            return true;
        }

        return false;
    }

    public ImprovedRtspPlayer GetPlayer(string contextId)
    {
        _players.TryGetValue(contextId, out var player);
        return player;
    }

    public bool HasPlayer(string contextId)
    {
        return _players.ContainsKey(contextId);
    }

    public void ClearAllPlayers()
    {
        foreach (var contextId in _players.Keys)
        {
            UnregisterPlayer(contextId);
        }
        _players.Clear();
    }

    #endregion

    #region - IRtspStreamingService Implementation -

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
        StreamingOptions? options = null,
        VideoView? videoView = null)
    {
        if (_disposed)
        {
            _log?.Warning("[ImprovedRtspStreamingService] Service is disposed");
            return false;
        }

        if (string.IsNullOrEmpty(contextId))
        {
            _log?.Error("[ImprovedRtspStreamingService] Invalid contextId");
            return false;
        }

        // Context별 Lock 획득
        var contextLock = _contextLocks.GetOrAdd(contextId, _ => new SemaphoreSlim(1, 1));
        await contextLock.WaitAsync();

        try
        {

            if (_setupModel.IsAutoDiscard)
            {
                // 연결 시작 시간 기록
                _connectionStartTimes[contextId] = DateTime.Now;
            }


            _log?.Info($"[ImprovedRtspStreamingService] Connecting stream: {contextId} to {connectionInfo?.IpAddress}");

            // RtspPlayer 가져오기
            ImprovedRtspPlayer rtspPlayer = GetPlayer(contextId);

            // 최대 연결 수 체크
            if (_contexts.Count >= _setupModel.MaxConnections)
            {
                _log?.Warning($"[ImprovedRtspStreamingService] Max connections reached: {_setupModel.MaxConnections}");
                OnErrorOccurred(contextId, "Maximum connections limit reached", ErrorSeverity.Warning);
                return false;
            }

            ImprovedStreamingContext? context;
            TryGetContext(contextId, out context);

            if(context != null && context.State == PlaybackState.Reconnecting)
            {
                // URL 유효성 검사
                var url = connectionInfo.GetFullUrl();
                if (!RtspUrlValidator.IsValidUrl(url))
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Invalid RTSP URL: {url}");
                    await CleanupContextAsync(contextId);
                    OnErrorOccurred(contextId, "Invalid RTSP URL", ErrorSeverity.Error);
                    return false;
                }

                // Player 상태 업데이트
                context.UpdatePlayerState(PlaybackState.Connecting, "Connecting...");

                // 재시도 정책으로 연결
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await ConnectInternalAsync(context, url, videoView);
                });
            }
            else
            {
                if (_contexts.ContainsKey(contextId))
                {
                    _log?.Warning($"[ImprovedRtspStreamingService] Context already exists, cleaning up: {contextId}");
                    await CleanupContextAsync(contextId);

                    // 정리 완료 대기
                    var verifyWait = 10;
                    while ((_contexts.ContainsKey(contextId) ||
                            _weakContexts.ContainsKey(contextId)) && verifyWait-- > 0)
                    {
                        await Task.Delay(100);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }

                    if (_contexts.ContainsKey(contextId))
                    {
                        _contexts.TryRemove(contextId, out _);
                        _weakContexts.TryRemove(contextId, out _);
                        _log?.Warning($"[ImprovedRtspStreamingService] Force removed lingering context: {contextId}");
                    }
                }

                // 새 컨텍스트 생성 (Pool에서 가져오기)
                context = _contextPool.Get() ?? new ImprovedStreamingContext();

                // Context 초기화 - RtspPlayer 포함
                context.Initialize(contextId, connectionInfo, options ?? new StreamingOptions(), _libVLC, rtspPlayer);

                // Context 유효성 확인
                if (string.IsNullOrEmpty(context.Id))
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Context initialization failed - Id is null");
                    context.Cleanup();
                    _contextPool.Return(context);
                    return false;
                }

                // Context 추가
                if (!_contexts.TryAdd(contextId, context))
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Failed to add context: {contextId}");
                    context.Cleanup();
                    _contextPool.Return(context);
                    return false;
                }

                // Weak Reference 추가
                _weakContexts[contextId] = new WeakReference<ImprovedStreamingContext>(context);

                // URL 유효성 검사
                var url = connectionInfo.GetFullUrl();
                if (!RtspUrlValidator.IsValidUrl(url))
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Invalid RTSP URL: {url}");
                    await CleanupContextAsync(contextId);
                    OnErrorOccurred(contextId, "Invalid RTSP URL", ErrorSeverity.Error);
                    return false;
                }
                // Player 상태 업데이트
                context.UpdatePlayerState(PlaybackState.Connecting, "Connecting...");

                // 재시도 정책으로 연결
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await ConnectInternalAsync(context, url, videoView);
                });
            }   
            
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] Connect failed for {contextId}: {ex.Message}");
            OnErrorOccurred(contextId, ex.Message, ErrorSeverity.Error);
            if (!string.IsNullOrEmpty(contextId))
            {
                await CleanupContextAsync(contextId);
            }
            return false;
        }
        finally
        {
            contextLock.Release();
        }
    }


    private async Task<bool> ConnectInternalAsync(ImprovedStreamingContext context, string url, VideoView videoViewParam)
    {
        try
        {
            if (context == null || string.IsNullOrEmpty(context.Id))
            {
                _log?.Error($"[ImprovedRtspStreamingService] Invalid context");
                return false;
            }

            var contextId = context.Id;
            _log?.Info($"[ImprovedRtspStreamingService] Creating media for {contextId}: {url}");

            // Media 생성 및 옵션 설정
            var media = CreateOptimizedMedia(url, context.Options!);
            context.SetMedia(media);

            if (context.MediaPlayer == null)
            {
                _log?.Error($"[ImprovedRtspStreamingService] MediaPlayer is null for {contextId}");
                context.UpdatePlayerState(PlaybackState.Error, "MediaPlayer creation failed");
                return false;
            }

            // VideoView 할당 부분을 UI 스레드에서 처리
            VideoView targetVideoView = null;

            // Player에서 VideoView 가져오기 (UI 스레드 필요)
            if (context.Player != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    targetVideoView = context.Player.GetVideoView();
                });
            }
            else if (videoViewParam != null)
            {
                targetVideoView = videoViewParam;
            }

            // VideoView MediaPlayer 설정 (UI 스레드에서만)
            if (targetVideoView != null)
            {
                _log?.Info($"[ImprovedRtspStreamingService] Assigning MediaPlayer to VideoView for {contextId}");

                var assignSuccess = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // 🟢 UI 스레드에서 안전하게 접근
                        if (targetVideoView.MediaPlayer != null)
                        {
                            _log?.Info($"[ImprovedRtspStreamingService] Clearing existing MediaPlayer");
                            targetVideoView.MediaPlayer = null;
                        }

                        targetVideoView.MediaPlayer = context.MediaPlayer;
                        _log?.Info($"[ImprovedRtspStreamingService] MediaPlayer assigned");

                        return targetVideoView.MediaPlayer == context.MediaPlayer;
                    }
                    catch (Exception ex)
                    {
                        _log?.Error($"[ImprovedRtspStreamingService] MediaPlayer assignment failed: {ex.Message}");
                        return false;
                    }
                });

                if (!assignSuccess)
                {
                    _log?.Error($"[ImprovedRtspStreamingService] MediaPlayer assignment verification failed");
                    context.UpdatePlayerState(PlaybackState.Error, "Failed to assign MediaPlayer");
                    return false;
                }
            }
            else
            {
                _log?.Warning($"[ImprovedRtspStreamingService] No VideoView available for {contextId}");
            }

            // 이벤트 핸들러 등록 (백그라운드 스레드 OK)
            RegisterEventHandlers(context);

            // 재생 시작 (백그라운드 스레드 OK)
            var result = context.MediaPlayer.Play();

            if (result)
            {
                _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} started successfully");
                context.LastConnectionTime = DateTime.Now;
            }
            else
            {
                _log?.Error($"[ImprovedRtspStreamingService] Failed to start stream {contextId}");
                UnregisterEventHandlers(context);
                context.UpdatePlayerState(PlaybackState.Error, "Failed to start stream");
            }

            return result;
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] ConnectInternal error: {ex.Message}");
            context.UpdatePlayerState(PlaybackState.Error, $"Error: {ex.Message}");
            throw;
        }
    }

    #region Deprecated
    //private async Task<bool> ConnectInternalAsync(
    //    ImprovedStreamingContext context,
    //    string url,
    //    VideoView videoViewParam)
    //{
    //    try
    //    {
    //        if (context == null || string.IsNullOrEmpty(context.Id))
    //        {
    //            _log?.Error($"[ImprovedRtspStreamingService] Invalid context or context.Id is null");
    //            return false;
    //        }

    //        var contextId = context.Id;

    //        _log?.Info($"[ImprovedRtspStreamingService] Creating media for {contextId}: {url}");

    //        // Media 생성 및 옵션 설정
    //        var media = CreateOptimizedMedia(url, context.Options!);
    //        context.SetMedia(media);

    //        if (context.MediaPlayer == null)
    //        {
    //            _log?.Error($"[ImprovedRtspStreamingService] MediaPlayer is null for {contextId}");
    //            context.UpdatePlayerState(PlaybackState.Error, "MediaPlayer creation failed");
    //            return false;
    //        }

    //        // VideoView 할당 (Player가 있으면 Player의 VideoView 사용, 없으면 파라미터 사용)
    //        VideoView targetVideoView = null;

    //        if (context.Player != null)
    //        {
    //            // Player에서 VideoView 가져오기 (UI 스레드에서 실행)
    //            var tcs = new TaskCompletionSource<VideoView>();
    //            await DispatcherService.BeginInvoke(() =>
    //            {
    //                try
    //                {
    //                    targetVideoView = context.Player.GetVideoView();
    //                    tcs.SetResult(targetVideoView);
    //                }
    //                catch (Exception ex)
    //                {
    //                    _log?.Error($"[ImprovedRtspStreamingService] Failed to get VideoView from Player: {ex.Message}");
    //                    tcs.SetException(ex);
    //                }
    //            });

    //            try
    //            {
    //                targetVideoView = await tcs.Task;
    //            }
    //            catch
    //            {
    //                targetVideoView = null;
    //            }
    //        }
    //        else if (videoViewParam != null)
    //        {
    //            targetVideoView = videoViewParam;
    //        }

    //        if (targetVideoView != null)
    //        {
    //            _log?.Info($"[ImprovedRtspStreamingService] Assigning MediaPlayer to VideoView for {contextId}");

    //            // UI 스레드에서 MediaPlayer 할당
    //            var assignTcs = new TaskCompletionSource<bool>();
    //            await DispatcherService.BeginInvoke(() =>
    //            {
    //                try
    //                {
    //                    // 기존 MediaPlayer 해제
    //                    if (targetVideoView.MediaPlayer != null)
    //                    {
    //                        _log?.Info($"[ImprovedRtspStreamingService] Clearing existing MediaPlayer for {contextId}");
    //                        targetVideoView.MediaPlayer = null;
    //                    }

    //                    // 새 MediaPlayer 할당
    //                    targetVideoView.MediaPlayer = context.MediaPlayer;
    //                    _log?.Info($"[ImprovedRtspStreamingService] MediaPlayer assigned to VideoView for {contextId}");
    //                    assignTcs.SetResult(true);
    //                }
    //                catch (Exception ex)
    //                {
    //                    _log?.Error($"[ImprovedRtspStreamingService] Failed to assign MediaPlayer: {ex.Message}");
    //                    assignTcs.SetException(ex);
    //                }
    //            });

    //            await assignTcs.Task;

    //            if (targetVideoView.MediaPlayer != context.MediaPlayer)
    //            {
    //                _log?.Error($"[ImprovedRtspStreamingService] MediaPlayer assignment verification failed for {contextId}");
    //                context.UpdatePlayerState(PlaybackState.Error, "Failed to assign MediaPlayer");
    //                return false;
    //            }
    //        }
    //        else
    //        {
    //            _log?.Warning($"[ImprovedRtspStreamingService] No VideoView available for {contextId}. Video may appear in separate window.");
    //        }

    //        // 이벤트 핸들러 등록
    //        RegisterEventHandlers(context);

    //        // 재생 시작
    //        var result = context.MediaPlayer.Play();

    //        if (result)
    //        {
    //            _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} started successfully");
    //            context.LastConnectionTime = DateTime.Now;
    //            context.ReconnectAttempts = 0;
    //        }
    //        else
    //        {
    //            _log?.Error($"[ImprovedRtspStreamingService] Failed to start stream {contextId}");
    //            UnregisterEventHandlers(context);
    //            context.UpdatePlayerState(PlaybackState.Error, "Failed to start stream");
    //        }

    //        return result;
    //    }
    //    catch (Exception ex)
    //    {
    //        _log?.Error($"[ImprovedRtspStreamingService] ConnectInternal error for {context.Id}: {ex.Message}");
    //        context.UpdatePlayerState(PlaybackState.Error, $"Error: {ex.Message}");
    //        throw;
    //    }
    //}
    #endregion

    public async Task DisconnectAsync(string contextId)
    {
        try
        {
            _log?.Info($"[ImprovedRtspStreamingService] Disconnecting stream: {contextId}");

            DisconnectPreparing(contextId);

            await CleanupContextAsync(contextId);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] Disconnect error for {contextId}: {ex.Message}");
        }
    }

    private void DisconnectPreparing(string contextId)
    {
        // 시작 시간 제거
        _connectionStartTimes.TryRemove(contextId, out _);

        // UI 초기화
        if (_players.TryGetValue(contextId, out var player))
        {
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                player.TimeoutDisplay = player.ContextId ?? "";
            });
        }

        // 재연결 플래그 해제
        if (_contexts.TryGetValue(contextId, out var context))
        {
            context.IsReconnecting = false;
        }
    }


    public async Task DisconnectAllAsync()
    {
        _log?.Info("[ImprovedRtspStreamingService] Disconnecting all streams");
        var tasks = _contexts.Keys.Select(DisconnectAsync).ToArray();
        await Task.WhenAll(tasks);
        _log?.Info("[ImprovedRtspStreamingService] All streams disconnected");
    }

    public void Play(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            context.MediaPlayer.Play();
            context.UpdatePlayerState(PlaybackState.Playing, "Playing");
            _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} playing");
        }
    }

    public void Pause(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            context.MediaPlayer.SetPause(true);
            context.UpdatePlayerState(PlaybackState.Paused, "Paused");
            _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} paused");
        }
    }

    public void Stop(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            if (context.MediaPlayer?.IsPlaying == true)
                context.MediaPlayer.Stop();
            
            context.UpdatePlayerState(PlaybackState.Stopped, "Stopped");
            _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} stopped");
        }
    }

    public void SetVolume(string contextId, int volume)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            var clampedVolume = Math.Clamp(volume, 0, 100);
            context.MediaPlayer.Volume = clampedVolume;
            context.Options.Volume = clampedVolume;
            _log?.Info($"[ImprovedRtspStreamingService] Volume set to {clampedVolume} for {contextId}");
        }
    }

    public void ToggleMute(string contextId)
    {
        if (TryGetContext(contextId, out var context) && context.MediaPlayer != null)
        {
            context.MediaPlayer.Mute = !context.MediaPlayer.Mute;
            context.Options.IsMuted = context.MediaPlayer.Mute;
            _log?.Info($"[ImprovedRtspStreamingService] Mute toggled for {contextId}: {context.MediaPlayer.Mute}");
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
                _log?.Info($"[ImprovedRtspStreamingService] Taking snapshot for {contextId}: {filePath}");
                return await Task.Run(() => context.MediaPlayer.TakeSnapshot(0, filePath, 0, 0));
            }
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] Snapshot failed for {contextId}: {ex.Message}");
            return false;
        }
    }

    public void SetFrameSkip(string contextId, int skipFrames)
    {
        if (TryGetContext(contextId, out var context))
        {
            context.FrameSkipCount = Math.Max(0, skipFrames);
            _log?.Info($"[ImprovedRtspStreamingService] Frame skip set to {skipFrames} for {contextId}");
        }
    }

    public void SetQuality(string contextId, int quality)
    {
        if (TryGetContext(contextId, out var context))
        {
            context.Options.VideoQuality = Math.Clamp(quality, 0, 100);
            _log?.Info($"[ImprovedRtspStreamingService] Quality set to {quality} for {contextId}");
        }
    }

    /// <summary>
    /// 스트림을 제한 상태로 변경
    /// </summary>
    public void RestrictStream(string contextId, string message = "Surveillance Not Allowed")
    {
        if (!TryGetContext(contextId, out var context))
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Context not found: {contextId}");
            return;
        }

        try
        {
            Stop(contextId);
            // 상태를 Restricted로 변경
            context.UpdatePlayerState(PlaybackState.Restricted, message);
            UpdateState(contextId, PlaybackState.Restricted);

            _log?.Info($"[ImprovedRtspStreamingService] Stream restricted: {contextId}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] RestrictStream error: {ex.Message}");
        }
    }

    /// <summary>
    /// 제한 해제
    /// </summary>
    public async Task UnrestrictStreamAsync(string contextId)
    {
        if (!TryGetContext(contextId, out var context))
            return;

        if (context.MediaPlayer != null && context.MediaPlayer?.IsPlaying == true)
        {
            return;
        }

        // 단순히 재연결
        await ConnectAsync(contextId, context.ConnectionInfo, context.Options);
    }

    #endregion

    #region - Events -

    public event EventHandler<StreamingStateChangedEventArgs>? StateChanged;
    public event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<StreamingProgressEventArgs>? ProgressUpdated;

    #endregion

    #region - Private Methods -

    private void InitializeLibVLC()
    {
        try
        {
            _libVLC = LibVLCInitializer.Initialize(_setupModel);
            _log?.Info("[ImprovedRtspStreamingService] LibVLC initialized successfully");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] LibVLC initialization failed: {ex.Message}");
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

    private void RegisterEventHandlers(ImprovedStreamingContext context)
    {
        if (context?.MediaPlayer == null)
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Cannot register handlers - MediaPlayer is null for {context?.Id}");
            return;
        }

        try
        {
            // 이미 핸들러가 등록되어 있으면 먼저 제거
            if (context.EventHandlers != null && context.EventHandlers.Count > 0)
            {
                UnregisterEventHandlers(context);
            }

            // WeakReference를 사용하여 서비스 참조
            var weakThis = new WeakReference<ImprovedRtspStreamingService>(this);
            var contextId = context.Id;

            // Playing 핸들러
            EventHandler<EventArgs> playingHandler = (s, e) =>
            {
                if (weakThis.TryGetTarget(out var service))
                {
                    service.OnStreamPlaying(context);
                }
            };

            // Buffering 핸들러
            EventHandler<MediaPlayerBufferingEventArgs> bufferingHandler = (s, e) =>
            {
                if (weakThis.TryGetTarget(out var service))
                {
                    service.OnStreamBuffering(context, e.Cache);
                }
            };

            // Error 핸들러
            EventHandler<EventArgs> errorHandler = (s, e) =>
            {
                if (weakThis.TryGetTarget(out var service))
                {
                    service.OnStreamError(context);
                }
            };

            // EndReached 핸들러
            EventHandler<EventArgs> endReachedHandler = (s, e) =>
            {
                if (weakThis.TryGetTarget(out var service))
                {
                    service.OnStreamEnded(context);
                }
            };

            // TimeChanged 핸들러 (통계 업데이트)
            EventHandler<MediaPlayerTimeChangedEventArgs> timeChangedHandler = (s, e) =>
            {
                if (weakThis.TryGetTarget(out var service))
                {
                    service.UpdateStatistics(context);
                }
            };

            // 핸들러를 딕셔너리에 저장
            context.EventHandlers = new Dictionary<string, Delegate>
            {
                ["Playing"] = playingHandler,
                ["Buffering"] = bufferingHandler,
                ["EncounteredError"] = errorHandler,
                ["EndReached"] = endReachedHandler,
                ["TimeChanged"] = timeChangedHandler
            };

            // 이벤트에 핸들러 등록
            context.MediaPlayer.Playing += playingHandler;
            context.MediaPlayer.Buffering += bufferingHandler;
            context.MediaPlayer.EncounteredError += errorHandler;
            context.MediaPlayer.EndReached += endReachedHandler;
            context.MediaPlayer.TimeChanged += timeChangedHandler;

            _log?.Info($"[ImprovedRtspStreamingService] Event handlers registered for context: {contextId}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] Failed to register event handlers for {context.Id}: {ex.Message}");
        }
    }

    private void UnregisterEventHandlers(ImprovedStreamingContext context)
    {
        if (context?.MediaPlayer == null || context.EventHandlers == null)
        {
            return;
        }

        try
        {
            // Playing 핸들러 제거
            if (context.EventHandlers.TryGetValue("Playing", out var playingHandler))
            {
                context.MediaPlayer.Playing -= (EventHandler<EventArgs>)playingHandler;
            }

            // Buffering 핸들러 제거
            if (context.EventHandlers.TryGetValue("Buffering", out var bufferingHandler))
            {
                context.MediaPlayer.Buffering -= (EventHandler<MediaPlayerBufferingEventArgs>)bufferingHandler;
            }

            // EncounteredError 핸들러 제거
            if (context.EventHandlers.TryGetValue("EncounteredError", out var errorHandler))
            {
                context.MediaPlayer.EncounteredError -= (EventHandler<EventArgs>)errorHandler;
            }

            // EndReached 핸들러 제거
            if (context.EventHandlers.TryGetValue("EndReached", out var endReachedHandler))
            {
                context.MediaPlayer.EndReached -= (EventHandler<EventArgs>)endReachedHandler;
            }

            // TimeChanged 핸들러 제거
            if (context.EventHandlers.TryGetValue("TimeChanged", out var timeChangedHandler))
            {
                context.MediaPlayer.TimeChanged -= (EventHandler<MediaPlayerTimeChangedEventArgs>)timeChangedHandler;
            }

            // 딕셔너리 클리어
            context.EventHandlers.Clear();

            _log?.Info($"[ImprovedRtspStreamingService] Event handlers unregistered for context: {context.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] Failed to unregister event handlers for {context.Id}: {ex.Message}");
        }
    }

    private void OnStreamPlaying(ImprovedStreamingContext context)
    {
        context.State = PlaybackState.Playing;
        context.LastConnectionTime = DateTime.Now;
        context.ReconnectAttempts = 0;
        context.UpdatePlayerState(PlaybackState.Playing, "Playing");
        UpdateState(context.Id, PlaybackState.Playing);
        _log?.Info($"[ImprovedRtspStreamingService] Stream {context.Id} is playing");
    }

    private void OnStreamBuffering(ImprovedStreamingContext context, float cache)
    {
        if (cache < 100)
        {
            context.State = PlaybackState.Buffering;
            context.UpdatePlayerState(PlaybackState.Buffering, $"Buffering {cache:F1}%");
            _log?.Info($"[ImprovedRtspStreamingService] Stream {context.Id} buffering: {cache:F1}%");
        }
        else
        {
            context.State = PlaybackState.Playing;
            context.UpdatePlayerState(PlaybackState.Playing, "Playing");
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

    private async void OnStreamError(ImprovedStreamingContext context)
    {
        context.State = PlaybackState.Error;
        context.Statistics.ErrorCount++;
        context.UpdatePlayerState(PlaybackState.Error, "Stream error occurred");
        UpdateState(context.Id, PlaybackState.Error);

        _log?.Error($"[ImprovedRtspStreamingService] Stream {context.Id} encountered error");

        //if (context.Options.EnableAutoReconnect &&
        //    context.ReconnectAttempts < context.Options.MaxReconnectAttempts)
        //{
        //   await HandleReconnectAsync(context.Id);
        //}

        if (context.Options.EnableAutoReconnect &&
            context.ReconnectAttempts < context.Options.MaxReconnectAttempts &&
            !context.IsReconnecting)  // 이미 재연결 중이면 스킵
        {
            // 전체 시간 체크
            if (_setupModel.IsAutoDiscard &&
                _connectionStartTimes.TryGetValue(context.Id, out var startTime))
            {
                var elapsed = DateTime.Now - startTime;
                if (elapsed.TotalSeconds >= _setupModel.TimeoutSeconds)
                {
                    _log?.Info($"[ImprovedRtspStreamingService] Timeout reached, skipping reconnect for {context.Id}");
                    await DisconnectAsync(context.Id);
                    return;
                }
            }

            await HandleReconnectAsync(context.Id);
        }
        else
        {
            // 재연결 조건 충족 안됨 - 연결 종료
            if (_setupModel.IsAutoDiscard)
            {
                await DisconnectAsync(context.Id);
            }
        }
    }

    private void OnStreamEnded(ImprovedStreamingContext context)
    {
        context.State = PlaybackState.Stopped;
        context.UpdatePlayerState(PlaybackState.Stopped, "Stream ended");
        UpdateState(context.Id, PlaybackState.Stopped);
        _log?.Info($"[ImprovedRtspStreamingService] Stream {context.Id} ended");
    }

    private void UpdateStatistics(ImprovedStreamingContext context)
    {
        if (context?.MediaPlayer == null || context.Statistics == null)
            return;

        // 통계 업데이트
        context.Statistics.TotalPlayTime = DateTime.Now - context.Statistics.StartTime;
        context.Statistics.MemoryUsageBytes = GC.GetTotalMemory(false);

        // MediaPlayer 통계 가져오기
        if (context.MediaPlayer.Media != null)
        {
            context.Statistics.CurrentBitrate = context.MediaPlayer.Media.Statistics.InputBitrate;
            context.Statistics.TotalBytesReceived = (long)(context.MediaPlayer.Media.Statistics.ReadBytes);
        }
    }

    private async Task HandleReconnectAsync(string contextId)
    {
        if (!TryGetContext(contextId, out var context))
            return;

        // 전체 연결 시간 체크
        if (_connectionStartTimes.TryGetValue(contextId, out var startTime))
        {
            var totalElapsed = DateTime.Now - startTime;
            if (_setupModel.IsAutoDiscard &&
                totalElapsed.TotalSeconds >= _setupModel.TimeoutSeconds)
            {
                _log?.Warning($"[ImprovedRtspStreamingService] Total timeout reached for {contextId}, stopping reconnection");
                await DisconnectAsync(contextId);
                return;
            }
        }

        // 재연결 플래그 체크
        if (context.IsReconnecting)
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Already reconnecting: {contextId}");
            return;
        }

        context.IsReconnecting = true;

        context.ReconnectAttempts++;
        context.Statistics.ReconnectCount++;

        // 최대 재연결 횟수 체크
        if (context.ReconnectAttempts > context.Options.MaxReconnectAttempts)
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Max reconnect attempts reached for {contextId}");
            context.IsReconnecting = false;
            await DisconnectAsync(contextId);
            return;
        }

        context.UpdatePlayerState(PlaybackState.Reconnecting, $"Reconnecting... (Attempt {context.ReconnectAttempts})");
        UpdateState(contextId, PlaybackState.Reconnecting);

        _log?.Info($"[ImprovedRtspStreamingService] Reconnect attempt {context.ReconnectAttempts}/{context.Options.MaxReconnectAttempts} for {contextId}");

        // Exponential backoff
        var delay = TimeSpan.FromSeconds(Math.Pow(2, context.ReconnectAttempts - 1) * context.Options.ReconnectDelaySeconds);

        // 남은 시간 체크
        if (_setupModel.IsAutoDiscard)
        {
            var elapsed = DateTime.Now - (_connectionStartTimes.TryGetValue(contextId, out var st) ? st : DateTime.Now);
            var remaining = TimeSpan.FromSeconds(_setupModel.TimeoutSeconds) - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                _log?.Info($"[ImprovedRtspStreamingService] Timeout during reconnect delay for {contextId}");
                context.IsReconnecting = false;
                await DisconnectAsync(contextId);
                return;
            }

            // delay가 남은 시간보다 길면 조정
            if (delay > remaining)
            {
                delay = remaining;
            }
        }

        await Task.Delay(delay);

        // 재연결 시도
        context.IsReconnecting = false;  // 플래그 해제

        await ConnectAsync(contextId, context.ConnectionInfo, context.Options);
    }

    private bool TryGetContext(string contextId, out ImprovedStreamingContext? context)
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
        if (string.IsNullOrEmpty(contextId))
        {
            _log?.Warning("[ImprovedRtspStreamingService] CleanupContextAsync called with null/empty contextId");
            return;
        }

        try
        {
            _log?.Info($"[ImprovedRtspStreamingService] Starting cleanup for context: {contextId}");

            if (!_contexts.TryRemove(contextId, out var context))
            {
                _log?.Warning($"[ImprovedRtspStreamingService] Context not found for cleanup: {contextId}");
                return;
            }

            

            var cleanupTask = Task.Run(async () =>
            {
                try
                {
                    if (context.MediaPlayer != null)
                    {
                        // MediaPlayer 정지
                        if (context.MediaPlayer.IsPlaying)
                        {
                            context.MediaPlayer.Stop();
                            await Task.Delay(50);
                        }

                        // 이벤트 핸들러 제거
                        UnregisterEventHandlers(context);
                    }

                    // UI 스레드에서 VideoView 정리
                    if (context.Player != null)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        DispatcherService.Invoke(() =>
                        {
                            try
                            {
                                var videoView = context.Player.GetVideoView();
                                if (videoView != null)
                                {
                                    videoView.MediaPlayer = null;
                                }
                                tcs.SetResult(true);
                            }
                            catch (Exception ex)
                            {
                                tcs.SetException(ex);
                            }
                        });
                        await tcs.Task;
                    }

                    // Context 정리 (Player 분리 포함)
                    context.Cleanup();

                    // Pool 반환
                    _contextPool.Return(context);

                    _log?.Info($"[ImprovedRtspStreamingService] Context {contextId} cleaned up");
                }
                catch (Exception ex)
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Cleanup error for {contextId}: {ex.Message}");
                }
            });

            // Cleanup 완료 대기 (최대 5초)
            try
            {
                await cleanupTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _log?.Warning($"[ImprovedRtspStreamingService] Cleanup timeout for {contextId}");
            }

            // Dictionary에서 제거
            _contexts.TryRemove(contextId, out _);
            _weakContexts.TryRemove(contextId, out _);

            // Player 상태 업데이트
            context.UpdatePlayerState(PlaybackState.Disconnected, "Disconnected");

            _log?.Info($"[ImprovedRtspStreamingService] Context {contextId} cleanup completed");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] CleanupContextAsync error for {contextId}: {ex.Message}");
        }
    }

    private void UpdateState(string contextId, PlaybackState newState)
    {
        if (TryGetContext(contextId, out var context))
        {
            var oldState = context.State;
            context.State = newState;

            StateChanged?.Invoke(this, new StreamingStateChangedEventArgs(contextId, oldState, newState));
            _log?.Info($"[ImprovedRtspStreamingService] State changed for {contextId}: {oldState} -> {newState}");
        }
    }

    private void OnErrorOccurred(string contextId, string errorMessage, ErrorSeverity severity)
    {
        var args = new StreamingErrorEventArgs(errorMessage, null, contextId, severity);
        ErrorOccurred?.Invoke(this, args);

        switch (severity)
        {
            case ErrorSeverity.Warning:
                _log?.Warning($"[ImprovedRtspStreamingService] {contextId}: {errorMessage}");
                break;
            case ErrorSeverity.Error:
                _log?.Error($"[ImprovedRtspStreamingService] {contextId}: {errorMessage}");
                break;
            case ErrorSeverity.Critical:
                _log?.Error($"[ImprovedRtspStreamingService] {contextId}: {errorMessage}");
                break;
            default:
                _log?.Info($"[ImprovedRtspStreamingService] {contextId}: {errorMessage}");
                break;
        }
    }

    private void CheckMemoryPressure(object? state)
    {
        var memoryInfo = GC.GetTotalMemory(false);

        if (memoryInfo > _setupModel.MaxMemoryUsageBytes)
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Memory pressure detected: {memoryInfo / 1024 / 1024}MB / {_setupModel.MaxMemoryUsageBytes / 1024 / 1024}MB");

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
                    _weakContexts[kvp.Key] = new WeakReference<ImprovedStreamingContext>(kvp.Value);
                    _contexts.TryRemove(kvp.Key, out _);
                }
            }

            _log?.Info($"[ImprovedRtspStreamingService] Memory cleanup completed. New size: {GC.GetTotalMemory(false) / 1024 / 1024}MB");
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
                    _log?.Warning($"[ImprovedRtspStreamingService] Retry {retryCount}/{_setupModel.MaxRetryAttempts} after {timeSpan.TotalSeconds:F1}s: {exception.Message}");
                });
    }

    private void CheckTimeouts(object? state)
    {
        if (!_setupModel.IsAutoDiscard)
            return;

        var now = DateTime.Now;
        var timeoutSeconds = _setupModel.TimeoutSeconds;

        foreach (var kvp in _connectionStartTimes)
        {
            var contextId = kvp.Key;
            var startTime = kvp.Value;
            var elapsed = (now - startTime).TotalSeconds;
            var remaining = timeoutSeconds - (int)elapsed;

            // UI 업데이트
            if (_players.TryGetValue(contextId, out var player))
            {
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    player.TimeoutDisplay = remaining > 0
                        ? $"{player.ContextId} ({remaining}s)"
                        : player.ContextId ?? "";
                });
            }

            // 타임아웃 체크
            if (remaining <= 0)
            {
                _log?.Info($"[ImprovedRtspStreamingService] Auto-discard timeout for {contextId}");

                // 시작 시간 제거 (재처리 방지)
                _connectionStartTimes.TryRemove(contextId, out _);

                if (_contexts.TryGetValue(contextId, out var context))
                {
                    context.IsReconnecting = false;
                }

                // 비동기 종료
                _ = Task.Run(() => DisconnectAsync(contextId));
            }
        }
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
            _log?.Info("[ImprovedRtspStreamingService] Disposing service...");
           
            // 운영 타이머 정지
            _operationTimer?.Dispose();
            _connectionStartTimes.Clear();

            // 메모리 모니터 타이머 정지
            _memoryMonitorTimer?.Dispose();

            // 모든 컨텍스트 정리
            var disconnectTask = DisconnectAllAsync();
            disconnectTask.Wait(TimeSpan.FromSeconds(10));

            // Player Registry 정리
            ClearAllPlayers();

            // Context Locks 정리
            foreach (var lockItem in _contextLocks)
            {
                lockItem.Value?.Dispose();
            }
            _contextLocks.Clear();

            // Pool 정리
            _contextPool?.Clear();

            // LibVLC 정리
            _libVLC?.Dispose();

            _log?.Info("[ImprovedRtspStreamingService] Service disposed");
        }

        _disposed = true;
    }

    ~ImprovedRtspStreamingService()
    {
        Dispose(false);
    }

    #endregion

    #region - Properties -
    // 이벤트 핸들러 저장용 딕셔너리
    public Dictionary<string, Delegate> EventHandlers { get; private set; }
    #endregion

    #region - Attributes -

    private readonly ILogService? _log;
    private readonly StreamingSetupModel _setupModel;
    private readonly IStreamingContextPool _contextPool;
    private readonly ConcurrentDictionary<string, ImprovedStreamingContext> _contexts;
    private readonly ConcurrentDictionary<string, ImprovedRtspPlayer> _players;  // Player Registry
    private readonly ConcurrentDictionary<string, WeakReference<ImprovedStreamingContext>> _weakContexts;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _contextLocks = new();

    // 컨텍스트별 타이머
    private Timer _operationTimer;
    // 전체 연결 시작 시간 추적
    private readonly ConcurrentDictionary<string, DateTime> _connectionStartTimes = new();
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly Timer _memoryMonitorTimer;
    private LibVLC? _libVLC;
    private bool _disposed;

    #endregion
}