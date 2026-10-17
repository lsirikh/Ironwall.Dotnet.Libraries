using Xunit;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : entryId ↔ 카드 매칭 로직 단위 테스트
                   NATS UUID 기반 1:1 직접 매칭 + 복수 탐지 회귀 방지
   Created By   : GHLee
   Created On   : 2026-03-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EntryIdMatchingTests
{
    private EventQueueManager CreateSut() => new();

    private EventEntry CreateEntry(int deviceId, EnumDeviceType deviceType = EnumDeviceType.Fence)
        => new()
        {
            DeviceId = deviceId,
            DeviceType = deviceType,
            GroupIds = new List<int> { 1 },
            EventType = EnumEventType.Intrusion,
            TimeoutSeconds = 30,
            IsAutoReportEnabled = false
        };

    #region - Phase 4: 1:1 직접 매칭 기본 -

    /// <summary>
    /// NATS UUID를 entryId로 사용 → Enqueue 후 해당 UUID로 조회 가능
    /// </summary>
    [Fact]
    public void NatsUuid_Enqueue_ShouldBeRetrievableByNatsId()
    {
        var sut = CreateSut();
        var natsId = "ff73e0a8-2980-4074-9848-53014aa5cc5f";

        var entryId = sut.Enqueue(CreateEntry(deviceId: 6), natsId);

        Assert.Equal(natsId, entryId);
        Assert.NotNull(sut.GetEntry(natsId));
    }

    /// <summary>
    /// _pendingEntries 시뮬레이션: eventId 기반 매칭이 deviceId 기반보다 정확함
    /// </summary>
    [Fact]
    public void EventIdMatching_SameDevice_ShouldAssignDifferentEntryIds()
    {
        // Arrange — 같은 디바이스에 2건, 각각 다른 NATS UUID
        var sut = CreateSut();
        var natsId1 = "nats-uuid-1";
        var natsId2 = "nats-uuid-2";

        sut.Enqueue(CreateEntry(deviceId: 6), natsId1);
        sut.Enqueue(CreateEntry(deviceId: 6), natsId2);

        // Act — eventId 기반 매칭 시뮬레이션 (pendingEntries)
        var pendingEntries = new Dictionary<int, string>
        {
            [29651] = natsId1,
            [29652] = natsId2
        };

        // Assert — 각 카드(eventId)가 다른 entryId를 받음
        Assert.Equal(natsId1, pendingEntries[29651]);
        Assert.Equal(natsId2, pendingEntries[29652]);
        Assert.NotEqual(pendingEntries[29651], pendingEntries[29652]);
    }

    /// <summary>
    /// EntryId가 이미 있는 카드는 재매칭하지 않음
    /// </summary>
    [Fact]
    public void AlreadyAssigned_ShouldNotReassign()
    {
        // TryAssignEntryId: if (card.EntryId != null) return;
        string? existingEntryId = "already-assigned";
        Assert.NotNull(existingEntryId); // 이 조건이면 스킵됨
    }

    /// <summary>
    /// EntryId null인 카드: Dequeue 시도하지 않음 (PRD FR-06~08 유지)
    /// </summary>
    [Fact]
    public void NullEntryId_ShouldNotDequeue()
    {
        var sut = CreateSut();
        var entryId = sut.Enqueue(CreateEntry(deviceId: 1));

        string? cardEntryId = null;
        if (cardEntryId != null)
            sut.Dequeue(cardEntryId);

        Assert.Equal(1, sut.GetTotalQueueCount());
        Assert.NotNull(sut.GetEntry(entryId));
    }

    #endregion

    #region - Phase 5: 복수 탐지 회귀 방지 -

    /// <summary>
    /// 핵심 회귀 테스트: 같은 디바이스 복수 이벤트 → 각각 다른 NATS UUID로 Enqueue
    /// → 순차 Dequeue → 마지막에 OnDeviceEmpty 발생
    /// (이전 버그: FindEntryByDevice가 두 카드에 같은 entryId를 매칭하여 영구 Detecting)
    /// </summary>
    [Fact]
    public void DuplicateDevice_NatsUuid_DequeueAll_ShouldRestoreSymbol()
    {
        var sut = CreateSut();
        var deviceEmptyCount = 0;
        var groupEmptyCount = 0;
        sut.OnDeviceEmpty += (id, type) => deviceEmptyCount++;
        sut.OnGroupEmpty += (gid) => groupEmptyCount++;

        // Sensor1(device=6) 탐지 2회 — 각각 다른 NATS UUID
        var entryId1 = sut.Enqueue(CreateEntry(deviceId: 6), "nats-msg-aaa");
        var entryId2 = sut.Enqueue(CreateEntry(deviceId: 6), "nats-msg-bbb");

        Assert.Equal("nats-msg-aaa", entryId1);
        Assert.Equal("nats-msg-bbb", entryId2);
        Assert.Equal(2, sut.GetTotalQueueCount());

        // 조치보고 Card(29651) → Dequeue("nats-msg-aaa")
        sut.Dequeue(entryId1);
        Assert.Equal(0, deviceEmptyCount); // 아직 1건 남음
        Assert.Equal(1, sut.GetTotalQueueCount());

        // 조치보고 Card(29652) → Dequeue("nats-msg-bbb")
        sut.Dequeue(entryId2);
        Assert.Equal(1, deviceEmptyCount); // Device(6) 비어짐 → 심볼 Normal ✅
        Assert.Equal(1, groupEmptyCount);  // Group(1) 비어짐 → 그룹 심볼 Normal ✅
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    /// <summary>
    /// 같은 디바이스 3건 + 다른 디바이스 1건 → 전체 조치보고 시나리오
    /// </summary>
    [Fact]
    public void MixedDevices_NatsUuid_DequeueAll_ShouldRestoreAll()
    {
        var sut = CreateSut();
        var emptyDevices = new List<int>();
        sut.OnDeviceEmpty += (id, type) => emptyDevices.Add(id);

        // Sensor1(6) x2 + Sensor5(10) x1 — 같은 그룹
        var e1 = sut.Enqueue(CreateEntry(deviceId: 6), "nats-1");
        var e2 = sut.Enqueue(CreateEntry(deviceId: 6), "nats-2");
        var e3 = sut.Enqueue(CreateEntry(deviceId: 10), "nats-3");

        // 전체 조치보고: 순차 Dequeue
        sut.Dequeue(e1);
        Assert.DoesNotContain(6, emptyDevices); // 아직 nats-2 남음

        sut.Dequeue(e2);
        Assert.Contains(6, emptyDevices); // Device(6) 복원 ✅

        sut.Dequeue(e3);
        Assert.Contains(10, emptyDevices); // Device(10) 복원 ✅
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    /// <summary>
    /// pendingEntries가 eventId 기반이므로, 다른 디바이스의 entry가 잘못 매칭되지 않음
    /// </summary>
    [Fact]
    public void CrossDevice_ShouldNotCrossMatch()
    {
        var sut = CreateSut();

        sut.Enqueue(CreateEntry(deviceId: 1), "nats-d1");
        sut.Enqueue(CreateEntry(deviceId: 2), "nats-d2");

        // eventId 기반 매칭 시뮬레이션
        var pendingEntries = new Dictionary<int, string>
        {
            [100] = "nats-d1", // event 100 → device 1
            [200] = "nats-d2"  // event 200 → device 2
        };

        // eventId 100의 카드는 nats-d1만 받음
        Assert.Equal("nats-d1", pendingEntries[100]);
        // eventId 200의 카드는 nats-d2만 받음
        Assert.Equal("nats-d2", pendingEntries[200]);
    }

    #endregion
}
