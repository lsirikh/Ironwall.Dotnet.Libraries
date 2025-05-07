using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db.Models;
using Ironwall.Dotnet.Libraries.Db.Services;
using Ironwall.Dotnet.Libraries.Db.Utils;
using System;

namespace Ironwall.Dotnet.Libraries.Db.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 1/31/2025 12:44:20 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DbModule : Module
{
    #region - Ctors -
    public DbModule(ILogService? log, string ipDbServer, int portDbServer, string dbDatabase, string uidDatabase, string passwordDbServer, string excelFolder, bool isLoadExcel)
    {
        _log = log;
        _ipDbServer = ipDbServer;
        _portDbServer = portDbServer;
        _dbDatabase = dbDatabase;
        _uidDbServer = uidDatabase;
        _passwordDbServer = passwordDbServer;
        _excelFolder = excelFolder;
        _isLoadExcel = isLoadExcel;
        

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
               ExcelFolder = _excelFolder,
               IsLoadExcel = _isLoadExcel
            };
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
            builder.RegisterType<DbServiceForGym>().AsImplementedInterfaces()
                .SingleInstance().WithMetadata("Order", 2);
            builder.RegisterType<ExcelImporter>().AsImplementedInterfaces()
                .SingleInstance().WithMetadata("Order", 3);
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
    private readonly int _portDbServer  = 3306; //MariaDb based Port
    private readonly string _dbDatabase = string.Empty;
    private readonly string _uidDbServer = string.Empty;
    private readonly string _passwordDbServer = string.Empty;
    private readonly string _excelFolder = string.Empty;
    private readonly bool _isLoadExcel;
    #endregion
}