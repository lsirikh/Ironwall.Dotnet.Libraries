using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Modules;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Api.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>FR-9 AccountApiModule DI 스모크 — 전 부품 해석 + 순환참조 없음 + SingleInstance 공유(V-07).</summary>
public class AccountApiModuleResolutionTests
{
    private static IContainer Build()
    {
        var builder = new ContainerBuilder();
        var setup = new ApiSetupModel { Url = "https://example.local/", Timeout = 10 };
        builder.RegisterModule(new AccountApiModule(null, setup, "Account"));
        return builder.Build();
    }

    [Fact]
    public void should_resolve_gateway_and_services_without_cycle()
    {
        using var c = Build();

        Assert.NotNull(c.Resolve<IAuthGateway>());
        Assert.NotNull(c.Resolve<IUserDirectoryGateway>());
        Assert.NotNull(c.Resolve<IProfileGateway>());
        Assert.NotNull(c.Resolve<IAccountApiService>());
        Assert.NotNull(c.Resolve<ITokenStorageService>());
        Assert.NotNull(c.Resolve<IPermissionService>());
    }

    [Fact]
    public void should_share_single_gateway_instance_across_three_interfaces()
    {
        using var c = Build();

        var auth = c.Resolve<IAuthGateway>();
        Assert.Same(auth, c.Resolve<IUserDirectoryGateway>());
        Assert.Same(auth, c.Resolve<IProfileGateway>());
    }

    [Fact]
    public void should_share_single_token_store()
    {
        using var c = Build();
        Assert.Same(c.Resolve<ITokenStorageService>(), c.Resolve<ITokenStorageService>());
    }
}
