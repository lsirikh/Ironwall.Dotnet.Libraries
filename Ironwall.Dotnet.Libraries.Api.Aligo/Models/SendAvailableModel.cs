using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 2:21:50 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SendAvailableModel : ResponseModel
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
    [JsonProperty("SMS_CNT", Order = 3)]
    public int SmsCount { get; set; }
    [JsonProperty("LMS_CNT", Order = 4)]
    public int LmsCount { get; set; }
    [JsonProperty("MMS_CNT", Order = 5)]
    public int MmsCount { get; set; }

    #endregion
    #region - Attributes -
    #endregion
}