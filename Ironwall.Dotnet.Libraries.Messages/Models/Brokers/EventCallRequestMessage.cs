using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.RtspPopups;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Models.Brokers;
/****************************************************************************
   Purpose      : EVENT_CALL 명령을 위한 요청 메시지
   Created By   : GHLee                                                
   Created On   : 10/30/2025 3:00:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

/// <summary>
/// EVENT_CALL 전용 BrokerRequest
/// BrokerRequest<EventCallDto>를 상속하여 타입 안정성 제공
/// </summary>
public class EventCallRequestMessage : BrokerRequest<EventCallDto>
{
    public EventCallRequestMessage()
    {
        Command = "EVENT_CALL";
        Data = new EventCallDto();
    }

    public override string ToString()
    {
        return $"[{TypeMessage}] EVENT_CALL from {From} - Event: {Data?.EventName}, State: {Data?.State}";
    }
}
