using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.RtspMessage.Defines;
/****************************************************************************
   Purpose      : 모든 메시지의 기본 클래스 (Non-Generic)                                                      
   Created By   : GHLee                                                
   Created On   : 10/30/2025 1:30:22 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseMessage
{
    /// <summary>
    /// 메시지 고유 식별자 (GUID)
    /// </summary>
    [JsonProperty(Order = 1, PropertyName = "id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 메시지 타입 (REQ, RSP, ACK 등)
    /// </summary>
    [JsonProperty(Order = 2, PropertyName = "m_type")]
    public EnumMessageType MType { get; set; }

    /// <summary>
    /// 명령 타입 (EVENT_CALL 등)
    /// </summary>
    [JsonProperty(Order = 3, PropertyName = "cmd")]
    public EnumCommandType Cmd { get; set; }

    /// <summary>
    /// 발신 시스템 UUID
    /// </summary>
    [JsonProperty(Order = 4, PropertyName = "from")]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// 수신 시스템 UUID
    /// </summary>
    [JsonProperty(Order = 5, PropertyName = "target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 메시지 생성 시간 (yyyy-MM-dd HH:mm:ss.fff)
    /// </summary>
    [JsonProperty(Order = 99, PropertyName = "created")]
    public string Created { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
}

/****************************************************************************
   Purpose      : Generic Body를 지원하는 메시지 기본 클래스                                                      
   Created By   : GHLee                                                
   Created On   : 10/30/2025 3:00:00 PM                                                    
****************************************************************************/
public abstract class BaseMessage<TBody> : BaseMessage where TBody : class
{
    /// <summary>
    /// 메시지 본문 (Generic 타입)
    /// </summary>
    [JsonProperty(Order = 6, PropertyName = "body")]
    public TBody? Body { get; set; }
}
