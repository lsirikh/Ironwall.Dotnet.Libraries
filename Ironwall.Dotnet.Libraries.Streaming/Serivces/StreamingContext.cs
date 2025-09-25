using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using LibVLCSharp.Shared;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 2:31:50 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 스트림 컨텍스트 (Object Pool 지원)
/// </summary>
public class StreamingContext : IPooledObject
{
    // 상태 관리 추가
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
    public ILogService? _log;
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
    public StreamingStatistics? Statistics { get; private set; }

    // 이벤트 핸들러 저장용 (메모리 누수 방지)
    public Dictionary<string, Delegate>? EventHandlers { get; set; }

    // Object Pool 관련
    public bool IsInUse { get; private set; }

    public void Initialize(
        string id,
        RtspConnectionInfo connectionInfo,
        StreamingOptions options,
        LibVLC libVLC)
    {
        lock (_lock)
        {
            // 상태 검증
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
                // MediaPlayer 생성 확인
                _log?.Info($"[StreamingContext] MediaPlayer created successfully for {id}");

                _state = ContextState.Active;
            }
            catch
            {
                _state = ContextState.Idle;
                throw;
            }
           
        }
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
                ReconnectAttempts = 0;
                FrameSkipCount = 0;
                LastConnectionTime = DateTime.MinValue;
                EventHandlers = null;
                State = PlaybackState.None;
                IsInUse = false;
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