namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 사운드 알람 상태머신 (IDLE ↔ PLAYING)
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class SoundAlarmController : ISoundAlarmController
{
    #region - Ctors -
    public SoundAlarmController(Action playSound, Func<bool> hasQueuedEvents)
    {
        _playSound = playSound;
        _hasQueuedEvents = hasQueuedEvents;
        State = SoundAlarmState.Idle;
    }
    #endregion

    #region - ISoundAlarmController -
    public SoundAlarmState State { get; private set; }

    public void OnEventArrived()
    {
        if (State == SoundAlarmState.Playing) return;

        State = SoundAlarmState.Playing;
        _playSound();
    }

    public void OnPlaybackStopped()
    {
        if (_hasQueuedEvents())
        {
            _playSound();
            // State stays Playing
        }
        else
        {
            State = SoundAlarmState.Idle;
        }
    }
    #endregion

    #region - Attributes -
    private readonly Action _playSound;
    private readonly Func<bool> _hasQueuedEvents;
    #endregion
}
