using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Db.Models;
using Ironwall.Dotnet.Libraries.Accounts.Db.Services;
using Ironwall.Dotnet.Libraries.Accounts.Models;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Db.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/26/2025 1:29:27 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AccountDbModule : Module
{
    #region - Ctors -
    public AccountDbModule(IMariaDbSetupModel? model, ILogService? log = default, int count = default)
    {
        _log = log;
        _model = model;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            var setupModel = new AccountDbSetupModel(_model);
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
            builder.RegisterType<AccountDbService>().As<IAccountDbService>()
                .As<IService>().SingleInstance().WithMetadata("Order", _count++);
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
    private IMariaDbSetupModel? _model;
    private int _count;
    #endregion
}