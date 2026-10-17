namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 와치독 IPC 명명 규약 + 공유 경로(앱↔와치독 exe 계약).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 앱과 와치독 실행파일이 공유하는 커널 오브젝트/파이프/파일 명명 규약.
/// <para>
/// 네임스페이스는 <c>Local\</c>(세션 로컬)을 사용한다. 앱과 와치독은 <b>동일 세션·동일 사용자</b>로
/// 실행되며(앱=asInvoker 표준권한), 표준 사용자는 <c>Global\</c> 커널 오브젝트 생성 권한
/// (SeCreateGlobalPrivilege)이 없기 때문이다. 세션 격리는 Fast User Switching/RDP 다중 세션에서
/// 오브젝트 충돌을 자동 방지한다.
/// </para>
/// 와치독 exe는 본 소스를 링크 컴파일하여 동일 규약을 공유한다(DLL 미참조).
/// </summary>
public static class WatchdogNaming
{
    /// <summary>하트비트 공유메모리 맵 이름(대상 PID 기준).</summary>
    public static string HeartbeatMap(int targetPid) => $"Local\\IW_HB_{targetPid}";

    /// <summary>정상 종료 신호 이벤트 이름(대상 PID 기준).</summary>
    public static string ShutdownEvent(int targetPid) => $"Local\\IW_Shutdown_{targetPid}";

    /// <summary>와치독 단일 인스턴스 뮤텍스 이름(세션 기준).</summary>
    public static string SingleInstanceMutex(int sessionId) => $"Local\\IW_WD_SINGLE_{sessionId}";

    /// <summary>상태 리포팅 Named Pipe 이름(세션 기준).</summary>
    public static string StatusPipe(int sessionId) => $"IW_WD_Status_{sessionId}";

    /// <summary>공유 데이터 디렉터리(%ProgramData%\Ironwall\Watchdog).</summary>
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Ironwall", "Watchdog");

    /// <summary>예약작업 기동 와치독의 대상 PID 조회용 sentinel(세션 기준).</summary>
    public static string AppPidFile(int sessionId) => Path.Combine(DataDir, $"app.{sessionId}.pid");

    /// <summary>DEGRADED 진입 시 파이프 fallback용 플래그 파일(세션 기준).</summary>
    public static string DegradedFlag(int sessionId) => Path.Combine(DataDir, $"degraded.{sessionId}.flag");

    /// <summary>와치독 로그 파일.</summary>
    public static string LogFile => Path.Combine(DataDir, "watchdog.log");
}
