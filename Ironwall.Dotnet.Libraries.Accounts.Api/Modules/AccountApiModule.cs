using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Modules;

/// <summary>
/// GOP 서버 인증 어댑터 DI 등록 (FR-9). 기존 <c>DbAccountGateway</c> 대칭.
/// <para><see cref="ApiAccountGateway"/>를 IAuthGateway+IUserDirectoryGateway+IProfileGateway 로 등록하면
/// 계정 VM 은 무편집으로 DB→서버 인증으로 전환된다(§2.4.1). AccountUiModule(useDbAuth=false)에서 본 모듈을 등록.</para>
/// </summary>
public class AccountApiModule : Module
{
    private readonly ILogService? _log;
    private readonly IApiSetupModel _setup;
    private readonly string _name;
    private readonly int _count;

    public AccountApiModule(ILogService? log, IApiSetupModel setup, string name = "Account", int count = 100)
    {
        _log = log;
        _setup = setup;
        _name = name;
        _count = count;
    }

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(new ApiSetupModel(_setup)).Named<ApiSetupModel>(_name).SingleInstance();

        // 토큰/권한 single source + 세션 라이프사이클(강제 로그아웃 단일 멱등 진입점, FR-FL-04)
        builder.RegisterType<TokenStorageService>().As<ITokenStorageService>().SingleInstance();
        builder.RegisterType<SystemClock>().As<IClock>().SingleInstance().PreserveExistingDefaults();   // FR-GS-01 clockSkew 산출용(기존 등록 있으면 보존)
        builder.RegisterType<PermissionService>().As<IPermissionService>().SingleInstance();
        builder.RegisterType<SessionLifecycle>().As<ISessionLifecycle>().SingleInstance();

        // Bearer 파이프라인이 끼워진 account 전용 IApiService
        builder.Register(ctx =>
        {
            var scope = ctx.Resolve<ILifetimeScope>();
            var handler = new BearerAuthHandler(
                scope.Resolve<ITokenStorageService>(),
                () => scope.Resolve<IAccountApiService>(),   // 순환 회피: 401 시점 지연 해석
                _log);
            // FR-FL-05: refresh 최종 실패(세션 만료) → 단일 진입점으로 강제 로그아웃(401 폴백 배선)
            handler.SessionExpired += () =>
            {
                try { scope.Resolve<ISessionLifecycle>().ForceLogoutOnce(EnumRevokeReason.Unauthorized); }
                catch (Exception ex) { _log?.Warning($"[AccountApiModule] SessionExpired→ForceLogout 실패: {ex.Message}"); }
            };
            return new ApiService(_log, scope.ResolveNamed<ApiSetupModel>(_name), handler);
        }).Named<IApiService>(_name)
          .As<IService>()   // ★ 앱 IService 러너가 ExecuteAsync→Initialize 호출(HttpClient 파이프라인 구성). 누락 시 로그인 시 client=null
          .SingleInstance().WithMetadata("Order", _count);

        // Auth + User CRUD + 프로필 래퍼
        builder.Register(ctx => new AccountApiService(ctx.ResolveNamed<IApiService>(_name), _log))
            .As<IAccountApiService>().SingleInstance();

        // FR-GS-03: grant 만료 능동 재조회(로그인→/me/permissions→valid_until 타이머). 앱 IService 러너가 기동/종료.
        builder.Register(ctx => new PermissionRefreshService(
                ctx.Resolve<IAccountApiService>(),
                ctx.Resolve<IPermissionService>(),
                ctx.Resolve<ISessionLifecycle>(),
                ctx.Resolve<IClock>(),
                _log))
            .As<IPermissionRefreshService>()
            .As<IService>()
            .SingleInstance().WithMetadata("Order", _count + 1);

        // 게이트웨이 — 3 인터페이스 동시 등록(VM 무편집 스왑 지점)
        builder.Register(ctx => new ApiAccountGateway(
                ctx.Resolve<IAccountApiService>(),
                ctx.Resolve<ITokenStorageService>(),
                ctx.Resolve<IPermissionService>(),
                ctx.Resolve<ISessionLifecycle>(),
                _log))
            .As<IAuthGateway>()
            .As<IUserDirectoryGateway>()
            .As<IProfileGateway>()
            .SingleInstance();

        _log?.Info($"[{nameof(AccountApiModule)}] loaded (name={_name})");
    }
}
