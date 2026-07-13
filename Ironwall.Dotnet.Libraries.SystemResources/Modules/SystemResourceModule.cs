using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.SystemResources.Services;

namespace Ironwall.Dotnet.Libraries.SystemResources.Modules;

/****************************************************************************
   Purpose      : SystemResources DI 모듈 (Autofac)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 시스템 리소스 모니터 등록. 소비자 모듈(예: GMapUiModule)에서
/// <c>builder.RegisterModule(new SystemResourceModule());</c>로 포함한다.
/// <see cref="IClock"/>이 앱에 이미 등록돼 있으면 그것을 쓰고, 없으면 <see cref="SystemClock"/>을 폴백 등록.
/// </summary>
public sealed class SystemResourceModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // 시계 — 미등록 시에만 폴백(앱 IClock 우선).
        builder.RegisterType<SystemClock>()
               .As<IClock>()
               .IfNotRegistered(typeof(IClock))
               .SingleInstance();

        // 실제 프로브(PDH/kernel32).
        builder.RegisterType<PdhResourceProbe>()
               .As<IResourceProbe>()
               .SingleInstance();

        // 모니터 — 앱 수명 단일 인스턴스(소비자가 Sample() 구동).
        builder.RegisterType<SystemResourceMonitor>()
               .As<ISystemResourceMonitor>()
               .SingleInstance();
    }
}
