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
