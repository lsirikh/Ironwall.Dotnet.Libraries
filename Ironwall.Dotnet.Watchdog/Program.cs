using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/****************************************************************************
   Purpose      : Ironwall .NET 8 프로세스 와치독 진입점(headless, Generic Host).
   Created By   : GHLee
   Created On   : 2026-07-13
   Company      : Sensorway Co., Ltd.
****************************************************************************/

var options = WatchdogOptions.Parse(args);

// 세션당 단일 인스턴스
var single = new SingleInstanceGuard();
if (!single.TryAcquire())
{
    WatchdogLog.Info("세션 내 와치독 이미 실행 중 — 중복 인스턴스 종료.");
    return 0;
}

WatchdogLog.Info($"와치독 프로세스 시작. args=[{string.Join(' ', args)}]");

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSingleton(options);
    builder.Services.AddSingleton<IClock, SystemClock>();
    builder.Services.AddHostedService<WatchdogEngine>();

    using var host = builder.Build();
    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    WatchdogLog.Error($"와치독 치명적 예외: {ex}");
    return 1;
}
finally
{
    single.Dispose();
}
