using Ironwall.Dotnet.Libraries.Streaming.Models;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:11:52 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 스트리밍 상태 변경 이벤트 인자
/// </summary>
public class StreamingStateChangedEventArgs : EventArgs
{
    public string ContextId { get; }
    public PlaybackState OldState { get; }
    public PlaybackState NewState { get; }
    public DateTime Timestamp { get; }

    public StreamingStateChangedEventArgs(
        string contextId,
        PlaybackState oldState,
        PlaybackState newState)
    {
        ContextId = contextId;
        OldState = oldState;
        NewState = newState;
        Timestamp = DateTime.Now;
    }
}