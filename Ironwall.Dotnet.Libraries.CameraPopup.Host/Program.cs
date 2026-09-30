using System.Windows;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host;

/****************************************************************************
   Purpose      : 카메라 팝업 호스트 진입점(PRD camera-popup-modes §0 · FR-24/25)
                  - 처리 안 된 예외 → 로그 + 0 아닌 코드로 종료(감시자가 재시작)
                  - 부모(GIS) 죽음 → 종료 · 메모리 한도 → 종료 코드 20(계획된 재시작)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // WER 대화 상자 금지 — 충돌하면 즉시 내려가야 감시자가 바로 다시 띄운다.
        NativeMethods.SetErrorMode(NativeMethods.SEM_FAILCRITICALERRORS | NativeMethods.SEM_NOGPFAULTERRORBOX | NativeMethods.SEM_NOOPENFILEERRORBOX);

        if (!HostLaunchArguments.TryParse(args, out var launch, out var error) || launch is null)
        {
            new HostLog(null).Error($"bad arguments: {error}");
            return HostExitCodes.BadArguments;
        }

        var log = new HostLog(launch.LogDirectory);
        HostExit.Initialize(log);
        log.Info($"start parent={launch.ParentProcessId} headless={launch.Headless} debug={launch.DebugCommands} memoryLimitMb={launch.MemoryLimitMb}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            log.Error($"unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}");
            HostExit.Now(HostExitCodes.UnhandledException, "unhandled exception");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Warn($"unobserved task exception: {e.Exception.GetBaseException().Message}");
            e.SetObserved();
        };

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            log.Error($"dispatcher unhandled exception: {e.Exception}");
            e.Handled = true;
            HostExit.Now(HostExitCodes.DispatcherException, "dispatcher exception");
        };

        var runtime = new HostRuntime(launch, log, app.Dispatcher);
        runtime.Start();
        app.Run();
        return HostExitCodes.Normal;
    }
}
