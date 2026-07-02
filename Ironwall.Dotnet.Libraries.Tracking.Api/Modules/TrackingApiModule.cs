using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Modules;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Tracking.Api.Services;

namespace Ironwall.Dotnet.Libraries.Tracking.Api.Modules;
/****************************************************************************
   Purpose      : Tracking History API Module (Autofac)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 추적 이력 API 모듈. EventApiModule 미러.
/// <para>AsImplementedInterfaces()로 IService를 노출 → Bootstrapper 서비스-시작 루프가
/// ExecuteAsync(HttpClient Initialize)를 호출한다.</para>
/// </summary>
public class TrackingApiModule : Module
{
    #region - Ctors -
    public TrackingApiModule(ILogService? log, ApiSetupModel setup, string name = "TrackingApi", int count = 100)
    {
        _log = log;
        _setup = setup;
        _name = name;
        _count = count;
    }
    #endregion

    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // 1. ApiModule 등록 (내부에서 Named<IApiService> 등록)
            // T7/레거시 통일 방향: 추적 이력 조회에도 Bearer 부착(AUTH_MODE=token 대비). Device/Event와 동일 패턴.
            //   ITokenStorageService 공유(미등록=DB모드→미부착 하위호환), 401 최종실패 시 SessionExpired→ForceLogoutOnce.
            builder.RegisterModule(new ApiModule(_log, _setup, $"{_name}", _count, authHandlerFactory: ctx =>
            {
                var store = ctx.ResolveOptional<ITokenStorageService>();
                if (store is null) return null;   // DB모드 등 미등록 → Bearer 미부착
                var scope = ctx.Resolve<ILifetimeScope>();
                var handler = new BearerAuthHandler(store, () => scope.Resolve<IAccountApiService>(), _log);
                handler.SessionExpired += () =>
                {
                    try { scope.Resolve<ISessionLifecycle>().ForceLogoutOnce(EnumRevokeReason.Unauthorized); }
                    catch (Exception ex) { _log?.Warning($"[{nameof(TrackingApiModule)}] SessionExpired→ForceLogout 실패: {ex.Message}"); }
                };
                return handler;
            }));

            // 2. ApiSetupModel Named 등록
            builder.RegisterInstance(_setup).Named<ApiSetupModel>(_name).SingleInstance();

            // 3. TrackingApiService 등록 (IService 노출 → ExecuteAsync 트리거)
            builder.Register(ctx => new TrackingApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}"),
                    ctx.ResolveNamed<ApiSetupModel>(_name)
                ))
                .Named<ITrackingApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count);

            _log?.Info($"[{nameof(TrackingApiModule)}] Module loaded successfully with name: {_name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(TrackingApiModule)}] Failed to load module: {ex.Message}");
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
