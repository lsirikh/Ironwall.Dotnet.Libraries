using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 2:10:48 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SendMessageResponseModel : ResponseModel
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
    List<SenderSpecModel> SenderList { get; set; } = new List<SenderSpecModel>();

    [JsonProperty("next_yn", Order = 4)]
    public string IsNext { get; set; } = string.Empty;
    #endregion
    #region - Attributes -
    #endregion
}