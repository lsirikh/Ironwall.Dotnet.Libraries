using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
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
    public EventApiModule(ILogService? log, ApiSetupModel setup, string name = "EventApi", int count=100)
    {
        _log = log;
        _setup = setup;
        _name = name;
        _count = count;

    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // 1. ApiModule 등록 (내부에서 IApiService 등록)
            // T7(로그인 게이팅 FR-BR): Event fetch/CRUD에 Bearer 부착(AUTH_MODE=token 대비). DeviceApiModule과 동일 패턴.
            //   ITokenStorageService 공유(미등록=DB모드→미부착 하위호환), 401 시 single-flight refresh + 최종실패 SessionExpired→ForceLogoutOnce.
            builder.RegisterModule(new ApiModule(_log, _setup, $"{_name}", _count, authHandlerFactory: ctx =>
            {
                var store = ctx.ResolveOptional<ITokenStorageService>();
                if (store is null) return null;   // DB모드 등 미등록 → Bearer 미부착
                var scope = ctx.Resolve<ILifetimeScope>();
                var handler = new BearerAuthHandler(store, () => scope.Resolve<IAccountApiService>(), _log);
                handler.SessionExpired += () =>
                {
                    try { scope.Resolve<ISessionLifecycle>().ForceLogoutOnce(EnumRevokeReason.Unauthorized); }
                    catch (Exception ex) { _log?.Warning($"[{nameof(EventApiModule)}] SessionExpired→ForceLogout 실패: {ex.Message}"); }
                };
                return handler;
            }));

            // 2. ApiSetupModel 등록
            builder.RegisterInstance(_setup).Named<ApiSetupModel>(_name).SingleInstance();

            // 3. EventApiService 등록
            builder.Register(ctx => new EventApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}"),
                    ctx.ResolveNamed<ApiSetupModel>(_name)
                ))
                .Named<IEventApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count);

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
    private readonly ILogService? _log;
    private readonly ApiSetupModel _setup;
    private readonly string _name;
    private readonly int _count;
    #endregion
}
