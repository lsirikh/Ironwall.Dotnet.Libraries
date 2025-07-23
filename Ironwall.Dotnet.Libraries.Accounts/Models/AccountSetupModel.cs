using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/8/2025 5:19:28 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AccountSetupModel : IAccountSetupModel
{
    #region - Ctors -
    public AccountSetupModel(IAccountSetupModel model)
    {
        if (model is null)
            throw new ArgumentNullException(nameof(model));

        // ---- 단순 값 타입은 얕은 복사로 충분 ----
        IsSession = model.IsSession;
        SessionExpiration = model.SessionExpiration;
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
    public bool IsSession { get; set; }
    public int SessionExpiration { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}