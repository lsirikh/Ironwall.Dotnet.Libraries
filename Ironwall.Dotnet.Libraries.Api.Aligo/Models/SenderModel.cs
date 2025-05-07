using Dotnet.Gym.Message.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 1:17:09 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SenderModel
{
    #region - Ctors -
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// 메시지 ID (전송된 메시지의 고유 식별자)
    /// </summary>
    [JsonProperty("msg_id", Order = 1)]
    public long MsgId { get; set; }

    /// <summary>
    /// 메시지 타입 (SMS, LMS, MMS)
    /// </summary>
    [JsonProperty("msg_type", Order = 2)]
    public EnumMsgType MsgType { get; set; }

    /// <summary>
    /// 발신번호
    /// </summary>
    [JsonProperty("sender", Order = 3)]
    public string? Sender { get; set; }

    /// <summary>
    /// 전송 요청 수
    /// </summary>
    [JsonProperty("sms_count", Order = 4)]
    public int SmsCount { get; set; }

    /// <summary>
    /// 요청상태
    /// </summary>
    [JsonProperty("reserve_state", Order = 5)]
    public string? ReserveState { get; set; }

    /// <summary>
    /// 메시지 내용
    /// </summary>
    [JsonProperty("msg", Order = 6)]
    public string? Message { get; set; }

    /// <summary>
    /// 처리실패건수
    /// </summary>
    [JsonProperty("fail_count", Order = 7)]
    public int ErrorCount { get; set; }

    /// <summary>
    /// 등록일
    /// </summary>
    [JsonProperty("reg_date", Order = 8)]
    public DateTime RegisterDate { get; set; }

    /// <summary>
    /// 예약일자
    /// </summary>
    [JsonProperty("reserve", Order = 9)]
    public DateTime? ReserveDate { get; set; }

    #endregion
    #region - Attributes -
    #endregion
}