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
    private Media _media;
    private MediaPlayer _mediaPlayer;
    private bool _isInitialized;
    private readonly object _lock = new object();

    // Properties
    public string Id { get; private set; }
    public RtspConnectionInfo ConnectionInfo { get; private set; }
    public StreamingOptions Options { get; private set; }
    public PlaybackState State { get; set; }
    public DateTime LastConnectionTime { get; set; }
    public int ReconnectAttempts { get; set; }
    public int FrameSkipCount { get; set; }
    public MediaPlayer MediaPlayer => _mediaPlayer;
    public StreamingStatistics Statistics { get; private set; }

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
            if (_isInitialized)
            {
                Cleanup();
            }

            Id = id;
            ConnectionInfo = connectionInfo;
            Options = options;
            State = PlaybackState.None;
            ReconnectAttempts = 0;
            FrameSkipCount = 0;
            LastConnectionTime = DateTime.MinValue;
            IsInUse = true;

            // 통계 초기화
            Statistics = new StreamingStatistics
            {
                ContextId = id,
                StartTime = DateTime.Now
            };

            // MediaPlayer 생성
            _mediaPlayer = new MediaPlayer(libVLC);
            _mediaPlayer.Volume = options.Volume;
            _mediaPlayer.Mute = options.IsMuted;
            _mediaPlayer.EnableHardwareDecoding = options.UseHardwareAcceleration;

            _isInitialized = true;
        }
    }

    public void SetMedia(Media media)
    {
        lock (_lock)
        {
            _media?.Dispose();
            _media = media;
            _mediaPlayer.Media = media;
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
            try
            {
                _mediaPlayer?.Stop();
                _mediaPlayer?.Dispose();
                _media?.Dispose();
            }
            catch
            {
                // 정리 중 예외 무시
            }
            finally
            {
                _mediaPlayer = null;
                _media = null;
                _isInitialized = false;
                IsInUse = false;
                State = PlaybackState.None;
            }
        }
    }

    // IPooledObject
    public void Reset()
    {
        Cleanup();
        Id = null;
        ConnectionInfo = null;
        Options = null;
        Statistics = null;
        ReconnectAttempts = 0;
        FrameSkipCount = 0;
        LastConnectionTime = DateTime.MinValue;
    }
}

public interface IPooledObject
{
    bool IsInUse { get; }
    void Reset();
}