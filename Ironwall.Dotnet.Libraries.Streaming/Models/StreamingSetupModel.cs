using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:09:32 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 스트리밍 설정 모델 인터페이스 (전체 서비스 설정)
/// </summary>
public interface IStreamingSetupModel
{
    int MaxConnections { get; set; }
    int MaxRetryAttempts { get; set; }
    long MaxMemoryUsageBytes { get; set; }
    bool EnableDebugLogging { get; set; }
    string LogPath { get; set; }
    string SnapshotPath { get; set; }
    bool UseHardwareAcceleration { get; set; }
    int DefaultNetworkCaching { get; set; }
    int ContextPoolSize { get; set; }
    bool EnableFrameSkipping { get; set; }
    int MaxFrameSkip { get; set; }
    bool IsAutoDiscard { get; set; } 
    int TimeoutSeconds { get; set; }
}

/// <summary>
/// 스트리밍 설정 모델 구현
/// </summary>
public class StreamingSetupModel : IStreamingSetupModel
{
    public int MaxConnections { get; set; } = 17;
    public int MaxRetryAttempts { get; set; } = 5;
    public long MaxMemoryUsageBytes { get; set; } = 1024L * 1024 * 1024; // 1GB
    public bool EnableDebugLogging { get; set; } = false;
    public string LogPath { get; set; } = "logs/streaming";
    public string SnapshotPath { get; set; } = "snapshots/";
    public bool UseHardwareAcceleration { get; set; } = true;
    public int DefaultNetworkCaching { get; set; } = 300;
    public int ContextPoolSize { get; set; } = 32;
    public bool EnableFrameSkipping { get; set; } = true;
    public int MaxFrameSkip { get; set; } = 5;

    // 추가 설정
    public int MemoryCheckIntervalSeconds { get; set; } = 30;
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public int StatisticsUpdateIntervalMs { get; set; } = 1000;
    public bool AutoCleanupInactiveStreams { get; set; } = true;
    public int InactiveStreamTimeoutMinutes { get; set; } = 30;

    public bool IsAutoDiscard { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public StreamingSetupModel()
    {
    }

    /// <summary>
    /// 복사 생성자
    /// </summary>
    public StreamingSetupModel(IStreamingSetupModel model)
    {
        if (model != null)
        {
            MaxConnections = model.MaxConnections;
            MaxRetryAttempts = model.MaxRetryAttempts;
            MaxMemoryUsageBytes = model.MaxMemoryUsageBytes;
            EnableDebugLogging = model.EnableDebugLogging;
            LogPath = model.LogPath;
            SnapshotPath = model.SnapshotPath;
            UseHardwareAcceleration = model.UseHardwareAcceleration;
            DefaultNetworkCaching = model.DefaultNetworkCaching;
            ContextPoolSize = model.ContextPoolSize;
            EnableFrameSkipping = model.EnableFrameSkipping;
            MaxFrameSkip = model.MaxFrameSkip;
            IsAutoDiscard = model.IsAutoDiscard;
            TimeoutSeconds = model.TimeoutSeconds;
        }
    }

    /// <summary>
    /// 개발 환경용 설정 생성
    /// </summary>
    public static StreamingSetupModel CreateForDevelopment()
    {
        return new StreamingSetupModel
        {
            EnableDebugLogging = true,
            MaxConnections = 4,
            ContextPoolSize = 8,
            EnablePerformanceMonitoring = true,
            StatisticsUpdateIntervalMs = 500
        };
    }

    /// <summary>
    /// 프로덕션 환경용 설정 생성
    /// </summary>
    public static StreamingSetupModel CreateForProduction()
    {
        return new StreamingSetupModel
        {
            EnableDebugLogging = false,
            MaxConnections = 32,
            ContextPoolSize = 64,
            MaxMemoryUsageBytes = 2L * 1024 * 1024 * 1024, // 2GB
            EnablePerformanceMonitoring = false
        };
    }
}