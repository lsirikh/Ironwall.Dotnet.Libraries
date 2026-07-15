using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 와치독 실행파일 부재 시 기동(최소권한·절대경로 검증).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 와치독 단일 인스턴스 뮤텍스를 확인하여 미실행 시에만 와치독 exe를 기동한다.
/// <c>runas</c>를 쓰지 않고(최소권한), 실행 경로는 <see cref="AppContext.BaseDirectory"/> 기준
/// 절대경로로 해석한다(CWD 동명 exe 하이재킹 방지).
/// </summary>
internal sealed class WatchdogLauncher
{
    #region - Ctors -
    public WatchdogLauncher(ILogService? log, WatchdogClientOptions opt)
    {
        _log = log;
        _opt = opt;
        _sessionId = Process.GetCurrentProcess().SessionId;
    }
    #endregion
    #region - Processes -
    public void EnsureRunning()
    {
        if (IsWatchdogRunning())
        {
            _log?.Info("[Watchdog] 와치독 이미 실행 중 — 기동 생략.");
            return;
        }

        var exePath = ResolveExePath();
        if (!File.Exists(exePath))
        {
            _log?.Warning($"[Watchdog] 와치독 실행파일 없음: {exePath} — 기동 생략(배포 확인 필요).");
            return;
        }

        try
        {
            var targetExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,   // no runas — 최소권한(동일 세션·동일 사용자)
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            };
            psi.ArgumentList.Add("--pid");    psi.ArgumentList.Add(Environment.ProcessId.ToString());
            psi.ArgumentList.Add("--target"); psi.ArgumentList.Add(targetExe);
            psi.ArgumentList.Add("--poll");   psi.ArgumentList.Add(_opt.PollIntervalMs.ToString());
            psi.ArgumentList.Add("--freeze"); psi.ArgumentList.Add(_opt.FreezeThresholdMs.ToString());
            psi.ArgumentList.Add("--restart-delay"); psi.ArgumentList.Add(_opt.RestartDelayMs.ToString());

            Process.Start(psi);
            _log?.Info($"[Watchdog] 와치독 기동: {exePath} (target pid={Environment.ProcessId})");
        }
        catch (Exception ex)
        {
            _log?.Error($"[Watchdog] 와치독 기동 실패: {ex.Message}");
        }
    }

    private bool IsWatchdogRunning()
    {
        try
        {
            using var m = Mutex.OpenExisting(WatchdogNaming.SingleInstanceMutex(_sessionId));
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false; // 뮤텍스 없음 = 와치독 미실행
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Watchdog] 와치독 실행 상태 확인 예외: {ex.Message}");
            return false;
        }
    }

    private string ResolveExePath()
    {
        var p = string.IsNullOrWhiteSpace(_opt.WatchdogExePath)
            ? "Ironwall.Dotnet.Libraries.Watchdog.exe"
            : _opt.WatchdogExePath;
        if (!Path.IsPathRooted(p))
            p = Path.Combine(AppContext.BaseDirectory, p);
        return p;
    }
    #endregion
    #region - Attributes -
    private readonly ILogService? _log;
    private readonly WatchdogClientOptions _opt;
    private readonly int _sessionId;
    #endregion
}
