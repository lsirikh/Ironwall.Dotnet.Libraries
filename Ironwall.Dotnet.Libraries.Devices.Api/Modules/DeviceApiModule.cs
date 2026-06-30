using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Modules;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Modules;
/****************************************************************************
   Purpose      : Device API Module
   Created By   : GHLee
   Created On   : 11/10/2025 6:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Device API 모듈 (Autofac)
/// </summary>
public class DeviceApiModule : Module
{
    #region - Ctors -
    public DeviceApiModule(ILogService? log, IApiSetupModel setup, string name = "DeviceApi", int count=100)
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
            // ApiSetupModel은 이제 Timeout 속성을 포함하므로 직접 사용 가능
            //builder.RegisterModule(new ApiModule(_log, setupModel, "MediaMTX"));
            // 로그인 게이팅(Login_Gated_GIS_Init, FR-BR/D5): Device·Server fetch에 Bearer 부착(서버 token 강화 대비).
            // ITokenStorageService(AccountApiModule 등록) 공유 → 토큰 자동 동기. 미등록(DB모드)이면 핸들러 미부착(하위호환).
            // 401 시 single-flight refresh + 최종실패 SessionExpired→ForceLogoutOnce (AccountApiModule와 동일 패턴).
            builder.RegisterModule(new ApiModule(_log, _setup, $"{_name}", _count, authHandlerFactory: ctx =>
            {
                var store = ctx.ResolveOptional<ITokenStorageService>();
                if (store is null) return null;   // DB모드 등 미등록 → Bearer 미부착
                var scope = ctx.Resolve<ILifetimeScope>();
                var handler = new BearerAuthHandler(store, () => scope.Resolve<IAccountApiService>(), _log);
                handler.SessionExpired += () =>
                {
                    try { scope.Resolve<ISessionLifecycle>().ForceLogoutOnce(EnumRevokeReason.Unauthorized); }
                    catch (Exception ex) { _log?.Warning($"[{nameof(DeviceApiModule)}] SessionExpired→ForceLogout 실패: {ex.Message}"); }
                };
                return handler;
            }));

            // 2. ApiSetupModel 등록
            builder.RegisterInstance(_setup).Named<ApiSetupModel>(_name).SingleInstance();

            // 3. DeviceApiService 등록
            builder.Register(ctx => new DeviceApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}"),
                    ctx.ResolveNamed<ApiSetupModel>(_name)
                ))
                .Named<IDeviceApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count);

            // 4. ServerApiService 등록
            builder.Register(ctx => new ServerApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}"),
                    ctx.ResolveNamed<ApiSetupModel>(_name)
                ))
                .Named<IServerApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count + 1);

            _log?.Info($"[{nameof(DeviceApiModule)}] Module loaded successfully with name: {_name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeviceApiModule)}] Failed to load module: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiSetupModel _setup;
    private readonly string _name;
    private readonly int _count;
    #endregion
}
