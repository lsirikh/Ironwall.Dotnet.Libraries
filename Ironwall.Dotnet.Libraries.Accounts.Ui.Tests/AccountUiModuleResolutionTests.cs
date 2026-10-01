using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Models;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Modules;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/// <summary>
/// 런타임 DI 스모크 — 앱 Bootstrapper와 동일하게 AccountUiModule + 공유 등록으로 컨테이너를 구성하고,
/// 계정 VM/게이트웨이가 실제로 resolve 되는지 확인한다("컴파일은 되는데 실행 시 DI 에러" 케이스 차단).
/// </summary>
public class AccountUiModuleResolutionTests
{
    private sealed class StubSetup : IAccountSetupModel, IMariaDbSetupModel
    {
        public bool IsSession { get; set; }
        public int SessionExpiration { get; set; } = 60;
        public string IpDbServer { get; set; } = "127.0.0.1";
        public int PortDbServer { get; set; } = 3306;
        public string DbDatabase { get; set; } = "test";
        public string UidDbServer { get; set; } = "test";
        public string PasswordDbServer { get; set; } = "test";
    }

    private sealed class StubApiSetup : Ironwall.Dotnet.Libraries.Api.Models.IApiSetupModel
    {
        public string Url { get; set; } = "https://127.0.0.1:8000/api";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int Timeout { get; set; } = 10;
    }

    /// <summary>GOP 모드(앱 실제 구성) — AccountUiModule(useDbAuth:false, apiSetup) 가 SSO 모듈까지 등록한다.</summary>
    private static IContainer BuildGopModeContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance<IEventAggregator>(new EventAggregator());
        builder.RegisterInstance(new Mock<ILogService>().Object).As<ILogService>();
        builder.RegisterType<AccountModel>().AsImplementedInterfaces().SingleInstance();
        var setup = new StubSetup();
        builder.RegisterModule(new AccountUiModule(setup, setup, null, 10, useDbAuth: false, apiSetup: new StubApiSetup()));
        return builder.Build();
    }

    /// <summary>
    /// SSO 배선 — SSO PRD FR-03. GOP 모드에서 로그인 패널이 조정자를 실제로 받고, 조정자의 로그인 마무리 경계가
    /// <b>비밀번호 로그인과 같은 게이트웨이 인스턴스</b>인지 본다. 다르면 SSO 로그인 뒤 권한·GIS 초기화가 돌지 않는다.
    /// (빌드·단위시험이 통과해도 DI 배선만 빠져 기능이 죽는 사례를 막는다.)
    /// </summary>
    [Fact]
    public void should_wire_sso_coordinator_into_login_panel_when_gop_mode()
    {
        using var c = BuildGopModeContainer();

        var coordinator = c.Resolve<Ironwall.Dotnet.Libraries.Sso.SsoSessionCoordinator>();
        Assert.NotNull(coordinator);

        var completer = c.Resolve<Ironwall.Dotnet.Libraries.Accounts.Api.Gateways.ISsoLoginCompleter>();
        var authGateway = c.Resolve<IAuthGateway>();
        Assert.Same(authGateway, completer);   // 같은 게이트웨이 · 같은 마무리 코드

        Assert.NotNull(c.Resolve<LoginPanelViewModel>());
        // 훅은 등록만으로 켜지지 않는다 — SSO 로그인이 성공했을 때만(비밀번호 세션은 레거시 refresh 경로).
        Assert.Null(Ironwall.Dotnet.Libraries.Accounts.Api.Handlers.BearerAuthHandler.SsoReauthenticator);
    }

    /// <summary>DB 모드에는 SSO 가 끼지 않는다 — 예전 그대로.</summary>
    [Fact]
    public void should_not_register_sso_when_db_mode()
    {
        using var c = BuildAppLikeContainer();
        Assert.False(c.IsRegistered<Ironwall.Dotnet.Libraries.Sso.SsoSessionCoordinator>());
    }

    private static IContainer BuildAppLikeContainer()
    {
        var builder = new ContainerBuilder();
        // 앱이 제공하는 등록(Bootstrapper와 동일 역할)
        builder.RegisterInstance<IEventAggregator>(new EventAggregator());
        builder.RegisterInstance(new Mock<ILogService>().Object).As<ILogService>();
        builder.RegisterType<AccountModel>().AsImplementedInterfaces().SingleInstance();   // 공유 IAccountModel
        // 계정 UI 모듈(앱 Bootstrapper: new AccountUiModule(setup, setup, _log, 10))
        var setup = new StubSetup();
        builder.RegisterModule(new AccountUiModule(setup, setup, null, 10));
        return builder.Build();
    }

    [Fact]
    public void should_resolve_all_account_viewmodels_when_container_built()
    {
        using var c = BuildAppLikeContainer();

        Assert.NotNull(c.Resolve<LoginPanelViewModel>());
        Assert.NotNull(c.Resolve<AccountManagerPanelViewModel>());
        Assert.NotNull(c.Resolve<MyPagePanelViewModel>());
        Assert.NotNull(c.Resolve<LogoutPanelViewModel>());
        Assert.NotNull(c.Resolve<AccountSetupPanelViewModel>());
        Assert.NotNull(c.Resolve<RegisterDialogViewModel>());
        Assert.NotNull(c.Resolve<EditorDialogViewModel>());
        Assert.NotNull(c.Resolve<DeleteAccountDialogViewModel>());
        Assert.NotNull(c.Resolve<ResetPassDialogViewModel>());
        Assert.NotNull(c.Resolve<LoginViewModel>());
        Assert.NotNull(c.Resolve<AccountViewModel>());
    }

    [Fact]
    public void should_resolve_gateway_and_services_when_container_built()
    {
        using var c = BuildAppLikeContainer();

        Assert.NotNull(c.Resolve<IAuthGateway>());
        Assert.NotNull(c.Resolve<IUserDirectoryGateway>());
        Assert.NotNull(c.Resolve<IProfileGateway>());
        Assert.NotNull(c.Resolve<ISessionConfigService>());
        Assert.NotNull(c.Resolve<IProfileImageService>());
        // 3개 게이트웨이 인터페이스가 동일한 DbAccountGateway 단일 인스턴스인지(어댑터 일관성)
        Assert.Same(c.Resolve<IAuthGateway>(), c.Resolve<IUserDirectoryGateway>());
    }
}
