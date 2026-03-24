using Xunit;
using Moq;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : EventQueueManager 단위 테스트
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventQueueManagerTests
{
    private EventQueueManager CreateSut()
    {
        return new EventQueueManager();
    }

    private EventEntry CreateEntry(int deviceId = 1, List<int>? groupIds = null, EnumEventType eventType = EnumEventType.Intrusion)
    {
        return new EventEntry
        {
            DeviceId = deviceId,
            DeviceType = EnumDeviceType.Fence,
            GroupIds = groupIds ?? new List<int> { 1 },
            EventType = eventType,
            TimeoutSeconds = 30,
            SourceEvent = new Mock<IExEventModel>().Object,
            IsAutoReportEnabled = true
        };
    }

    #region - Phase 3: Enqueue -

    [Fact]
    public void Enqueue_ShouldRegisterEntryInGlobalStore()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();

        // Act
        var entryId = sut.Enqueue(entry);

        // Assert
        Assert.Equal(1, sut.GetTotalQueueCount());
        var retrieved = sut.GetEntry(entryId);
        Assert.NotNull(retrieved);
        Assert.Equal(entry.DeviceId, retrieved.DeviceId);
    }

    [Fact]
    public void Enqueue_ShouldRegisterGroupReverseIndex()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry(groupIds: new List<int> { 10, 20 });

        // Act
        sut.Enqueue(entry);

        // Assert
        Assert.True(sut.HasEventsForGroup(10));
        Assert.True(sut.HasEventsForGroup(20));
        Assert.False(sut.HasEventsForGroup(99));
    }

    [Fact]
    public void Enqueue_FirstEventInGroup_ShouldRaiseGroupFirstEvent()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry(groupIds: new List<int> { 5 });
        var raisedGroupIds = new List<int>();
        sut.OnGroupFirstEvent += (groupId, eventType) => raisedGroupIds.Add(groupId);

        // Act
        sut.Enqueue(entry);

        // Assert
        Assert.Contains(5, raisedGroupIds);
    }

    [Fact]
    public void Enqueue_SecondEventInSameGroup_ShouldNotRaiseGroupFirstEvent()
    {
        // Arrange
        var sut = CreateSut();
        var entry1 = CreateEntry(deviceId: 1, groupIds: new List<int> { 5 });
        var entry2 = CreateEntry(deviceId: 2, groupIds: new List<int> { 5 });
        var raisedCount = 0;
        sut.OnGroupFirstEvent += (groupId, eventType) => raisedCount++;

        // Act
        sut.Enqueue(entry1);
        sut.Enqueue(entry2);

        // Assert
        Assert.Equal(1, raisedCount); // 첫 번째만 발행
    }

    [Fact]
    public void Enqueue_ShouldReturnNonEmptyUuid()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();

        // Act
        var entryId = sut.Enqueue(entry);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(entryId));
        Assert.NotNull(sut.GetEntry(entryId));
    }

    #endregion

    #region - Phase 4: Dequeue -

    [Fact]
    public void Dequeue_ShouldRemoveEntryFromGlobalStore()
    {
        // Arrange
        var sut = CreateSut();
        var entryId = sut.Enqueue(CreateEntry());

        // Act
        sut.Dequeue(entryId);

        // Assert
        Assert.Equal(0, sut.GetTotalQueueCount());
        Assert.Null(sut.GetEntry(entryId));
    }

    [Fact]
    public void Dequeue_LastEventInGroup_ShouldRaiseGroupEmpty()
    {
        // Arrange
        var sut = CreateSut();
        var entryId = sut.Enqueue(CreateEntry(groupIds: new List<int> { 7 }));
        var emptyGroupIds = new List<int>();
        sut.OnGroupEmpty += (groupId) => emptyGroupIds.Add(groupId);

        // Act
        sut.Dequeue(entryId);

        // Assert
        Assert.Contains(7, emptyGroupIds);
        Assert.False(sut.HasEventsForGroup(7));
    }

    [Fact]
    public void Dequeue_NotLastEventInGroup_ShouldNotRaiseGroupEmpty()
    {
        // Arrange
        var sut = CreateSut();
        var entryId1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 7 }));
        var entryId2 = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 7 }));
        var emptyRaised = false;
        sut.OnGroupEmpty += (groupId) => emptyRaised = true;

        // Act
        sut.Dequeue(entryId1);

        // Assert
        Assert.False(emptyRaised);
        Assert.True(sut.HasEventsForGroup(7));
        Assert.Equal(1, sut.GetTotalQueueCount());
    }

    [Fact]
    public void Dequeue_CrossGroup_ShouldRestoreOnlyEmptyGroup()
    {
        // Arrange
        var sut = CreateSut();
        // Group1에 ev1, Group2에 ev2
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        var ev2 = sut.Enqueue(CreateEntry(deviceId: 15, groupIds: new List<int> { 2 }));
        var emptyGroupIds = new List<int>();
        sut.OnGroupEmpty += (groupId) => emptyGroupIds.Add(groupId);

        // Act — Group1만 비우기
        sut.Dequeue(ev1);

        // Assert
        Assert.Contains(1, emptyGroupIds);       // Group1 복원
        Assert.DoesNotContain(2, emptyGroupIds);  // Group2 유지
        Assert.False(sut.HasEventsForGroup(1));
        Assert.True(sut.HasEventsForGroup(2));
    }

    [Fact]
    public void DequeueAll_ShouldClearAllAndRaiseGroupEmptyForAll()
    {
        // Arrange
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 1 }));
        sut.Enqueue(CreateEntry(deviceId: 15, groupIds: new List<int> { 2 }));
        var emptyGroupIds = new List<int>();
        sut.OnGroupEmpty += (groupId) => emptyGroupIds.Add(groupId);

        // Act
        sut.DequeueAll();

        // Assert
        Assert.Equal(0, sut.GetTotalQueueCount());
        Assert.Contains(1, emptyGroupIds);
        Assert.Contains(2, emptyGroupIds);
    }

    [Fact]
    public void Dequeue_NonExistentEntryId_ShouldNotThrow()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert — 예외 없이 무시
        var exception = Record.Exception(() => sut.Dequeue("non-existent-uuid"));
        Assert.Null(exception);
    }

    #endregion

    #region - Phase 5: SharedTimer -

    [Fact]
    public void OnSharedTimerTick_ShouldDequeueExpiredEntry()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();
        entry.TimeoutSeconds = 10;
        var entryId = sut.Enqueue(entry);

        // 과거로 EnqueuedAt 조작 (11초 전 → 10초 타임아웃 초과)
        var retrieved = sut.GetEntry(entryId)!;
        retrieved.EnqueuedAt = DateTime.Now.AddSeconds(-11);

        var reportedIds = new List<string>();
        sut.OnAutoReport += (id) => reportedIds.Add(id);

        // Act
        sut.OnSharedTimerTick();

        // Assert
        Assert.Equal(0, sut.GetTotalQueueCount());
        Assert.Contains(entryId, reportedIds);
    }

    [Fact]
    public void OnSharedTimerTick_ShouldKeepNonExpiredEntry()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();
        entry.TimeoutSeconds = 60; // 60초 타임아웃 — 방금 등록했으므로 초과 안 됨
        var entryId = sut.Enqueue(entry);

        // Act
        sut.OnSharedTimerTick();

        // Assert
        Assert.Equal(1, sut.GetTotalQueueCount());
        Assert.NotNull(sut.GetEntry(entryId));
    }

    [Fact]
    public void OnSharedTimerTick_ShouldSkipAutoReportDisabledEntry()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();
        entry.TimeoutSeconds = 10;
        entry.IsAutoReportEnabled = false; // 자동 조치보고 비활성
        var entryId = sut.Enqueue(entry);

        // 과거로 조작 (타임아웃 초과)
        var retrieved = sut.GetEntry(entryId)!;
        retrieved.EnqueuedAt = DateTime.Now.AddSeconds(-11);

        // Act
        sut.OnSharedTimerTick();

        // Assert — 제거되지 않아야 함
        Assert.Equal(1, sut.GetTotalQueueCount());
        Assert.NotNull(sut.GetEntry(entryId));
    }

    #endregion

    #region - Phase 7: PRD Scenario -

    [Fact]
    public void PRD_Scenario_FullLifecycle_ShouldTrackGroupTransitionsCorrectly()
    {
        // PRD §4.5: 센서1(G1), 센서2(G1), 센서1(G1), 센서15(G2) 순차 도착
        // → ev1~ev4 순차 Dequeue → 그룹별 심볼 전이 검증
        var sut = CreateSut();
        var firstEvents = new List<int>();
        var emptyGroups = new List<int>();
        sut.OnGroupFirstEvent += (gid, _) => firstEvents.Add(gid);
        sut.OnGroupEmpty += (gid) => emptyGroups.Add(gid);

        // ── Enqueue Phase ──
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        Assert.Contains(1, firstEvents); // Group1 첫 이벤트 → Detecting

        var ev2 = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 1 }));
        Assert.Equal(1, firstEvents.Count(g => g == 1)); // Group1 중복 발행 안 됨

        var ev3 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));

        var ev4 = sut.Enqueue(CreateEntry(deviceId: 15, groupIds: new List<int> { 2 }));
        Assert.Contains(2, firstEvents); // Group2 첫 이벤트 → Detecting

        Assert.Equal(4, sut.GetTotalQueueCount());

        // ── Dequeue Phase ──
        sut.Dequeue(ev1); // Group1에 ev2, ev3 남음
        Assert.True(sut.HasEventsForGroup(1));
        Assert.Empty(emptyGroups);

        sut.Dequeue(ev2); // Group1에 ev3 남음
        Assert.True(sut.HasEventsForGroup(1));
        Assert.Empty(emptyGroups);

        sut.Dequeue(ev3); // Group1 비어짐 → Normal 복원
        Assert.False(sut.HasEventsForGroup(1));
        Assert.Contains(1, emptyGroups);

        sut.Dequeue(ev4); // Group2 비어짐 → Normal 복원
        Assert.False(sut.HasEventsForGroup(2));
        Assert.Contains(2, emptyGroups);

        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    [Fact]
    public void DataIntegrity_SourceEventShouldBePresentAfterEnqueue()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();
        Assert.NotNull(entry.SourceEvent); // Mock object 설정 확인

        // Act
        var entryId = sut.Enqueue(entry);

        // Assert — GetEntry로 조회한 SourceEvent가 원본과 동일
        var retrieved = sut.GetEntry(entryId)!;
        Assert.NotNull(retrieved.SourceEvent);
        Assert.Same(entry.SourceEvent, retrieved.SourceEvent);
    }

    #endregion

    #region - Phase BatchUI: 배치 Dequeue 검증 (FR-08) -

    [Fact]
    public void OnSharedTimerTick_BatchExpired_ShouldDequeueAllExpired()
    {
        // Arrange — 3건 모두 만료
        var sut = CreateSut();
        var e1 = CreateEntry(deviceId: 1, groupIds: new List<int> { 1 });
        e1.TimeoutSeconds = 5;
        var e2 = CreateEntry(deviceId: 2, groupIds: new List<int> { 1 });
        e2.TimeoutSeconds = 5;
        var e3 = CreateEntry(deviceId: 3, groupIds: new List<int> { 2 });
        e3.TimeoutSeconds = 5;

        var id1 = sut.Enqueue(e1);
        var id2 = sut.Enqueue(e2);
        var id3 = sut.Enqueue(e3);

        // 과거로 조작 (모두 만료)
        sut.GetEntry(id1)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id2)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id3)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);

        var reportedIds = new List<string>();
        sut.OnAutoReport += (id) => reportedIds.Add(id);

        // Act
        sut.OnSharedTimerTick();

        // Assert — 3건 모두 Dequeue + AutoReport
        Assert.Equal(0, sut.GetTotalQueueCount());
        Assert.Equal(3, reportedIds.Count);
        Assert.Contains(id1, reportedIds);
        Assert.Contains(id2, reportedIds);
        Assert.Contains(id3, reportedIds);
    }

    [Fact]
    public void OnSharedTimerTick_BatchExpired_ShouldPreserveGroupEmptyOrder()
    {
        // Arrange — Group1에 2건, Group2에 1건 → 모두 만료
        var sut = CreateSut();
        var e1 = CreateEntry(deviceId: 1, groupIds: new List<int> { 1 });
        e1.TimeoutSeconds = 5;
        var e2 = CreateEntry(deviceId: 2, groupIds: new List<int> { 1 });
        e2.TimeoutSeconds = 5;
        var e3 = CreateEntry(deviceId: 3, groupIds: new List<int> { 2 });
        e3.TimeoutSeconds = 5;

        var id1 = sut.Enqueue(e1);
        var id2 = sut.Enqueue(e2);
        var id3 = sut.Enqueue(e3);

        // 과거로 조작
        sut.GetEntry(id1)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id2)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id3)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);

        var emptyGroupIds = new List<int>();
        sut.OnGroupEmpty += (gid) => emptyGroupIds.Add(gid);

        // Act
        sut.OnSharedTimerTick();

        // Assert — 두 그룹 모두 OnGroupEmpty 발행
        Assert.Contains(1, emptyGroupIds);
        Assert.Contains(2, emptyGroupIds);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    #endregion

    #region - Phase: DeviceIndex Enqueue (PRD_EventQueue_Symbol_Unification) -

    [Fact]
    public void Enqueue_ShouldRegisterDeviceIndex()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry(deviceId: 5);

        // Act
        sut.Enqueue(entry);

        // Assert
        Assert.True(sut.HasEventsForDevice(5, EnumDeviceType.Fence));
        Assert.False(sut.HasEventsForDevice(99, EnumDeviceType.Fence));
    }

    [Fact]
    public void Enqueue_FirstEventForDevice_ShouldRaiseOnDeviceFirstEvent()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry(deviceId: 3);
        var raisedDevices = new List<(int Id, EnumDeviceType Type)>();
        sut.OnDeviceFirstEvent += (id, type, eventType) => raisedDevices.Add((id, type));

        // Act
        sut.Enqueue(entry);

        // Assert
        Assert.Contains((3, EnumDeviceType.Fence), raisedDevices);
    }

    [Fact]
    public void Enqueue_SecondEventForSameDevice_ShouldNotRaiseOnDeviceFirstEvent()
    {
        // Arrange
        var sut = CreateSut();
        var entry1 = CreateEntry(deviceId: 3);
        var entry2 = CreateEntry(deviceId: 3);
        var raisedCount = 0;
        sut.OnDeviceFirstEvent += (id, type, eventType) => raisedCount++;

        // Act
        sut.Enqueue(entry1);
        sut.Enqueue(entry2);

        // Assert
        Assert.Equal(1, raisedCount); // 첫 번째만 발행
    }

    #endregion

    #region - Phase: DeviceIndex Dequeue (PRD_EventQueue_Symbol_Unification) -

    [Fact]
    public void Dequeue_LastEventForDevice_ShouldRaiseOnDeviceEmpty()
    {
        // Arrange
        var sut = CreateSut();
        var entryId = sut.Enqueue(CreateEntry(deviceId: 5));
        var emptyDevices = new List<(int Id, EnumDeviceType Type)>();
        sut.OnDeviceEmpty += (id, type) => emptyDevices.Add((id, type));

        // Act
        sut.Dequeue(entryId);

        // Assert
        Assert.Contains((5, EnumDeviceType.Fence), emptyDevices);
        Assert.False(sut.HasEventsForDevice(5, EnumDeviceType.Fence));
    }

    [Fact]
    public void Dequeue_NotLastEventForDevice_ShouldNotRaiseOnDeviceEmpty()
    {
        // Arrange
        var sut = CreateSut();
        var entryId1 = sut.Enqueue(CreateEntry(deviceId: 5));
        var entryId2 = sut.Enqueue(CreateEntry(deviceId: 5));
        var emptyRaised = false;
        sut.OnDeviceEmpty += (id, type) => emptyRaised = true;

        // Act
        sut.Dequeue(entryId1);

        // Assert
        Assert.False(emptyRaised);
        Assert.True(sut.HasEventsForDevice(5, EnumDeviceType.Fence));
    }

    [Fact]
    public void Dequeue_CrossDevice_ShouldRestoreOnlyEmptyDevice()
    {
        // Arrange
        var sut = CreateSut();
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        var ev2 = sut.Enqueue(CreateEntry(deviceId: 8, groupIds: new List<int> { 1 }));
        var emptyDevices = new List<int>();
        sut.OnDeviceEmpty += (id, type) => emptyDevices.Add(id);

        // Act — Device1만 비우기
        sut.Dequeue(ev1);

        // Assert
        Assert.Contains(1, emptyDevices);
        Assert.DoesNotContain(8, emptyDevices);
        Assert.False(sut.HasEventsForDevice(1, EnumDeviceType.Fence));
        Assert.True(sut.HasEventsForDevice(8, EnumDeviceType.Fence));
    }

    [Fact]
    public void Dequeue_DeviceEmptyButGroupRemains_ShouldRaiseOnDeviceEmptyOnly()
    {
        // Arrange — 같은 그룹에 Device1, Device8 각 1건
        var sut = CreateSut();
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        var ev2 = sut.Enqueue(CreateEntry(deviceId: 8, groupIds: new List<int> { 1 }));
        var deviceEmptyRaised = false;
        var groupEmptyRaised = false;
        sut.OnDeviceEmpty += (id, type) => { if (id == 1) deviceEmptyRaised = true; };
        sut.OnGroupEmpty += (gid) => groupEmptyRaised = true;

        // Act — Device1 Dequeue → Group1에 ev2 남아있음
        sut.Dequeue(ev1);

        // Assert
        Assert.True(deviceEmptyRaised);   // Device1 비어짐
        Assert.False(groupEmptyRaised);   // Group1에 ev2 남아있으므로 미발행
        Assert.True(sut.HasEventsForGroup(1));
    }

    [Fact]
    public void DequeueAll_ShouldRaiseOnDeviceEmptyForAllDevices()
    {
        // Arrange
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        sut.Enqueue(CreateEntry(deviceId: 8, groupIds: new List<int> { 1 }));
        var emptyDevices = new List<(int Id, EnumDeviceType Type)>();
        sut.OnDeviceEmpty += (id, type) => emptyDevices.Add((id, type));

        // Act
        sut.DequeueAll();

        // Assert
        Assert.Contains((1, EnumDeviceType.Fence), emptyDevices);
        Assert.Contains((8, EnumDeviceType.Fence), emptyDevices);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    #endregion

    #region - Phase: E2E Scenario (PRD_EventQueue_Symbol_Unification) -

    /// <summary>
    /// PRD §4.8 동작 예시 시나리오:
    /// 센서1 탐지 2회 + 센서8 탐지 1회 → ev1 Dequeue → ev2 Dequeue (Device1 Empty, GroupA 유지) → ev3 Dequeue (GroupA Empty)
    /// </summary>
    [Fact]
    public void PRD_Scenario_DeviceAndGroupTransitions()
    {
        var sut = CreateSut();

        var deviceFirstEvents = new List<(int Id, EnumDeviceType Type, EnumEventType Event)>();
        var deviceEmptyEvents = new List<(int Id, EnumDeviceType Type)>();
        var groupFirstEvents = new List<(int GroupId, EnumEventType Event)>();
        var groupEmptyEvents = new List<int>();

        sut.OnDeviceFirstEvent += (id, type, evt) => deviceFirstEvents.Add((id, type, evt));
        sut.OnDeviceEmpty += (id, type) => deviceEmptyEvents.Add((id, type));
        sut.OnGroupFirstEvent += (gid, evt) => groupFirstEvents.Add((gid, evt));
        sut.OnGroupEmpty += (gid) => groupEmptyEvents.Add(gid);

        // 센서1 탐지 1회 → Device1 FirstEvent + GroupA FirstEvent
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        Assert.Single(deviceFirstEvents);
        Assert.Equal(1, deviceFirstEvents[0].Id);
        Assert.Single(groupFirstEvents);
        Assert.Equal(1, groupFirstEvents[0].GroupId);

        // 센서1 탐지 2회 → Device1 중복 (FirstEvent 미발행)
        var ev2 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        Assert.Single(deviceFirstEvents); // 여전히 1회
        Assert.Single(groupFirstEvents);  // 여전히 1회

        // 센서8 탐지 1회 → Device8 FirstEvent (GroupA는 이미 있으므로 미발행)
        var ev3 = sut.Enqueue(CreateEntry(deviceId: 8, groupIds: new List<int> { 1 }));
        Assert.Equal(2, deviceFirstEvents.Count);
        Assert.Equal(8, deviceFirstEvents[1].Id);
        Assert.Single(groupFirstEvents); // GroupA 이미 존재

        // ev1 Dequeue → Device1에 아직 ev2 남음 → DeviceEmpty 미발행
        sut.Dequeue(ev1);
        Assert.Empty(deviceEmptyEvents);
        Assert.Empty(groupEmptyEvents);

        // ev2 Dequeue → Device1 Empty, GroupA에 ev3 남음 → GroupEmpty 미발행
        sut.Dequeue(ev2);
        Assert.Single(deviceEmptyEvents);
        Assert.Equal(1, deviceEmptyEvents[0].Id);
        Assert.Empty(groupEmptyEvents);
        Assert.True(sut.HasEventsForGroup(1)); // GroupA 유지

        // ev3 Dequeue → Device8 Empty + GroupA Empty
        sut.Dequeue(ev3);
        Assert.Equal(2, deviceEmptyEvents.Count);
        Assert.Equal(8, deviceEmptyEvents[1].Id);
        Assert.Single(groupEmptyEvents);
        Assert.Equal(1, groupEmptyEvents[0]);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    /// <summary>
    /// 그룹 미소속 심볼: GroupIds = null → OnDeviceFirstEvent/OnDeviceEmpty만 발행
    /// </summary>
    [Fact]
    public void GrouplessDevice_ShouldWorkWithDeviceIndexOnly()
    {
        var sut = CreateSut();

        var deviceFirstRaised = false;
        var groupFirstRaised = false;
        var deviceEmptyRaised = false;
        var groupEmptyRaised = false;

        sut.OnDeviceFirstEvent += (id, type, evt) => deviceFirstRaised = true;
        sut.OnGroupFirstEvent += (gid, evt) => groupFirstRaised = true;
        sut.OnDeviceEmpty += (id, type) => deviceEmptyRaised = true;
        sut.OnGroupEmpty += (gid) => groupEmptyRaised = true;

        // GroupIds = null
        var entryId = sut.Enqueue(new EventEntry
        {
            DeviceId = 99,
            DeviceType = EnumDeviceType.Fence,
            GroupIds = null,
            EventType = EnumEventType.Intrusion,
            TimeoutSeconds = 30,
            SourceEvent = new Mock<IExEventModel>().Object,
            IsAutoReportEnabled = false
        });

        Assert.True(deviceFirstRaised);
        Assert.False(groupFirstRaised);

        sut.Dequeue(entryId);

        Assert.True(deviceEmptyRaised);
        Assert.False(groupEmptyRaised);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    #endregion

    #region - Phase: External EntryId (PRD_EntryId_Nats_Uuid_DirectMatch) -

    [Fact]
    public void Enqueue_WithExternalEntryId_ShouldUseIt()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();

        // Act
        var entryId = sut.Enqueue(entry, "nats-uuid-123");

        // Assert — 자체 UUID 대신 외부 entryId 사용
        Assert.Equal("nats-uuid-123", entryId);
        Assert.Equal("nats-uuid-123", entry.EntryId);
        Assert.NotNull(sut.GetEntry("nats-uuid-123"));
    }

    [Fact]
    public void Enqueue_WithNullExternalId_ShouldGenerateUuid()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();

        // Act
        var entryId = sut.Enqueue(entry, null);

        // Assert — Guid 형식 UUID 자동 생성
        Assert.False(string.IsNullOrWhiteSpace(entryId));
        Assert.True(Guid.TryParse(entryId, out _));
        Assert.NotNull(sut.GetEntry(entryId));
    }

    [Fact]
    public void Enqueue_WithEmptyExternalId_ShouldGenerateUuid()
    {
        // Arrange
        var sut = CreateSut();
        var entry = CreateEntry();

        // Act
        var entryId = sut.Enqueue(entry, "");

        // Assert — 빈 문자열도 폴백
        Assert.False(string.IsNullOrWhiteSpace(entryId));
        Assert.True(Guid.TryParse(entryId, out _));
    }

    #endregion

    #region - Phase: FindEntryByDevice (PRD_EventCard_EntryId_Connection) -

    [Fact]
    public void FindEntryByDevice_ShouldReturnOldestEntry()
    {
        // Arrange — 같은 device에 3건 등록
        var sut = CreateSut();
        var e1 = CreateEntry(deviceId: 5);
        var e2 = CreateEntry(deviceId: 5);
        var e3 = CreateEntry(deviceId: 5);

        var id1 = sut.Enqueue(e1);
        var id2 = sut.Enqueue(e2);
        var id3 = sut.Enqueue(e3);

        // Act
        var result = sut.FindEntryByDevice(5, EnumDeviceType.Fence);

        // Assert — 가장 먼저 등록된 entry 반환
        Assert.NotNull(result);
        Assert.Equal(id1, result.EntryId);
    }

    [Fact]
    public void FindEntryByDevice_EmptyDevice_ShouldReturnNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.FindEntryByDevice(99, EnumDeviceType.Fence);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindEntryByDevice_ShouldNotRemoveEntry()
    {
        // Arrange
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 5));
        var countBefore = sut.GetTotalQueueCount();

        // Act
        var result = sut.FindEntryByDevice(5, EnumDeviceType.Fence);

        // Assert — 조회 후 큐 상태 변경 없음
        Assert.NotNull(result);
        Assert.Equal(countBefore, sut.GetTotalQueueCount());
        Assert.True(sut.HasEventsForDevice(5, EnumDeviceType.Fence));
    }

    #endregion

    #region - Phase: Timer 청크 분할 (PRD_SharedTimer_Chunk_Dequeue) -

    [Fact]
    public void OnSharedTimerTick_50Expired_ShouldDequeueOnlyChunkSize()
    {
        // Arrange — 50건 모두 즉시 만료
        var sut = CreateSut();
        sut.MaxDequeuePerTick = 5;

        for (int i = 0; i < 50; i++)
        {
            var entry = CreateEntry(deviceId: 100 + i, groupIds: new List<int> { 1 });
            entry.TimeoutSeconds = 0;
            var id = sut.Enqueue(entry);
            sut.GetEntry(id)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        }

        var reportedIds = new List<string>();
        sut.OnAutoReport += (id) => reportedIds.Add(id);

        // Act — 1 tick
        sut.OnSharedTimerTick();

        // Assert — 5건만 Dequeue, 45건 잔여
        Assert.Equal(5, reportedIds.Count);
        Assert.Equal(45, sut.GetTotalQueueCount());
    }

    [Fact]
    public void OnSharedTimerTick_MultipleTicks_ShouldDrainAll()
    {
        // Arrange — 12건 만료
        var sut = CreateSut();
        sut.MaxDequeuePerTick = 5;

        for (int i = 0; i < 12; i++)
        {
            var entry = CreateEntry(deviceId: 200 + i, groupIds: new List<int> { 1 });
            entry.TimeoutSeconds = 0;
            var id = sut.Enqueue(entry);
            sut.GetEntry(id)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        }

        var reportedIds = new List<string>();
        sut.OnAutoReport += (id) => reportedIds.Add(id);

        // Act — Tick 1회(5건) + Tick 2회(5건) + Tick 3회(2건)
        sut.OnSharedTimerTick();
        Assert.Equal(5, reportedIds.Count);
        Assert.Equal(7, sut.GetTotalQueueCount());

        sut.OnSharedTimerTick();
        Assert.Equal(10, reportedIds.Count);
        Assert.Equal(2, sut.GetTotalQueueCount());

        sut.OnSharedTimerTick();
        Assert.Equal(12, reportedIds.Count);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    [Fact]
    public void OnSharedTimerTick_LessThanChunk_ShouldDequeueAll()
    {
        // Arrange — 3건 만료 < ChunkSize 5
        var sut = CreateSut();
        sut.MaxDequeuePerTick = 5;

        for (int i = 0; i < 3; i++)
        {
            var entry = CreateEntry(deviceId: 300 + i, groupIds: new List<int> { 1 });
            entry.TimeoutSeconds = 0;
            var id = sut.Enqueue(entry);
            sut.GetEntry(id)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        }

        var reportedIds = new List<string>();
        sut.OnAutoReport += (id) => reportedIds.Add(id);

        // Act
        sut.OnSharedTimerTick();

        // Assert — 3건 전부 처리
        Assert.Equal(3, reportedIds.Count);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    [Fact]
    public void OnSharedTimerTick_OrderByEnqueuedAt()
    {
        // Arrange — 3건, 서로 다른 EnqueuedAt, MaxDequeuePerTick=1
        var sut = CreateSut();
        sut.MaxDequeuePerTick = 1;

        var baseTime = DateTime.Now.AddSeconds(-30);

        var eA = CreateEntry(deviceId: 401, groupIds: new List<int> { 1 });
        eA.TimeoutSeconds = 0;
        var idA = sut.Enqueue(eA);
        sut.GetEntry(idA)!.EnqueuedAt = baseTime; // 가장 오래된

        var eB = CreateEntry(deviceId: 402, groupIds: new List<int> { 1 });
        eB.TimeoutSeconds = 0;
        var idB = sut.Enqueue(eB);
        sut.GetEntry(idB)!.EnqueuedAt = baseTime.AddSeconds(1);

        var eC = CreateEntry(deviceId: 403, groupIds: new List<int> { 1 });
        eC.TimeoutSeconds = 0;
        var idC = sut.Enqueue(eC);
        sut.GetEntry(idC)!.EnqueuedAt = baseTime.AddSeconds(2);

        var reportedIds = new List<string>();
        sut.OnAutoReport += (id) => reportedIds.Add(id);

        // Act — 1 tick, MaxDequeuePerTick=1
        sut.OnSharedTimerTick();

        // Assert — 가장 오래된 A만 Dequeue
        Assert.Single(reportedIds);
        Assert.Equal(idA, reportedIds[0]);
        Assert.Equal(2, sut.GetTotalQueueCount());
    }

    [Fact]
    public void MaxDequeuePerTick_Configurable()
    {
        // Arrange — 50건 만료, MaxDequeuePerTick=10
        var sut = CreateSut();
        sut.MaxDequeuePerTick = 10;

        for (int i = 0; i < 50; i++)
        {
            var entry = CreateEntry(deviceId: 500 + i, groupIds: new List<int> { 1 });
            entry.TimeoutSeconds = 0;
            var id = sut.Enqueue(entry);
            sut.GetEntry(id)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        }

        var reportedIds = new List<string>();
        sut.OnAutoReport += (id) => reportedIds.Add(id);

        // Act
        sut.OnSharedTimerTick();

        // Assert — 10건 Dequeue
        Assert.Equal(10, reportedIds.Count);
        Assert.Equal(40, sut.GetTotalQueueCount());
    }

    #endregion
}
