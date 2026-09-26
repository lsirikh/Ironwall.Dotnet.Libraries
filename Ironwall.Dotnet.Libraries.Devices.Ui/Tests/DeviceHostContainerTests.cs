using Autofac;
using Autofac.Core;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Modules;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 호스트와 같은 모양으로 조립한 컨테이너에서 장비 쪽 콘솔 부품이 실제로 만들어지는가.
/// <para>이벤트 맵핑 워크벤치가 실창에서 "Cannot resolve parameter IApiService" 로 숨겨졌던 결함(D1)과 같은 원인이
/// 서버 모니터 콘솔에도 있었다 — <c>ServerAxisApiService</c> 를 이름 없는 <c>IApiService</c> 로 풀었는데, ApiModule 은
/// IApiService 를 이름("DeviceApi" 등)으로만 등록한다. 단위 시험은 부품을 <c>new</c> 로 세워 이 경로를 지나지 않았다.</para>
/// <para>호스트와 다른 점: 로그 · NATS 는 가짜, Accounts/Events 모듈은 뺐다(장비 콘솔 부품의 필수 의존이 아니다 —
/// 권한 서비스 등은 선택 인자다).</para>
/// </summary>
public class DeviceHostContainerTests
{
    private static IContainer BuildHostLikeContainer()
    {
        var log = new Mock<ILogService>().Object;
        var apiSetup = new ApiSetupModel { Url = "https://localhost:8000/api" };

        var builder = new ContainerBuilder();
        builder.RegisterType<WindowManager>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<EventAggregator>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterInstance(log).As<ILogService>().SingleInstance();
        builder.RegisterType<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterInstance(new Mock<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>().Object)
               .As<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>().SingleInstance();
        builder.RegisterModule(new DeviceUiModule(apiSetup, log, 20));
        return builder.Build();
    }

    [Fact]
    public void should_resolve_the_server_axis_api_when_modules_are_registered_like_the_host()
    {
        using var container = BuildHostLikeContainer();

        var api = container.Resolve<IServerAxisApiService>();

        Assert.IsType<ServerAxisApiService>(api);
    }

    [Fact]
    public void should_give_the_server_axis_api_the_device_api_http_client_when_resolved_from_the_host_container()
    {
        using var container = BuildHostLikeContainer();

        var api = container.Resolve<IServerAxisApiService>();
        var deviceClient = container.ResolveNamed<Ironwall.Dotnet.Libraries.Api.Services.IApiService>(DeviceUiModule.DeviceApiName);

        // 호스트가 Initialize() 하고 Bearer 를 붙이는 그 클라이언트여야 한다.
        var field = typeof(ServerAxisApiService)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .FirstOrDefault(f => typeof(Ironwall.Dotnet.Libraries.Api.Services.IApiService).IsAssignableFrom(f.FieldType));
        Assert.NotNull(field);
        Assert.Same(deviceClient, field!.GetValue(api));
    }

    /// <summary>
    /// 장비 UI 어셈블리의 콘솔 부품(…Consoles.* 의 ViewModel · Launcher · Service)이 전부 만들어지는가 — 같은 유형의
    /// 결함(이름 없는 등록을 찾음)이 다른 콘솔에 숨어 있으면 여기서 이름과 함께 드러난다.
    /// </summary>
    [Fact]
    public void should_construct_every_device_console_component_when_resolved_from_the_host_container()
    {
        using var container = BuildHostLikeContainer();
        var consoleServices = container.ComponentRegistry.Registrations
            .SelectMany(r => r.Services.OfType<TypedService>())
            .Select(s => s.ServiceType)
            .Where(t => t.Assembly == typeof(DeviceUiModule).Assembly
                        && (t.Namespace ?? "").Contains(".Consoles")
                        && !t.IsGenericTypeDefinition)
            .Distinct()
            .ToList();

        var failures = new List<string>();
        foreach (var type in consoleServices)
        {
            try { _ = container.Resolve(type); }
            catch (DependencyResolutionException ex) { failures.Add($"{type.FullName}: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        Assert.NotEmpty(consoleServices);
        Assert.True(failures.Count == 0, "만들어지지 않는 콘솔 부품:\n" + string.Join("\n", failures));
    }
}
