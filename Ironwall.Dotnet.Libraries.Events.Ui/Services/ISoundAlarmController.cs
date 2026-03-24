namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 사운드 알람 상태머신 인터페이스 (IDLE ↔ PLAYING)
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 사운드 알람 상태머신.
/// <para>
/// - IDLE: 사운드 미재생 상태. 이벤트 도착 시 PLAYING 전이 + 재생 요청.
/// - PLAYING: 사운드 재생 중. 추가 이벤트 도착 시 무시.
///   재생 종료(OnPlaybackStopped) 시 큐 확인 → 비어있으면 IDLE, 남아있으면 재생 재시작.
/// </para>
/// </summary>
public interface ISoundAlarmController
{
    /// <summary>현재 상태</summary>
    SoundAlarmState State { get; }

    /// <summary>이벤트 도착 시 호출 — IDLE이면 재생 시작</summary>
    void OnEventArrived();

    /// <summary>사운드 재생 완료 콜백 — 큐 상태에 따라 IDLE 또는 재생 재시작</summary>
    void OnPlaybackStopped();
}

public enum SoundAlarmState
{
    Idle,
    Playing
}
