using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using System.Collections.Concurrent;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : SYNC_DEVICE 상태 동기화(WP-1 ㉔)의 UI 스레드 합치기 — 앱(Application)이 있을 때의 길.
                  SymbolEventManagerLifecycleTests 는 헤드리스(동기 폴백)만 본다. 여기서는 STA 스레드에 Application ·
                  Dispatcher 를 세우고, NATS 스레드에서 N 번 불러도 SetUpdate 가 <b>UI 스레드에서 한 번</b>만 도는지 본다(Loop A, WP-7).
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection(WpfApplicationSingletonCollection.Name)]
public class SymbolLookupDispatcherMarshalTests
{
    private static SymbolEventManager CreateManager()
    {
        var setup = new Mock<IEventSetupModel>();
        setup.SetupAllProperties();
        return new SymbolEventManager(new Mock<IEventAggregator>().Object, new Mock<ILogService>().Object, new EventSetupModel(setup.Object));
    }

    [Fact]
    public void should_coalesce_symbol_updates_onto_the_ui_dispatcher_when_sync_device_status_arrives_off_the_ui_thread()
    {
        // Arrange — 심볼 등록은 앱이 없을 때(동기 폴백) 끝내 두고, 그 뒤의 SetUpdate 만 센다.
        var sem = CreateManager();
        var device = new Mock<IBaseDeviceModel>();
        device.SetupGet(d => d.Id).Returns(4);
        device.SetupGet(d => d.DeviceType).Returns(EnumDeviceType.Fence);
        device.SetupGet(d => d.Status).Returns(EnumDeviceStatus.DEACTIVATED);
        var symbol = new Mock<IPidsEventCapable>();
        symbol.SetupAllProperties();
        sem.RegisterDeviceSymbol(device.Object, symbol.Object);
        var updateThreads = new ConcurrentQueue<int>();
        symbol.Setup(s => s.SetUpdate()).Callback(() => updateThreads.Enqueue(Environment.CurrentManagedThreadId));

        using var ui = UiApplicationScope.Start();
        using var hold = new ManualResetEventSlim(false);
        ui.Dispatcher.InvokeAsync(() => hold.Wait(TimeSpan.FromSeconds(10)));   // UI 스레드를 잠시 붙잡아 합치기를 결정적으로 만든다

        // Act — 이 스레드(= NATS 수신 스레드 역할)에서 연달아 다섯 번
        for (var i = 0; i < 5; i++)
            sem.SyncDeviceStatus(4, EnumDeviceType.Fence, EnumDeviceStatus.ERROR);   // ACTIVATED 는 큐 재계산 길이 따로 있어 뺀다
        var beforeFlush = updateThreads.Count;
        hold.Set();
        Assert.True(ui.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task.Wait(TimeSpan.FromSeconds(10)),
            "UI 디스패처가 제시간에 비지 않았다");

        // Assert
        Assert.Equal(0, beforeFlush);                                    // 부른 스레드에서 곧장 SetUpdate 하지 않는다
        var only = Assert.Single(updateThreads);                         // 다섯 번 → 한 번으로 합쳐졌다
        Assert.Equal(ui.ThreadId, only);                                 // 그 한 번은 UI 스레드에서
        Assert.NotEqual(Environment.CurrentManagedThreadId, only);
        Assert.Equal(EnumOperationState.ERROR, symbol.Object.OperationState);
    }

    /// <summary>
    /// STA 스레드에 Application 과 Dispatcher 를 세운다. 끝나면 디스패처를 멈추고 <c>Application.Current</c> 를 비워
    /// 이 어셈블리의 다른 시험(헤드리스 동기 폴백을 기대한다)이 앱을 물려받지 않게 한다.
    /// </summary>
    private sealed class UiApplicationScope : IDisposable
    {
        private readonly Thread _thread;
        public Dispatcher Dispatcher { get; }
        public int ThreadId { get; }

        private UiApplicationScope(Thread thread, Dispatcher dispatcher, int threadId)
        {
            _thread = thread;
            Dispatcher = dispatcher;
            ThreadId = threadId;
        }

        public static UiApplicationScope Start()
        {
            Assert.Null(Application.Current);   // 이 시험 전에 누가 앱을 세워 두었다면 격리가 깨진 것이다
            using var ready = new ManualResetEventSlim(false);
            Dispatcher? dispatcher = null;
            var threadId = 0;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    dispatcher = Dispatcher.CurrentDispatcher;
                    threadId = Environment.CurrentManagedThreadId;
                }
                catch (Exception ex) { failure = ex; }
                finally { ready.Set(); }
                if (failure is null) Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "UI 스레드가 제시간에 서지 않았다");
            if (failure is not null) throw new InvalidOperationException("Application 을 세우지 못했다", failure);
            return new UiApplicationScope(thread, dispatcher!, threadId);
        }

        public void Dispose()
        {
            Dispatcher.InvokeShutdown();
            _thread.Join(TimeSpan.FromSeconds(10));
            // WPF 는 Application 을 AppDomain 당 하나로 묶고 Current 를 비우지 않는다 — 비공개 정적 칸을 되돌린다.
            //   (되돌리지 못해도 디스패처가 멈췄으므로 MarshalUpdate 는 HasShutdownStarted 로 동기 폴백한다.)
            typeof(Application).GetField("_appInstance", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
        }
    }
}

/// <summary>프로세스 전역 Application 을 잠시 세우는 시험 — 다른 시험과 겹쳐 돌면 그 시험의 MarshalUpdate 가 이 디스패처로 간다.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfApplicationSingletonCollection
{
    public const string Name = "WPF Application singleton (process-wide)";
}
