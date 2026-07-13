namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 와치독 감시 상태(Setup 패널 표시 + 상태 채널 계약).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 와치독이 판정한 감시 대상(메인 앱)의 상태.
/// 이 열거형은 인앱 서비스(<see cref="IConnectionWatchdog"/>)와 와치독 실행파일이
/// <b>동일한 계약</b>으로 공유한다(와치독 exe는 본 소스를 링크 컴파일 — DLL 미참조).
/// </summary>
public enum WatchdogState
{
    /// <summary>상태 미확정(파이프 미연결 등).</summary>
    Unknown = 0,
    /// <summary>정상 — 프로세스 생존 + 하트비트 신선.</summary>
    Running = 1,
    /// <summary>프리즈 — 프로세스는 살아있으나 하트비트 stale(UI 데드락 등).</summary>
    Frozen = 2,
    /// <summary>사망 — 프로세스 소멸(재시작 대기/진행).</summary>
    Dead = 3,
    /// <summary>퇴화 — 크래시루프 서킷브레이커 개방, 재시작 중단.</summary>
    Degraded = 4,
    /// <summary>일시정지 — 자동업데이트 등으로 감시 보류.</summary>
    Paused = 5,
    /// <summary>정상 종료 — 사용자 의도 종료, 재시작하지 않음.</summary>
    GracefulStop = 6,
}
