using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Messages;
/****************************************************************************
       Purpose      : 외부 채널에서 수신하는 이벤트 메시지 모델                                                     
       Created By   : GHLee                                                
       Created On   : 10/13/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/

/// <summary>
/// Redis 또는 외부 메시지 채널에서 수신하는 이벤트 메시지
/// </summary>
public class EventMessage
{
    /// <summary>
    /// 이벤트 고유 ID (외부 시스템에서 생성)
    /// </summary>
    [JsonProperty("event_id", Order = 1)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// 이벤트에 연동된 디바이스 그룹
    /// </summary>
    [JsonProperty("event_group", Order = 1)]
    public int EventGroup { get; set; }

    /// <summary>
    /// 이벤트 발생 시각
    /// </summary>
    [JsonProperty("created", Order = 3)]
    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>
    /// 이벤트 상세 정보
    /// </summary>
    [JsonProperty("details", Order = 4)]
    public string? Details { get; set; }

    /// <summary>
    /// 이벤트 타입
    /// </summary>
    [JsonProperty("event_command", Order = 5)]
    public EnumPopupCmd Cmd { get; set; }

    public override string ToString()
    {
        return $"[EventMessage] Id={EventId}, Group={EventGroup}, Cmd={Cmd}, Time={Created:yyyy-MM-dd HH:mm:ss}";
    }
}