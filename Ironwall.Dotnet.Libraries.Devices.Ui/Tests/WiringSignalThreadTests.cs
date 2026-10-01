using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 신호등 알림의 스레드(간헐 "Collection was modified" 대응) — ping 모니터는 작업 스레드에서 표본을 올린다. 알림(<see cref="WiringViewModel.FenceChanged"/> ·
/// 속성 알림)은 창을 만든 스레드에서만 오르고, 창이 닫힌 뒤 도착한 표본은 아무것도 하지 않는다.
/// </summary>
public class WiringSignalThreadTests
{
    private static WiringViewModel Build(FakePing ping)
    {
        var seeds = Enumerable.Range(0, 3).Select(i => new WiringSensorSeed(101 + i, i + 1,
            new SensorFacts(1101 + i, $"북측 {i + 1}구간", "SmartSensor2", "북측"), new WiringPlacement(1, i + 1)));
        return WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"), seeds,
            new[] { "SmartSensor2" }, null, new WiringFakeDialogs(), fence: new WiringFenceContext(null, new FakeFenceStore(), ping));
    }

    [Fact]
    public async Task should_ignore_a_sample_that_arrives_on_a_worker_thread_after_the_window_closed()
    {
        // Arrange — 창이 열려(루프가 돈다) 모니터를 붙잡아 두고 닫는다
        var ping = new FakePing();
        var vm = Build(ping);
        await ((IActivate)vm).ActivateAsync();
        var monitor = vm.SignalMonitor!;
        await ((IDeactivate)vm).DeactivateAsync(close: true);
        var raised = 0;
        vm.PropertyChanged += (_, _) => Interlocked.Increment(ref raised);
        vm.FenceChanged += (_, _) => Interlocked.Increment(ref raised);
        ping.Next.Enqueue(new PingSample(false, 0));

        // Act — 비행 중이던 ping 이 닫힌 뒤 작업 스레드에서 도착한다
        var exception = await Record.ExceptionAsync(() => Task.Run(() => monitor.PingOnceAsync()));

        // Assert
        Assert.Null(exception);
        Assert.Equal(0, raised);
        Assert.Null(vm.SignalMonitor);
        Assert.False(vm.IsPinging);
    }

    [Fact]
    public async Task should_not_raise_from_a_worker_thread_when_the_window_has_no_dispatcher_and_signals_are_stopped()
    {
        var ping = new FakePing();
        var vm = Build(ping);
        await vm.PingControllerOnceAsync();
        var raised = 0;
        vm.PropertyChanged += (_, _) => Interlocked.Increment(ref raised);
        vm.FenceChanged += (_, _) => Interlocked.Increment(ref raised);

        // Act — 멈추지 않은 채 작업 스레드에서 표본(디스패처 없음 → 알리지 않는다) · 멈춘 뒤 표본(버린다)
        await Task.Run(() => vm.SignalMonitor!.PingOnceAsync());
        var fromWorker = raised;
        vm.StopSignals();
        await Task.Run(() => vm.SignalMonitor!.PingOnceAsync());

        // Assert — 상태는 모니터가 쥐고 있어 읽으면 맞다
        Assert.Equal(0, fromWorker);
        Assert.Equal(0, raised);
        Assert.Equal(SignalLevel.Ok, vm.ControllerSignal);
    }

    [Fact]
    public void should_raise_signal_notifications_only_on_the_dispatcher_thread_when_samples_arrive_on_workers()
    {
        // Arrange — 디스패처가 있는 STA 에서 창을 만든다(제품: 입구가 UI 스레드에서 만든다)
        var threads = new ConcurrentBag<int>();
        int owner = 0;
        Exception? failure = null;
        var sta = new Thread(() =>
        {
            try
            {
                owner = Environment.CurrentManagedThreadId;
                var dispatcher = Dispatcher.CurrentDispatcher;
                var ping = new FakePing();
                for (var i = 0; i < 3; i++) ping.Next.Enqueue(new PingSample(false, 0));            // Down 으로 바뀌어 FenceChanged 까지 오른다
                var vm = Build(ping);
                Assert.Same(dispatcher, vm.SignalDispatcher);
                vm.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
                vm.FenceChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
                vm.PingControllerOnceAsync().GetAwaiter().GetResult();                                // 주인 스레드에서 모니터를 만든다

                // Act — 작업 스레드에서 표본 넷(모두 그 스레드에서 이벤트가 난다)
                Task.Run(async () => { for (var i = 0; i < 4; i++) await vm.SignalMonitor!.PingOnceAsync(); }).GetAwaiter().GetResult();
                dispatcher.Invoke(() => { }, DispatcherPriority.Background);                             // 넘겨진 알림을 처리한다
                vm.StopSignals();
            }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        Assert.True(sta.Join(TimeSpan.FromSeconds(20)));

        // Assert
        Assert.Null(failure);
        Assert.NotEmpty(threads);
        Assert.All(threads, id => Assert.Equal(owner, id));
    }
}
