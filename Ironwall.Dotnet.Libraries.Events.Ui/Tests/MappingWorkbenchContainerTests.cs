using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Modules;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.Modules;
using Moq;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 실창(GIS 호스트) 결함 D1 — 이벤트 콘솔을 열면 "맵핑 워크벤치 입구를 만들지 못했다 — 입구를 감춘다:
/// An exception was thrown while activating MappingWorkbenchLauncher -&gt; MappingWorkbenchGateway" 가 찍히고
/// 워크벤치 입구가 영영 사라졌다(log-2026-09-27.txt:778).
/// </summary>
/// <remarks>
/// <para>단위 시험은 게이트웨이를 <c>new</c> 로 세워 이 경로를 한 번도 지나지 않았다. 여기서는 호스트
/// <c>Bootstrapper.ConfigureContainer</c> 와 <b>같은 모듈을 같은 순서로</b> 올린 컨테이너에서 입구를 푼다.</para>
/// <para>호스트와 다른 점(대체한 것): ① <c>ParentBootstrapper.RegisterBaseType</c> 의 세 가지(WindowManager ·
/// EventAggregator · ILogService)는 그대로 두되 로그는 가짜다. ② <c>AccountUiModule</c> 은 뺐다 — Events.Ui 가
/// Accounts.Ui 를 참조하지 않고, 입구가 거기서 받는 <c>IPermissionService</c> 는 선택 인자다(없으면 "보기 허용").
/// ③ 설정은 호스트의 <c>SetupModel</c> 대신 <see cref="ApiSetupModel"/> · 가짜 <see cref="IEventSetupModel"/> 이다.
/// Redis · NATS · GMaps · Streaming 모듈은 이 입구의 의존 사슬에 없다.</para>
/// </remarks>
public class MappingWorkbenchContainerTests
{
    private static IContainer BuildHostLikeContainer()
    {
        var log = new Mock<ILogService>().Object;
        var apiSetup = new ApiSetupModel { Url = "https://localhost:8000/api" };
        var eventSetup = new Mock<IEventSetupModel>().Object;

        var builder = new ContainerBuilder();
        // ParentBootstrapper.RegisterBaseType 와 같다.
        builder.RegisterType<WindowManager>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<EventAggregator>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterInstance(log).As<ILogService>().SingleInstance();
        // 호스트가 직접 등록하는 공유 로그인 상태(Bootstrapper: RegisterType<AccountModel>().AsImplementedInterfaces()).
        builder.RegisterType<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel>().AsImplementedInterfaces().SingleInstance();
        // NatsModule(호스트 50번) 대신 가짜 — 이벤트 모듈의 빌드 콜백이 NATS 동기 서비스를 세우지만 연결은 하지 않는다.
        builder.RegisterInstance(new Mock<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>().Object)
               .As<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>().SingleInstance();
        // Bootstrapper.ConfigureContainer 의 순서(Device 20 → Event 30).
        builder.RegisterModule(new DeviceUiModule(apiSetup, log, 20));
        builder.RegisterModule(new EventUiModule(eventSetup, apiSetup, log, 30));
        return builder.Build();
    }

    [Fact]
    public void should_resolve_the_mapping_workbench_launcher_when_modules_are_registered_like_the_host()
    {
        // Arrange
        using var container = BuildHostLikeContainer();
        // 이벤트 콘솔은 입구를 Lazy 로 받는다 — 콘솔과 같은 방법으로 푼다.
        var lazy = container.Resolve<Lazy<IMappingWorkbenchLauncher>>();

        // Act
        var launcher = lazy.Value;

        // Assert
        Assert.IsType<MappingWorkbenchLauncher>(launcher);
    }

    [Fact]
    public void should_give_the_gateway_the_event_api_http_client_when_resolved_from_the_host_container()
    {
        // Arrange
        using var container = BuildHostLikeContainer();

        // Act
        var gateway = container.Resolve<IMappingWorkbenchGateway>();
        var eventApiClient = container.ResolveNamed<IApiService>("EventApi");

        // Assert — 호스트가 Initialize() 해 두고 Bearer 를 붙이는 그 클라이언트여야 한다.
        //          (이름 없는 새 ApiService 를 만들어 주면 HttpClient 가 없어 모든 호출이 실패한다.)
        var field = typeof(MappingWorkbenchGateway).GetField("_api",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Same(eventApiClient, field!.GetValue(gateway));
    }
}
