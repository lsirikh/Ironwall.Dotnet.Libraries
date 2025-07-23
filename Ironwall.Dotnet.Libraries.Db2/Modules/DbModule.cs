using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db2.Models;
using Ironwall.Dotnet.Libraries.Db2.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Db2.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/7/2025 8:27:21 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DbModule : Module
{
    #region - Ctors -
    public DbModule(ILogService? log, string ipDbServer, int portDbServer, string dbDatabase, string uidDatabase, string passwordDbServer)
    {
        _log = log;
        _ipDbServer = ipDbServer;
        _portDbServer = portDbServer;
        _dbDatabase = dbDatabase;
        _uidDbServer = uidDatabase;
        _passwordDbServer = passwordDbServer;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            var setupModel = new DbSetupModel()
            {
                IpDbServer = _ipDbServer,
                PortDbServer = _portDbServer,
                DbDatabase = _dbDatabase,
                UidDbServer = _uidDbServer,
                PasswordDbServer = _passwordDbServer,
            };
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
            builder.RegisterType<DbServiceForMonitor>()
                .As<IDbServiceForMonitor>()
                .As<IService>()
                .SingleInstance()
                .WithMetadata("Order", 1);
        }
        catch
        {
            throw;
        }
    }
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
    #endregion
    #region - Attributes -
    private ILogService? _log;
    private readonly string _ipDbServer = string.Empty;
    private readonly int _portDbServer = 3306; //MariaDb based Port
    private readonly string _dbDatabase = string.Empty;
    private readonly string _uidDbServer = string.Empty;
    private readonly string _passwordDbServer = string.Empty;
    #endregion
}