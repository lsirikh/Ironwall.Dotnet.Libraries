using System.Diagnostics;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;

namespace Ironwall.Dotnet.Watchdog;
/****************************************************************************
   Purpose      : 대상 프로세스 식별/생존확인/kill/재기동(PID+경로+StartTime 3중).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 감시 대상 프로세스를 <b>PID + 실행파일 절대경로 + StartTime</b> 3중으로 식별한다.
/// 이름 폴링을 쓰지 않아 PID 재사용/동명 프로세스로 인한 오킬을 방지한다.
/// 재기동 시 새 PID/StartTime을 추적한다.
/// </summary>
internal sealed class ProcessGuard
{
    #region - Ctors -
    public ProcessGuard(WatchdogOptions opt)
    {
        _opt = opt;
        _sessionId = Process.GetCurrentProcess().SessionId;
    }
    #endregion
    #region - Processes -
    /// <summary>discovery 모드 — app.pid sentinel에서 대상 PID/경로를 확보한다.</summary>
    public bool TryDiscoverTarget()
    {
        try
        {
            var file = WatchdogNaming.AppPidFile(_sessionId);
            if (!File.Exists(file)) return false;
            if (!int.TryParse(File.ReadAllText(file).Trim(), out var pid)) return false;

            using var p = Process.GetProcessById(pid); // 없으면 예외
            if (p.HasExited) return false;

            var path = SafeMainModulePath(p);
            _opt.TargetPid = pid;
            if (!string.IsNullOrEmpty(path)) _opt.TargetExePath = path;
            _knownStartUtc = TryStartUtc(p);
            WatchdogLog.Info($"discovery: 대상 확보 pid={pid}, exe='{_opt.TargetExePath}'");
            return true;
        }
        catch { return false; }
    }

    /// <summary>대상 생존 여부(경로 불일치=PID 재사용으로 간주).</summary>
    public bool IsAlive()
    {
        try
        {
            using var p = Process.GetProcessById(_opt.TargetPid);
            bool exited = p.HasExited;
            var path = SafeMainModulePath(p);
            if (!exited && _knownStartUtc == null) _knownStartUtc = TryStartUtc(p);
            if (exited) return false;

            if (!string.IsNullOrEmpty(_opt.TargetExePath) && !string.IsNullOrEmpty(path)
                && !PathEquals(path, _opt.TargetExePath))
                return false; // PID 재사용

            return true;
        }
        catch (ArgumentException) { return false; } // 프로세스 없음
        catch (Exception ex) { WatchdogLog.Warn($"IsAlive 예외: {ex.Message}"); return false; }
    }

    /// <summary>프리즈 대상 kill — PID+경로+StartTime 3중 일치 시에만 수행.</summary>
    public bool KillIfMatches()
    {
        try
        {
            using var p = Process.GetProcessById(_opt.TargetPid);
            if (p.HasExited) return false;

            var path = SafeMainModulePath(p);
            var startUtc = TryStartUtc(p);
            bool pathOk = string.IsNullOrEmpty(_opt.TargetExePath) || string.IsNullOrEmpty(path)
                          || PathEquals(path, _opt.TargetExePath);
            bool startOk = _knownStartUtc == null || startUtc == null
                           || Math.Abs((startUtc.Value - _knownStartUtc.Value).TotalSeconds) < 2;

            if (!pathOk || !startOk)
            {
                WatchdogLog.Warn($"kill 취소 — 식별 불일치(pathOk={pathOk}, startOk={startOk})");
                return false;
            }

            p.Kill(entireProcessTree: true);
            p.WaitForExit(5000);
            WatchdogLog.Info($"프리즈 프로세스 kill 완료 pid={_opt.TargetPid}");
            return true;
        }
        catch (Exception ex) { WatchdogLog.Error($"kill 실패: {ex.Message}"); return false; }
    }

    /// <summary>대상 재기동(절대경로). 성공 시 새 PID/StartTime을 추적한다.</summary>
    public bool StartTarget()
    {
        try
        {
            if (string.IsNullOrEmpty(_opt.TargetExePath) || !File.Exists(_opt.TargetExePath))
            {
                WatchdogLog.Error($"대상 exe 없음/미지정: '{_opt.TargetExePath}'");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = _opt.TargetExePath,
                UseShellExecute = true, // 대상 데스크톱 앱 정상 기동(no runas)
                WorkingDirectory = Path.GetDirectoryName(_opt.TargetExePath) ?? Environment.CurrentDirectory,
            };
            using var np = Process.Start(psi);
            if (np == null) return false;

            _opt.TargetPid = np.Id;
            _knownStartUtc = TryStartUtc(np);
            WatchdogLog.Info($"대상 재기동: pid={_opt.TargetPid}");
            return true;
        }
        catch (Exception ex) { WatchdogLog.Error($"대상 기동 실패: {ex.Message}"); return false; }
    }
    #endregion
    #region - Helpers -
    private static string SafeMainModulePath(Process p)
    {
        try { return p.MainModule?.FileName ?? string.Empty; }
        catch { return string.Empty; } // 권한/타이밍 예외 시 경로 비교 생략
    }

    private static DateTime? TryStartUtc(Process p)
    {
        try { return p.StartTime.ToUniversalTime(); }
        catch { return null; }
    }

    private static bool PathEquals(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }
    #endregion
    #region - Attributes -
    private readonly WatchdogOptions _opt;
    private readonly int _sessionId;
    private DateTime? _knownStartUtc;
    #endregion
}
