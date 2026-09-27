using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 커널 CoalescingTrigger — 몰려온 신호를 창 안에서 한 번의 실행으로 합치는가(시간은 가짜 지연으로)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class CoalescingTriggerTests
{
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();
        public List<TimeSpan> Windows { get; } = new();

        public Task Delay(TimeSpan window, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pending) { _pending.Add(tcs); Windows.Add(window); }
            token.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }

        public void ReleaseAll()
        {
            List<TaskCompletionSource> now;
            lock (_pending) { now = _pending.ToList(); _pending.Clear(); }
            foreach (var t in now) t.TrySetResult();
        }
    }

    [Fact]
    public async Task should_run_once_when_a_burst_of_pulses_arrives_within_the_window()
    {
        var delay = new ManualDelay();
        var runs = 0;
        var trigger = new CoalescingTrigger(_ => { runs++; return Task.CompletedTask; }, delay: delay.Delay);

        var pulses = Enumerable.Range(0, 7).Select(_ => trigger.Pulse()).ToList();
        delay.ReleaseAll();
        await Task.WhenAll(pulses);

        Assert.Equal(1, runs);
        Assert.Equal(1, trigger.FiredCount);
        Assert.All(delay.Windows, w => Assert.Equal(CoalescingTrigger.DefaultWindow, w));
        Assert.Equal(TimeSpan.FromMilliseconds(500), CoalescingTrigger.DefaultWindow);
    }

    [Fact]
    public async Task should_run_again_when_a_second_burst_arrives_after_the_first_settled()
    {
        var delay = new ManualDelay();
        var runs = 0;
        var trigger = new CoalescingTrigger(_ => { runs++; return Task.CompletedTask; }, delay: delay.Delay);

        var first = trigger.Pulse();
        delay.ReleaseAll();
        await first;
        var second = trigger.Pulse();
        delay.ReleaseAll();
        await second;

        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task should_drop_the_pending_pulse_when_cancelled()
    {
        var delay = new ManualDelay();
        var runs = 0;
        var trigger = new CoalescingTrigger(_ => { runs++; return Task.CompletedTask; }, delay: delay.Delay);

        var pending = trigger.Pulse();
        trigger.Cancel();
        await pending;
        delay.ReleaseAll();

        Assert.Equal(0, runs);
    }

    [Fact]
    public async Task should_hand_the_exception_to_the_error_sink_when_the_action_throws()
    {
        var delay = new ManualDelay();
        Exception? seen = null;
        var trigger = new CoalescingTrigger(_ => throw new InvalidOperationException("boom"), delay: delay.Delay, onError: ex => seen = ex);

        var pending = trigger.Pulse();
        delay.ReleaseAll();
        await pending;                 // 실패로 끝나지 않는다

        Assert.IsType<InvalidOperationException>(seen);
    }
}
