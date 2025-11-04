using Ironwall.Dotnet.Libraries.Messages.Defines;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Models;
/****************************************************************************
   Purpose      : EVENT_CALL 명령을 위한 요청 메시지
   Created By   : GHLee                                                
   Created On   : 10/30/2025 3:00:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

/// <summary>
/// EVENT_CALL 전용 RequestMessage
/// RequestMessage<EventCallRequestBody>를 상속하여 타입 안정성 제공
/// </summary>
public class EventCallRequestMessage : RequestMessage<EventCallRequestBody>
{
    public EventCallRequestMessage()
    {
        Cmd = EnumCommandType.EVENT_CALL;
        Body = new EventCallRequestBody();
    }

    public override string ToString()
    {
        return $"[{MType}] EVENT_CALL from {From} to {Target} - Event: {Body?.EventName}, State: {Body?.State}";
    }
}
