namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 인앱 와치독 서비스 설정(appsettings 바인딩 대상).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 인앱 <see cref="ConnectionWatchdog"/> 설정. 메인 앱의 appsettings.json(AppSettings 섹션)에서
/// 바인딩하거나 부트스트래퍼에서 <c>SetupModel.Watchdog</c> 등으로 채운다.
/// </summary>
public sealed class WatchdogClientOptions
{
    /// <summary>감시 활성화 여부. false면 서비스가 완전 no-op.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 와치독 실행파일 경로. 상대경로면 <see cref="AppContext.BaseDirectory"/> 기준으로 절대화한다
    /// (이름폴링/CWD 하이재킹 방지).
    /// </summary>
    public string WatchdogExePath { get; set; } = "Ironwall.Dotnet.Libraries.Watchdog.exe";

    /// <summary>하트비트 기록 주기(ms). 기본 3000.</summary>
    public int HeartbeatIntervalMs { get; set; } = 3000;

    /// <summary>와치독이 프리즈로 판정하는 하트비트 stale 임계(ms). 기본 30000(부하 중 거짓 프리즈 방지). 와치독에 인자로 전달.</summary>
    public int FreezeThresholdMs { get; set; } = 30000;

    /// <summary>와치독 폴 주기(ms). 기본 5000. 와치독에 인자로 전달.</summary>
    public int PollIntervalMs { get; set; } = 5000;

    /// <summary>크래시/프리즈 재시작 전 옛 인스턴스 종료 대기 상한(ms). 기본 10000 — 앱 단일인스턴스 가드
    /// 오발동(종료 중 프로세스와 새 인스턴스 겹침 → "Redundant Execution") 방지. 와치독에 인자로 전달.</summary>
    public int RestartDelayMs { get; set; } = 10000;
}
