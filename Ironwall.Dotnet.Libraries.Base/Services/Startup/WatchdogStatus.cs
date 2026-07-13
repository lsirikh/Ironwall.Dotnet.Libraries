namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 와치독 → 앱(Setup 패널) 상태 리포팅 DTO.
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 와치독이 Named Pipe로 발행하고 앱이 폴링하는 상태 스냅샷.
/// System.Text.Json으로 양측 직렬화/역직렬화(기본 옵션 동일 — 계약 일치).
/// 와치독 exe는 본 소스를 링크 컴파일하여 동일 타입을 공유한다(DLL 미참조).
/// </summary>
public sealed record WatchdogStatus(
    WatchdogState State,
    bool IsHeartbeatOk,
    DateTime? LastRestartUtc,
    int RestartCount,
    DateTime QueriedUtc,
    string? Detail = null);
