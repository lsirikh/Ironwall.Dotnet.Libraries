using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 1:15:35 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SendListResponseModel : ResponseModel
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
    /// 목록 배열	
    /// </summary>
    [JsonProperty("list", Order = 3)]
    List<SenderModel> SenderList { get; set; } = new List<SenderModel>();

    [JsonProperty("next_yn", Order = 4)]
    public string IsNext { get; set; } = string.Empty;
    #endregion
    #region - Attributes -
    #endregion
}