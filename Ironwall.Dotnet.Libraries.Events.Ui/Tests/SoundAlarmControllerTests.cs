using Xunit;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : SoundAlarmController 상태머신 단위 테스트
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class SoundAlarmControllerTests
{
    #region - Phase 6 -

    [Fact]
    public void OnEventArrived_WhenIdle_ShouldTransitionToPlayingAndRequestSound()
    {
        // Arrange
        var playCount = 0;
        var sut = new SoundAlarmController(
            playSound: () => playCount++,
            hasQueuedEvents: () => false);

        // Act
        sut.OnEventArrived();

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void OnEventArrived_WhenPlaying_ShouldNotPlayAgain()
    {
        // Arrange
        var playCount = 0;
        var sut = new SoundAlarmController(
            playSound: () => playCount++,
            hasQueuedEvents: () => true);

        sut.OnEventArrived(); // IDLE → PLAYING

        // Act
        sut.OnEventArrived(); // 추가 이벤트

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(1, playCount); // 재생 1회만
    }

    [Fact]
    public void OnPlaybackStopped_WhenQueueEmpty_ShouldTransitionToIdle()
    {
        // Arrange
        var sut = new SoundAlarmController(
            playSound: () => { },
            hasQueuedEvents: () => false);
        sut.OnEventArrived(); // IDLE → PLAYING

        // Act
        sut.OnPlaybackStopped();

        // Assert
        Assert.Equal(SoundAlarmState.Idle, sut.State);
    }

    [Fact]
    public void OnPlaybackStopped_WhenQueueHasEvents_ShouldReplayAndStayPlaying()
    {
        // Arrange
        var playCount = 0;
        var sut = new SoundAlarmController(
            playSound: () => playCount++,
            hasQueuedEvents: () => true);
        sut.OnEventArrived(); // IDLE → PLAYING (play #1)

        // Act
        sut.OnPlaybackStopped(); // 큐에 이벤트 있음 → 재생 재시작

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(2, playCount); // 재생 2회
    }

    #endregion
}
