using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:10:15 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 스트리밍 통계 정보 DTO
/// </summary>
public class StreamingStatistics
{
    // 기본 정보
    public string ContextId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public TimeSpan TotalPlayTime { get; set; }

    // 데이터 통계
    public long TotalBytesReceived { get; set; }
    public long TotalBytesSent { get; set; }
    public long TotalPacketsReceived { get; set; }
    public long TotalPacketsLost { get; set; }

    // 프레임 통계
    public long TotalFramesReceived { get; set; }
    public long TotalFramesDecoded { get; set; }
    public long DroppedFrames { get; set; }
    public long CorruptedFrames { get; set; }

    // 비트레이트
    public double AverageBitrate { get; set; }
    public double CurrentBitrate { get; set; }
    public double PeakBitrate { get; set; }
    public double MinBitrate { get; set; }

    // 연결 정보
    public int ReconnectCount { get; set; }
    public int ErrorCount { get; set; }
    public int BufferingCount { get; set; }
    public TimeSpan TotalBufferingTime { get; set; }

    // 성능 메트릭
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageBytes { get; set; }
    public double GpuUsagePercent { get; set; }
    public int DecodingThreadCount { get; set; }

    // 네트워크 지연
    public double NetworkLatencyMs { get; set; }
    public double JitterMs { get; set; }

    // 비디오 정보
    public int VideoWidth { get; set; }
    public int VideoHeight { get; set; }
    public double FrameRate { get; set; }
    public string? VideoCodec { get; set; }

    // 오디오 정보
    public int AudioChannels { get; set; }
    public int AudioSampleRate { get; set; }
    public string? AudioCodec { get; set; }

    /// <summary>
    /// 프레임 드롭 비율 계산 (%)
    /// </summary>
    public double GetDroppedFramesPercent()
    {
        if (TotalFramesReceived == 0) return 0;
        return (double)DroppedFrames / TotalFramesReceived * 100;
    }

    /// <summary>
    /// 패킷 손실율 계산 (%)
    /// </summary>
    public double GetPacketLossPercent()
    {
        var totalPackets = TotalPacketsReceived + TotalPacketsLost;
        if (totalPackets == 0) return 0;
        return (double)TotalPacketsLost / totalPackets * 100;
    }

    /// <summary>
    /// 비트레이트 포맷팅
    /// </summary>
    public string GetFormattedBitrate()
    {
        return FormatBitrate(CurrentBitrate);
    }

    public string GetFormattedAverageBitrate()
    {
        return FormatBitrate(AverageBitrate);
    }

    private string FormatBitrate(double bitrate)
    {
        if (bitrate > 1024 * 1024)
            return $"{bitrate / 1024 / 1024:F2} Mbps";
        else if (bitrate > 1024)
            return $"{bitrate / 1024:F2} Kbps";
        else
            return $"{bitrate:F2} bps";
    }

    /// <summary>
    /// 메모리 사용량 포맷팅
    /// </summary>
    public string GetFormattedMemoryUsage()
    {
        if (MemoryUsageBytes > 1024 * 1024 * 1024)
            return $"{MemoryUsageBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        else if (MemoryUsageBytes > 1024 * 1024)
            return $"{MemoryUsageBytes / 1024.0 / 1024.0:F2} MB";
        else if (MemoryUsageBytes > 1024)
            return $"{MemoryUsageBytes / 1024.0:F2} KB";
        else
            return $"{MemoryUsageBytes} B";
    }

    /// <summary>
    /// 통계 요약 문자열 생성
    /// </summary>
    public string GetSummary()
    {
        return $"Stream: {ContextId}\n" +
               $"Play Time: {TotalPlayTime:hh\\:mm\\:ss}\n" +
               $"Bitrate: {GetFormattedBitrate()}\n" +
               $"Resolution: {VideoWidth}x{VideoHeight}@{FrameRate:F1}fps\n" +
               $"Dropped Frames: {GetDroppedFramesPercent():F1}%\n" +
               $"Packet Loss: {GetPacketLossPercent():F1}%\n" +
               $"Memory: {GetFormattedMemoryUsage()}\n" +
               $"Errors: {ErrorCount}, Reconnects: {ReconnectCount}";
    }

    /// <summary>
    /// 통계 초기화
    /// </summary>
    public void Reset()
    {
        StartTime = DateTime.Now;
        LastUpdateTime = DateTime.Now;
        TotalPlayTime = TimeSpan.Zero;

        TotalBytesReceived = 0;
        TotalBytesSent = 0;
        TotalPacketsReceived = 0;
        TotalPacketsLost = 0;

        TotalFramesReceived = 0;
        TotalFramesDecoded = 0;
        DroppedFrames = 0;
        CorruptedFrames = 0;

        AverageBitrate = 0;
        CurrentBitrate = 0;
        PeakBitrate = 0;
        MinBitrate = 0;

        ReconnectCount = 0;
        ErrorCount = 0;
        BufferingCount = 0;
        TotalBufferingTime = TimeSpan.Zero;

        CpuUsagePercent = 0;
        MemoryUsageBytes = 0;
        GpuUsagePercent = 0;

        NetworkLatencyMs = 0;
        JitterMs = 0;
    }
}