using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
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
using System.Data;
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
    public bool RegisterPlayer(string? contextId, ImprovedRtspPlayer player)
    {
        if (string.IsNullOrEmpty(contextId) || player == null)
        {
            _log?.Error("[ImprovedRtspStreamingService] Invalid parameters for RegisterPlayer");
            return false;
        }

        if (_players.TryAdd(contextId, player))
        {
            //_log?.Info($"[ImprovedRtspStreamingService] Player registered: {contextId}");

            // 이미 존재하는 Context가 있으면 연결
            if (_contexts.TryGetValue(contextId, out var context))
            {
                context.AttachPlayer(player);
                // Player 상태 동기화
                UpdateState(contextId, context.State, "Register Player");
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

    public ImprovedRtspPlayer? GetPlayer(string? contextId)
    {
        if (contextId == null) return null;
        _players.TryGetValue(contextId, out var player);
        return player;
    }

    public bool HasPlayer(string? contextId)
    {
        if (contextId == null) return false;
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
        RtspConnectionInfo? connectionInfo,
        StreamingOptions? options = null,
        VideoView? videoView = null,
        CancellationToken ct = default)
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

        if(connectionInfo == null)
        {
            _log?.Error("[ImprovedRtspStreamingService] Invalid ConnectionInfo");
            return false;
        }

        // 취소 요청이 이미 들어온 경우 즉시 반환
        if (ct.IsCancellationRequested)
        {
            _log?.Info($"[ImprovedRtspStreamingService] Connect cancelled before lock for {contextId}");
            return false;
        }

        // Context별 Lock 획득 (타임아웃 추가 - 데드락 방지)
        var contextLock = _contextLocks.GetOrAdd(contextId, _ => new SemaphoreSlim(1, 1));
        if (!await contextLock.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false))
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Lock timeout or cancelled for {contextId}, skipping connection");
            return false;
        }

        try
        {
            // Lock 획득 후 _disposed 재확인 — Dispose 완료 직전 진입 케이스 차단 (SE-07)
            if (_disposed)
            {
                contextLock.Release();
                return false;
            }

            if (_setupModel.IsAutoDiscard)
            {
                // 연결 시작 시간 기록
                _connectionStartTimes[contextId] = DateTime.Now;
            }

            // ── Phase 1: Shared Session Pool 분기 ──────────────────────────
            // IsLocalRelay: Hub relay URL → Shared Session/sout relay 불필요, 직접 연결 경로 진행
            // BypassSharedSession: 호출자가 명시적으로 SharedSession 우회 요청
            var cameraKey = (connectionInfo.IsLocalRelay || options?.BypassSharedSession == true)
                ? string.Empty
                : DeriveKey(connectionInfo);
            if (!string.IsNullOrEmpty(cameraKey))
            {
                var (isNewPrimary, sharedState) = await TryAcquireSharedSession(contextId, cameraKey, ct)
                    .ConfigureAwait(false);

                if (!isNewPrimary)
                {
                    // Secondary 경로 (Phase 2): sout 릴레이 사용 시 공유 재생, 미준비 시 직접 RTSP 연결로 폴백
                    var secondaryCtx = _contextPool.Get() ?? new ImprovedStreamingContext();
                    secondaryCtx.Initialize(contextId, connectionInfo!, options ?? new StreamingOptions(), _libVLC!, GetPlayer(contextId));
                    if (!_contexts.TryAdd(contextId, secondaryCtx))
                    {
                        secondaryCtx.Cleanup();
                        _contextPool.Return(secondaryCtx);
                        return true;
                    }
                    _weakContexts[contextId] = new WeakReference<ImprovedStreamingContext>(secondaryCtx);

                    if (sharedState.HasSoutRelay && sharedState.SoutRelayPort > 0)
                    {
                        // 로컬 릴레이 URL로 Secondary MediaPlayer 연결
                        var relayUrl = $"rtsp://127.0.0.1:{sharedState.SoutRelayPort}/{SanitizeSoutPath(cameraKey)}";
                        var relayMedia = new Media(_libVLC!, relayUrl, FromType.FromLocation);
                        relayMedia.AddOption(":network-caching=500");

                        secondaryCtx.SetMedia(relayMedia);

                        EventHandler<EventArgs> playingHandler = (s, e) =>
                            UpdateState(contextId, PlaybackState.SharedStreamPlaying, "공유 스트림 재생 중");
                        EventHandler<EventArgs> errorHandler = (s, e) =>
                            UpdateState(contextId, PlaybackState.SharedStreamWaiting, "릴레이 연결 오류 — 대기 중");

                        secondaryCtx.MediaPlayer!.Playing += playingHandler;
                        secondaryCtx.MediaPlayer.EncounteredError += errorHandler;
                        secondaryCtx.EventHandlers ??= new Dictionary<string, Delegate>();
                        secondaryCtx.EventHandlers["Playing"] = playingHandler;
                        secondaryCtx.EventHandlers["EncounteredError"] = errorHandler;

                        secondaryCtx.MediaPlayer.Play();
                        UpdateState(contextId, PlaybackState.Connecting, "릴레이 연결 중...");
                        _log?.Info($"[SharedSession] Secondary sout: {contextId} → relay {relayUrl}");
                        return true;
                    }

                    // sout 릴레이 미준비: Secondary 등록 해제 후 직접 RTSP 연결로 폴백
                    // 이렇게 하면 중복 연결이 생기지만 영상이 정상 표시됨
                    _contexts.TryRemove(contextId, out _);
                    _weakContexts.TryRemove(contextId, out _);
                    secondaryCtx.Cleanup();
                    _contextPool.Return(secondaryCtx);
                    await ReleaseSharedSession(contextId).ConfigureAwait(false);
                    _log?.Info($"[SharedSession] Secondary fallback to direct connect: {contextId}");
                    // fall through — 일반 연결 경로 진행 (아래 코드가 직접 RTSP 연결 처리)
                }
                // Primary 경로: 기존 로직으로 계속
            }
            // ── End Phase 1 분기 ───────────────────────────────────────────

            //_log?.Info($"[ImprovedRtspStreamingService] Connecting stream: {contextId} to {connectionInfo?.IpAddress}");

            // RtspPlayer 가져오기
            ImprovedRtspPlayer rtspPlayer = GetPlayer(contextId);

            // 최대 연결 수 체크
            if (_contexts.Count >= _setupModel.MaxConnections + 3) // 여유를 둔다
            {
                _log?.Warning($"[ImprovedRtspStreamingService] Max connections reached: {_setupModel.MaxConnections}, deferring {contextId}");
                // OnErrorOccurred 호출 금지 — 즉시 에러→AutoDiscard→ProcessWaitingQueue→무한루프 방지
                // 대신 Connecting 상태 유지 → IsAutoDiscard 타임아웃 후 Row가 정상 종료됨
                return false;
            }

            ImprovedStreamingContext? context;
            TryGetContext(contextId, out context);

            if(context != null && context.State == PlaybackState.Reconnecting)
            {
                // URL 유효성 검사
                var url = connectionInfo!.GetFullUrl();
                if (!RtspUrlValidator.IsValidUrl(url))
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Invalid RTSP URL: {url}");
                    await CleanupContextAsync(contextId);
                    OnErrorOccurred(contextId, "Invalid RTSP URL", ErrorSeverity.Error);
                    return false;
                }

                // Player 상태 업데이트
                //context.UpdatePlayerState(PlaybackState.Connecting, "Connecting...");
                UpdateState(contextId, PlaybackState.Connecting, "Connecting...");

                // 재시도 정책으로 연결
                return await _retryPolicy.ExecuteAsync(async (token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return await ConnectInternalAsync(context, url, videoView);
                }, ct);
            }
            else
            {
                if (_contexts.ContainsKey(contextId))
                {
                    _log?.Warning($"[ImprovedRtspStreamingService] Context already exists, cleaning up: {contextId}");
                    await CleanupContextAsync(contextId);

                    // 정리 완료 대기 (GC 강제 호출 제거 - 성능 최적화)
                    var verifyWait = 10;
                    while ((_contexts.ContainsKey(contextId) ||
                            _weakContexts.ContainsKey(contextId)) && verifyWait-- > 0)
                    {
                        await Task.Delay(50);
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
                context.Initialize(contextId, connectionInfo!, options ?? new StreamingOptions(), _libVLC!, rtspPlayer);

                // Context 유효성 확인
                if (string.IsNullOrEmpty(context.Id))
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Context initialization failed - Guid is null");
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
                var url = connectionInfo!.GetFullUrl();
                if (!RtspUrlValidator.IsValidUrl(url))
                {
                    _log?.Error($"[ImprovedRtspStreamingService] Invalid RTSP URL: {url}");
                    await CleanupContextAsync(contextId);
                    OnErrorOccurred(contextId, "Invalid RTSP URL", ErrorSeverity.Error);
                    return false;
                }
                // Player 상태 업데이트
                UpdateState(contextId, PlaybackState.Connecting, "Connecting...");
                
                // 재시도 정책으로 연결
                return await _retryPolicy.ExecuteAsync(async (token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return await ConnectInternalAsync(context, url, videoView);
                }, ct);
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


    private async Task<bool> ConnectInternalAsync(ImprovedStreamingContext context, string url, VideoView? videoViewParam)
    {
        if (context == null || string.IsNullOrEmpty(context.Id))
        {
            _log?.Error($"[ImprovedRtspStreamingService] Invalid context");
            return false;
        }

        var contextId = context.Id;
        //_log?.Info($"[ImprovedRtspStreamingService] Creating media for {contextId}: {url}");

        try
        {
            var media = CreateOptimizedMedia(url, context.Options!);
            context.SetMedia(media);

            if (context.MediaPlayer == null)
            {
                _log?.Error($"[ImprovedRtspStreamingService] MediaPlayer is null for {contextId}");
                UpdateState(contextId, PlaybackState.Error, "MediaPlayer creation failed");
                return false;
            }

            {
                // VideoView 할당 부분을 UI 스레드에서 처리
                VideoView? targetVideoView = null;

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
                    //_log?.Info($"[ImprovedRtspStreamingService] Assigning MediaPlayer to VideoView for {contextId}");

                    var assignSuccess = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            // UI 스레드에서 안전하게 접근
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
                        //context.UpdatePlayerState(PlaybackState.Error, "Failed to assign MediaPlayer");
                        UpdateState(contextId, PlaybackState.Error, "Failed to assign MediaPlayer");
                        return false;
                    }
                }
                else
                {
                    _log?.Warning($"[ImprovedRtspStreamingService] No VideoView available for {contextId}, aborting play");
                    // VideoView 없이 Play() 하면 VLC가 자체 OS 창을 생성 ("영상 탈출" 현상)
                    // → 즉시 중단하고 Connecting 상태 유지 (타임아웃 후 Row 정상 종료)
                    return false;
                }
            }

            // 이벤트 핸들러 등록 (백그라운드 스레드 OK)
            RegisterEventHandlers(context);

            // DisconnectAsync 레이스로 인해 Play() 직전에 MediaPlayer가 null이 될 수 있음
            if (context.MediaPlayer == null)
            {
                _log?.Warning($"[ImprovedRtspStreamingService] MediaPlayer disposed before Play() for {contextId}");
                return false;
            }

            // 재생 시작 (백그라운드 스레드 OK)
            _log?.Info($"[Service] MediaPlayer.Play() contextId={contextId}  url={url}");
            var result = context.MediaPlayer.Play();
            _log?.Info($"[Service] MediaPlayer.Play() result={result}  contextId={contextId}");

            if (result)
            {
                context.LastConnectionTime = DateTime.Now;
            }
            else
            {
                _log?.Error($"[ImprovedRtspStreamingService] Failed to start stream {contextId}");
                UnregisterEventHandlers(context);
                UpdateState(contextId, PlaybackState.Error, "Failed to start stream");
            }

            return result;
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] ConnectInternal error: {ex.Message}");
            //context.UpdatePlayerState(PlaybackState.Error, $"Error: {ex.Message}");
            UpdateState(contextId,PlaybackState.Error, $"Error: {ex.Message}");
            throw;
        }
    }

    public async Task DisconnectAsync(string contextId)
    {
        try
        {
            _log?.Info($"[ImprovedRtspStreamingService] Disconnecting stream: {contextId}");

            // ConnectInternalAsync와의 레이스 컨디션 방지: contextLock 획득 후 정리
            var contextLock = _contextLocks.GetOrAdd(contextId, _ => new SemaphoreSlim(1, 1));
            if (!await contextLock.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                _log?.Warning($"[ImprovedRtspStreamingService] Disconnect lock timeout for {contextId}");
                return;
            }

            try
            {
                DisconnectPreparing(contextId);

                // ── Phase 1: Shared Session 분기 ─────────────────────────
                if (_secondaryToPrimary.ContainsKey(contextId))
                {
                    // Shared 세션 참여자 — ReleaseSharedSession이 RefCount를 관리
                    // Primary가 아닌 Secondary면 Context만 제거, Primary면 ReleaseSharedSession이 실제 정리
                    var isSecondary = _sharedSessions.TryGetValue(
                        _secondaryToPrimary.GetValueOrDefault(contextId, string.Empty),
                        out var shared) && shared?.PrimaryContextId != contextId;

                    if (isSecondary)
                    {
                        // Secondary: Context 제거 + 공유 세션 RefCount 감소
                        _contexts.TryRemove(contextId, out _);
                        _weakContexts.TryRemove(contextId, out _);
                        contextLock.Release();
                        await ReleaseSharedSession(contextId).ConfigureAwait(false);
                        return;
                    }
                    else
                    {
                        // Primary: 먼저 SecondaryContextIds를 Waiting으로 전환
                        if (shared != null)
                            BroadcastStateToSecondaries(contextId, PlaybackState.Disconnected);
                        await ReleaseSharedSession(contextId).ConfigureAwait(false);
                        return;
                    }
                }
                // ── End Phase 1 분기 ─────────────────────────────────────

                await CleanupContextAsync(contextId);
            }
            finally
            {
                if (contextLock.CurrentCount == 0)
                    contextLock.Release();
            }
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
            context.EndReconnect();
        }

    }


    public async Task DisconnectAllAsync()
    {
        _log?.Info("[ImprovedRtspStreamingService] Disconnecting all streams");
        var tasks = _contexts.Keys.Select(DisconnectAsync).ToArray();
        await Task.WhenAll(tasks);
        _log?.Info("[ImprovedRtspStreamingService] All streams disconnected");
    }

    public void Play(string? contextId)
    {
        if (contextId == null) return;
        if (TryGetContext(contextId, out var context) && context != null)
        {
            if (context.MediaPlayer != null)
            {
                context.MediaPlayer.Play();
                //context.UpdatePlayerState(PlaybackState.Playing, "Playing");
                UpdateState(contextId, PlaybackState.Playing, "Playing");
            }
            _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} playing");
        }
    }

    public void Pause(string? contextId)
    {
        if (contextId == null) return;
        if (TryGetContext(contextId, out var context) && context != null)
        {
            if(context.MediaPlayer != null)
            {
                context.MediaPlayer.SetPause(true);
                //context.UpdatePlayerState(PlaybackState.Paused, "Paused");
                UpdateState(contextId, PlaybackState.Paused, "Paused");
            }
            _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} paused");
        }
    }

    public void Stop(string? contextId)
    {
        if (contextId == null) return;
        if (TryGetContext(contextId, out var context) && context != null)
        {
            if (context.MediaPlayer != null && context.MediaPlayer?.IsPlaying == true)
            {
                context.MediaPlayer.Stop();
                //context.UpdatePlayerState(PlaybackState.Stopped, "Stopped");
                UpdateState(contextId, PlaybackState.Stopped, "Stopped");
            }
            _log?.Info($"[ImprovedRtspStreamingService] Stream {contextId} stopped");
        }
    }

    public void SetVolume(string? contextId, int volume)
    {
        if (contextId == null) return;
        if (TryGetContext(contextId, out var context) && context != null)
        {
            if (context.MediaPlayer != null)
            {
                var clampedVolume = Math.Clamp(volume, 0, 100);
                context.MediaPlayer.Volume = clampedVolume;
                if(context.Options != null)
                    context.Options.Volume = clampedVolume;
                _log?.Info($"[ImprovedRtspStreamingService] Volume set to {clampedVolume} for {contextId}");
            }
        }
    }

    public void ToggleMute(string? contextId)
    {
        if (contextId == null) return;
        if (TryGetContext(contextId, out var context) && context != null)
        {
            if (context.MediaPlayer != null)
            { 
                context.MediaPlayer.Mute = !context.MediaPlayer.Mute;
                if (context.Options != null)
                    context.Options.IsMuted = context.MediaPlayer.Mute;
                _log?.Info($"[ImprovedRtspStreamingService] Mute toggled for {contextId}: {context.MediaPlayer.Mute}");
            }
        }
    }

    public MediaPlayer? GetMediaPlayer(string? contextId)
    {
        if (contextId == null) return null;
        return TryGetContext(contextId, out var context) ? context.MediaPlayer : null;
    }

    public PlaybackState GetPlaybackState(string contextId)
    {
        return TryGetContext(contextId, out var context) ? context.State : PlaybackState.None;
    }

    public StreamingStatistics? GetStatistics(string? contextId)
    {
        if (contextId == null) return null;
        return TryGetContext(contextId, out var context) ? context.Statistics : null;
    }

    public bool IsConnected(string contextId)
    {
        return TryGetContext(contextId, out var context) &&
               context.State == PlaybackState.Playing;
    }

    public async Task<bool> TakeSnapshotAsync(string contextId, string fileName)
    {
        try
        {
            if (contextId == null) return false;
            if (TryGetContext(contextId, out var context) && context != null)
            {
                if (context.MediaPlayer != null)
                {
                    var fullPath = ResolveSnapshotPath(fileName);
                    _log?.Info($"[ImprovedRtspStreamingService] Taking snapshot for {contextId}: {fullPath}");
                    return await Task.Run(() => context.MediaPlayer.TakeSnapshot(0, fullPath, 0, 0));
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] Snapshot failed for {contextId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>스냅샷 저장 전체 경로 해석 — 설정 SnapshotPath(없으면 "snapshots"), 상대경로면 앱 기준 절대화, 폴더 자동 생성.
    /// Hub 모드 플레이어가 공유 프레임을 직접 저장할 때도 동일 규칙을 쓰도록 공개.</summary>
    public string ResolveSnapshotPath(string fileName)
    {
        var dir = _setupModel.SnapshotPath;
        if (string.IsNullOrWhiteSpace(dir)) dir = "snapshots";
        if (!System.IO.Path.IsPathRooted(dir))
            dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir);
        System.IO.Directory.CreateDirectory(dir);
        return System.IO.Path.Combine(dir, fileName);
    }

    public void RegisterConnectionStartTime(string contextId)
    {
        if (!string.IsNullOrEmpty(contextId))
            _connectionStartTimes[contextId] = DateTime.Now;
    }

    public void SetFrameSkip(string? contextId, int skipFrames)
    {
        if (contextId == null) return;
        if (TryGetContext(contextId, out var context) && context != null)
        {
            context.FrameSkipCount = Math.Max(0, skipFrames);
            _log?.Info($"[ImprovedRtspStreamingService] Frame skip set to {skipFrames} for {contextId}");
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
            //context.UpdatePlayerState(PlaybackState.Restricted, message);
            UpdateState(contextId, PlaybackState.Restricted, message);

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
        if (contextId == null) return;
        if (!TryGetContext(contextId, out var context)) return;
        
        if (context != null)
        {
            if (context.MediaPlayer != null && context.MediaPlayer?.IsPlaying == true) return;
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
            _libVLC.Log += OnLibVLCLog;
            _log?.Info("[ImprovedRtspStreamingService] LibVLC initialized successfully");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] LibVLC initialization failed: {ex.Message}");
            throw;
        }
    }

    private void OnLibVLCLog(object? sender, LibVLCSharp.Shared.LogEventArgs e)
    {
        var module = e.Module ?? "?";
        var msg = e.Message ?? "";

        // sout/rtp/stream_out 관련: 레벨 무관하게 전부 출력 (stream chain failed 원인 진단)
        bool isSoutRelated = msg.Contains("sout", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("rtp", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("stream_out", StringComparison.OrdinalIgnoreCase)
            || module.Contains("rtp", StringComparison.OrdinalIgnoreCase)
            || module.Contains("sout", StringComparison.OrdinalIgnoreCase)
            || module.Contains("stream_out", StringComparison.OrdinalIgnoreCase);

        if (isSoutRelated)
        {
            _log?.Info($"[LibVLC][{e.Level}][{module}] {msg}");
            return;
        }

        // 그 외는 Warning 이상 + 키워드 필터
        if (e.Level < LogLevel.Warning) return;
        if (e.Level >= LogLevel.Error
            || msg.Contains("rtsp", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("access", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("vout", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("aout", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("codec", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("network", StringComparison.OrdinalIgnoreCase))
        {
            _log?.Info($"[LibVLC][{e.Level}][{module}] {msg}");
        }
    }

    private Media CreateOptimizedMedia(string url, StreamingOptions options)
    {
        var media = new Media(_libVLC, url, FromType.FromLocation);

        // ===== 네트워크 캐싱 =====
        var caching = CalculateOptimalCaching(options);
        media.AddOption($":network-caching={caching}");
        media.AddOption($":live-caching={caching}");

        // ===== 프로토콜 설정 =====
        if (options.UseTcp)
        {
            media.AddOption(":rtsp-tcp");
        }
        else
        {
            media.AddOption(":rtsp-udp");
            media.AddOption(":rtp-max-misorder=100");
        }

        // ==== Multicast 설정 (안정적) ====
        if (options.EnableMulticast)
        {
            media.AddOption(":rtsp-mcast");
        }

        // ===== 버퍼 설정 =====
        media.AddOption($":rtsp-frame-buffer-size={options.FrameBufferSize}");

        // ===== 메모리 최적화 (안정적) =====
        if (options.EnableMemoryOptimization && options.MaxBufferSizeMB > 0)
        {
            var fileCache = Math.Min(options.MaxBufferSizeMB * 1024, caching * 10);
            media.AddOption($":file-caching={fileCache}");
        }

        // 하드웨어 가속
        if (options.UseHardwareAcceleration)
        {
            media.AddOption(":avcodec-hw=any");
            // 디코딩 스레드 설정 (안정적)
            if (options.MaxDecodingThreads > 0)
            {
                media.AddOption($":avcodec-threads={options.MaxDecodingThreads}");
            }
            else
            {
                media.AddOption(":avcodec-threads=0"); // auto
            }
        }

        // ===== 프레임 스킵 =====
        if (options.AllowFrameSkip)
        {
            media.AddOption(":avcodec-fast");
            media.AddOption(":avcodec-skip-frame=1"); // Non-ref frames skip
            media.AddOption(":avcodec-skip-idct=1");
            media.AddOption(":avcodec-hurry-up");
        }

        // ===== 비디오 코덱 힌트 (안정적) =====
        if (!string.IsNullOrEmpty(options.VideoCodec))
        {
            media.AddOption($":avcodec-codec={options.VideoCodec}");
        }

        // ===== 오디오 설정 (안정적) =====
        if (!options.EnableAudio)
        {
            media.AddOption(":no-audio");
        }
        else if (options.AudioSampleRate > 0 && options.AudioSampleRate != 44100)
        {
            // 비표준 샘플레이트만 명시
            media.AddOption($":audio-rate={options.AudioSampleRate}");
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

    /// <summary>
    /// Hub relay 전용 미디어 생성 — sout RTP 서버 포함, conflicting 옵션 제외.
    /// 서버 바인드 주소 rtsp://:PORT (all interfaces) 사용.
    /// </summary>
    private Media CreateHubRelayMedia(string url, int relayPort)
    {
        var media = new Media(_libVLC!, url, FromType.FromLocation);
        media.AddOption(":network-caching=300");
        media.AddOption(":rtsp-tcp");
        // :sout= 설정 시 LibVLC는 로컬 vout/aout를 자동 비활성화.
        // :vout=dummy / :aout=dummy를 함께 설정하면 sout 체인 초기화 실패(stream chain failed) 유발.
        media.AddOption(":avcodec-hw=none");
        // mux=ts 명시: mux 미지정 시 LibVLC가 mux 결정 실패로 stream chain failed 유발 가능
        media.AddOption($":sout=#rtp{{mux=ts,sdp=rtsp://:{relayPort}/stream}}");
        media.AddOption(":sout-all");
        media.AddOption(":sout-keep");
        media.AddOption(":ipv4-timeout=10000");
        return media;
    }

    #region - Phase 2: sout 릴레이 헬퍼 -

    /// <summary>
    /// sout 옵션 문자열 생성: 원본 디스플레이 + 로컬 RTSP 릴레이.
    /// </summary>
    private static string BuildSoutOption(int port, string pathKey)
    {
        // pathKey는 URL에 사용할 수 있도록 영숫자와 '_'만 허용
        var safePath = SanitizeSoutPath(pathKey);
        return $"#duplicate{{dst=display,dst=rtp{{sdp=rtsp://127.0.0.1:{port}/{safePath}}}}}";
    }

    /// <summary>
    /// CameraKey를 RTSP 경로로 사용할 수 있도록 정규화.
    /// 예) "192.168.1.1:554/cam1" → "192_168_1_1_554_cam1"
    /// </summary>
    private static string SanitizeSoutPath(string cameraKey) =>
        string.Concat(cameraKey.Select(c => char.IsLetterOrDigit(c) ? c : '_'));

    private const int MAX_SOUT_RETRY = 3;

    /// <summary>
    /// Primary MediaPlayer에 sout 릴레이 옵션을 적용하여 로컬 RTSP 릴레이를 활성화.
    /// SE-12: 포트 바인딩 실패 감지 시 AcquireNew로 최대 MAX_SOUT_RETRY(3)회 재시도.
    /// </summary>
    private async Task<bool> ActivateSoutRelayAsync(string primaryContextId, int relayPort, CancellationToken ct = default)
    {
        if (!TryGetContext(primaryContextId, out var ctx) || ctx == null)
            return false;
        if (ctx.MediaPlayer == null || ctx.ConnectionInfo == null || ctx.Options == null)
            return false;

        var cameraKey = DeriveKey(ctx.ConnectionInfo);
        var url = ctx.ConnectionInfo.GetFullUrl();
        var port = relayPort;

        for (int attempt = 1; attempt <= MAX_SOUT_RETRY; attempt++)
        {
            // SE-12: 이미 바인딩된 포트이면 새 포트로 교체
            if (attempt > 1 || RtspRelayPortRegistry.IsPortBound(port))
            {
                port = _portRegistry.AcquireNew(cameraKey);
                if (port < 0)
                {
                    _log?.Warning($"[SharedSession] No port available (attempt {attempt}) for {cameraKey}");
                    return false;
                }
            }

            try
            {
                var soutOption = BuildSoutOption(port, cameraKey);

                if (ctx.MediaPlayer.IsPlaying)
                    ctx.MediaPlayer.Stop();

                await Task.Delay(200, ct).ConfigureAwait(false);

                var media = CreateOptimizedMedia(url, ctx.Options);
                media.AddOption($":sout={soutOption}");
                media.AddOption(":sout-keep");

                ctx.SetMedia(media);
                ctx.MediaPlayer.Play();

                if (_sharedSessions.TryGetValue(cameraKey, out var shared))
                {
                    shared.HasSoutRelay = true;
                    shared.SoutRelayPort = port;
                }

                _log?.Info($"[SharedSession] sout relay activated: port={port}, attempt={attempt}, path={SanitizeSoutPath(cameraKey)}");
                return true;
            }
            catch (Exception ex)
            {
                _log?.Warning($"[SharedSession] ActivateSoutRelay attempt {attempt} failed: {ex.Message}");
                if (attempt == MAX_SOUT_RETRY)
                    _log?.Error($"[SharedSession] ActivateSoutRelay exhausted retries for {primaryContextId}");
            }
        }
        return false;
    }

    /// <summary>
    /// SE-03: Primary 재연결 중 SharedStreamPlaying Secondary를 일시정지.
    /// NotifySecondariesAfterReconnectAsync가 복구 시 재연결한다.
    /// </summary>
    private void PauseSecondariesForReconnect(string primaryContextId)
    {
        if (!_secondaryToPrimary.TryGetValue(primaryContextId, out var cameraKey))
            return;
        if (!_sharedSessions.TryGetValue(cameraKey, out var shared))
            return;
        if (shared.PrimaryContextId != primaryContextId)
            return;

        foreach (var secId in shared.SecondaryContextIds.ToList())
        {
            if (TryGetContext(secId, out var secCtx) &&
                secCtx?.State == PlaybackState.SharedStreamPlaying &&
                secCtx.MediaPlayer?.IsPlaying == true)
            {
                secCtx.MediaPlayer.Pause();
            }
        }
    }

    /// <summary>
    /// Primary 재연결 성공 후 모든 Secondary를 새 포트로 재연결.
    /// SE-04: AcquireNew로 새 포트 할당하여 TIME_WAIT 방지.
    /// </summary>
    private async Task NotifySecondariesAfterReconnectAsync(string primaryContextId)
    {
        if (!_secondaryToPrimary.TryGetValue(primaryContextId, out var cameraKey))
            return;
        if (!_sharedSessions.TryGetValue(cameraKey, out var shared))
            return;
        if (shared.PrimaryContextId != primaryContextId)
            return;

        var secondaryIds = shared.SecondaryContextIds.ToList();
        if (secondaryIds.Count == 0) return;

        // SE-04: 새 포트 강제 할당 (TIME_WAIT 방지)
        var newPort = _portRegistry.AcquireNew(cameraKey);
        if (newPort < 0)
        {
            _log?.Warning($"[SharedSession] NotifySecondaries: no port available for {cameraKey}");
            BroadcastStateToSecondaries(primaryContextId, PlaybackState.SharedStreamWaiting);
            return;
        }

        // Primary sout 릴레이 재활성화
        var activated = await ActivateSoutRelayAsync(primaryContextId, newPort, _serviceCts.Token)
            .ConfigureAwait(false);
        if (!activated)
        {
            _log?.Warning($"[SharedSession] NotifySecondaries: relay reactivation failed for {primaryContextId}");
            BroadcastStateToSecondaries(primaryContextId, PlaybackState.SharedStreamWaiting);
            return;
        }

        // Secondary들을 새 릴레이 URL로 재연결
        var relayUrl = $"rtsp://127.0.0.1:{shared.SoutRelayPort}/{SanitizeSoutPath(cameraKey)}";
        BroadcastStateToSecondaries(primaryContextId, PlaybackState.Reconnecting);

        foreach (var secId in secondaryIds)
        {
            if (!TryGetContext(secId, out var secCtx) || secCtx?.MediaPlayer == null)
                continue;

            try
            {
                secCtx.MediaPlayer.Stop();
                var relayMedia = new Media(_libVLC!, relayUrl, FromType.FromLocation);
                relayMedia.AddOption(":network-caching=500");
                secCtx.SetMedia(relayMedia);
                secCtx.MediaPlayer.Play();
            }
            catch (Exception ex)
            {
                _log?.Warning($"[SharedSession] Secondary reconnect failed: {secId} — {ex.Message}");
            }
        }

        _log?.Info($"[SharedSession] Notified {secondaryIds.Count} secondaries after Primary reconnect ({cameraKey})");
    }

    #endregion

    private int CalculateOptimalCaching(StreamingOptions options)
    {
        var baseCaching = options.NetworkCaching;

        // TCP는 약간의 추가 버퍼링 필요 (1.2배)
        if (options.UseTcp)
        {
            baseCaching = (int)(baseCaching * 1.2);
        }

        return Math.Min(baseCaching, 1000); // 최대 1초 (빠른 초기 연결)
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
                    _ = service.OnStreamErrorAsync(context, service._serviceCts.Token);
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
        //context.UpdatePlayerState(PlaybackState.Playing, "Playing");
        UpdateState(context.Id, PlaybackState.Playing, "Playing");
        BroadcastStateToSecondaries(context.Id, PlaybackState.Playing);
        _log?.Info($"[ImprovedRtspStreamingService] Stream {context.Id} is playing");
    }

    private void OnStreamBuffering(ImprovedStreamingContext context, float cache)
    {
        if (cache < 100)
        {
            context.State = PlaybackState.Buffering;
            //context.UpdatePlayerState(PlaybackState.Buffering, $"Buffering {cache:F1}%");
            //UpdateState(context.Guid, PlaybackState.Buffering, $"Buffering {cache:F1}%");
            _log?.Info($"[ImprovedRtspStreamingService] Stream {context.Id} buffering: {cache:F1}%");
        }
        else
        {
            context.State = PlaybackState.Playing;
            //context.UpdatePlayerState(PlaybackState.Playing, "Playing");
            UpdateState(context.Id, PlaybackState.Playing, "Playing");
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

    private async Task OnStreamErrorAsync(ImprovedStreamingContext context, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            context.State = PlaybackState.Error;
            if (context.Statistics != null)
                context.Statistics.ErrorCount++;
            UpdateState(context.Id, PlaybackState.Error, "Stream error occurred");

            _log?.Error($"[ImprovedRtspStreamingService] Stream {context.Id} encountered error");

            // SE-03: Primary 재연결 중 SharedStreamPlaying Secondary 일시정지
            PauseSecondariesForReconnect(context.Id);
            BroadcastStateToSecondaries(context.Id, PlaybackState.Reconnecting);

            if (context.Options?.EnableAutoReconnect == true &&
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

                await HandleReconnectAsync(context.Id, ct);
            }
            else
            {
                // 재연결 조건 충족 안됨(재연결 비활성/시도 소진) - 연결 종료.
                // IsAutoDiscard와 무관하게 항상 정리: 실패 스트림 컨텍스트 누수 방지(슬롯 잠식 방지).
                await DisconnectAsync(context.Id);
            }
        }
        catch (OperationCanceledException)
        {
            _log?.Info($"[ImprovedRtspStreamingService] OnStreamErrorAsync cancelled for {context?.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] OnStreamErrorAsync unhandled exception for {context?.Id}: {ex.Message}");
        }
    }

    private void OnStreamEnded(ImprovedStreamingContext context)
    {
        context.State = PlaybackState.Stopped;
        //context.UpdatePlayerState(PlaybackState.Stopped, "Stream ended");
        UpdateState(context.Id, PlaybackState.Stopped, "Stream ended");
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

    private async Task HandleReconnectAsync(string contextId, CancellationToken ct = default)
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

        // 재연결 플래그 체크 — CAS 원자적 진입 (BUG-02)
        if (!context.TryBeginReconnect())
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Already reconnecting: {contextId}");
            return;
        }

        context.ReconnectAttempts++;
        if(context.Statistics != null)
            context.Statistics.ReconnectCount++;

        // 최대 재연결 횟수 체크
        if (context.ReconnectAttempts > context.Options.MaxReconnectAttempts)
        {
            _log?.Warning($"[ImprovedRtspStreamingService] Max reconnect attempts reached for {contextId}");
            context.EndReconnect();
            await DisconnectAsync(contextId);
            return;
        }

        //context.UpdatePlayerState(PlaybackState.Reconnecting, $"Reconnecting... (Attempt {context.ReconnectAttempts})");
        UpdateState(contextId, PlaybackState.Reconnecting, $"Reconnecting... (Attempt {context.ReconnectAttempts})");

        _log?.Info($"[ImprovedRtspStreamingService] Reconnect attempt {context.ReconnectAttempts}/{context.Options.MaxReconnectAttempts} for {contextId}");

        // Linear backoff (500ms * attempt) - 빠른 재연결
        // 기존 Exponential: 2s, 4s, 8s... → 변경: 500ms, 1000ms, 1500ms...
        var delay = context.Options.ExponentialBackoff
            ? TimeSpan.FromSeconds(Math.Pow(2, context.ReconnectAttempts - 1) * context.Options.ReconnectDelaySeconds)
            : TimeSpan.FromMilliseconds(500 * context.ReconnectAttempts);

        // 남은 시간 체크
        if (_setupModel.IsAutoDiscard)
        {
            var elapsed = DateTime.Now - (_connectionStartTimes.TryGetValue(contextId, out var st) ? st : DateTime.Now);
            var remaining = TimeSpan.FromSeconds(_setupModel.TimeoutSeconds) - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                _log?.Info($"[ImprovedRtspStreamingService] Timeout during reconnect delay for {contextId}");
                context.EndReconnect();
                await DisconnectAsync(contextId);
                return;
            }

            // delay가 남은 시간보다 길면 조정
            if (delay > remaining)
            {
                delay = remaining;
            }
        }

        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            context.EndReconnect();
            return;
        }

        // 재연결 시도
        context.EndReconnect();  // 플래그 해제

        var reconnected = await ConnectAsync(contextId, context.ConnectionInfo, context.Options, ct: ct);

        // Phase 2: 재연결 성공 시 Secondary 자동 복구
        if (reconnected)
            await NotifySecondariesAfterReconnectAsync(contextId).ConfigureAwait(false);
    }

    private bool TryGetContext(string contextId, out ImprovedStreamingContext? context)
    {
        // 강한 참조에서 먼저 찾기
        if (_contexts.TryGetValue(contextId, out context))
            return true;

        // Weak Reference에서 복구 시도 — Cleaning 상태이면 복구 불가 (SE-06)
        if (_weakContexts.TryGetValue(contextId, out var weakRef) &&
            weakRef.TryGetTarget(out context) &&
            context.IsActive)
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
                        //var tcs = new TaskCompletionSource<bool>();
                        //DispatcherService.Invoke(() =>
                        //{
                        //    try
                        //    {
                        //        var videoView = context.Player.GetVideoView();
                        //        if (videoView != null)
                        //        {
                        //            videoView.MediaPlayer = null;
                        //        }
                        //        tcs.SetResult(true);
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        tcs.SetException(ex);
                        //    }
                        //});
                        //await tcs.Task;
                        // 비블로킹으로 VideoView 정리 (성능 최적화)
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            var videoView = context.Player?.GetVideoView();
                            if (videoView != null)
                            {
                                videoView.MediaPlayer = null;
                            }
                        });
                    }
                    // Context 정리 (Player 분리 포함)
                    context.Cleanup();

                    // Player 상태 업데이트
                    //context.UpdatePlayerState(PlaybackState.Disconnected, "Camera was disconnected and Context was Cleaned up!");
                    //UpdateState(contextId, PlaybackState.Disconnected, "Camera was disconnected and Context was Cleaned up!");
                    

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
            
            // Cleanup 완료 후에 Disconnected 이벤트 발생
            StateChanged?.Invoke(this, new StreamingStateChangedEventArgs(
                contextId,
                context?.State ?? PlaybackState.None,
                PlaybackState.Disconnected));

            _log?.Info($"[ImprovedRtspStreamingService] Context {contextId} cleanup completed");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] CleanupContextAsync error for {contextId}: {ex.Message}");
        }
    }

    /// <summary>
    /// 상태 업데이트 이벤트 처리 및 Context 상태 처리
    /// </summary>
    /// <param name="contextId"></param>
    /// <param name="newState"></param>
    /// <param name="message"></param>
    private void UpdateState(string contextId, PlaybackState newState, string? message = null)
    {
        try
        {
            if (TryGetContext(contextId, out var context))
            {
                if (context == null)
                    throw new NullReferenceException($"{contextId}에 해당하는 Context가 Pool에 존재하지 않습니다.");

                var oldState = context.State;
                context.State = newState;

                // Player 상태 먼저 업데이트 (UI 업데이트)
                context.UpdatePlayerState(newState, message);

                // Disconnected/Stopped 상태는 지연 처리
                if (newState == PlaybackState.Disconnected || newState == PlaybackState.Stopped)
                {
                    // 비동기로 이벤트 발생 (cleanup 완료 후)
                    Task.Run(async () =>
                    {
                        await Task.Delay(100); // cleanup 대기
                        StateChanged?.Invoke(this, new StreamingStateChangedEventArgs(contextId, oldState, newState));
                    });
                }
                else
                {
                    // 다른 상태는 즉시 이벤트 발생
                    StateChanged?.Invoke(this, new StreamingStateChangedEventArgs(contextId, oldState, newState));
                }

                //_log?.Info($"[ImprovedRtspStreamingService] State changed for {contextId}: {oldState} -> {newState}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[ImprovedRtspStreamingService] UpdateState error: {ex.Message}");
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

            // Playing 상태가 아닌 컨텍스트를 Weak Reference로 이동 (GC가 자동으로 수집하도록)
            foreach (var kvp in _contexts.ToList())
            {
                if (kvp.Value.State != PlaybackState.Playing &&
                    kvp.Value.State != PlaybackState.Buffering)
                {
                    _weakContexts[kvp.Key] = new WeakReference<ImprovedStreamingContext>(kvp.Value);
                    _contexts.TryRemove(kvp.Key, out _);
                }
            }

            // 비동기 GC 요청 (비블로킹 - 백그라운드에서 수집)
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);

            _log?.Info($"[ImprovedRtspStreamingService] Memory cleanup requested. Current: {memoryInfo / 1024 / 1024}MB");
        }
    }

    private AsyncRetryPolicy CreateRetryPolicy()
    {
        // Linear backoff (500ms 간격) - 빠른 재시도로 초기 연결 속도 개선
        // 기존 Exponential: 2s, 4s, 8s... → 총 14초 이상
        // 변경 Linear: 500ms, 1000ms, 1500ms... → 총 3초 (3회 시)
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _setupModel.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromMilliseconds(500 * retryAttempt),
                (exception, timeSpan, retryCount, context) =>
                {
                    _log?.Warning($"[ImprovedRtspStreamingService] Retry {retryCount}/{_setupModel.MaxRetryAttempts} after {timeSpan.TotalMilliseconds}ms: {exception.Message}");
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
                        ? $"{remaining}s"  : "-s";
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
                    // Direct-connect 경로: 기존 VLC 세션 종료
                    context.EndReconnect();
                    UpdateState(contextId, PlaybackState.Stopped);
                    _ = Task.Run(() => DisconnectAsync(contextId));
                }
                else if (_players.TryGetValue(contextId, out var hubPlayer))
                {
                    // Hub-mode 경로: _contexts에 없는 contextId는 Hub 뷰어
                    // Hub MediaPlayer는 공유이므로 끄지 않고, 뷰어(Row)만 종료
                    _log?.Info($"[ImprovedRtspStreamingService] Hub-mode timeout — stopping viewer  contextId={contextId}");
                    hubPlayer.RequestHubStop();
                }
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

            // 운영 타이머 정지 (신규 타임아웃/메모리 체크 방지)
            _operationTimer?.Dispose();
            _connectionStartTimes.Clear();

            // 메모리 모니터 타이머 정지
            _memoryMonitorTimer?.Dispose();

            // 서비스 수준 CancellationToken 취소 — async void 잔존 Task 일괄 취소 (SE-08)
            _serviceCts.Cancel();
            _serviceCts.Dispose();

            // SE-10: Secondary 먼저 정리 후 Primary 정리 (Shared Session RefCount 순서 보장)
            var secondaryIds = _secondaryToPrimary.Keys
                .Where(id => _sharedSessions.TryGetValue(
                    _secondaryToPrimary.GetValueOrDefault(id, string.Empty), out var s)
                    && s.PrimaryContextId != id)
                .ToList();
            if (secondaryIds.Count > 0)
                Task.WhenAll(secondaryIds.Select(DisconnectAsync)).Wait(TimeSpan.FromSeconds(5));

            // 나머지(Primary + 비공유) 컨텍스트 정리
            var disconnectTask = DisconnectAllAsync();
            disconnectTask.Wait(TimeSpan.FromSeconds(15));

            // Player Registry 정리
            ClearAllPlayers();

            // Context Locks 정리
            foreach (var lockItem in _contextLocks)
            {
                lockItem.Value?.Dispose();
            }
            _contextLocks.Clear();

            // WeakReference 정리 (SE-09 — 장시간 운영 후 메모리 누수 방지)
            _weakContexts.Clear();

            // Shared Session Pool 정리 (SE-10, IMPL-06)
            _sharedSessions.Clear();
            foreach (var sem in _cameraKeyLocks.Values) sem.Dispose();
            _cameraKeyLocks.Clear();
            _secondaryToPrimary.Clear();
            _portRegistry.Clear();

            // Pool 정리
            _contextPool?.Clear();

            // LibVLC 정리
            if (_libVLC != null)
            {
                _libVLC.Log -= OnLibVLCLog;
                _libVLC.Dispose();
            }

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

    // ── Phase 1: Shared Session Pool 필드 ──────────────────────────────────
    // CameraKey → SharedSessionState (동일 카메라 세션 공유 레지스트리)
    private readonly ConcurrentDictionary<string, SharedSessionState> _sharedSessions = new();
    // CameraKey → SemaphoreSlim (cameraKey 단위 TOCTOU 방지 Lock)
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cameraKeyLocks = new();
    // contextId → CameraKey (Secondary → Primary 역방향 조회)
    private readonly ConcurrentDictionary<string, string> _secondaryToPrimary = new();
    // Phase 2: sout 릴레이 포트 레지스트리
    private readonly RtspRelayPortRegistry _portRegistry = new();

    // 컨텍스트별 타이머
    private Timer _operationTimer;
    // 전체 연결 시작 시간 추적
    private readonly ConcurrentDictionary<string, DateTime> _connectionStartTimes = new();
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly Timer _memoryMonitorTimer;
    private LibVLC? _libVLC;
    private bool _disposed;
    private readonly CancellationTokenSource _serviceCts = new();

    #endregion

    #region - Phase 1: Shared Session Pool Helpers -

    /// <summary>
    /// 공유 세션 획득.
    /// CameraKey 단위 Lock으로 TOCTOU 방지.
    /// 반환: (isNewPrimary=true → 새 Primary로 연결 진행, false → Secondary 등록만)
    /// </summary>
    private async Task<(bool isNewPrimary, SharedSessionState sharedState)>
        TryAcquireSharedSession(string contextId, string cameraKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(cameraKey))
        {
            // CameraKey 없으면 항상 독립 Primary
            var fallback = new SharedSessionState { CameraKey = string.Empty, PrimaryContextId = contextId };
            Interlocked.Exchange(ref fallback.RefCount, 1);
            return (true, fallback);
        }

        var cameraLock = _cameraKeyLocks.GetOrAdd(cameraKey, _ => new SemaphoreSlim(1, 1));
        if (!await cameraLock.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false))
        {
            _log?.Warning($"[SharedSession] CameraKey lock timeout for {cameraKey}, falling back to independent session");
            var fallback = new SharedSessionState { CameraKey = cameraKey, PrimaryContextId = contextId };
            Interlocked.Exchange(ref fallback.RefCount, 1);
            return (true, fallback);
        }

        try
        {
            if (_sharedSessions.TryGetValue(cameraKey, out var existing))
            {
                // 기존 세션 있음 — Secondary로 등록
                Interlocked.Increment(ref existing.RefCount);
                existing.SecondaryContextIds.Add(contextId);
                _secondaryToPrimary[contextId] = cameraKey;
                _log?.Info($"[SharedSession] Secondary registered: {contextId} → {cameraKey} (RefCount={existing.RefCount})");
                return (false, existing);
            }
            else
            {
                // 신규 세션 — Primary로 생성
                var newState = new SharedSessionState
                {
                    CameraKey = cameraKey,
                    PrimaryContextId = contextId
                };
                Interlocked.Exchange(ref newState.RefCount, 1);
                _sharedSessions[cameraKey] = newState;
                _secondaryToPrimary[contextId] = cameraKey;
                _log?.Info($"[SharedSession] Primary created: {contextId} → {cameraKey}");
                return (true, newState);
            }
        }
        finally
        {
            cameraLock.Release();
        }
    }

    /// <summary>
    /// 공유 세션 해제. RefCount가 0이 되면 실제 Primary 정리.
    /// </summary>
    private async Task ReleaseSharedSession(string contextId)
    {
        if (!_secondaryToPrimary.TryGetValue(contextId, out var cameraKey))
            return;

        if (!_sharedSessions.TryGetValue(cameraKey, out var shared))
        {
            _secondaryToPrimary.TryRemove(contextId, out _);
            return;
        }

        var cameraLock = _cameraKeyLocks.GetOrAdd(cameraKey, _ => new SemaphoreSlim(1, 1));
        await cameraLock.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        try
        {
            var remaining = Interlocked.Decrement(ref shared.RefCount);
            _secondaryToPrimary.TryRemove(contextId, out _);
            shared.SecondaryContextIds.Remove(contextId);

            if (remaining <= 0)
            {
                // 마지막 소비자 — Primary 실제 정리
                _sharedSessions.TryRemove(cameraKey, out _);
                _log?.Info($"[SharedSession] Last consumer released, cleaning up Primary: {shared.PrimaryContextId}");
                await CleanupContextAsync(shared.PrimaryContextId).ConfigureAwait(false);
            }
            else
            {
                _log?.Info($"[SharedSession] Released: {contextId}, remaining RefCount={remaining}");
            }
        }
        finally
        {
            cameraLock.Release();
            // RefCount=0이면 cameraLock도 제거
            if (!_sharedSessions.ContainsKey(cameraKey))
            {
                if (_cameraKeyLocks.TryRemove(cameraKey, out var removedLock))
                    removedLock.Dispose();
            }
        }
    }

    /// <summary>
    /// Primary 에러/재연결 상태를 모든 Secondary에 브로드캐스트.
    /// </summary>
    private void BroadcastStateToSecondaries(string primaryContextId, PlaybackState state)
    {
        // primaryContextId로 SharedSessionState 역방향 조회
        if (!_secondaryToPrimary.TryGetValue(primaryContextId, out var cameraKey))
            return;
        if (!_sharedSessions.TryGetValue(cameraKey, out var shared))
            return;
        if (shared.PrimaryContextId != primaryContextId)
            return;

        foreach (var secondaryId in shared.SecondaryContextIds.ToList())
        {
            UpdateState(secondaryId, state);
            _log?.Info($"[SharedSession] Broadcast {state} → Secondary {secondaryId}");
        }
    }

    /// <summary>
    /// 연결 정보 → 카메라 고유 키.
    /// 동일 물리 스트림이면 이벤트/Row에 관계없이 동일 키를 반환.
    /// 형식: "{IpAddress}:{Port}/{StreamPath}" 또는 URL 기반 정규화 키
    /// </summary>
    private static string DeriveKey(RtspConnectionInfo? info)
    {
        if (info == null) return string.Empty;
        return info.GetCameraKey();
    }

    #endregion
}

/// <summary>
/// 동일 카메라 키에 대한 공유 세션 상태.
/// RefCount가 0이 될 때만 Primary MediaPlayer를 실제로 종료한다.
/// </summary>
internal sealed class SharedSessionState
{
    /// <summary>물리 카메라 고유 키 (IpAddress:Port/Path)</summary>
    public string CameraKey { get; init; } = string.Empty;

    /// <summary>현재 이 세션을 사용하는 contextId 수 (Primary 포함)</summary>
    public int RefCount;  // Interlocked로 증감

    /// <summary>실제 MediaPlayer가 생성된 Primary contextId</summary>
    public string PrimaryContextId { get; set; } = string.Empty;

    /// <summary>Primary에 종속된 Secondary contextId 목록</summary>
    public List<string> SecondaryContextIds { get; } = new();

    /// <summary>Phase 2: sout 릴레이가 활성화되었는지 여부</summary>
    public bool HasSoutRelay { get; set; }

    /// <summary>Phase 2: sout 릴레이 포트 번호</summary>
    public int SoutRelayPort { get; set; }
}