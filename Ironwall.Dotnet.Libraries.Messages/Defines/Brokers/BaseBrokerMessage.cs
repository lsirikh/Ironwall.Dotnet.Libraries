using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
/****************************************************************************
   Purpose      : 모든 메시지의 기본 클래스 (Non-Generic)                                                      
   Created By   : GHLee                                                
   Created On   : 10/30/2025 1:30:22 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseBrokerMessage
{
    /// <summary>
    /// 메시지 고유 식별자 (GUID)
    /// </summary>
    [JsonProperty(Order = 1, PropertyName = "id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 메시지 타입 (REQ, RSP, ACK 등)
    /// </summary>
    [JsonProperty(Order = 2, PropertyName = "type_message")]
    public string TypeMessage { get; set; }=string.Empty;

    /// <summary>
    /// 명령 타입 (EVENT_CALL 등)
    /// </summary>
    [JsonProperty(Order = 3, PropertyName = "type_command")]
    public string Command { get; set; }= string.Empty;

    /// <summary>
    /// 발신 시스템 UUID
    /// </summary>
    [JsonProperty(Order = 4, PropertyName = "from")]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// 응답 생성 시각 (ISO 8601 형식)
    /// </summary>
    [JsonProperty(Order = 99, PropertyName = "timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}

/****************************************************************************
   Purpose      : Generic Body를 지원하는 메시지 기본 클래스                                                      
   Created By   : GHLee                                                
   Created On   : 10/30/2025 3:00:00 PM                                                    
****************************************************************************/
public abstract class BaseMessage<T> : BaseBrokerMessage where T : class
{
    /// <summary>
    /// 메시지 본문 (Generic 타입)
    /// </summary>
    [JsonProperty(Order = 5, PropertyName = "data")]
    public T? Data { get; set; }
}
