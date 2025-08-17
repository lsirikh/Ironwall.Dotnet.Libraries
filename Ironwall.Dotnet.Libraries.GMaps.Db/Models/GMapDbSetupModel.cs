using Ironwall.Dotnet.Libraries.Base.Models;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/25/2025 10:30:10 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapDbSetupModel : IMariaDbSetupModel
{
    #region - Ctors -
    public GMapDbSetupModel()
    {

    }
    public GMapDbSetupModel(IMariaDbSetupModel model)
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