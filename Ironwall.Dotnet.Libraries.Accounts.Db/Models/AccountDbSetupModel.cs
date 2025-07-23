using Ironwall.Dotnet.Libraries.Base.Models;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Db.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/26/2025 1:30:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AccountDbSetupModel : IMariaDbSetupModel
{
    #region - Ctors -
    public AccountDbSetupModel()
    {
        
    }
    public AccountDbSetupModel(IMariaDbSetupModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        IpDbServer = model.IpDbServer;
        PortDbServer = model.PortDbServer;
        DbDatabase = model.DbDatabase;
        UidDbServer = model.UidDbServer;
        PasswordDbServer = model.PasswordDbServer;
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
    public string IpDbServer { get; set; } = string.Empty;
    public int PortDbServer { get; set; } = 3306; //MariaDb based Port
    public string DbDatabase { get; set; } = string.Empty;
    public string UidDbServer { get; set; } = string.Empty;
    public string PasswordDbServer { get; set; } = string.Empty;
    #endregion
    #region - Attributes -
    #endregion
}