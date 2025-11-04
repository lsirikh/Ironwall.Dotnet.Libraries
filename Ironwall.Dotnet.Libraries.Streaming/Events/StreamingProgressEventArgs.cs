using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:13:04 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 스트리밍 진행률 이벤트 인자
/// </summary>
public class StreamingProgressEventArgs : EventArgs
{
    public string ContextId { get; }
    public float BufferPercentage { get; }
    public TimeSpan Position { get; }
    public TimeSpan Duration { get; }
    public long BytesReceived { get; }
    public double Bitrate { get; }

    public StreamingProgressEventArgs(
        string contextId,
        float bufferPercentage,
        TimeSpan position,
        TimeSpan duration,
        long bytesReceived = 0,
        double bitrate = 0)
    {
        ContextId = contextId;
        BufferPercentage = bufferPercentage;
        Position = position;
        Duration = duration;
        BytesReceived = bytesReceived;
        Bitrate = bitrate;
    }
}