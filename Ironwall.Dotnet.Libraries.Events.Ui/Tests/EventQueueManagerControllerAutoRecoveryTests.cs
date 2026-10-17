using Xunit;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 제어기 고장 자동복구 확장(Controller_Fault_AutoRecovery_Extension) 검증 —
                  (a)소속 센서 탐지·(b)SYNC_DEVICE 복구(TryAutoRecoverController)로 제어기 블랙아웃
                  Fault 자동복구 + 그룹 검정 해제. controller-ownership 정밀 매칭(공유그룹 오매칭 방지).
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventQueueManagerControllerAutoRecoveryTests
{
    private static EventEntry ControllerBlackout(int ctrlId, int groupId, int eventId)
        => new()
        {
            DeviceId = ctrlId, DeviceType = EnumDeviceType.Controller, EventType = EnumEventType.Fault,
            IsControllerBlackout = true, GroupIds = new List<int> { groupId }, EventId = eventId
        };

    private static EventEntry MemberDetection(int sensorId, int groupId, int eventId, int? owningControllerId)
        => new()
        {
            DeviceId = sensorId, DeviceType = EnumDeviceType.Fence, EventType = EnumEventType.Intrusion,
            GroupIds = new List<int> { groupId }, EventId = eventId, OwningControllerId = owningControllerId
        };

    [Fact]
    public void should_autorecover_controller_when_member_sensor_detects()
    {
        // 트리거 (a): 고장 제어기 소속 센서가 탐지 → 제어기 Fault 자동복구 + 그룹 Detecting.
        var eqm = new EventQueueManager();
        string? recoveredId = null;
        eqm.OnAutoRecovery += id => recoveredId = id;

        var ctrlEntryId = eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetGroupState(276));

        eqm.Enqueue(MemberDetection(2066, 276, 2, owningControllerId: 1352));

        Assert.Equal(ctrlEntryId, recoveredId);                                    // 제어기 Fault 자동복구 발화
        Assert.Equal(EnumCompositeEventStatus.Detecting, eqm.GetGroupState(276));   // 검정 해제 → 잔여 탐지만
    }

    [Fact]
    public void should_not_autorecover_when_detection_owns_different_controller()
    {
        // V-03: 그룹 공유하더라도 다른 제어기 소속 센서 탐지는 제어기A 검정을 풀지 않음(controller-ownership 정밀 매칭).
        var eqm = new EventQueueManager();
        string? recoveredId = null;
        eqm.OnAutoRecovery += id => recoveredId = id;

        eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        eqm.Enqueue(MemberDetection(3001, 276, 2, owningControllerId: 9999));       // 다른 제어기(9999) 소속

        Assert.Null(recoveredId);                                                  // 자동복구 미발동
        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetGroupState(276));    // 제어기A 검정 유지
    }

    [Fact]
    public void should_autorecover_controller_when_TryAutoRecoverController_called()
    {
        // 트리거 (b): 제어기 통신 복구(SYNC_DEVICE ACTIVATED) → TryAutoRecoverController.
        var eqm = new EventQueueManager();
        string? recoveredId = null;
        eqm.OnAutoRecovery += id => recoveredId = id;

        var ctrlEntryId = eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetGroupState(276));

        var result = eqm.TryAutoRecoverController(1352);

        Assert.True(result);
        Assert.Equal(ctrlEntryId, recoveredId);                                    // 조치보고 경로 발화
        Assert.Equal(EnumCompositeEventStatus.Normal, eqm.GetGroupState(276));      // 잔여 엔트리 없음 → 검정 해제
    }

    [Fact]
    public void should_return_false_when_TryAutoRecoverController_has_no_entry()
    {
        // 멱등: 대상 제어기 블랙아웃 엔트리가 없으면 no-op false.
        var eqm = new EventQueueManager();
        Assert.False(eqm.TryAutoRecoverController(1352));
    }

    [Fact]
    public void should_skip_controller_autorecovery_when_owning_controller_id_null()
    {
        // V-05: 소속 제어기 미해석(provider 미주입 등) → OwningControllerId null → 트리거 (a) 스킵(안전실패).
        var eqm = new EventQueueManager();
        string? recoveredId = null;
        eqm.OnAutoRecovery += id => recoveredId = id;

        eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        eqm.Enqueue(MemberDetection(2066, 276, 2, owningControllerId: null));

        Assert.Null(recoveredId);                                                  // 미발동
        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetGroupState(276));    // 검정 유지
    }

    // ══════ 제어기 '자신' 심볼 전이 (실사고: 그룹만 풀리고 제어기 아이콘은 검은색 잔존) ══════

    [Fact]
    public void should_report_controller_device_state_normal_after_member_detection_recovery()
    {
        // 종전 갭: 자동복구가 수신 엔트리(센서) 키로만 전이를 발화해 제어기 자기 상태가 갱신 안 됨.
        var eqm = new EventQueueManager();
        eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        Assert.Equal(EnumCompositeEventStatus.Blackout, eqm.GetDeviceState(1352, EnumDeviceType.Controller));

        eqm.Enqueue(MemberDetection(2066, 276, 2, owningControllerId: 1352));

        Assert.Equal(EnumCompositeEventStatus.Normal, eqm.GetDeviceState(1352, EnumDeviceType.Controller));
    }

    [Fact]
    public void should_fire_device_state_changed_for_controller_key_on_autorecovery()
    {
        // 심볼 복원은 OnDeviceStateChanged 구독으로 이뤄지므로, 제어기 '키'로 발화되어야 한다.
        var eqm = new EventQueueManager();
        var fired = new List<(int Id, EnumDeviceType Type, EnumCompositeEventStatus Prev, EnumCompositeEventStatus Next)>();
        eqm.OnDeviceStateChanged += (id, type, prev, next) => fired.Add((id, type, prev, next));

        eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        fired.Clear();

        eqm.Enqueue(MemberDetection(2066, 276, 2, owningControllerId: 1352));

        var ctrl = fired.Find(f => f.Id == 1352 && f.Type == EnumDeviceType.Controller);
        Assert.Equal(1352, ctrl.Id);                                   // 제어기 키 전이 발화됨
        Assert.Equal(EnumCompositeEventStatus.Blackout, ctrl.Prev);
        Assert.Equal(EnumCompositeEventStatus.Normal, ctrl.Next);
    }

    [Fact]
    public void should_not_duplicate_device_transition_when_controller_is_incoming_device()
    {
        // 수신 엔트리 자체가 제어기 키인 경우(제어기 자신 탐지) — 기존 device 전이가 담당하므로 중복 발화 금지.
        var eqm = new EventQueueManager();
        var fired = new List<(int Id, EnumDeviceType Type)>();
        eqm.OnDeviceStateChanged += (id, type, _, _) => fired.Add((id, type));

        eqm.Enqueue(ControllerBlackout(1352, 276, 1));
        fired.Clear();

        eqm.Enqueue(new EventEntry
        {
            DeviceId = 1352, DeviceType = EnumDeviceType.Controller, EventType = EnumEventType.Intrusion,
            GroupIds = new List<int> { 276 }, EventId = 3, OwningControllerId = 1352
        });

        Assert.Equal(1, fired.FindAll(f => f.Id == 1352 && f.Type == EnumDeviceType.Controller).Count);
    }
}
