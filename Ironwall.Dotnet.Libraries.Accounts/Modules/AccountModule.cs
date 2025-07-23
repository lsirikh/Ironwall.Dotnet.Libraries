using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Models;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/8/2025 5:17:49 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AccountModule : Module
{
    #region - Ctors -
    public AccountModule(IAccountSetupModel model, ILogService? log = default, int count = default)
    {
        _model = model;
        _log = log;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        var setupModel = new AccountSetupModel(_model);
        builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
        builder.RegisterType<AccountProvider>().SingleInstance();
        builder.RegisterType<LoginProvider>().SingleInstance();
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
    private IAccountSetupModel _model;
    private ILogService? _log;
    private int _count;
    #endregion
}