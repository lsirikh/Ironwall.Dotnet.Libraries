using System;

namespace Ironwall.Dotnet.Libraries.Api.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/4/2025 9:31:25 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public partial class ApiSetupModel : IApiSetupModel
{
    #region - Ctors -
    public ApiSetupModel()
    {
        
    }
    public ApiSetupModel(IApiSetupModel model)
    {
        Url = model.Url;
        Username = model.Username;
        Password = model.Password;
        ApiKey = model.ApiKey;
        Phone = model.Phone;
        Timeout = model.Timeout;
        BearerToken = model.BearerToken;
        RefreshToken = model.RefreshToken;
    }
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
    public string Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 요청 타임아웃 (초 단위, 기본값: 10초)
    /// </summary>
    public int Timeout { get; set; } = 10;

    /// <summary>GOP-00 (FR-4): Bearer/refresh 토큰 폴백.</summary>
    public string BearerToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    #endregion
    #region - Attributes -
    #endregion
}