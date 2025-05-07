using Dotnet.Gym.Message.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/6/2025 1:11:19 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SendResponseModel : ResponseModel
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
    [JsonProperty("msg_id", Order = 3)]
    public long MsgId { get; set; }

    /// <summary>
    /// 성공적으로 전송된 메시지 수
    /// </summary>
    [JsonProperty("success_cnt", Order = 4)]
    public int SuccessCount { get; set; }

    /// <summary>
    /// 전송 실패한 메시지 수
    /// </summary>
    [JsonProperty("error_cnt", Order = 5)]
    public int ErrorCount { get; set; }

    /// <summary>
    /// 메시지 타입 (SMS, LMS, MMS)
    /// </summary>
    [JsonProperty("msg_type", Order = 6)]
    public EnumMsgType MsgType { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}