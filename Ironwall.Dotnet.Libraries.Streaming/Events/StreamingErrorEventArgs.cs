using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:12:18 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 스트리밍 에러 이벤트 인자
/// </summary>
public class StreamingErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public Exception Exception { get; }
    public DateTime Timestamp { get; }
    public string ContextId { get; }
    public ErrorSeverity Severity { get; }

    public StreamingErrorEventArgs(
        string errorMessage,
        Exception exception = null,
        string contextId = null,
        ErrorSeverity severity = ErrorSeverity.Error)
    {
        ErrorMessage = errorMessage;
        Exception = exception;
        ContextId = contextId;
        Severity = severity;
        Timestamp = DateTime.Now;
    }
}