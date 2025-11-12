using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Modules;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;

namespace Ironwall.Dotnet.Libraries.Events.Api.Modules;
/****************************************************************************
   Purpose      : Event API Module
   Created By   : GHLee
   Created On   : 11/11/2025 12:00:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Event API 모듈 (Autofac)
/// </summary>
public class EventApiModule : Module
{
    #region - Ctors -
    public EventApiModule(ILogService log, ApiSetupModel setup, string name = "EventApi")
    {
        _log = log;
        _setup = setup;
        _name = name;
    }
    #endregion

    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // 1. ApiModule 등록 (내부에서 IApiService 등록)
            // ApiSetupModel은 partial이므로 _setup을 그대로 사용 가능
            builder.RegisterModule(new ApiModule(_log, _setup, $"{_name}-Base"));

            // 2. ApiSetupModel 등록
            builder.RegisterInstance(_setup)
                .Named<ApiSetupModel>(_name)
                .SingleInstance();

            // 3. EventApiService 등록
            builder.Register(ctx => new EventApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}-Base"),
                    ctx.ResolveNamed<ApiSetupModel>(_name)
                ))
                .Named<IEventApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", 4);

            _log?.Info($"[{nameof(EventApiModule)}] Module loaded successfully with name: {_name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(EventApiModule)}] Failed to load module: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly ApiSetupModel _setup;
    private readonly string _name;
    #endregion
}
