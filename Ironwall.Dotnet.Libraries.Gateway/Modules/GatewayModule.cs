using Autofac;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Gateway.Models;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Services;
using Ironwall.Dotnet.Libraries.Gateway.ViewModels;

namespace Ironwall.Dotnet.Libraries.Gateway.Modules;
/****************************************************************************
   Purpose      : Gateway 모듈 (Autofac DI 등록)
   Created By   : GHLee
   Created On   : 10/29/2025 12:16:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class GatewayModule : Module
{
    #region - Ctors -
    public GatewayModule(IMariaDbSetupModel model, ILogService? log = default, int count = default)
    {
        _model = model;
        _log = log;
        _count = count;
    }
    #endregion

    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // Setup Model 등록
            var setupModel = new GatewaySetupModel(_model);
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();

            // Provider 등록
            builder.RegisterType<GatewayEventProvider>().SingleInstance();
            // ViewModel 등록
            builder.RegisterType<GatewaySetupViewModel>().SingleInstance();

            // DB Service 등록
            builder.RegisterType<GatewayDbService>().As<IGatewayDbService>().As<IService>().SingleInstance().WithMetadata("Order", _count);
        }
        catch
        {
            throw;
        }
    }

    #region - Attributes -
    private readonly IMariaDbSetupModel _model;
    private readonly ILogService? _log;
    private readonly int _count;
    #endregion
}
