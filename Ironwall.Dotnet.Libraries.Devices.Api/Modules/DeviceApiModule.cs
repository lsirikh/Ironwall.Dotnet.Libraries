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
                    ctx.ResolveNamed<ApiSetupModel>(_name),
                    // ⚠ 이 인자를 빠뜨리면 버전 분기가 통째로 죽은 코드가 된다.
                    //    프로브가 null 이면 서비스는 영구히 V6_3(운영 판본) 경로로만 동작해
                    //    7.0/8.0 서버를 상대로도 구계약 본문을 보낸다 — 422 인데 원인이 안 보인다.
                    //    등록이 없는 호스트(Aligo 단독 등)에서도 죽지 않도록 옵셔널 해석 2단.
                    ctx.ResolveOptionalNamed<IServerContractProbe>(_name)
                        ?? ctx.ResolveOptional<IServerContractProbe>()
                ))
                .Named<IDeviceApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count);

            // 4. ServerApiService 등록
            builder.Register(ctx => new ServerApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}"),
                    ctx.ResolveNamed<ApiSetupModel>(_name),
                    // ⚠ 이 인자를 빠뜨리면 버전 분기가 통째로 죽은 코드가 된다.
                    //    프로브가 null 이면 서비스는 영구히 V6_3(운영 판본) 경로로만 동작해
                    //    7.0/8.0 서버를 상대로도 구계약 본문을 보낸다 — 422 인데 원인이 안 보인다.
                    //    등록이 없는 호스트(Aligo 단독 등)에서도 죽지 않도록 옵셔널 해석 2단.
                    ctx.ResolveOptionalNamed<IServerContractProbe>(_name)
                        ?? ctx.ResolveOptional<IServerContractProbe>()
                ))
                .Named<IServerApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count + 1);

            // 5. UnitApiService 등록 (부대 편제 /api/units — API 8.0 신설 표면)
            //    ⚠ 프로브가 필수적인 이유가 다른 서비스들과 다르다: 부대 표면은 8.0 에서 '생겼다'.
            //    프로브가 null 이면 V6_3 으로 간주되어 모든 부대 호출이 네트워크 전에 차단된다(의도된 안전 방향).
            //    운영 6.3.2 에는 /api/units 가 0건이라, 차단하지 않으면 404 폭격이 되고 원인이 안 보인다.
            builder.Register(ctx => new UnitApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}"),
                    ctx.ResolveNamed<ApiSetupModel>(_name),
                    ctx.ResolveOptionalNamed<IServerContractProbe>(_name)
                        ?? ctx.ResolveOptional<IServerContractProbe>()
                ))
                .Named<IUnitApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count + 2);

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
