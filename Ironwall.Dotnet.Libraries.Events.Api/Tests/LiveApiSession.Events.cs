using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;

namespace Ironwall.Dotnet.Libraries.Tests.LiveApi;

/// <summary>이벤트 어셈블리의 라이브 세션 — 이벤트 서비스를 <see cref="LiveApiSession.Name"/> 으로 등록한다.</summary>
internal static partial class LiveApiSession
{
    static partial void RegisterDomainModules(ContainerBuilder builder, ApiSetupModel setup)
        => builder.RegisterModule(new EventApiModule(Log, setup, Name));
}
