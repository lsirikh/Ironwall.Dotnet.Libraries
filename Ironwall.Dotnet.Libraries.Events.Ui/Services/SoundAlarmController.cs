using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 사운드 알람 상태머신 (IDLE ↔ PLAYING) — 타입별 사운드 라우팅
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class SoundAlarmController : ISoundAlarmController
{
    #region - Ctors -
    public SoundAlarmController(
        Action<EnumEventType> stopAndPlay,
        int durationSeconds = 20,
        int typeSwitchThrottleMs = 200,
        Action? stopAll = null)
    {
        _stopAndPlay = stopAndPlay;
        _stopAll = stopAll;
        _durationSeconds = durationSeconds;
        _typeSwitchThrottleMs = typeSwitchThrottleMs;
        State = SoundAlarmState.Idle;
    }
    #endregion

    #region - ISoundAlarmController -
    public SoundAlarmState State { get; private set; }

    public void OnEventArrived(EnumEventType eventType)
    {
        EnumEventType? toPlay = null;
        lock (_lock)
        {
            _lastEventTime = DateTime.Now;

            if (State == SoundAlarmState.Idle)
            {
                _currentType = eventType;
                _lastTypeSwitchTime = DateTime.Now;
                _pendingType = null;
                State = SoundAlarmState.Playing;
                toPlay = eventType;
            }
            else if (_currentType != eventType)
            {
                var sinceLastSwitch = (DateTime.Now - _lastTypeSwitchTime).TotalMilliseconds;
                if (sinceLastSwitch >= _typeSwitchThrottleMs)
                {
                    // 200ms 경과 → 즉시 전환
                    _currentType = eventType;
                    _lastTypeSwitchTime = DateTime.Now;
                    _pendingType = null;
                    State = SoundAlarmState.Playing;
                    toPlay = eventType;
                }
                else
                {
                    // 200ms 미경과 → pendingType에 저장, OnPlaybackStopped에서 재생
                    _pendingType = eventType;
                }
            }
            // 동일 타입 + Playing → _lastEventTime 갱신만 (슬라이딩 타이머 연장)
        }
        if (toPlay.HasValue)
            _stopAndPlay(toPlay.Value);
    }

    public void OnPlaybackStopped()
    {
        EnumEventType? toPlay = null;
        lock (_lock)
        {
            if (_pendingType.HasValue && _pendingType.Value != _currentType)
            {
                // throttle 중 누적된 타입 전환 우선 재생 (R6-C3)
                _currentType = _pendingType.Value;
                _lastTypeSwitchTime = DateTime.Now;
                _pendingType = null;
                State = SoundAlarmState.Playing;
                toPlay = _currentType;
            }
            else
            {
                _pendingType = null;
                var elapsed = (DateTime.Now - _lastEventTime).TotalSeconds;
                if (elapsed < _durationSeconds)
                {
                    // 슬라이딩 타이머 살아있음 → 현재 타입으로 재시작
                    toPlay = _currentType;
                }
                else
                {
                    State = SoundAlarmState.Idle;
                }
            }
        }
        if (toPlay.HasValue)
            _stopAndPlay(toPlay.Value);
    }

    public void OnQueueCleared()
    {
        lock (_lock)
        {
            State = SoundAlarmState.Idle;
            _lastEventTime = default;
            _pendingType = null;
            _lastTypeSwitchTime = default;
        }
    }

    public void OnActiveCountsChanged(int detection, int fault)
    {
        EnumEventType? toPlay = null;
        var stop = false;
        lock (_lock)
        {
            if (State != SoundAlarmState.Playing) return;
            var current = _currentType == EnumEventType.Fault ? fault : detection;
            if (current > 0) return;                     // 지금 울리는 종류가 아직 남았다 — 그대로

            var other = _currentType == EnumEventType.Fault ? EnumEventType.Intrusion : EnumEventType.Fault;
            var otherCount = other == EnumEventType.Fault ? fault : detection;
            _pendingType = null;
            if (otherCount > 0)
            {
                // 이 종류는 다 조치됐지만 다른 종류가 남았다 — 그 소리로 바꾼다.
                _currentType = other;
                _lastTypeSwitchTime = DateTime.Now;
                _lastEventTime = DateTime.Now;
                toPlay = other;
            }
            else
            {
                // 남은 이벤트가 없다 — 멈춘다(취소된 재생은 완료 콜백을 내지 않으므로 다시 울리지 않는다).
                State = SoundAlarmState.Idle;
                _lastEventTime = default;
                stop = true;
            }
        }
        if (toPlay.HasValue) _stopAndPlay(toPlay.Value);
        else if (stop) _stopAll?.Invoke();
    }
    #endregion

    #region - Attributes -
    private readonly object _lock = new();
    private readonly Action<EnumEventType> _stopAndPlay;
    private readonly Action? _stopAll;
    private readonly int _durationSeconds;
    private DateTime _lastEventTime;
    private DateTime _lastTypeSwitchTime;
    private EnumEventType _currentType = EnumEventType.Intrusion;
    private EnumEventType? _pendingType;
    private readonly int _typeSwitchThrottleMs;
    #endregion
}
