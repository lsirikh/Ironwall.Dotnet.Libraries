using System;

namespace Ironwall.Dotnet.Monitoring.Models.Accounts;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/8/2025 9:10:51 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class LoginModel : ILoginModel
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
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsIdSaved { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}