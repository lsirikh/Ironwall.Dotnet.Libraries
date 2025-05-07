using Dotnet.Gym.Message.Enums;
using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/6/2025 1:06:38 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ResponseModel
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
    /// 결과 코드 (1: 성공, 그 외: 실패)
    /// </summary>
    [JsonProperty("result_code", Order = 1)]
    public int ResultCode { get; set; }

    /// <summary>
    /// 응답 메시지 (에러 메시지 포함)
    /// </summary>
    [JsonProperty("message", Order = 2)]
    public string Message { get; set; } = string.Empty;
    #endregion
    #region - Attributes -
    #endregion
}