using System.ComponentModel;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 지도 심볼 ↔ 이벤트 수명(WP-2 A1 · A2 · A3 · A5) — 실제 조회표(SymbolEventManager) + 실제 큐(EventQueueManager)로 돌린다.
/// </summary>
public class SymbolLifecycleTests
{
    private readonly EventQueueManager _queue = new();
    private readonly Mock<ILogService> _log = new();
    private readonly SymbolEventManager _sem;
    private readonly SymbolLifecycleCoordinator _coordinator;

    public SymbolLifecycleTests()
    {
        _sem = new SymbolEventManager(new Mock<IEventAggregator>().Object, _log.Object, new EventSetupModel(new Mock<IEventSetupModel>().Object), _queue);
        _queue.OnDeviceStateChanged += _sem.HandleDeviceStateChanged;   // 호스트 배선(EventUiModule)과 같다
        _coordinator = new SymbolLifecycleCoordinator(_sem, _queue, _log.Object);
    }

    private static BaseDeviceModel Device(int id, EnumDeviceType type, EnumDeviceStatus status, params int[] groups)
        => new() { Id = id, DeviceType = type, Status = status, DeviceGroups = groups.Length > 0 ? groups.ToList() : null };

    /// <summary>DB 에서 막 읽힌 심볼 — EventStatus 컬럼만 채워지고 CompositeStatus 는 기본값(영속 안 됨).</summary>
    private static PidsSymbolModel PersistedSymbol(EnumDeviceType type, EnumEventStatus persisted)
        => new() { Title = $"{type}", DeviceType = type, EventStatus = persisted };

    private void Enqueue(int deviceId, EnumDeviceType type, EnumEventType evt, params int[] groups)
        => _queue.Enqueue(new EventEntry { DeviceId = deviceId, DeviceType = type, EventType = evt, GroupIds = groups.Length > 0 ? groups.ToList() : null, EnqueuedAt = DateTime.Now, TimeoutSeconds = 3600 });

    [Theory]
    [InlineData(EnumDeviceStatus.ERROR)]
    [InlineData(EnumDeviceStatus.DEACTIVATED)]
    [InlineData(EnumDeviceStatus.ACTIVATED)]
    public void should_clear_persisted_detecting_on_boot_when_queue_is_empty(EnumDeviceStatus status)
    {
        // A1: 저장된 Detecting 이 ERROR · DEACTIVATED 장비에서 재시작 뒤에도 영원히 펄스하던 것.
        var symbol = PersistedSymbol(EnumDeviceType.Fence, EnumEventStatus.Detecting);
        _coordinator.BeginRebuild();
        _coordinator.RegisterDevice(Device(11, EnumDeviceType.Fence, status), symbol);
        _coordinator.CompleteRebuild();
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    public void should_keep_live_alarm_on_boot_when_queue_already_holds_event()
    {
        // 맹목 Normal 이 아니다 — 큐가 진실.
        Enqueue(12, EnumDeviceType.Fence, EnumEventType.Intrusion);
        var symbol = PersistedSymbol(EnumDeviceType.Fence, EnumEventStatus.Normal);
        _coordinator.BeginRebuild();
        _coordinator.RegisterDevice(Device(12, EnumDeviceType.Fence, EnumDeviceStatus.ERROR), symbol);
        _coordinator.CompleteRebuild();
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
    }

    [Fact]
    public void should_restore_transitions_dropped_during_rebuild_when_registration_completes()
    {
        // A2: 조회표를 비운 사이 온 큐 전이는 조회표가 버린다 — 재등록 뒤 큐 기준으로 복원돼야 한다.
        var device = Device(13, EnumDeviceType.Fence, EnumDeviceStatus.ERROR, 7);   // ERROR: 조회표 자체 자가치유(ACTIVATED 만) 밖
        var symbol = PersistedSymbol(EnumDeviceType.Fence, EnumEventStatus.Normal);
        var line = new PidsGroupSymbolModel { Title = "구역 7", LinkedDeviceGroup = 7 };

        _coordinator.BeginRebuild();
        Enqueue(13, EnumDeviceType.Fence, EnumEventType.Intrusion, 7);   // 이 순간 조회표는 비어 있다 → 전이 유실
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        _coordinator.RegisterDevice(device, symbol);
        _coordinator.RegisterGroup(7, device, line);
        var result = _coordinator.CompleteRebuild();

        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);
        Assert.Equal(2, result.Changed);
    }

    [Fact]
    public void should_reset_unlinked_symbol_when_no_device_matches()
    {
        var orphan = PersistedSymbol(EnumDeviceType.IpCamera, EnumEventStatus.Fault);
        _coordinator.BeginRebuild();
        var result = _coordinator.CompleteRebuild(new IPidsEventCapable[] { orphan });
        Assert.Equal(EnumEventStatus.Normal, orphan.EventStatus);
        Assert.Equal(1, result.Unlinked);
    }

    [Fact]
    public void should_detach_symbol_and_ignore_late_transitions_when_device_is_deleted()
    {
        // A3: 조회표에 해제가 없어 삭제된 장비의 심볼이 마지막 색으로 굳고, 늦은 전이가 계속 칠했다.
        var symbol = PersistedSymbol(EnumDeviceType.Fence, EnumEventStatus.Normal);
        _coordinator.BeginRebuild();
        _coordinator.RegisterDevice(Device(14, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED), symbol);
        _coordinator.CompleteRebuild();
        Enqueue(14, EnumDeviceType.Fence, EnumEventType.Intrusion);
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);

        var detached = _coordinator.UnregisterDevice(14, EnumDeviceType.Fence);

        Assert.Same(symbol, detached);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        _sem.HandleDeviceStateChanged(14, EnumDeviceType.Fence, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Faulted);
        _sem.SetDoorState(14, EnumDeviceType.Fence, EnumDoorState.Open);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);   // 흡수용 모델이 받았다
        Assert.Equal(EnumDoorState.Unknown, symbol.DoorState);
        Assert.False(_coordinator.IsRegistered(14, EnumDeviceType.Fence));
        Assert.Null(_coordinator.UnregisterDevice(14, EnumDeviceType.Fence));   // 두 번째는 no-op
    }

    [Fact]
    public void should_log_unmapped_transition_once_per_device_when_symbol_is_missing()
    {
        // A5: 조회표는 미등록 장비 전이를 로그 없이 버린다 — 곁에서 장비당 한 번 기록.
        Assert.True(_coordinator.ObserveDeviceTransition(99, EnumDeviceType.PIR, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Detecting));
        Assert.False(_coordinator.ObserveDeviceTransition(99, EnumDeviceType.PIR, EnumCompositeEventStatus.Detecting, EnumCompositeEventStatus.Normal));
        Assert.True(_coordinator.ObserveDeviceTransition(98, EnumDeviceType.PIR, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Faulted));
        _log.Verify(l => l.Info(It.Is<string>(m => m.Contains("[심볼 미등록]") && m.Contains("99")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);

        _coordinator.RegisterDevice(Device(99, EnumDeviceType.PIR, EnumDeviceStatus.ACTIVATED), PersistedSymbol(EnumDeviceType.PIR, EnumEventStatus.Normal));
        Assert.False(_coordinator.ObserveDeviceTransition(99, EnumDeviceType.PIR, EnumCompositeEventStatus.Normal, EnumCompositeEventStatus.Detecting));
    }
}
