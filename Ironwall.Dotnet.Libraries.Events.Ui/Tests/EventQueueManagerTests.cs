using Xunit;
using Moq;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : EventQueueManager ?⑥쐞 ?뚯뒪??
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
        Assert.Equal(1, raisedCount); // 泥?踰덉㎏留?諛쒗뻾
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
        // Group1??ev1, Group2??ev2
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        var ev2 = sut.Enqueue(CreateEntry(deviceId: 15, groupIds: new List<int> { 2 }));
        var emptyGroupIds = new List<int>();
        sut.OnGroupEmpty += (groupId) => emptyGroupIds.Add(groupId);

        // Act ??Group1留?鍮꾩슦湲?
        sut.Dequeue(ev1);

        // Assert
        Assert.Contains(1, emptyGroupIds);       // Group1 蹂듭썝
        Assert.DoesNotContain(2, emptyGroupIds);  // Group2 ?좎?
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

        // Act & Assert ???덉쇅 ?놁씠 臾댁떆
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

        // 怨쇨굅濡?EnqueuedAt 議곗옉 (11珥?????10珥???꾩븘??珥덇낵)
        var retrieved = sut.GetEntry(entryId)!;
        retrieved.EnqueuedAt = DateTime.Now.AddSeconds(-11);

        var reportedIds = new List<string>();
        sut.OnAutoReport += (entry) => { reportedIds.Add(entry.EntryId!); sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

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
        entry.TimeoutSeconds = 60; // 60珥???꾩븘????諛⑷툑 ?깅줉?덉쑝誘濡?珥덇낵 ????
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
        entry.IsAutoReportEnabled = false; // ?먮룞 議곗튂蹂닿퀬 鍮꾪솢??
        var entryId = sut.Enqueue(entry);

        // 怨쇨굅濡?議곗옉 (??꾩븘??珥덇낵)
        var retrieved = sut.GetEntry(entryId)!;
        retrieved.EnqueuedAt = DateTime.Now.AddSeconds(-11);

        // Act
        sut.OnSharedTimerTick();

        // Assert ???쒓굅?섏? ?딆븘????
        Assert.Equal(1, sut.GetTotalQueueCount());
        Assert.NotNull(sut.GetEntry(entryId));
    }

    #endregion

    #region - Phase 7: PRD Scenario -

    [Fact]
    public void PRD_Scenario_FullLifecycle_ShouldTrackGroupTransitionsCorrectly()
    {
        // PRD 짠4.5: ?쇱꽌1(G1), ?쇱꽌2(G1), ?쇱꽌1(G1), ?쇱꽌15(G2) ?쒖감 ?꾩갑
        // ??ev1~ev4 ?쒖감 Dequeue ??洹몃９蹂??щ낵 ?꾩씠 寃利?
        var sut = CreateSut();
        var firstEvents = new List<int>();
        var emptyGroups = new List<int>();
        sut.OnGroupFirstEvent += (gid, _) => firstEvents.Add(gid);
        sut.OnGroupEmpty += (gid) => emptyGroups.Add(gid);

        // ?? Enqueue Phase ??
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        Assert.Contains(1, firstEvents); // Group1 泥??대깽????Detecting

        var ev2 = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 1 }));
        Assert.Equal(1, firstEvents.Count(g => g == 1)); // Group1 以묐났 諛쒗뻾 ????

        var ev3 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));

        var ev4 = sut.Enqueue(CreateEntry(deviceId: 15, groupIds: new List<int> { 2 }));
        Assert.Contains(2, firstEvents); // Group2 泥??대깽????Detecting

        Assert.Equal(4, sut.GetTotalQueueCount());

        // ?? Dequeue Phase ??
        sut.Dequeue(ev1); // Group1??ev2, ev3 ?⑥쓬
        Assert.True(sut.HasEventsForGroup(1));
        Assert.Empty(emptyGroups);

        sut.Dequeue(ev2); // Group1??ev3 ?⑥쓬
        Assert.True(sut.HasEventsForGroup(1));
        Assert.Empty(emptyGroups);

        sut.Dequeue(ev3); // Group1 鍮꾩뼱吏???Normal 蹂듭썝
        Assert.False(sut.HasEventsForGroup(1));
        Assert.Contains(1, emptyGroups);

        sut.Dequeue(ev4); // Group2 鍮꾩뼱吏???Normal 蹂듭썝
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
        Assert.NotNull(entry.SourceEvent); // Mock object ?ㅼ젙 ?뺤씤

        // Act
        var entryId = sut.Enqueue(entry);

        // Assert ??GetEntry濡?議고쉶??SourceEvent媛 ?먮낯怨??숈씪
        var retrieved = sut.GetEntry(entryId)!;
        Assert.NotNull(retrieved.SourceEvent);
        Assert.Same(entry.SourceEvent, retrieved.SourceEvent);
    }

    #endregion

    #region - Phase BatchUI: 諛곗튂 Dequeue 寃利?(FR-08) -

    [Fact]
    public void OnSharedTimerTick_BatchExpired_ShouldDequeueAllExpired()
    {
        // Arrange ??3嫄?紐⑤몢 留뚮즺
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

        // 怨쇨굅濡?議곗옉 (紐⑤몢 留뚮즺)
        sut.GetEntry(id1)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id2)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id3)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);

        var reportedIds = new List<string>();
        sut.OnAutoReport += (entry) => { reportedIds.Add(entry.EntryId!); sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

        // Act
        sut.OnSharedTimerTick();

        // Assert ??3嫄?紐⑤몢 Dequeue + AutoReport
        Assert.Equal(0, sut.GetTotalQueueCount());
        Assert.Equal(3, reportedIds.Count);
        Assert.Contains(id1, reportedIds);
        Assert.Contains(id2, reportedIds);
        Assert.Contains(id3, reportedIds);
    }

    [Fact]
    public void OnSharedTimerTick_BatchExpired_ShouldPreserveGroupEmptyOrder()
    {
        // Arrange ??Group1??2嫄? Group2??1嫄???紐⑤몢 留뚮즺
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

        // 怨쇨굅濡?議곗옉
        sut.GetEntry(id1)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id2)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);
        sut.GetEntry(id3)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);

        var emptyGroupIds = new List<int>();
        sut.OnGroupEmpty += (gid) => emptyGroupIds.Add(gid);
        // OnAutoReport ?몃뱾?ш? Dequeue 梨낆엫 (HandleAutoReport ??븷 ?쒕??덉씠??
        sut.OnAutoReport += (entry) => { sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

        // Act
        sut.OnSharedTimerTick();

        // Assert ????洹몃９ 紐⑤몢 OnGroupEmpty 諛쒗뻾
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
        Assert.Equal(1, raisedCount); // 泥?踰덉㎏留?諛쒗뻾
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

        // Act ??Device1留?鍮꾩슦湲?
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
        // Arrange ??媛숈? 洹몃９??Device1, Device8 媛?1嫄?
        var sut = CreateSut();
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        var ev2 = sut.Enqueue(CreateEntry(deviceId: 8, groupIds: new List<int> { 1 }));
        var deviceEmptyRaised = false;
        var groupEmptyRaised = false;
        sut.OnDeviceEmpty += (id, type) => { if (id == 1) deviceEmptyRaised = true; };
        sut.OnGroupEmpty += (gid) => groupEmptyRaised = true;

        // Act ??Device1 Dequeue ??Group1??ev2 ?⑥븘?덉쓬
        sut.Dequeue(ev1);

        // Assert
        Assert.True(deviceEmptyRaised);   // Device1 鍮꾩뼱吏?
        Assert.False(groupEmptyRaised);   // Group1??ev2 ?⑥븘?덉쑝誘濡?誘몃컻??
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
    /// PRD 짠4.8 ?숈옉 ?덉떆 ?쒕굹由ъ삤:
    /// ?쇱꽌1 ?먯? 2??+ ?쇱꽌8 ?먯? 1????ev1 Dequeue ??ev2 Dequeue (Device1 Empty, GroupA ?좎?) ??ev3 Dequeue (GroupA Empty)
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

        // ?쇱꽌1 ?먯? 1????Device1 FirstEvent + GroupA FirstEvent
        var ev1 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        Assert.Single(deviceFirstEvents);
        Assert.Equal(1, deviceFirstEvents[0].Id);
        Assert.Single(groupFirstEvents);
        Assert.Equal(1, groupFirstEvents[0].GroupId);

        // ?쇱꽌1 ?먯? 2????Device1 以묐났 (FirstEvent 誘몃컻??
        var ev2 = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 1 }));
        Assert.Single(deviceFirstEvents); // ?ъ쟾??1??
        Assert.Single(groupFirstEvents);  // ?ъ쟾??1??

        // ?쇱꽌8 ?먯? 1????Device8 FirstEvent (GroupA???대? ?덉쑝誘濡?誘몃컻??
        var ev3 = sut.Enqueue(CreateEntry(deviceId: 8, groupIds: new List<int> { 1 }));
        Assert.Equal(2, deviceFirstEvents.Count);
        Assert.Equal(8, deviceFirstEvents[1].Id);
        Assert.Single(groupFirstEvents); // GroupA ?대? 議댁옱

        // ev1 Dequeue ??Device1???꾩쭅 ev2 ?⑥쓬 ??DeviceEmpty 誘몃컻??
        sut.Dequeue(ev1);
        Assert.Empty(deviceEmptyEvents);
        Assert.Empty(groupEmptyEvents);

        // ev2 Dequeue ??Device1 Empty, GroupA??ev3 ?⑥쓬 ??GroupEmpty 誘몃컻??
        sut.Dequeue(ev2);
        Assert.Single(deviceEmptyEvents);
        Assert.Equal(1, deviceEmptyEvents[0].Id);
        Assert.Empty(groupEmptyEvents);
        Assert.True(sut.HasEventsForGroup(1)); // GroupA ?좎?

        // ev3 Dequeue ??Device8 Empty + GroupA Empty
        sut.Dequeue(ev3);
        Assert.Equal(2, deviceEmptyEvents.Count);
        Assert.Equal(8, deviceEmptyEvents[1].Id);
        Assert.Single(groupEmptyEvents);
        Assert.Equal(1, groupEmptyEvents[0]);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    /// <summary>
    /// 洹몃９ 誘몄냼???щ낵: GroupIds = null ??OnDeviceFirstEvent/OnDeviceEmpty留?諛쒗뻾
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

        // Assert ???먯껜 UUID ????몃? entryId ?ъ슜
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

        // Assert ??Guid ?뺤떇 UUID ?먮룞 ?앹꽦
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

        // Assert ??鍮?臾몄옄?대룄 ?대갚
        Assert.False(string.IsNullOrWhiteSpace(entryId));
        Assert.True(Guid.TryParse(entryId, out _));
    }

    #endregion

    #region - Device_CompositeState_SSOT: BUG-01, BUG-02, REQ-01 -

    // BUG-01: ?숈씪 ?쇱꽌 Detection ??Fault ?꾪솚 ??OnDeviceStateChanged 諛쒗솕 寃利?
    [Fact]
    public void should_fire_DeviceStateChanged_FaultedDetecting_when_fault_added_to_detecting_device()
    {
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        var deviceTransitions = new List<(int, EnumDeviceType, EnumCompositeEventStatus, EnumCompositeEventStatus)>();
        sut.OnDeviceStateChanged += (id, type, prev, next) => deviceTransitions.Add((id, type, prev, next));

        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        Assert.Single(deviceTransitions);
        Assert.Equal((1, EnumDeviceType.Fence, EnumCompositeEventStatus.Detecting, EnumCompositeEventStatus.FaultedDetecting), deviceTransitions[0]);
    }

    // BUG-01: 洹몃９???숈씪?섍쾶 FaultedDetecting?쇰줈 ?꾩씠?섏뼱????(湲곗〈 洹몃９ 濡쒖쭅 ?좎? ?뺤씤)
    [Fact]
    public void should_fire_GroupStateChanged_FaultedDetecting_when_same_device_fault_after_detection()
    {
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        var groupTransitions = new List<(int, EnumCompositeEventStatus, EnumCompositeEventStatus)>();
        sut.OnGroupStateChanged += (gid, prev, next) => groupTransitions.Add((gid, prev, next));

        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        Assert.Single(groupTransitions);
        Assert.Equal((10, EnumCompositeEventStatus.Detecting, EnumCompositeEventStatus.FaultedDetecting), groupTransitions[0]);
    }

    // BUG-02 Scenario A: FaultedDetecting ??Fault 議곗튂蹂닿퀬 ??Detecting ?꾩씠
    // Detection??癒쇱? ?꾩갑?댁빞 auto-recovery 誘몃컻??(Fault ?꾩갑 ?쒖뿉??auto-recovery ?놁쓬)
    [Fact]
    public void should_fire_DeviceStateChanged_Detecting_when_fault_dequeued_from_FaultedDetecting_device()
    {
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));
        var faultId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        var deviceTransitions = new List<(int, EnumDeviceType, EnumCompositeEventStatus, EnumCompositeEventStatus)>();
        sut.OnDeviceStateChanged += (id, type, prev, next) => deviceTransitions.Add((id, type, prev, next));

        sut.Dequeue(faultId);

        Assert.Single(deviceTransitions);
        Assert.Equal((1, EnumDeviceType.Fence, EnumCompositeEventStatus.FaultedDetecting, EnumCompositeEventStatus.Detecting), deviceTransitions[0]);
    }

    // BUG-02 Scenario B: FaultedDetecting ??Detection 議곗튂蹂닿퀬 ??Faulted ?꾩씠
    // Detection??癒쇱? ?꾩갑?댁빞 auto-recovery 誘몃컻??
    [Fact]
    public void should_fire_DeviceStateChanged_Faulted_when_detection_dequeued_from_FaultedDetecting_device()
    {
        var sut = CreateSut();
        var detectId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        var deviceTransitions = new List<(int, EnumDeviceType, EnumCompositeEventStatus, EnumCompositeEventStatus)>();
        sut.OnDeviceStateChanged += (id, type, prev, next) => deviceTransitions.Add((id, type, prev, next));

        sut.Dequeue(detectId);

        Assert.Single(deviceTransitions);
        Assert.Equal((1, EnumDeviceType.Fence, EnumCompositeEventStatus.FaultedDetecting, EnumCompositeEventStatus.Faulted), deviceTransitions[0]);
    }

    // REQ-01: Fault ?쒖꽦 以?Detection ?꾩갑 ??OnAutoRecovery 諛쒗솕 + Fault ?먯뿉???쒓굅
    [Fact]
    public void should_fire_OnAutoRecovery_and_remove_fault_when_detection_arrives_for_faulted_device()
    {
        var sut = CreateSut();
        var faultId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        var recoveredIds = new List<string>();
        sut.OnAutoRecovery += id => recoveredIds.Add(id);

        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        Assert.Single(recoveredIds);
        Assert.Equal(faultId, recoveredIds[0]);
        Assert.Null(sut.GetEntry(faultId));    // Fault ?뷀듃由??쒓굅??
        Assert.Equal(1, sut.GetTotalQueueCount()); // Detection留??⑥쓬
    }

    // REQ-01: ?먮룞蹂듦뎄 ???붾컮?댁뒪 ?곹깭媛 Faulted?묭etecting?쇰줈 ?꾩씠
    [Fact]
    public void should_fire_DeviceStateChanged_Faulted_to_Detecting_on_auto_recovery()
    {
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        var deviceTransitions = new List<(int, EnumDeviceType, EnumCompositeEventStatus, EnumCompositeEventStatus)>();
        sut.OnDeviceStateChanged += (id, type, prev, next) => deviceTransitions.Add((id, type, prev, next));

        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        Assert.Single(deviceTransitions);
        Assert.Equal((1, EnumDeviceType.Fence, EnumCompositeEventStatus.Faulted, EnumCompositeEventStatus.Detecting), deviceTransitions[0]);
    }

    // REQ-01: ?먮룞蹂듦뎄 ??洹몃９ ?곹깭??Faulted?묭etecting ?꾩씠
    [Fact]
    public void should_fire_GroupStateChanged_Faulted_to_Detecting_on_auto_recovery()
    {
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        var groupTransitions = new List<(int, EnumCompositeEventStatus, EnumCompositeEventStatus)>();
        sut.OnGroupStateChanged += (gid, prev, next) => groupTransitions.Add((gid, prev, next));

        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        Assert.Single(groupTransitions);
        Assert.Equal((10, EnumCompositeEventStatus.Faulted, EnumCompositeEventStatus.Detecting), groupTransitions[0]);
    }

    // V-05: ?ㅻⅨ ?쇱꽌??Fault???먮룞蹂듦뎄 誘몄쟻??
    [Fact]
    public void should_not_auto_recover_fault_of_different_device()
    {
        var sut = CreateSut();
        var faultId = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        var recoveredIds = new List<string>();
        sut.OnAutoRecovery += id => recoveredIds.Add(id);

        // Device 1 (?ㅻⅨ ?쇱꽌) Detection ?꾩갑
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        Assert.Empty(recoveredIds);                          // ?먮룞蹂듦뎄 誘몃컻??
        Assert.NotNull(sut.GetEntry(faultId));               // Device 2 Fault ?좎?
        Assert.Equal(2, sut.GetTotalQueueCount());           // Fault + Detection 紐⑤몢 ?좎?
    }

    #endregion

    #region - Phase: FindEntryByDevice (PRD_EventCard_EntryId_Connection) -

    [Fact]
    public void FindEntryByDevice_ShouldReturnOldestEntry()
    {
        // Arrange ??媛숈? device??3嫄??깅줉
        var sut = CreateSut();
        var e1 = CreateEntry(deviceId: 5);
        var e2 = CreateEntry(deviceId: 5);
        var e3 = CreateEntry(deviceId: 5);

        var id1 = sut.Enqueue(e1);
        var id2 = sut.Enqueue(e2);
        var id3 = sut.Enqueue(e3);

        // Act
        var result = sut.FindEntryByDevice(5, EnumDeviceType.Fence);

        // Assert ??媛??癒쇱? ?깅줉??entry 諛섑솚
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

        // Assert ??議고쉶 ?????곹깭 蹂寃??놁쓬
        Assert.NotNull(result);
        Assert.Equal(countBefore, sut.GetTotalQueueCount());
        Assert.True(sut.HasEventsForDevice(5, EnumDeviceType.Fence));
    }

    #endregion

    #region - Phase: Timer 泥?겕 遺꾪븷 (PRD_SharedTimer_Chunk_Dequeue) -

    [Fact]
    public void OnSharedTimerTick_50Expired_ShouldDequeueOnlyChunkSize()
    {
        // Arrange ??50嫄?紐⑤몢 利됱떆 留뚮즺
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
        sut.OnAutoReport += (entry) => { reportedIds.Add(entry.EntryId!); sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

        // Act ??1 tick
        sut.OnSharedTimerTick();

        // Assert ??5嫄대쭔 Dequeue, 45嫄??붿뿬
        Assert.Equal(5, reportedIds.Count);
        Assert.Equal(45, sut.GetTotalQueueCount());
    }

    [Fact]
    public void OnSharedTimerTick_MultipleTicks_ShouldDrainAll()
    {
        // Arrange ??12嫄?留뚮즺
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
        sut.OnAutoReport += (entry) => { reportedIds.Add(entry.EntryId!); sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

        // Act ??Tick 1??5嫄? + Tick 2??5嫄? + Tick 3??2嫄?
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
        // Arrange ??3嫄?留뚮즺 < ChunkSize 5
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
        sut.OnAutoReport += (entry) => { reportedIds.Add(entry.EntryId!); sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

        // Act
        sut.OnSharedTimerTick();

        // Assert ??3嫄??꾨? 泥섎━
        Assert.Equal(3, reportedIds.Count);
        Assert.Equal(0, sut.GetTotalQueueCount());
    }

    [Fact]
    public void OnSharedTimerTick_OrderByEnqueuedAt()
    {
        // Arrange ??3嫄? ?쒕줈 ?ㅻⅨ EnqueuedAt, MaxDequeuePerTick=1
        var sut = CreateSut();
        sut.MaxDequeuePerTick = 1;

        var baseTime = DateTime.Now.AddSeconds(-30);

        var eA = CreateEntry(deviceId: 401, groupIds: new List<int> { 1 });
        eA.TimeoutSeconds = 0;
        var idA = sut.Enqueue(eA);
        sut.GetEntry(idA)!.EnqueuedAt = baseTime; // 媛???ㅻ옒??

        var eB = CreateEntry(deviceId: 402, groupIds: new List<int> { 1 });
        eB.TimeoutSeconds = 0;
        var idB = sut.Enqueue(eB);
        sut.GetEntry(idB)!.EnqueuedAt = baseTime.AddSeconds(1);

        var eC = CreateEntry(deviceId: 403, groupIds: new List<int> { 1 });
        eC.TimeoutSeconds = 0;
        var idC = sut.Enqueue(eC);
        sut.GetEntry(idC)!.EnqueuedAt = baseTime.AddSeconds(2);

        var reportedIds = new List<string>();
        sut.OnAutoReport += (entry) => { reportedIds.Add(entry.EntryId!); sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

        // Act ??1 tick, MaxDequeuePerTick=1
        sut.OnSharedTimerTick();

        // Assert ??媛???ㅻ옒??A留?Dequeue
        Assert.Single(reportedIds);
        Assert.Equal(idA, reportedIds[0]);
        Assert.Equal(2, sut.GetTotalQueueCount());
    }

    [Fact]
    public void MaxDequeuePerTick_Configurable()
    {
        // Arrange ??50嫄?留뚮즺, MaxDequeuePerTick=10
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
        sut.OnAutoReport += (entry) => { reportedIds.Add(entry.EntryId!); sut.Dequeue(entry.EntryId!); entry.AutoReportInFlight = false; };

        // Act
        sut.OnSharedTimerTick();

        // Assert — 10건 Dequeue
        Assert.Equal(10, reportedIds.Count);
        Assert.Equal(40, sut.GetTotalQueueCount());
    }

    [Fact]
    public void OnSharedTimerTick_AutoReportInFlight_ShouldNotRefireEntry()
    {
        // Arrange — AutoReportInFlight=true 상태에서 두 번째 tick 발화 금지
        var sut = CreateSut();
        var entry = CreateEntry();
        entry.TimeoutSeconds = 0;
        var entryId = sut.Enqueue(entry);
        sut.GetEntry(entryId)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);

        var reportedCount = 0;
        sut.OnAutoReport += (e) =>
        {
            reportedCount++;
            // AutoReportInFlight 리셋하지 않음 → 두 번째 tick에서 skip 되어야 함
        };

        sut.OnSharedTimerTick();
        sut.OnSharedTimerTick();

        Assert.Equal(1, reportedCount);
    }

    [Fact]
    public void OnSharedTimerTick_NextRetryAfter_ShouldSkipEntryUntilBackoffExpires()
    {
        // Arrange — API 실패 시나리오: NextRetryAfter 설정 후 두 번째 tick skip
        var sut = CreateSut();
        var entry = CreateEntry();
        entry.TimeoutSeconds = 0;
        var entryId = sut.Enqueue(entry);
        sut.GetEntry(entryId)!.EnqueuedAt = DateTime.Now.AddSeconds(-10);

        var reportedCount = 0;
        sut.OnAutoReport += (e) =>
        {
            reportedCount++;
            e.AutoReportInFlight = false;
            e.NextRetryAfter = DateTime.Now.AddSeconds(30); // 30초 backoff
        };

        sut.OnSharedTimerTick(); // 첫 tick: 발화 + NextRetryAfter 설정
        sut.OnSharedTimerTick(); // 두 번째 tick: NextRetryAfter < now → skip

        Assert.Equal(1, reportedCount); // 두 번째 tick에서 발화되지 않아야 함
    }

    #endregion

    #region - Malfunction CompositeState Tests (NFR-01, NFR-02, NFR-03) -

    // NFR-01: 蹂듯빀 ?곹깭 ?낅┰????Fault + Detection ?좏샇 議고빀 OnGroupStateChanged ?쒗??寃利?
    [Fact]
    public void should_fire_Faulted_when_fault_entry_enqueued_to_empty_group()
    {
        var sut = CreateSut();
        var transitions = new List<(int GroupId, EnumCompositeEventStatus Prev, EnumCompositeEventStatus Next)>();
        sut.OnGroupStateChanged += (gid, prev, next) => transitions.Add((gid, prev, next));

        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        Assert.Single(transitions);
        Assert.Equal((10, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Faulted), transitions[0]);
    }

    [Fact]
    public void should_fire_FaultedDetecting_when_detection_added_to_faulted_group()
    {
        var sut = CreateSut();
        var faultId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));
        var transitions = new List<(int GroupId, EnumCompositeEventStatus Prev, EnumCompositeEventStatus Next)>();
        sut.OnGroupStateChanged += (gid, prev, next) => transitions.Add((gid, prev, next));

        sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        Assert.Single(transitions);
        Assert.Equal((10, EnumCompositeEventStatus.Faulted, EnumCompositeEventStatus.FaultedDetecting), transitions[0]);
    }

    [Fact]
    public void should_fire_Faulted_when_detection_dequeued_from_FaultedDetecting_group()
    {
        var sut = CreateSut();
        sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));
        var detectId = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        var transitions = new List<(int GroupId, EnumCompositeEventStatus Prev, EnumCompositeEventStatus Next)>();
        sut.OnGroupStateChanged += (gid, prev, next) => transitions.Add((gid, prev, next));

        sut.Dequeue(detectId);

        Assert.Single(transitions);
        Assert.Equal((10, EnumCompositeEventStatus.FaultedDetecting, EnumCompositeEventStatus.Faulted), transitions[0]);
    }

    [Fact]
    public void should_fire_Normal_when_last_fault_dequeued_from_Faulted_group()
    {
        var sut = CreateSut();
        var faultId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));

        var transitions = new List<(int GroupId, EnumCompositeEventStatus Prev, EnumCompositeEventStatus Next)>();
        sut.OnGroupStateChanged += (gid, prev, next) => transitions.Add((gid, prev, next));

        sut.Dequeue(faultId);

        Assert.Single(transitions);
        Assert.Equal((10, EnumCompositeEventStatus.Faulted, EnumCompositeEventStatus.Normal), transitions[0]);
    }

    [Fact]
    public void should_complete_full_FaultedDetecting_sequence_correctly()
    {
        // Full NFR-01 ?쒗?? Normal?묯aulted?묯aultedDetecting?묯aulted?묿ormal
        var sut = CreateSut();
        var transitions = new List<(int, EnumCompositeEventStatus, EnumCompositeEventStatus)>();
        sut.OnGroupStateChanged += (gid, prev, next) => transitions.Add((gid, prev, next));

        var faultId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));
        var detectId = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));
        sut.Dequeue(detectId);
        sut.Dequeue(faultId);

        Assert.Equal(4, transitions.Count);
        Assert.Equal(EnumCompositeEventStatus.Normal,          transitions[0].Item2);
        Assert.Equal(EnumCompositeEventStatus.Faulted,         transitions[0].Item3);
        Assert.Equal(EnumCompositeEventStatus.Faulted,         transitions[1].Item2);
        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting,transitions[1].Item3);
        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting,transitions[2].Item2);
        Assert.Equal(EnumCompositeEventStatus.Faulted,         transitions[2].Item3);
        Assert.Equal(EnumCompositeEventStatus.Faulted,         transitions[3].Item2);
        Assert.Equal(EnumCompositeEventStatus.Normal,          transitions[3].Item3);
    }

    // NFR-02: 硫?곗뒪?덈뱶 ?ㅽ듃?덉뒪 ??2?ㅻ젅??횞 100???덉쇅 0嫄?
    [Fact]
    public async Task should_not_throw_when_concurrent_enqueue_dequeue()
    {
        var sut = CreateSut();
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var t1 = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var id = sut.Enqueue(CreateEntry(deviceId: i, groupIds: new List<int> { i % 5 }));
                    sut.Dequeue(id);
                }
                catch (Exception ex) { exceptions.Add(ex); }
            }
        });

        var t2 = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var id = sut.Enqueue(CreateEntry(deviceId: 1000 + i, groupIds: new List<int> { i % 5 }));
                    sut.Dequeue(id);
                }
                catch (Exception ex) { exceptions.Add(ex); }
            }
        });

        await Task.WhenAll(t1, t2);
        Assert.Empty(exceptions);
    }

    // NFR-03: 蹂듭썝 ?덉쟾????Fault+Detection 以??섎굹 Dequeue ???섎㉧吏 ?곹깭 ?좎?
    [Fact]
    public void should_preserve_Fault_state_after_dequeuing_detection_only()
    {
        var sut = CreateSut();
        var faultId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));
        var detectId = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        sut.Dequeue(detectId);

        // Fault ?대깽?멸? 洹몃９???ъ쟾???⑥븘 ?덉뼱????
        Assert.True(sut.HasEventsForGroup(10));
        Assert.NotNull(sut.GetEntry(faultId));
    }

    [Fact]
    public void should_preserve_Detection_state_after_dequeuing_fault_only()
    {
        var sut = CreateSut();
        var faultId = sut.Enqueue(CreateEntry(deviceId: 1, groupIds: new List<int> { 10 }, eventType: EnumEventType.Fault));
        var detectId = sut.Enqueue(CreateEntry(deviceId: 2, groupIds: new List<int> { 10 }, eventType: EnumEventType.Intrusion));

        sut.Dequeue(faultId);

        // Detection ?대깽?멸? 洹몃９???ъ쟾???⑥븘 ?덉뼱????
        Assert.True(sut.HasEventsForGroup(10));
        Assert.NotNull(sut.GetEntry(detectId));
    }

    #endregion
}

