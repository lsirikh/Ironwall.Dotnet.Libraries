using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Controls;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using LibVLCSharp.Shared;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/29/2025 3:55:45 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 개선된 스트리밍 컨텍스트 - RtspPlayer 통합 관리
/// </summary>
public class ImprovedStreamingContext : IPooledObject
{
    private enum ContextState
    {
        Idle,
        Initializing,
        Active,
        Cleaning,
        Disposed
    }

    private ContextState _state = ContextState.Idle;
    private Media? _media;
    private MediaPlayer? _mediaPlayer;
    private ImprovedRtspPlayer? _rtspPlayer;  // RtspPlayer 참조 추가
    private ILogService? _log;
    private readonly object _lock = new object();


    // Properties
    public string? Id { get; private set; }
    public RtspConnectionInfo? ConnectionInfo { get; private set; }
    public StreamingOptions? Options { get; private set; }
    public PlaybackState State { get; set; }
    public DateTime LastConnectionTime { get; set; }
    public int ReconnectAttempts { get; set; }
    public int FrameSkipCount { get; set; }
    public MediaPlayer? MediaPlayer => _mediaPlayer;
    public ImprovedRtspPlayer? Player => _rtspPlayer;  // Player 접근자 추가
    public StreamingStatistics? Statistics { get; private set; }
    public Dictionary<string, Delegate>? EventHandlers { get; set; }
    public bool IsInUse { get; private set; }

    // 재연결 상태 플래그 추가
    private volatile bool _isReconnecting = false;
    public bool IsReconnecting
    {
        get => _isReconnecting;
        set => _isReconnecting = value;
    }

    /// <summary>
    /// Context 초기화 - RtspPlayer 포함
    /// </summary>
    public void Initialize(
        string id,
        RtspConnectionInfo connectionInfo,
        StreamingOptions options,
        LibVLC libVLC,
        ImprovedRtspPlayer? player = null)
    {
        lock (_lock)
        {
            if (_state != ContextState.Idle)
            {
                throw new InvalidOperationException($"Cannot initialize context in state {_state}");
            }

            _state = ContextState.Initializing;

            try
            {
                // 기존 리소스 정리
                if (_mediaPlayer != null || _media != null)
                {
                    CleanupInternal();
                }

                _log = IoC.Get<ILogService>();

                Id = id;
                ConnectionInfo = connectionInfo;
                Options = options;
                _rtspPlayer = player;  // Player 할당
                State = PlaybackState.None;
                ReconnectAttempts = 0;
                FrameSkipCount = 0;
                LastConnectionTime = DateTime.MinValue;
                IsInUse = true;
                EventHandlers = new Dictionary<string, Delegate>();

                // 통계 초기화
                Statistics = new StreamingStatistics
                {
                    ContextId = id,
                    StartTime = DateTime.Now
                };

                // MediaPlayer 생성
                _mediaPlayer = new MediaPlayer(libVLC);
                if (_mediaPlayer == null)
                {
                    throw new InvalidOperationException("Failed to create MediaPlayer");
                }

                _mediaPlayer.Volume = options.Volume;
                _mediaPlayer.Mute = options.IsMuted;
                _mediaPlayer.EnableHardwareDecoding = options.UseHardwareAcceleration;

                // RtspPlayer가 있으면 VideoView 연결
                if (_rtspPlayer != null)
                {
                    _log?.Info($"[ImprovedStreamingContext] RtspPlayer linked for {id}");
                    // Player의 VideoView는 나중에 UI 스레드에서 연결
                }

                _log?.Info($"[ImprovedStreamingContext] Context initialized for {id}");
                _state = ContextState.Active;
            }
            catch
            {
                _state = ContextState.Idle;
                throw;
            }
        }
    }

    /// <summary>
    /// RtspPlayer 연결
    /// </summary>
    public void AttachPlayer(ImprovedRtspPlayer player)
    {
        lock (_lock)
        {
            if (_state != ContextState.Active)
            {
                throw new InvalidOperationException($"Cannot attach player in state {_state}");
            }

            _rtspPlayer = player;
            _log?.Info($"[ImprovedStreamingContext] Player attached for {Id}");
        }
    }

    /// <summary>
    /// RtspPlayer 분리
    /// </summary>
    public void DetachPlayer()
    {
        lock (_lock)
        {
            _rtspPlayer = null;
            _log?.Info($"[ImprovedStreamingContext] Player detached for {Id}");
        }
    }

    /// <summary>
    /// Player 상태 업데이트
    /// </summary>
    public void UpdatePlayerState(PlaybackState state, string? statusMessage = null)
    {

        // NULL 체크 추가
        if (_rtspPlayer == null)
        {
            _log?.Warning($"[ImprovedStreamingContext] Player is null, skipping update for {Id}");
            State = state;  // 상태만 업데이트
            return;
        }

        // UI 스레드에서 실행
        DispatcherService.Invoke(() =>
        {
            try
            {
                // Player가 여전히 유효한지 재확인
                if (_rtspPlayer != null)
                {
                    _rtspPlayer.SetValue(ImprovedRtspPlayer.PlaybackStateProperty, state);

                    if (!string.IsNullOrEmpty(statusMessage))
                    {
                        _rtspPlayer.SetValue(ImprovedRtspPlayer.StatusMessageProperty, statusMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"[ImprovedStreamingContext] Failed to update player state: {ex.Message}");
            }
        });

        State = state;
    }

    public void SetMedia(Media media)
    {
        lock (_lock)
        {
            // 이전 Media 정리
            if (_media != null)
            {
                try
                {
                    _media.Dispose();
                }
                catch { }
            }

            _media = media;

            if (_mediaPlayer != null && media != null)
            {
                _mediaPlayer.Media = media;
            }
        }
    }

    public void UpdateStatistics(long bytesReceived = 0, long framesReceived = 0, long droppedFrames = 0)
    {
        if (Statistics != null)
        {
            Statistics.TotalBytesReceived += bytesReceived;
            Statistics.TotalFramesReceived += framesReceived;
            Statistics.DroppedFrames += droppedFrames;
            Statistics.TotalPlayTime = DateTime.Now - Statistics.StartTime;
        }
    }

    public void Cleanup()
    {
        lock (_lock)
        {
            if (_state == ContextState.Disposed || _state == ContextState.Cleaning)
            {
                return;
            }

            _state = ContextState.Cleaning;

            try
            {
                CleanupInternal();
            }
            finally
            {
                _state = ContextState.Idle;
            }
        }
    }

    private void CleanupInternal()
    {
        try
        {
            // Player 분리
            if (_rtspPlayer != null)
            {
                // UI 스레드에서 Player 정리
                DispatcherService.Invoke(() =>
                {
                    try
                    {
                        _rtspPlayer.SetValue(ImprovedRtspPlayer.PlaybackStateProperty, PlaybackState.None);
                        _rtspPlayer.SetValue(ImprovedRtspPlayer.StatusMessageProperty, "Disconnected");
                    }
                    catch { }
                });

                _rtspPlayer = null;
            }

            // 이벤트 핸들러 제거
            RemoveEventHandlers();

            // MediaPlayer 정지 및 정리
            if (_mediaPlayer != null)
            {
                try
                {
                    if (_mediaPlayer.IsPlaying)
                    {
                        _mediaPlayer.Stop();
                    }
                    _mediaPlayer.Dispose();
                }
                catch (Exception ex)
                {
                    _log?.Warning($"Error disposing MediaPlayer: {ex.Message}");
                }
                finally
                {
                    _mediaPlayer = null;
                }
            }

            // Media 정리
            if (_media != null)
            {
                try
                {
                    _media.Dispose();
                }
                catch (Exception ex)
                {
                    _log?.Warning($"Error disposing Media: {ex.Message}");
                }
                finally
                {
                    _media = null;
                }
            }

            IsInUse = false;
            State = PlaybackState.None;
            EventHandlers?.Clear();
        }
        catch (Exception ex)
        {
            _log?.Error($"Error in CleanupInternal: {ex.Message}");
        }
    }

    private void RemoveEventHandlers()
    {
        if (_mediaPlayer == null || EventHandlers == null) return;

        try
        {
            // 저장된 핸들러 제거
            if (EventHandlers.TryGetValue("Playing", out var playingHandler))
                _mediaPlayer.Playing -= (EventHandler<EventArgs>)playingHandler;

            if (EventHandlers.TryGetValue("Buffering", out var bufferingHandler))
                _mediaPlayer.Buffering -= (EventHandler<MediaPlayerBufferingEventArgs>)bufferingHandler;

            if (EventHandlers.TryGetValue("Error", out var errorHandler))
                _mediaPlayer.EncounteredError -= (EventHandler<EventArgs>)errorHandler;

            if (EventHandlers.TryGetValue("Ended", out var endedHandler))
                _mediaPlayer.EndReached -= (EventHandler<EventArgs>)endedHandler;

            if (EventHandlers.TryGetValue("TimeChanged", out var timeHandler))
                _mediaPlayer.TimeChanged -= (EventHandler<MediaPlayerTimeChangedEventArgs>)timeHandler;

            EventHandlers.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing event handlers: {ex.Message}");
        }
    }

    // IPooledObject
    public void Reset()
    {
        lock (_lock)
        {
            try
            {
                if (_state != ContextState.Idle)
                {
                    Cleanup();
                }

                // 모든 필드 초기화
                Id = null;
                ConnectionInfo = null;
                Options = null;
                Statistics = null;
                _rtspPlayer = null;  // Player 참조 초기화
                ReconnectAttempts = 0;
                FrameSkipCount = 0;
                LastConnectionTime = DateTime.MinValue;
                EventHandlers = null;
                State = PlaybackState.None;
                IsInUse = false;
                _isReconnecting = false;
            }
            catch (Exception ex)
            {
                _log?.Error($"Error during Reset: {ex.Message}");
            }
        }
    }
}


public interface IPooledObject
{
    bool IsInUse { get; }
    void Reset();
}