using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 심볼 조회표(SymbolEventManager) 수명 — 장비 해제(WP-1 ㉒) · 미등록 장비 장비당 1회 경고(㉓) ·
                  SYNC_DEVICE 상태 동기화의 UI 스레드 합치기(㉔, 헤드리스는 동기 폴백).
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class SymbolEventManagerLifecycleTests
{
    private readonly Mock<ILogService> _log = new();

    private SymbolEventManager CreateManager()
    {
        var setup = new Mock<IEventSetupModel>();
        setup.SetupAllProperties();
        return new SymbolEventManager(new Mock<IEventAggregator>().Object, _log.Object, new EventSetupModel(setup.Object));
    }

    private static (IBaseDeviceModel Device, Mock<IPidsEventCapable> Symbol) Pair(int id, EnumDeviceType type)
    {
        var device = new Mock<IBaseDeviceModel>();
        device.SetupGet(d => d.Id).Returns(id);
        device.SetupGet(d => d.DeviceType).Returns(type);
        device.SetupGet(d => d.Status).Returns(EnumDeviceStatus.DEACTIVATED);
        var symbol = new Mock<IPidsEventCapable>();
        symbol.SetupAllProperties();
        return (device.Object, symbol);
    }

    [Fact]
    public void should_normalize_the_symbol_and_stop_painting_it_when_the_device_is_unregistered()
    {
        var sem = CreateManager();
        var (device, symbol) = Pair(14, EnumDeviceType.Fence);
        sem.RegisterDeviceSymbol(device, symbol.Object);
        sem.HandleDeviceStateChanged(14, EnumDeviceType.Fence, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Detecting);
        Assert.Equal(EnumCompositeEventStatus.Detecting, symbol.Object.CompositeStatus);

        Assert.True(sem.UnregisterDeviceSymbol(14, EnumDeviceType.Fence));

        Assert.Equal(EnumCompositeEventStatus.Normal, symbol.Object.CompositeStatus);
        sem.HandleDeviceStateChanged(14, EnumDeviceType.Fence, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Faulted);
        Assert.Equal(EnumCompositeEventStatus.Normal, symbol.Object.CompositeStatus);   // 늦은 전이가 칠하지 않는다(Id 폴백도 끊김)
        Assert.False(sem.HasDeviceSymbol(14, EnumDeviceType.Fence));
        Assert.False(sem.UnregisterDeviceSymbol(14, EnumDeviceType.Fence));               // 두 번째는 no-op
    }

    [Fact]
    public void should_not_paint_the_remaining_kind_when_a_none_type_transition_arrives_after_one_of_two_kinds_is_unregistered()
    {
        // WP-8 H2(57abf5db) — Id 단독 폴백은 NONE 을 받지 않는다. 종류를 모르는 전이가 같은 번호의 다른 장비를 칠하면
        //   그 조치가 남의 상태색을 지운다((7,NONE) 이 카메라 7 을 칠하던 결함). 종전 이 시험은 그 오칠을 기대했다.
        var sem = CreateManager();
        var (fence, fenceSymbol) = Pair(20, EnumDeviceType.Fence);
        var (camera, cameraSymbol) = Pair(20, EnumDeviceType.IpCamera);
        sem.RegisterDeviceSymbol(fence, fenceSymbol.Object);
        sem.RegisterDeviceSymbol(camera, cameraSymbol.Object);

        sem.UnregisterDeviceSymbol(20, EnumDeviceType.IpCamera);
        sem.HandleDeviceStateChanged(20, EnumDeviceType.NONE, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Detecting);

        Assert.Equal(EnumCompositeEventStatus.Normal, fenceSymbol.Object.CompositeStatus);    // 남은 울타리를 칠하지 않는다
        Assert.Equal(EnumCompositeEventStatus.Normal, cameraSymbol.Object.CompositeStatus);   // 해제된 카메라도(해제 때 정상화된 그대로)
    }

    [Fact]
    public void should_warn_once_per_device_when_transitions_arrive_for_an_unregistered_device()
    {
        var sem = CreateManager();

        sem.HandleDeviceStateChanged(99, EnumDeviceType.PIR, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Detecting);
        sem.SetDoorState(99, EnumDeviceType.PIR, EnumDoorState.Open);
        sem.ApplyDoorEvent(99, EnumDeviceType.PIR, EnumEventType.ContactOn);
        sem.HandleDeviceStateChanged(98, EnumDeviceType.PIR, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Faulted);

        _log.Verify(l => l.Warning(It.Is<string>(m => m.Contains("[심볼 미등록]") && m.Contains("Device(99,")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        _log.Verify(l => l.Warning(It.Is<string>(m => m.Contains("[심볼 미등록]") && m.Contains("Device(98,")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void should_still_notify_the_symbol_headless_when_a_sync_device_status_arrives()
    {
        // ㉔ — SyncFromDevice 는 이제 UI 스레드로 합쳐 넘긴다. 앱이 없으면(헤드리스) 동기 폴백으로 곧바로 통지한다.
        var sem = CreateManager();
        var (device, symbol) = Pair(3, EnumDeviceType.Fence);
        sem.RegisterDeviceSymbol(device, symbol.Object);
        symbol.Invocations.Clear();

        sem.SyncDeviceStatus(3, EnumDeviceType.Fence, EnumDeviceStatus.ERROR);

        Assert.Equal(EnumOperationState.ERROR, symbol.Object.OperationState);
        symbol.Verify(s => s.SetUpdate(), Times.Once);
    }
}
