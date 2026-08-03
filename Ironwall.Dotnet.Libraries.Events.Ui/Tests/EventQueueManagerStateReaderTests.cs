using Xunit;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : EventQueueManager GetGroupState/GetDeviceState(SSOT 재계산 readers) 검증 —
                  FR-03 심볼 재계산 복원이 "맹목 Normal"이 아니라 잔여 활성 이벤트를 보존함을 고정(RISK-01).
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventQueueManagerStateReaderTests
{
    private static EventEntry ControllerBlackout(int ctrlId, int groupId, int eventId)
        => new()
        {
            DeviceId = ctrlId, DeviceType = EnumDeviceType.Controller, EventType = EnumEventType.Fault,
            IsControllerBlackout = true, GroupIds = new List<int> { groupId }, EventId = eventId
        };

    private static EventEntry SensorFault(int sensorId, int groupId, int eventId)
        => new()
        {
            DeviceId = sensorId, DeviceType = EnumDeviceType.Fence, EventType = EnumEventType.Fault,
            GroupIds = new List<int> { groupId }, EventId = eventId
        };

    [Fact]
    public void should_return_blackout_when_controller_blackout_entry_active()
    {
        var eqm = new EventQueueManager();
        eqm.Enqueue(ControllerBlackout(1352, 276, 1));

        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetGroupState(276));
        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetDeviceState(1352, EnumDeviceType.Controller));
    }

    [Fact]
    public void should_return_normal_when_no_entries()
    {
        var eqm = new EventQueueManager();

        Assert.Equal(EnumCompositeEventStatus.Normal, eqm.GetGroupState(999));
        Assert.Equal(EnumCompositeEventStatus.Normal, eqm.GetDeviceState(1, EnumDeviceType.Controller));
    }

    [Fact]
    public void should_return_faulted_not_normal_when_sensor_fault_coexists_after_blackout_cleared()
    {
        // RISK-01 핵심: 컨트롤러 Blackout + 같은 그룹 센서 Fault 공존 → Blackout.
        // 컨트롤러 엔트리 제거(조치보고/복구) 후엔 잔여 센서 Fault만 → Faulted(맹목 Normal 금지).
        var eqm = new EventQueueManager();
        var ctrlEntryId = eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        eqm.Enqueue(SensorFault(2066, 276, 2));

        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetGroupState(276));   // Blackout 최우선

        eqm.Dequeue(ctrlEntryId);

        Assert.Equal(EnumCompositeEventStatus.Faulted, eqm.GetGroupState(276));     // 잔여 센서 Fault → Faulted, Normal 아님
    }
}
