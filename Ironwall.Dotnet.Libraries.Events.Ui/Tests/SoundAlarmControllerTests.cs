using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : SoundAlarmController 상태머신 단위 테스트 (슬라이딩 타이머 + 타입별 라우팅)
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class SoundAlarmControllerTests
{
    #region - FR-04 슬라이딩 타이머 테스트 -

    [Fact]
    public void OnEventArrived_WhenIdle_ShouldTransitionToPlayingAndRequestSound()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20);

        // Act
        sut.OnEventArrived(EnumEventType.Intrusion);

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Single(calls);
        Assert.Equal(EnumEventType.Intrusion, calls[0]);
    }

    [Fact]
    public void OnEventArrived_WhenPlaying_ShouldNotPlayAgainButUpdateLastEventTime()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20);

        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING (call #1)

        // Act
        sut.OnEventArrived(EnumEventType.Intrusion); // 동일 타입 — _lastEventTime 갱신만

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Single(calls); // 추가 호출 없음
    }

    [Fact]
    public void OnPlaybackStopped_WhenWithinDuration_ShouldReplayAndStayPlaying()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 3600); // 1시간 → 만료 없음

        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING (call #1)

        // Act — elapsed ≈ 0 < 3600 → 재시작
        sut.OnPlaybackStopped();

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(2, calls.Count); // 재생 2회
        Assert.All(calls, c => Assert.Equal(EnumEventType.Intrusion, c));
    }

    [Fact]
    public void OnPlaybackStopped_WhenDurationExpired_ShouldTransitionToIdle()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 0); // 즉시 만료

        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING (call #1)

        // Act — elapsed >= 0 == durationSeconds → Idle 전이
        sut.OnPlaybackStopped();

        // Assert
        Assert.Equal(SoundAlarmState.Idle, sut.State);
        Assert.Single(calls); // 재시작 없음
    }

    [Fact]
    public void OnEventArrived_AfterIdle_ShouldRestartSound()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 0); // 즉시 만료

        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING (call #1)
        sut.OnPlaybackStopped();                     // 만료 → IDLE
        Assert.Equal(SoundAlarmState.Idle, sut.State);

        // Act — Idle 상태에서 새 이벤트
        sut.OnEventArrived(EnumEventType.Intrusion);

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(2, calls.Count); // call #2
    }

    #endregion

    #region - FR-07 타입별 사운드 라우팅 테스트 -

    [Fact]
    public void OnEventArrived_WithIntrusionType_ShouldPlayDetectionNotMalfunction()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20);

        // Act
        sut.OnEventArrived(EnumEventType.Intrusion);

        // Assert
        Assert.Single(calls);
        Assert.Equal(EnumEventType.Intrusion, calls[0]);
    }

    [Fact]
    public void OnEventArrived_WithFaultType_ShouldPlayMalfunctionNotDetection()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20);

        // Act
        sut.OnEventArrived(EnumEventType.Fault);

        // Assert
        Assert.Single(calls);
        Assert.Equal(EnumEventType.Fault, calls[0]);
    }

    [Fact]
    public void OnEventArrived_TypeSwitch_ShouldStopCurrentAndPlayNewType()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20,
            typeSwitchThrottleMs: 0); // throttle 비활성 — 즉시 전환 테스트

        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING, call #1 (Intrusion)

        // Act — 다른 타입 이벤트 → StopAndPlayAsync(Fault) 호출
        sut.OnEventArrived(EnumEventType.Fault);

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(2, calls.Count);
        Assert.Equal(EnumEventType.Intrusion, calls[0]); // 초기 재생
        Assert.Equal(EnumEventType.Fault, calls[1]);     // 타입 전환 재생
    }

    [Fact]
    public void OnEventArrived_SameTypeContinued_ShouldNotStopOrPlayAgain()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20);

        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING

        // Act — 동일 타입 반복: 추가 호출 없음 (슬라이딩 타이머 연장만)
        sut.OnEventArrived(EnumEventType.Intrusion);
        sut.OnEventArrived(EnumEventType.Intrusion);

        // Assert
        Assert.Single(calls); // 최초 1회만
    }

    [Fact]
    public void OnPlaybackStopped_AfterFaultType_ShouldReplayMalfunctionNotDetection()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 3600); // 만료 없음

        sut.OnEventArrived(EnumEventType.Fault); // IDLE → PLAYING, call #1 (Fault)

        // Act — 재생 완료, 슬라이딩 타이머 내 → 동일 타입으로 재시작
        sut.OnPlaybackStopped();

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(2, calls.Count);
        Assert.All(calls, c => Assert.Equal(EnumEventType.Fault, c)); // 항상 Fault
    }

    #endregion

    #region - 타입 전환 직렬화 테스트 (신규) -

    [Fact]
    public void OnEventArrived_TypeSwitch_ShouldCallStopAndPlayExactlyOnce()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20,
            typeSwitchThrottleMs: 0);

        sut.OnEventArrived(EnumEventType.Intrusion); // call #1

        // Act — 타입 전환
        sut.OnEventArrived(EnumEventType.Fault);

        // Assert — 전환 시 정확히 1회만 추가 호출 (stop 분리 없음)
        Assert.Equal(2, calls.Count); // Intrusion 1 + Fault 1
        Assert.Equal(EnumEventType.Fault, calls[1]);
    }

    [Fact]
    public void OnEventArrived_TypeSwitch_ShouldCallWithNewEventType()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20,
            typeSwitchThrottleMs: 0);

        sut.OnEventArrived(EnumEventType.Fault); // IDLE → PLAYING (Fault)

        // Act — Fault → Intrusion 전환
        sut.OnEventArrived(EnumEventType.Intrusion);

        // Assert — 마지막 호출이 새 타입(Intrusion)으로 이루어짐
        Assert.Equal(2, calls.Count);
        Assert.Equal(EnumEventType.Intrusion, calls.Last());
    }

    [Fact]
    public void OnPlaybackStopped_AfterTypeSwitch_ShouldReplayWithCurrentType()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 3600,
            typeSwitchThrottleMs: 0); // 만료 없음, throttle 비활성

        sut.OnEventArrived(EnumEventType.Intrusion); // call #1 (Intrusion)
        sut.OnEventArrived(EnumEventType.Fault);     // call #2 (Fault — 타입 전환)

        // Act — 재생 완료 콜백: 현재 타입(Fault)으로 재시작
        sut.OnPlaybackStopped();

        // Assert
        Assert.Equal(3, calls.Count);
        Assert.Equal(EnumEventType.Fault, calls[2]); // 재시작도 Fault
    }

    [Fact]
    public void OnEventArrived_MultipleTypeSwitches_ShouldCallStopAndPlayEachTime()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 20,
            typeSwitchThrottleMs: 0);

        // Act — 여러 번 타입 전환
        sut.OnEventArrived(EnumEventType.Intrusion); // call #1
        sut.OnEventArrived(EnumEventType.Fault);     // call #2 (전환)
        sut.OnEventArrived(EnumEventType.Intrusion); // call #3 (전환)
        sut.OnEventArrived(EnumEventType.Fault);     // call #4 (전환)

        // Assert — 각 전환마다 stopAndPlay 호출
        Assert.Equal(4, calls.Count);
        Assert.Equal(EnumEventType.Intrusion, calls[0]);
        Assert.Equal(EnumEventType.Fault, calls[1]);
        Assert.Equal(EnumEventType.Intrusion, calls[2]);
        Assert.Equal(EnumEventType.Fault, calls[3]);
    }

    #endregion

    #region - TEST-04: OnQueueCleared 완전 리셋 (IMPL-08) -

    [Fact]
    public void OnQueueCleared_WhenPlaying_ShouldSetStateToIdle()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 3600);
        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING
        Assert.Equal(SoundAlarmState.Playing, sut.State);

        // Act
        sut.OnQueueCleared();

        // Assert
        Assert.Equal(SoundAlarmState.Idle, sut.State);
    }

    [Fact]
    public void OnQueueCleared_ShouldPreventReplayOnNextPlaybackStopped()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 3600); // 만료 없음 — 리셋 전이면 재생 재시작됨
        sut.OnEventArrived(EnumEventType.Intrusion); // call #1
        Assert.Single(calls);

        // Act — 큐 비워짐 → 슬라이딩 타이머 무효화
        sut.OnQueueCleared();
        sut.OnPlaybackStopped(); // _lastEventTime=default → elapsed 무한대 → Idle 유지

        // Assert: 추가 재생 없음
        Assert.Equal(SoundAlarmState.Idle, sut.State);
        Assert.Single(calls);
    }

    [Fact]
    public void OnQueueCleared_ShouldAllowImmediateRestartOnNextEvent()
    {
        // Arrange
        var calls = new List<EnumEventType>();
        var sut = new SoundAlarmController(
            stopAndPlay: et => calls.Add(et),
            durationSeconds: 3600);
        sut.OnEventArrived(EnumEventType.Intrusion); // IDLE → PLAYING
        sut.OnQueueCleared();                        // 리셋 → IDLE

        // Act — Idle에서 새 이벤트 → 즉시 재생
        sut.OnEventArrived(EnumEventType.Fault);

        // Assert
        Assert.Equal(SoundAlarmState.Playing, sut.State);
        Assert.Equal(2, calls.Count);
        Assert.Equal(EnumEventType.Fault, calls[1]);
    }

    #endregion
}
