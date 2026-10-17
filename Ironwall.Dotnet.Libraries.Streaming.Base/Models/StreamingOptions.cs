using Ironwall.Dotnet.Libraries.Base.Models;
using System;
using System.Dynamic;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;
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
    // 네트워크 설정 (최적화: 100ms 캐싱으로 초기 연결 속도 개선)
    public int NetworkCaching { get; set; } = 100;
    public bool UseTcp { get; set; } = false;
    public int FrameBufferSize { get; set; } = 200000;
    public int ConnectionTimeoutSeconds { get; set; } = 8;

    // 성능 설정
    public bool UseHardwareAcceleration { get; set; } = true;
    public bool AllowFrameSkip { get; set; } = true;
    public int MaxDecodingThreads { get; set; } = 0; // 0 = auto
    public bool EnableMulticast { get; set; } = false;

    // 재연결 설정 (최적화: Linear backoff로 빠른 재연결)
    public bool EnableAutoReconnect { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 3;
    public int ReconnectDelaySeconds { get; set; } = 1;
    public bool ExponentialBackoff { get; set; } = false;

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
    public int MaxBufferSizeMB { get; set; } = 80;

    // 로깅
    public bool EnableDebugLogging { get; set; } = false;
    public bool EnableStatistics { get; set; } = true;

    // 클럭 동기화 설정
    public int ClockJitterMs { get; set; } = 500;
    public bool EnableClockSync { get; set; } = true;

    // Hub 설정
    public TimeSpan HubGracePeriod { get; set; } = TimeSpan.FromSeconds(1.5);
    public TimeSpan HubSweepInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int RelayPortMin { get; set; } = 15554;
    public int RelayPortMax { get; set; } = 15700;

    /// <summary>SharedSession 풀을 우회하고 독립 Primary로 연결한다.
    /// Hub 직접 연결(Alt B) 등에서 카메라마다 독립 컨텍스트가 필요할 때 사용.</summary>
    public bool BypassSharedSession { get; set; } = false;

    /// <summary>
    /// 기본 옵션 생성
    /// </summary>
    public static StreamingOptions CreateDefault()
    {
        return new StreamingOptions();
    }

    /// <summary>
    /// 빠른 연결 옵션 생성 (초기 연결 속도 최적화)
    /// </summary>
    public static StreamingOptions CreateFastConnect()
    {
        return new StreamingOptions
        {
            NetworkCaching = 100,
            FrameBufferSize = 150000,
            ConnectionTimeoutSeconds = 5,
            ClockJitterMs = 300,
            EnableClockSync = true,
        };
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
    /// 안정성 중시 옵션 생성 (연결 안정성 최적화)
    /// </summary>
    public static StreamingOptions CreateStable()
    {
        return new StreamingOptions
        {
            NetworkCaching = 300,
            FrameBufferSize = 300000,
            MaxReconnectAttempts = 5,
            ReconnectDelaySeconds = 3,
            ExponentialBackoff = true,
            EnableAutoReconnect = true,
            ClockJitterMs = 700,
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

            // 클럭 동기화 설정
            ClockJitterMs = this.ClockJitterMs,
            EnableClockSync = this.EnableClockSync,

            // Hub 설정
            HubGracePeriod = this.HubGracePeriod,
            HubSweepInterval = this.HubSweepInterval,
            RelayPortMin = this.RelayPortMin,
            RelayPortMax = this.RelayPortMax,

            BypassSharedSession = this.BypassSharedSession,
        };
    }
}