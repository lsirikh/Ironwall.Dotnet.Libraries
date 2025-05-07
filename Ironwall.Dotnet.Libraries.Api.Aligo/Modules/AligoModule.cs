using Autofac;
using Ironwall.Dotnet.Libraries.Api.Aligo.Providers;
using Ironwall.Dotnet.Libraries.Api.Aligo.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Modules;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/5/2025 2:02:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AligoModule : Module
{
    #region - Ctors -
    public AligoModule(ILogService log, ApiSetupModel setup)
    {
        _log = log;
        _setup = setup;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterModule(new ApiModule(_log, _setup, "Aligo"));
        builder.RegisterType<EmsMessageProvider>().SingleInstance();

        _log.Info($"{nameof(AligoModule)} is trying to create a single {nameof(AligoModule)} instance.");
        builder.Register(build => new AligoService(
                _log,
                build.ResolveNamed<IApiService>("Aligo"),
                build.Resolve<EmsMessageProvider>()
            )).AsImplementedInterfaces().SingleInstance().WithMetadata("Order", 3);
    }
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
    private readonly ILogService _log;
    private readonly ApiSetupModel _setup;
    #endregion
}