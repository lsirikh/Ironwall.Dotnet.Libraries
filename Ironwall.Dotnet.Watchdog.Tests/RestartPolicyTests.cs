using System;
using Xunit;

namespace Ironwall.Dotnet.Watchdog.Tests;
/****************************************************************************
   Purpose      : RestartPolicy 단위 테스트 — 지수 백오프 + 서킷브레이커(FR-03).
****************************************************************************/
public class RestartPolicyTests
{
    private static WatchdogOptions Opt(int max = 100, int windowMs = 600000, int maxBackoffMs = 60000)
        => new WatchdogOptions
        {
            MaxRestartsPerWindow = max,
            RestartWindowMs = windowMs,
            MaxBackoffMs = maxBackoffMs,
        };

    [Fact]
    public void should_backoff_exponentially_when_repeated_restart()
    {
        // Arrange — 서킷브레이커 개입 배제(max 크게)
        var clock = new FakeClock();
        var policy = new RestartPolicy(Opt(max: 100), clock);
        long[] expectedBackoff = { 5000, 10000, 20000, 40000, 60000, 60000 }; // 5/10/20/40/60 cap

        foreach (var backoff in expectedBackoff)
        {
            // Act + Assert
            Assert.True(policy.CanRestartNow());
            policy.RecordRestart();

            Assert.False(policy.CanRestartNow());        // 백오프 진행 중
            clock.AdvanceMs(backoff - 1);
            Assert.False(policy.CanRestartNow());        // 경계 직전
            clock.AdvanceMs(1);
            Assert.True(policy.CanRestartNow());          // 백오프 경과 → 허용
        }
    }

    [Fact]
    public void should_enter_degraded_when_exceeds_window()
    {
        // Arrange — 3회 상한, 백오프 최소화
        var clock = new FakeClock();
        var policy = new RestartPolicy(Opt(max: 3, maxBackoffMs: 1), clock);

        // Act — 윈도우 내 3회 재시작
        for (int i = 0; i < 3; i++)
        {
            Assert.True(policy.CanRestartNow());
            policy.RecordRestart();
            clock.AdvanceMs(10);
        }

        // Assert — 4번째는 서킷브레이커 개방
        Assert.False(policy.CanRestartNow());
        Assert.True(policy.IsDegraded);
        Assert.Equal(3, policy.RestartCount);
    }

    [Fact]
    public void should_reset_backoff_when_healthy()
    {
        // Arrange
        var clock = new FakeClock();
        var policy = new RestartPolicy(Opt(max: 100), clock);
        policy.RecordRestart();                 // consecutive=1 → 5s
        clock.AdvanceMs(5000);
        policy.RecordRestart();                 // consecutive=2 → 10s

        // Act — 정상 회복 통지
        policy.NotifyHealthy();

        // Assert — 백오프 즉시 해제, 다음 백오프는 5s부터 재시작
        Assert.True(policy.CanRestartNow());
        policy.RecordRestart();                 // consecutive=1 → 5s
        clock.AdvanceMs(4999);
        Assert.False(policy.CanRestartNow());
        clock.AdvanceMs(1);
        Assert.True(policy.CanRestartNow());
    }

    [Fact]
    public void should_not_degrade_when_restarts_outside_window()
    {
        // Arrange — 상한 2, 윈도우 10s
        var clock = new FakeClock();
        var policy = new RestartPolicy(Opt(max: 2, windowMs: 10000, maxBackoffMs: 1), clock);

        // Act — 윈도우보다 넓게 간격을 두고 재시작(슬라이딩 윈도우로 과거 기록 프루닝)
        policy.RecordRestart();
        clock.AdvanceMs(11000);
        Assert.True(policy.CanRestartNow());     // 과거 1건 프루닝됨
        policy.RecordRestart();
        clock.AdvanceMs(11000);

        // Assert — 동시 윈도우 내 누적이 상한에 도달하지 않아 DEGRADED 아님
        Assert.True(policy.CanRestartNow());
        Assert.False(policy.IsDegraded);
    }
}
