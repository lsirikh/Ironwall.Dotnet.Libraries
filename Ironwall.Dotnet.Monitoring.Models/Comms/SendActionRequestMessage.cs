using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Comms;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 10/30/2025 11:20:43 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SendActionRequestMessage
{
    /// <summary>
    /// DetectionEvent/Malfunction의 DB ID
    /// </summary>
    public int EventId { get; set; }

    /// <summary>
    /// EventType을 통한 타입 조회
    /// </summary>
    public EnumEventType EventType { get; set; }

    /// <summary>
    /// 조치 내용
    /// </summary>
    public string? ActionDetails { get; set; }

    /// <summary>
    /// 조치 시각
    /// </summary>
    public DateTime ActionTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 조치자 정보 (사용자명 또는 ID)
    /// </summary>
    public string? ActionUser { get; set; }
}