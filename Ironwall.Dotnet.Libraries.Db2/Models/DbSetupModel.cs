using System;

namespace Ironwall.Dotnet.Libraries.Db2.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/7/2025 8:23:09 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DbSetupModel
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
    public string IpDbServer { get; set; } = string.Empty;
    public int PortDbServer { get; set; } = 3306; //MariaDb based Port
    public string DbDatabase { get; set; } = string.Empty;
    public string UidDbServer { get; set; } = string.Empty;
    public string PasswordDbServer { get; set; } = string.Empty;
    #endregion
    #region - Attributes -
    #endregion
}