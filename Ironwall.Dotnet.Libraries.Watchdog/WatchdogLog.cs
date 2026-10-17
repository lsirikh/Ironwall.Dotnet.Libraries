using Ironwall.Dotnet.Libraries.Base.Services.Startup;

namespace Ironwall.Dotnet.Libraries.Watchdog;
/****************************************************************************
   Purpose      : 와치독 파일 로거(스레드 안전 append).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// %ProgramData%\Ironwall\Watchdog\watchdog.log에 append하는 단순 파일 로거.
/// 로그 라인 타임스탬프는 DateTime.Now(관측용) — 프리즈/백오프 판정 로직은 IClock을 사용한다.
/// </summary>
internal static class WatchdogLog
{
    private static readonly object _gate = new();

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        try
        {
            Directory.CreateDirectory(WatchdogNaming.DataDir);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}";
            lock (_gate)
                File.AppendAllText(WatchdogNaming.LogFile, line);
        }
        catch { /* 로깅 실패는 무시 */ }
    }
}
