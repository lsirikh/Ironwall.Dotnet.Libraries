using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Sso;

/// <summary>
/// SSO 연동 DI 등록 — SSO PRD FR-01 · FR-03.
///
/// <para><b>AccountApiModule 뒤에</b> 등록한다 — <see cref="IAccountApiService"/> · <see cref="ITokenStorageService"/> 를 그쪽이 만든다.
/// 이 모듈은 <b>훅을 켜지 않는다.</b> 훅은 <see cref="SsoSessionCoordinator.TrySignInAsync"/> 가 성공했을 때만 켜진다 —
/// 아이디/비밀번호 로그인 세션은 refresh 가 있으니 레거시 경로가 맞기 때문이다.</para>
/// <para>등록만 해 두고 쓰지 않으면 동작은 예전과 같다(무회귀).</para>
/// </summary>
public sealed class SsoModule : Module
{
    private readonly string _clientId;

    /// <param name="clientId">SSO 에 등록된 앱 이름. 기본 <c>gis-monitoring</c>.</param>
    public SsoModule(string clientId = SsoAgentGateway.AppClientId) => _clientId = clientId;

    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(_ => new SsoAgentGateway(_clientId))
               .As<ISsoAgentGateway>()
               .SingleInstance();

        builder.Register(ctx => new SsoSessionCoordinator(
                   ctx.Resolve<ISsoAgentGateway>(),
                   ctx.Resolve<IAccountApiService>(),
                   ctx.Resolve<ITokenStorageService>(),
                   ctx.ResolveOptional<ILogService>()))
               .AsSelf()
               .SingleInstance();
    }
}
