namespace Ironwall.Dotnet.Watchdog;
/****************************************************************************
   Purpose      : 와치독 실행 설정(CLI 인자 파싱).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 와치독 동작 파라미터. 앱이 기동 시 CLI 인자로 전달하거나(<c>--pid --target --poll --freeze</c>),
/// 예약작업(discovery) 기동 시 <see cref="TargetPid"/>=0으로 두고 app.pid sentinel로 대상을 탐색한다.
/// </summary>
public sealed class WatchdogOptions
{
    /// <summary>감시 대상 PID(0 이하면 discovery 모드).</summary>
    public int TargetPid { get; set; }

    /// <summary>감시 대상 exe 절대경로(3중 식별·재기동에 사용).</summary>
    public string TargetExePath { get; set; } = string.Empty;

    /// <summary>폴 주기(ms).</summary>
    public int PollIntervalMs { get; set; } = 5000;

    /// <summary>프리즈 판정 하트비트 stale 임계(ms).</summary>
    public int FreezeThresholdMs { get; set; } = 15000;

    /// <summary>윈도우당 최대 재시작 횟수(초과 시 DEGRADED).</summary>
    public int MaxRestartsPerWindow { get; set; } = 5;

    /// <summary>서킷브레이커 윈도우(ms).</summary>
    public int RestartWindowMs { get; set; } = 600000;

    /// <summary>백오프 상한(ms).</summary>
    public int MaxBackoffMs { get; set; } = 60000;

    public static WatchdogOptions Parse(string[] args)
    {
        var o = new WatchdogOptions();
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--pid":
                    if (int.TryParse(args[i + 1], out var pid)) o.TargetPid = pid;
                    break;
                case "--target":
                    o.TargetExePath = args[i + 1];
                    break;
                case "--poll":
                    if (int.TryParse(args[i + 1], out var poll)) o.PollIntervalMs = Math.Max(1000, poll);
                    break;
                case "--freeze":
                    if (int.TryParse(args[i + 1], out var fr)) o.FreezeThresholdMs = Math.Max(3000, fr);
                    break;
            }
        }
        return o;
    }
}
