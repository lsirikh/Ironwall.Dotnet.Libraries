using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.RtspPopups;
/****************************************************************************
   Purpose      : EVENT_CALL 명령의 요청 Data                                                      
   Created By   : GHLee                                                
   Created On   : 10/30/2025 3:00:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventCallDto
{
    public EventCallDto()
    {
        EventName = string.Empty;
        Details = string.Empty;
        State = "ON";
    }

    /// <summary>
    /// 이벤트 이름
    /// </summary>
    [JsonProperty(Order = 1, PropertyName = "event_name")]
    public string EventName { get; set; }

    /// <summary>
    /// 이벤트 상세 정보
    /// </summary>
    [JsonProperty(Order = 2, PropertyName = "details")]
    public string Details { get; set; }

    /// <summary>
    /// 이벤트 상태 (on, off)
    /// </summary>
    [JsonProperty(Order = 3, PropertyName = "state")]
    public string State { get; set; }
}
