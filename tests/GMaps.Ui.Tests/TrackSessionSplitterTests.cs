using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// TrackSessionSplitter(트랙 포인트 시간공백 세션 분절) 단위 테스트.
/// 실제 소스를 &lt;Compile Include&gt;로 링크해 검증(net8.0 격리, WPF 무의존).
/// 핵심 시나리오: 같은 track이 10:10~10:12, 10:20~10:25 두 번 잡히면 2세션으로 분리.
/// </summary>
public class TrackSessionSplitterTests
{
    private static List<DateTime> Run(DateTime start, int count, int stepSec = 1)
        => Enumerable.Range(0, count).Select(i => start.AddSeconds(i * stepSec)).ToList();

    [Fact(DisplayName = "should_return_single_session_when_no_gap")]
    public void NoGap_SingleSession()
    {
        var times = Run(new DateTime(2026, 6, 29, 10, 10, 0), 120);   // 연속 1Hz
        var segs = TrackSessionSplitter.SplitByGap(times, 10);
        Assert.Single(segs);
        Assert.Equal((0, 120), segs[0]);
    }

    [Fact(DisplayName = "should_split_two_when_gap_exceeds_threshold")]
    public void TwoAppearances_Split()
    {
        // 10:10:00~10:12:00(121점) … 8분 공백 … 10:20:00~10:25:00(301점)
        var s1 = Run(new DateTime(2026, 6, 29, 10, 10, 0), 121);
        var s2 = Run(new DateTime(2026, 6, 29, 10, 20, 0), 301);
        var times = s1.Concat(s2).ToList();

        var segs = TrackSessionSplitter.SplitByGap(times, 10);

        Assert.Equal(2, segs.Count);
        Assert.Equal((0, 121), segs[0]);
        Assert.Equal((121, 301), segs[1]);
    }

    [Fact(DisplayName = "should_not_split_when_gap_equals_threshold")]
    public void GapEqualsThreshold_NoSplit()
    {
        // 간격 정확히 10초 = 임계값 → '초과'가 아니므로 분절 안 함
        var times = new List<DateTime>
        {
            new(2026, 6, 29, 10, 0, 0),
            new(2026, 6, 29, 10, 0, 10),
            new(2026, 6, 29, 10, 0, 20),
        };
        var segs = TrackSessionSplitter.SplitByGap(times, 10);
        Assert.Single(segs);
        Assert.Equal((0, 3), segs[0]);
    }

    [Fact(DisplayName = "should_split_when_gap_just_over_threshold")]
    public void GapJustOver_Split()
    {
        var times = new List<DateTime>
        {
            new(2026, 6, 29, 10, 0, 0),
            new(2026, 6, 29, 10, 0, 11),   // 11초 > 10 → 분절
            new(2026, 6, 29, 10, 0, 12),
        };
        var segs = TrackSessionSplitter.SplitByGap(times, 10);
        Assert.Equal(2, segs.Count);
        Assert.Equal((0, 1), segs[0]);
        Assert.Equal((1, 2), segs[1]);
    }

    [Fact(DisplayName = "should_return_single_when_gap_nonpositive")]
    public void NonPositiveGap_SingleSession()
    {
        var times = Run(new DateTime(2026, 6, 29, 10, 0, 0), 5, stepSec: 100);
        var segs = TrackSessionSplitter.SplitByGap(times, 0);
        Assert.Single(segs);
        Assert.Equal((0, 5), segs[0]);
    }

    [Fact(DisplayName = "should_return_empty_when_no_points")]
    public void Empty_NoSegments()
        => Assert.Empty(TrackSessionSplitter.SplitByGap(new List<DateTime>(), 10));

    [Fact(DisplayName = "should_split_into_three_sessions")]
    public void ThreeSessions()
    {
        var times = Run(new DateTime(2026, 6, 29, 9, 0, 0), 5)
            .Concat(Run(new DateTime(2026, 6, 29, 9, 30, 0), 5))
            .Concat(Run(new DateTime(2026, 6, 29, 10, 0, 0), 5))
            .ToList();
        var segs = TrackSessionSplitter.SplitByGap(times, 30);
        Assert.Equal(3, segs.Count);
        Assert.All(segs, s => Assert.Equal(5, s.Count));
        Assert.Equal(15, segs.Sum(s => s.Count));   // 손실 없음
    }
}
