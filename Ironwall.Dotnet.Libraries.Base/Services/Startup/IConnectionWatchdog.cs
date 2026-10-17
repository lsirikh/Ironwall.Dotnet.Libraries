using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;

/// <summary>
/// 인앱 와치독 서비스 계약. 앱 프로세스의 생존/프리즈를 외부 와치독이 감시하도록
/// 하트비트를 기록하고, 와치독 실행파일을 기동하며, 상태를 조회한다.
/// <para><see cref="IService"/>로 부트스트래퍼 수명주기에 연동(ExecuteAsync=Start, StopAsync=Dispose).</para>
/// </summary>
public interface IConnectionWatchdog : IService, IDisposable
{
    /// <summary>감시 시작(하트비트 + 와치독 기동). 부팅 시퀀스에서 호출.</summary>
    void Start();

    /// <summary>현재 와치독 상태 조회(Setup 패널 폴링용). 미연결 시 null.</summary>
    WatchdogStatus? QueryStatus();

    /// <summary>사용자 의도 종료 확정 시 호출 — 와치독이 재시작하지 않도록 graceful 신호를 즉시 발화한다.</summary>
    void NotifyIntentionalShutdown();

    /// <summary>런타임 감시 중지(사용자 비활성) — 와치독 종료 유도 + 재기동 차단. 이후 <see cref="Start"/>로 재활성 가능.</summary>
    void StopWatching();
}
