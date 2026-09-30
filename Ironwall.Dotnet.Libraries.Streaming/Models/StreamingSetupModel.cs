using System;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:09:32 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
// IStreamingSetupModel(계약)은 Streaming.Base\Models\IStreamingSetupModel.cs 로 옮겼다 — 이름공간은 그대로(T-07).

/// <summary>
/// 스트리밍 설정 모델 구현
/// </summary>
public class StreamingSetupModel : IStreamingSetupModel
{
    public int MaxConnections { get; set; } = 25; // 4 rows × 3 cameras + 여유 (기존 17)
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
    public int TimeoutSeconds { get; set; } = 15;

    // 클럭 동기화 설정
    public int ClockJitterMs { get; set; } = 500;
    public bool EnableClockSync { get; set; } = true;

    // 맵 카메라 팝업 연동 on/off (기본 ON — 신규 설치 시 더블클릭으로 열림)
    public bool IsCameraPopupUsed { get; set; } = true;

    // 맵 카메라 팝업 RTSP 소스 (기본 Url=수동 URL — 현행 동작 무변경)
    public EnumCameraPopupRtspSource CameraPopupRtspSource { get; set; } = EnumCameraPopupRtspSource.Url;

    /// <summary>카메라 팝업 새 설정 덩어리(appsettings <c>CameraPopup</c>). 없으면(<c>null</c>) 옛 키에서 이관한다.</summary>
    public CameraPopupSettingsRecord? CameraPopup { get; set; }

    /// <summary>해석한 카메라 팝업 설정(새 키 + 옛 키 이관 + 정규화).</summary>
    public CameraPopupSettings CameraPopupSettings
        => CameraPopupSettingsCodec.Resolve(CameraPopup, IsCameraPopupUsed, CameraPopupRtspSource, IsAutoDiscard, TimeoutSeconds);

    // 팝업 기동 딜레이
    public int PopupStartupDelayMs { get; set; } = 0;

    // 큐 항목 최소 표시 대기 시간 (ms) — 표시 슬롯이 비워진 후 다음 큐 항목을 올리기까지 대기
    public int QueueMinDisplayMs { get; set; } = 1000;

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
            ClockJitterMs = model.ClockJitterMs;
            EnableClockSync = model.EnableClockSync;
            PopupStartupDelayMs = model.PopupStartupDelayMs;
            QueueMinDisplayMs = model.QueueMinDisplayMs;
            IsCameraPopupUsed = model.IsCameraPopupUsed;
            CameraPopupRtspSource = model.CameraPopupRtspSource;
            // 새 설정은 해석한 값으로 옮긴다(원본이 옛 키만 가진 소비자여도 같은 값이 된다).
            CameraPopup = CameraPopupSettingsCodec.ToRecord(model.CameraPopupSettings);
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