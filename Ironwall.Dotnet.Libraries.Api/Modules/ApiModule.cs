using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Net;

namespace Ironwall.Dotnet.Libraries.Api.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/5/2025 12:15:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ApiModule : Module
{
    #region - Ctors -
    public ApiModule(ILogService? log, IApiSetupModel setup, string name = "default", int count=100)
    {
        _log = log;
        _setup = setup;
        _name = name;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(_setup).Named<ApiSetupModel>(_name).SingleInstance();
        builder.Register(build => new ApiService(_log, build.ResolveNamed<ApiSetupModel>(_name)))
            .Named<IApiService>(_name).SingleInstance().WithMetadata("Order", _count);
        _log?.Info($"{nameof(ApiModule)} is trying to create a single {nameof(ApiService)} instance.");
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
    private readonly ILogService? _log;
    private readonly IApiSetupModel _setup;
    private readonly string _name;
    private readonly int _count;
    #endregion
}