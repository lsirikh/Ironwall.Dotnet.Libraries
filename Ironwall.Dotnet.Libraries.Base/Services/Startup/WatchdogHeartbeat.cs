namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 하트비트 공유메모리 레이아웃(앱 writer ↔ 와치독 reader 계약).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 하트비트 공유메모리(<see cref="WatchdogNaming.HeartbeatMap"/>)의 바이트 레이아웃.
/// <para>
/// EpochMs는 오프셋 0(8바이트 정렬)에 두어 x64에서 <b>원자적</b> 읽기/쓰기를 보장한다
/// (ECMA: 8바이트 정렬 정수 접근은 원자적) → tearing 없이 신선도 비교 가능.
/// </para>
/// 와치독 exe는 본 소스를 링크 컴파일하여 동일 레이아웃을 공유한다(DLL 미참조).
/// </summary>
public static class WatchdogHeartbeat
{
    /// <summary>공유메모리 총 크기(바이트). 여유 padding 포함.</summary>
    public const int Size = 64;

    /// <summary>UTC Unix epoch(ms) — Int64, 8바이트 정렬(원자적).</summary>
    public const long OffsetEpochMs = 0;

    /// <summary>기록 주체 PID — Int32.</summary>
    public const long OffsetPid = 8;

    /// <summary>상태 바이트(1=Healthy) — Byte.</summary>
    public const long OffsetState = 12;

    /// <summary>Healthy 상태 값.</summary>
    public const byte StateHealthy = 1;
}
