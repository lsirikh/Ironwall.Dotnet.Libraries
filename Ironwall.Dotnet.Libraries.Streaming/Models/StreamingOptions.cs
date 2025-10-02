using Ironwall.Dotnet.Libraries.Base.Models;
using System;
using System.Dynamic;

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
public class StreamingOptions : BaseModel
{
    // 네트워크 설정
    public int NetworkCaching { get; set; } = 300;
    public bool UseTcp { get; set; } = true;
    public int FrameBufferSize { get; set; } = 100000;
    public int ConnectionTimeoutSeconds { get; set; } = 10;

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
    public string VideoCodec { get; set; } = "h264";

    // 메모리 최적화
    public bool EnableMemoryOptimization { get; set; } = true;
    public int MaxBufferSizeMB { get; set; } = 50;

    // 로깅
    public bool EnableDebugLogging { get; set; } = false;
    public bool EnableStatistics { get; set; } = true;

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
            AllowFrameSkip = true,
            EnableMemoryOptimization = true,
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
            AllowFrameSkip = false,
            UseHardwareAcceleration = true,
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
            VideoCodec = this.VideoCodec,

            // 메모리 최적화
            EnableMemoryOptimization = this.EnableMemoryOptimization,
            MaxBufferSizeMB = this.MaxBufferSizeMB,

            // 로깅
            EnableDebugLogging = this.EnableDebugLogging,
            EnableStatistics = this.EnableStatistics,

        };
    }
}