using System.Windows;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/****************************************************************************
   Purpose      : 모니터 식별 카드 관리자 수명 — 겹치지 않기 · 시간 끝 · 닫기 (가짜 창 · 수동 타이머, 화면 없음)
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public class MonitorIdentifyOverlayTests
{
    private sealed class FakeSurface : IMonitorIdentifySurface
    {
        public FakeSurface(MonitorIdentifyCard card) => Card = card;
        public MonitorIdentifyCard Card { get; }
        public bool? PresentedAnimated;
        public bool? DismissedAnimated;
        public int DismissCount;
        public bool ThrowOnPresent;

        public void Present(bool animate)
        {
            if (ThrowOnPresent) throw new InvalidOperationException("창을 만들지 못함");
            PresentedAnimated = animate;
        }

        public void Dismiss(bool animate)
        {
            DismissCount++;
            DismissedAnimated ??= animate;
            IsClosed = true;
        }

        public bool IsClosed { get; private set; }
    }

    /// <summary>손으로 돌리는 타이머 — 시험이 기다리지 않는다.</summary>
    private sealed class ManualClock
    {
        public readonly List<(TimeSpan After, Action Action, Handle Handle)> Scheduled = new();

        public sealed class Handle : IDisposable
        {
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        public IDisposable Schedule(TimeSpan after, Action action)
        {
            var handle = new Handle();
            Scheduled.Add((after, action, handle));
            return handle;
        }

        /// <summary>지금까지 걸린 타이머를 모두 울린다(해제된 것도 — 늦게 도착한 Tick 을 흉내 낸다).</summary>
        public void FireAll()
        {
            foreach (var s in Scheduled.ToList()) s.Action();
        }
    }

    private static MonitorIdentifyCard Card(int n, bool selected = false)
        => new(n, $"모니터 {n} · 1920×1080", $@"\\.\DISPLAY{n}", new Int32Rect((n - 1) * 1920, 0, 1920, 1080), 96, selected);

    private static (MonitorIdentifyOverlay Overlay, List<FakeSurface> Made, ManualClock Clock) Make(bool animate = true, Func<MonitorIdentifyCard, bool>? fail = null)
    {
        var made = new List<FakeSurface>();
        var clock = new ManualClock();
        var overlay = new MonitorIdentifyOverlay(
            card =>
            {
                var s = new FakeSurface(card) { ThrowOnPresent = fail?.Invoke(card) ?? false };
                made.Add(s);
                return s;
            },
            clock.Schedule,
            () => animate);
        return (overlay, made, clock);
    }

    [Fact]
    public void should_present_one_surface_per_card_when_show_is_called()
    {
        var (overlay, made, clock) = Make();

        overlay.Show(new[] { Card(1, selected: true), Card(2), Card(3) });

        Assert.Equal(3, made.Count);
        Assert.All(made, s => Assert.True(s.PresentedAnimated));
        Assert.Equal(3, overlay.OpenCount);
        Assert.Single(clock.Scheduled);
        Assert.Equal(MonitorIdentifyMath.DismissAfter(true), clock.Scheduled[0].After);
    }

    [Fact]
    public void should_close_previous_cards_immediately_when_show_is_called_again()
    {
        var (overlay, made, _) = Make();
        overlay.Show(new[] { Card(1), Card(2) });
        var first = made.ToList();

        overlay.Show(new[] { Card(1), Card(2) });

        Assert.All(first, s => Assert.True(s.IsClosed));
        Assert.All(first, s => Assert.False(s.DismissedAnimated));    // 겹치지 않게 사라짐 없이 바로
        Assert.Equal(2, overlay.OpenCount);                            // 쌓이지 않는다
        Assert.Equal(4, made.Count);
    }

    [Fact]
    public void should_fade_out_all_cards_when_timer_expires()
    {
        var (overlay, made, clock) = Make(animate: true);
        overlay.Show(new[] { Card(1), Card(2) });

        clock.FireAll();

        Assert.All(made, s => Assert.True(s.DismissedAnimated));
        Assert.Equal(0, overlay.OpenCount);
    }

    [Fact]
    public void should_ignore_stale_timer_when_newer_show_replaced_the_cards()
    {
        var (overlay, made, clock) = Make();
        overlay.Show(new[] { Card(1) });
        overlay.Show(new[] { Card(2) });          // 앞 타이머는 해제됐지만 Tick 이 늦게 올 수 있다
        var second = made[1];

        clock.Scheduled[0].Action();             // 늦게 온 앞 차례 Tick

        Assert.True(clock.Scheduled[0].Handle.Disposed);
        Assert.False(second.IsClosed);
        Assert.Equal(1, overlay.OpenCount);
    }

    [Fact]
    public void should_close_without_fade_when_animation_is_off()
    {
        var (overlay, made, clock) = Make(animate: false);
        overlay.Show(new[] { Card(1) });

        Assert.False(made[0].PresentedAnimated);
        Assert.Equal(MonitorIdentifyMath.Duration, clock.Scheduled[0].After);

        clock.FireAll();
        Assert.False(made[0].DismissedAnimated);
    }

    [Fact]
    public void should_close_all_cards_and_stop_timer_when_close_all_is_called()
    {
        var (overlay, made, clock) = Make();
        overlay.Show(new[] { Card(1), Card(2) });

        overlay.CloseAll();

        Assert.All(made, s => Assert.True(s.IsClosed));
        Assert.True(clock.Scheduled[0].Handle.Disposed);
        Assert.Equal(0, overlay.OpenCount);
    }

    [Fact]
    public void should_close_cards_and_ignore_later_show_when_disposed()
    {
        var (overlay, made, _) = Make();
        overlay.Show(new[] { Card(1) });

        overlay.Dispose();
        overlay.Show(new[] { Card(2) });

        Assert.True(made[0].IsClosed);
        Assert.Single(made);
        Assert.Equal(0, overlay.OpenCount);
    }

    [Fact]
    public void should_not_schedule_when_cards_are_empty()
    {
        var (overlay, made, clock) = Make();

        overlay.Show(Array.Empty<MonitorIdentifyCard>());
        overlay.Show(null);

        Assert.Empty(made);
        Assert.Empty(clock.Scheduled);
    }

    [Fact]
    public void should_show_remaining_cards_when_one_card_fails_to_present()
    {
        var (overlay, made, clock) = Make(fail: c => c.Number == 2);

        overlay.Show(new[] { Card(1), Card(2), Card(3) });

        Assert.Equal(2, overlay.OpenCount);
        Assert.True(made.Single(s => s.Card.Number == 2).IsClosed);   // 실패한 카드는 닫아 둔다
        Assert.Single(clock.Scheduled);
    }
}
