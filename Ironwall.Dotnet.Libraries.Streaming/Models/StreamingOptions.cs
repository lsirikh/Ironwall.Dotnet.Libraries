using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 2:24:59 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 스트리밍 옵션 DTO
/// </summary>
public class StreamingOptions
{
    // 네트워크 설정
    public int NetworkCaching { get; set; } = 300;
    public bool UseTcp { get; set; } = true;
    public int FrameBufferSize { get; set; } = 100000;
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    public int RtpTimeout { get; set; } = 60000; // RTP 타임아웃 (ms)

    // 성능 설정
    public bool UseHardwareAcceleration { get; set; } = true;
    public bool AllowFrameSkip { get; set; } = true;
    public int MaxDecodingThreads { get; set; } = 0; // 0 = auto
    public bool EnableMulticast { get; set; } = false;

    // 재연결 설정
    public bool EnableAutoReconnect { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 3;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public bool ExponentialBackoff { get; set; } = true;

    // 오디오 설정
    public bool IsMuted { get; set; } = false;
    public int Volume { get; set; } = 50;
    public bool EnableAudio { get; set; } = true;
    public int AudioSampleRate { get; set; } = 44100;

    // 비디오 설정
    public bool KeepAspectRatio { get; set; } = true;
    public int MaxFrameRate { get; set; } = 30;
    public int VideoQuality { get; set; } = 100; // 0-100
    public string VideoCodec { get; set; } = "h264";
    public int MaxWidth { get; set; } = 1920;
    public int MaxHeight { get; set; } = 1080;

    // 메모리 최적화
    public bool EnableMemoryOptimization { get; set; } = true;
    public int MaxBufferSizeMB { get; set; } = 50;
    public bool UseRingBuffer { get; set; } = true;
    public int RingBufferSize { get; set; } = 1024 * 1024 * 10; // 10MB

    // 로깅
    public bool EnableDebugLogging { get; set; } = false;
    public bool EnableStatistics { get; set; } = true;

    // 스냅샷
    public string SnapshotFormat { get; set; } = "png";
    public int SnapshotQuality { get; set; } = 85; // JPEG quality

    /// <summary>
    /// 기본 옵션 생성
    /// </summary>
    public static StreamingOptions CreateDefault()
    {
        return new StreamingOptions();
    }

    /// <summary>
    /// 저대역폭 옵션 생성
    /// </summary>
    public static StreamingOptions CreateLowBandwidth()
    {
        return new StreamingOptions
        {
            NetworkCaching = 1000,
            FrameBufferSize = 50000,
            MaxFrameRate = 15,
            VideoQuality = 50,
            AllowFrameSkip = true,
            EnableMemoryOptimization = true,
            MaxWidth = 1280,
            MaxHeight = 720
        };
    }

    /// <summary>
    /// 고품질 옵션 생성
    /// </summary>
    public static StreamingOptions CreateHighQuality()
    {
        return new StreamingOptions
        {
            NetworkCaching = 100,
            FrameBufferSize = 200000,
            MaxFrameRate = 60,
            VideoQuality = 100,
            AllowFrameSkip = false,
            UseHardwareAcceleration = true,
            MaxWidth = 3840,
            MaxHeight = 2160
        };
    }

    /// <summary>
    /// 객체 복제
    /// </summary>
    public StreamingOptions Clone()
    {
        return new StreamingOptions
        {
            // 네트워크 설정
            NetworkCaching = this.NetworkCaching,
            UseTcp = this.UseTcp,
            FrameBufferSize = this.FrameBufferSize,
            ConnectionTimeoutSeconds = this.ConnectionTimeoutSeconds,
            RtpTimeout = this.RtpTimeout,

            // 성능 설정
            UseHardwareAcceleration = this.UseHardwareAcceleration,
            AllowFrameSkip = this.AllowFrameSkip,
            MaxDecodingThreads = this.MaxDecodingThreads,
            EnableMulticast = this.EnableMulticast,

            // 재연결 설정
            EnableAutoReconnect = this.EnableAutoReconnect,
            MaxReconnectAttempts = this.MaxReconnectAttempts,
            ReconnectDelaySeconds = this.ReconnectDelaySeconds,
            ExponentialBackoff = this.ExponentialBackoff,

            // 오디오 설정
            IsMuted = this.IsMuted,
            Volume = this.Volume,
            EnableAudio = this.EnableAudio,
            AudioSampleRate = this.AudioSampleRate,

            // 비디오 설정
            KeepAspectRatio = this.KeepAspectRatio,
            MaxFrameRate = this.MaxFrameRate,
            VideoQuality = this.VideoQuality,
            VideoCodec = this.VideoCodec,
            MaxWidth = this.MaxWidth,
            MaxHeight = this.MaxHeight,

            // 메모리 최적화
            EnableMemoryOptimization = this.EnableMemoryOptimization,
            MaxBufferSizeMB = this.MaxBufferSizeMB,
            UseRingBuffer = this.UseRingBuffer,
            RingBufferSize = this.RingBufferSize,

            // 로깅
            EnableDebugLogging = this.EnableDebugLogging,
            EnableStatistics = this.EnableStatistics,

            // 스냅샷
            SnapshotFormat = this.SnapshotFormat,
            SnapshotQuality = this.SnapshotQuality
        };
    }
}