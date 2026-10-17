using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : SYNC_DEVICE 복구(ACTIVATED) 시 심볼 이벤트상태 stale 고착 회귀 방지.
                  실사고: 제어기2(1352) — 조치보고가 '카드종결 no-op(부재)'로 dequeue되지
                  않은 뒤 SYNC_DEVICE ACTIVATED가 와도 EventStatus=Blackout 잔존 →
                  PidsSymbols.EventStatus 컬럼에 영속 → 앱 재시작해도 심볼이 검은색 유지.
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class SymbolRecoveryStaleStatusTests
{
    private static SymbolEventManager CreateManager(IEventQueueManager? eqm)
        => new(new Mock<IEventAggregator>().Object,
               new Mock<ILogService>().Object,
               new EventSetupModel(new Mock<IEventSetupModel>().Object),
               eqm);

    private static Mock<IControllerDeviceModel> Controller(int id)
    {
        var m = new Mock<IControllerDeviceModel>();
        m.Setup(d => d.Id).Returns(id);
        m.Setup(d => d.DeviceType).Returns(EnumDeviceType.Controller);
        m.Setup(d => d.DeviceGroups).Returns(new List<int>());
        return m;
    }

    /// <summary>복구(ACTIVATED) 수신 시 EQM이 비어 있으면 심볼 상태가 Normal로 재계산되어야 한다.</summary>
    [Fact]
    public void should_clear_stale_event_status_when_device_recovers_and_queue_empty()
    {
        // Arrange — EQM은 해당 장비에 살아있는 이벤트가 없음(Normal)
        var eqm = new Mock<IEventQueueManager>();
        eqm.Setup(q => q.GetDeviceState(1352, EnumDeviceType.Controller))
           .Returns(EnumCompositeEventStatus.Normal);

        var manager = CreateManager(eqm.Object);
        var symbol = new Mock<IPidsSymbolModel>();
        symbol.SetupAllProperties();
        manager.RegisterDeviceSymbol(Controller(1352).Object, symbol.Object);

        symbol.Object.CompositeStatus = EnumCompositeEventStatus.Blackout;   // stale 상태 재현

        // Act — SYNC_DEVICE 복구
        manager.SyncDeviceStatus(1352, EnumDeviceType.Controller, EnumDeviceStatus.ACTIVATED);

        // Assert — OperationState 뿐 아니라 이벤트 상태도 Normal로 복귀
        Assert.Equal(EnumOperationState.ACTIVATED, symbol.Object.OperationState);
        Assert.Equal(EnumCompositeEventStatus.Normal, symbol.Object.CompositeStatus);
    }

    /// <summary>큐에 실제 장애가 남아 있으면 복구 신호가 와도 그 상태를 임의로 지우지 않는다(권위=EQM).</summary>
    [Fact]
    public void should_keep_state_when_queue_still_has_live_event()
    {
        var eqm = new Mock<IEventQueueManager>();
        eqm.Setup(q => q.GetDeviceState(1352, EnumDeviceType.Controller))
           .Returns(EnumCompositeEventStatus.Faulted);

        var manager = CreateManager(eqm.Object);
        var symbol = new Mock<IPidsSymbolModel>();
        symbol.SetupAllProperties();
        manager.RegisterDeviceSymbol(Controller(1352).Object, symbol.Object);

        manager.SyncDeviceStatus(1352, EnumDeviceType.Controller, EnumDeviceStatus.ACTIVATED);

        Assert.Equal(EnumCompositeEventStatus.Faulted, symbol.Object.CompositeStatus);
    }

    /// <summary>기동 시 등록 경로 자가치유 — 장비가 ACTIVATED면 DB에서 실려온 stale 상태를 정리한다.</summary>
    [Fact]
    public void should_selfheal_stale_status_on_register_when_device_activated()
    {
        var eqm = new Mock<IEventQueueManager>();
        eqm.Setup(q => q.GetDeviceState(1352, EnumDeviceType.Controller))
           .Returns(EnumCompositeEventStatus.Normal);

        var manager = CreateManager(eqm.Object);
        var symbol = new Mock<IPidsSymbolModel>();
        symbol.SetupAllProperties();
        symbol.Object.CompositeStatus = EnumCompositeEventStatus.Blackout;   // DB에서 실려온 stale 값

        var ctrl = Controller(1352);
        ctrl.Setup(d => d.Status).Returns(EnumDeviceStatus.ACTIVATED);

        manager.RegisterDeviceSymbol(ctrl.Object, symbol.Object);

        Assert.Equal(EnumCompositeEventStatus.Normal, symbol.Object.CompositeStatus);
    }

    /// <summary>기동 시 장비가 ERROR면 재계산하지 않는다(실제 장애 상태 보존).</summary>
    [Fact]
    public void should_not_selfheal_on_register_when_device_error()
    {
        var eqm = new Mock<IEventQueueManager>();
        var manager = CreateManager(eqm.Object);
        var symbol = new Mock<IPidsSymbolModel>();
        symbol.SetupAllProperties();
        symbol.Object.CompositeStatus = EnumCompositeEventStatus.Blackout;

        var ctrl = Controller(1352);
        ctrl.Setup(d => d.Status).Returns(EnumDeviceStatus.ERROR);

        manager.RegisterDeviceSymbol(ctrl.Object, symbol.Object);

        eqm.Verify(q => q.GetDeviceState(It.IsAny<int>(), It.IsAny<EnumDeviceType>()), Times.Never);
        Assert.Equal(EnumCompositeEventStatus.Blackout, symbol.Object.CompositeStatus);
    }

    /// <summary>복구가 아닌 상태(ERROR)에서는 재계산을 돌리지 않는다(불필요한 덮어쓰기 방지).</summary>
    [Fact]
    public void should_not_recompute_when_status_is_not_activated()
    {
        var eqm = new Mock<IEventQueueManager>();
        var manager = CreateManager(eqm.Object);
        var symbol = new Mock<IPidsSymbolModel>();
        symbol.SetupAllProperties();

        // 등록 시점 자가치유가 끼어들지 않도록 ERROR로 등록(EnumDeviceStatus 기본값=ACTIVATED 주의)
        var ctrl = Controller(1352);
        ctrl.Setup(d => d.Status).Returns(EnumDeviceStatus.ERROR);
        manager.RegisterDeviceSymbol(ctrl.Object, symbol.Object);

        manager.SyncDeviceStatus(1352, EnumDeviceType.Controller, EnumDeviceStatus.ERROR);

        eqm.Verify(q => q.GetDeviceState(It.IsAny<int>(), It.IsAny<EnumDeviceType>()), Times.Never);
        Assert.Equal(EnumOperationState.ERROR, symbol.Object.OperationState);
    }
}
