using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.Watchdog;
/****************************************************************************
   Purpose      : 상호감시 — per-user 로그온 예약작업 등록(비승격).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 로그온 시 와치독을 discovery 모드로 기동하는 사용자 예약작업을 등록한다(비승격 <c>/rl limited</c>).
/// 세션 정합성을 자연히 확보하며, 와치독 프로세스가 죽어도 다음 로그온에 부활한다.
/// 이미 등록돼 있으면 아무것도 하지 않는다.
/// </summary>
internal static class ScheduledTaskInstaller
{
    private const string TaskName = "Ironwall\\WatchdogLauncher";

    public static void EnsureRegistered(string watchdogExePath)
    {
        try
        {
            if (string.IsNullOrEmpty(watchdogExePath) || !File.Exists(watchdogExePath))
                return;
            if (TaskExists())
                return;

            // discovery 모드(인자 없음) — app.pid sentinel로 대상 탐색.
            // /tr 값의 따옴표 처리는 ArgumentList에 위임(공백 포함 경로 안전).
            var code = RunSchtasks("/create", "/tn", TaskName, "/tr", $"\"{watchdogExePath}\"",
                                   "/sc", "onlogon", "/rl", "limited", "/f");
            WatchdogLog.Info($"예약작업 등록(onlogon) — exit={code}");
        }
        catch (Exception ex)
        {
            WatchdogLog.Warn($"예약작업 등록 실패: {ex.Message}");
        }
    }

    private static bool TaskExists()
    {
        try { return RunSchtasks("/query", "/tn", TaskName) == 0; }
        catch { return false; }
    }

    private static int RunSchtasks(params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in arguments) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        if (p == null) return -1;
        p.WaitForExit(10000);
        return p.HasExited ? p.ExitCode : -1;
    }
}
