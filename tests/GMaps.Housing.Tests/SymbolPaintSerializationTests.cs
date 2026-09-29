using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// WP-8 L7 — 지도 재칠(<see cref="SymbolLifecycleCoordinator"/>)이 큐를 읽은 뒤 칠하기 전에 NATS 전이가 먼저 칠하면,
/// 재칠의 옛 값이 마지막에 덮어 큐와 어긋났다. 재칠과 전이는 같은 칠하기 문을 지나야 한다.
/// </summary>
public class SymbolPaintSerializationTests
{
    private readonly Mock<ILogService> _log = new();

    /// <summary>
    /// 첫 <c>GetDeviceState</c> 읽기 직후 큐가 Detecting 으로 바뀌고, 그 전이가 다른 스레드에서 조회표로 온다.
    /// </summary>
    private (Mock<IEventQueueManager> Queue, Func<Thread?> Transition) RacingQueue(Func<SymbolEventManager> sem, int id, EnumDeviceType type)
    {
        var state = EnumCompositeEventStatus.Normal;
        Thread? nats = null;
        var queue = new Mock<IEventQueueManager>();
        queue.Setup(q => q.GetDeviceState(id, type)).Returns(() =>
        {
            var read = state;
            if (nats == null)
            {
                state = EnumCompositeEventStatus.Detecting;
                nats = new Thread(() => sem().HandleDeviceStateChanged(id, type, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Detecting));
                nats.Start();
                nats.Join(TimeSpan.FromMilliseconds(300));   // 막히지 않으면 먼저 칠한다(종전 순서 재현)
            }
            return read;
        });
        return (queue, () => nats);
    }

    [Fact]
    public void should_end_with_queue_state_when_transition_lands_during_complete_rebuild()
    {
        SymbolEventManager? sem = null;
        var (queue, transition) = RacingQueue(() => sem!, 21, EnumDeviceType.Fence);
        sem = new SymbolEventManager(new Mock<IEventAggregator>().Object, _log.Object, new EventSetupModel(new Mock<IEventSetupModel>().Object), queue.Object);
        var coordinator = new SymbolLifecycleCoordinator(sem, queue.Object, _log.Object);
        var symbol = new PidsSymbolModel { Title = "Fence", DeviceType = EnumDeviceType.Fence };

        coordinator.BeginRebuild();
        coordinator.RegisterDevice(new BaseDeviceModel { Id = 21, DeviceType = EnumDeviceType.Fence, Status = EnumDeviceStatus.ERROR }, symbol);
        coordinator.CompleteRebuild();
        Assert.True(transition()!.Join(TimeSpan.FromSeconds(5)));

        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.CompositeStatus);
    }

    [Fact]
    public void should_end_with_queue_state_when_transition_lands_during_single_reconcile()
    {
        SymbolEventManager? sem = null;
        var (queue, transition) = RacingQueue(() => sem!, 22, EnumDeviceType.PIR);
        sem = new SymbolEventManager(new Mock<IEventAggregator>().Object, _log.Object, new EventSetupModel(new Mock<IEventSetupModel>().Object), queue.Object);
        var coordinator = new SymbolLifecycleCoordinator(sem, queue.Object, _log.Object);
        var symbol = new PidsSymbolModel { Title = "PIR", DeviceType = EnumDeviceType.PIR };
        coordinator.RegisterDevice(new BaseDeviceModel { Id = 22, DeviceType = EnumDeviceType.PIR, Status = EnumDeviceStatus.ERROR }, symbol);

        Assert.True(coordinator.ReconcileDevice(22, EnumDeviceType.PIR));
        Assert.True(transition()!.Join(TimeSpan.FromSeconds(5)));

        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.CompositeStatus);
    }
}
