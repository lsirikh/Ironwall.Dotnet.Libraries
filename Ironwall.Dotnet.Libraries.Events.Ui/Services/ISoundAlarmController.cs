using Ironwall.Dotnet.Libraries.Enums;

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
/// - PLAYING: 사운드 재생 중. 추가 이벤트 도착 시 _lastEventTime만 갱신.
///   재생 종료(OnPlaybackStopped) 시 슬라이딩 타이머 확인:
///   (Now - _lastEventTime) &lt; durationSeconds → 재시작, 아니면 IDLE 전이.
/// </para>
/// </summary>
public interface ISoundAlarmController
{
    /// <summary>현재 상태</summary>
    SoundAlarmState State { get; }

    /// <summary>이벤트 도착 시 호출 — 타입별 사운드 라우팅. 타입 전환 시 현재 사운드 즉시 중단.</summary>
    void OnEventArrived(EnumEventType eventType);

    /// <summary>사운드 재생 완료 콜백 — 슬라이딩 타이머 기준으로 IDLE 또는 재생 재시작</summary>
    void OnPlaybackStopped();

    /// <summary>이벤트 큐 전체 클리어 시 호출 — pendingType 초기화 + throttle 리셋</summary>
    void OnQueueCleared();
}

public enum SoundAlarmState
{
    Idle,
    Playing
}
