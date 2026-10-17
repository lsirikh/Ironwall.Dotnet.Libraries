using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.SystemResources.Models;

namespace Ironwall.Dotnet.Libraries.SystemResources.Services;

/****************************************************************************
   Purpose      : 비-DI 소비자를 위한 모니터 팩토리
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// Autofac을 쓰지 않는 소비자(예: 진단 콘솔, 단순 호스트)를 위해 기본 구성으로
/// <see cref="ISystemResourceMonitor"/>를 생성한다(실제 PDH 프로브 + <see cref="SystemClock"/>).
/// DI 환경에서는 <c>SystemResourceModule</c>을 사용한다.
/// </summary>
public static class SystemResourceMonitorFactory
{
    /// <summary>기본 구성(PDH/kernel32 프로브 + SystemClock)으로 모니터를 생성한다.</summary>
    public static ISystemResourceMonitor CreateDefault(ILogService? log = null, ResourceThresholds? thresholds = null)
        => new SystemResourceMonitor(new PdhResourceProbe(log), new SystemClock(), log, thresholds);
}
