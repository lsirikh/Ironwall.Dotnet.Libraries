using System.Collections.Concurrent;
using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Moq;
using Xunit;
using ContractProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;
using SettingsProviderKind = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 탐지 → 이벤트 창 트리거 · 매핑 카메라 캐시 · 조치보고 닫기 헤드리스 시험 (PRD camera-popup-modes FR-07 · 09 · 10 · 13 · 14 · 15 · 28)
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventWindowTriggerTests
{
    // ───────────────────────── 가짜 ─────────────────────────

    private sealed class FakeManager : IEventWindowManager
    {
        public ConcurrentQueue<EventWindowRequest> Requests { get; } = new();
        public ConcurrentQueue<(EventWindowKind Kind, string Id)> Closes { get; } = new();
        public ConcurrentQueue<string> Fronts { get; } = new();
        public HashSet<string> Open { get; } = new();
        public CameraPopupSettings CurrentSettings { get; set; } = new CameraPopupSettings().Normalize();
        public int OpenCount => Open.Count;
        public event EventHandler<string>? NoticeRaised { add { } remove { } }

        EventWindowOpenResult IEventWindowManager.Open(EventWindowRequest request)
        {
            Requests.Enqueue(request);
            lock (Open) Open.Add(request.EventKey);
            return EventWindowOpenResult.Opened;
        }

        public bool BringToFront(EventWindowKind kind, string eventId)
        {
            Fronts.Enqueue(EventKeys.Build(kind, eventId));
            return true;
        }

        public bool CloseForActionReport(EventWindowKind kind, string eventId)
        {
            Closes.Enqueue((kind, eventId));
            lock (Open) return Open.Remove(EventKeys.Build(kind, eventId));
        }

        public bool IsOpen(string eventKey)
        {
            lock (Open) return Open.Contains(eventKey);
        }
    }

    private sealed class FakeSource : IEventMappingCameraSource
    {
        public List<MappingCameraEntry> Entries { get; set; } = new();
        public int Calls;
        public int Invalidations;
        public TaskCompletionSource? Gate { get; set; }
        public List<IReadOnlyCollection<int>> Groups { get; } = new();

        public async Task<IReadOnlyList<MappingCameraEntry>> GetCamerasForGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken token = default)
        {
            Interlocked.Increment(ref Calls);
            lock (Groups) Groups.Add(groupIds);
            if (Gate is { } gate) await gate.Task.ConfigureAwait(false);
            return Entries;
        }

        public void Invalidate() => Interlocked.Increment(ref Invalidations);
    }

    private sealed class FakeDirectory : IEventWindowDeviceDirectory
    {
        public Dictionary<int, ICameraDeviceModel> Cameras { get; } = new();
        public ICameraDeviceModel? FindCamera(int cameraId) => Cameras.TryGetValue(cameraId, out var c) ? c : null;
        public string? DeviceName(int deviceId, EnumDeviceType deviceType) => $"펜스 센서 #{deviceId}";
        public string? GroupName(int groupId) => $"구역-{groupId:00}";
    }

    private static CameraDeviceModel Camera(int id, bool ptz = true) => new(id)
    {
        DeviceName = $"카메라 {id}",
        DeviceType = EnumDeviceType.IpCamera,
        Category = ptz ? EnumCameraType.PTZ : EnumCameraType.FIXED,
        IpAddress = $"10.0.0.{id}",
        IpPort = 80,
        UserName = "admin",
        UserPassword = "pw",
        Urls = new CameraUrlsModel { RtspMain = $"rtsp://10.0.0.{id}/main" },
    };

    private static MappingCameraEntry Wire(int cameraId, int? priority, int configId = 0, int? target = null, int delay = 0)
        => new(cameraId, priority, configId == 0 ? cameraId : configId, delay, true, target, target is null ? null : $"정문 {target}", null);

    private static EventEntry Detection(int eventId, EnumEventType type = EnumEventType.Intrusion, params int[] groups) => new()
    {
        EventId = eventId,
        EventType = type,
        DeviceId = 104,
        DeviceType = EnumDeviceType.Fence,
        GroupIds = (groups.Length == 0 ? new[] { 7 } : groups).ToList(),
        EnqueuedAt = new DateTime(2026, 9, 30, 13, 0, 0),
    };

    private static (EventWindowTrigger Trigger, FakeManager Manager, FakeSource Source, FakeDirectory Directory) NewTrigger(bool ptzAllowed = true)
    {
        var manager = new FakeManager();
        var source = new FakeSource();
        var directory = new FakeDirectory();
        for (var i = 1; i <= 9; i++) directory.Cameras[i] = Camera(i);
        var trigger = new EventWindowTrigger(manager, source, directory, () => ptzAllowed);
        return (trigger, manager, source, directory);
    }

    private static async Task RunAsync(EventWindowTrigger trigger, EventEntry entry)
    {
        var task = trigger.TryStart(entry);
        if (task is not null) await task;
    }

    // ───────────────────────── 모드 · 종류 ─────────────────────────

    [Fact]
    public async Task should_open_window_with_mapped_cameras_when_detection_enqueued_in_self_mode()
    {
        var (trigger, manager, source, _) = NewTrigger();
        source.Entries = new() { Wire(1, 1), Wire(2, 2) };

        await RunAsync(trigger, Detection(501));

        var request = Assert.Single(manager.Requests);
        Assert.Equal(EventWindowKind.Detection, request.Kind);
        Assert.Equal("501", request.EventId);
        Assert.Equal(new[] { "1", "2" }, request.Cameras.Select(c => c.CameraId));
        Assert.Equal("구역-07", request.Header.ZoneName);
        Assert.Equal("펜스 센서 #104", request.Header.DeviceName);
        Assert.Equal("탐지", request.Header.KindLabel);
        Assert.Equal(new[] { 7 }, source.Groups.Single());
    }

    [Theory]
    [InlineData(CameraPopupMode.Broker)]
    [InlineData(CameraPopupMode.None)]
    public async Task should_not_open_or_fetch_when_mode_is_broker_or_none(CameraPopupMode mode)
    {
        var (trigger, manager, source, _) = NewTrigger();
        manager.CurrentSettings = new CameraPopupSettings { Mode = mode }.Normalize();
        source.Entries = new() { Wire(1, 1) };

        await RunAsync(trigger, Detection(1));

        Assert.Empty(manager.Requests);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task should_open_malfunction_window_only_when_malfunction_option_is_on()
    {
        var (trigger, manager, source, _) = NewTrigger();
        source.Entries = new() { Wire(1, 1) };
        manager.CurrentSettings = new CameraPopupSettings { EventWindowOnMalfunction = false }.Normalize();

        await RunAsync(trigger, Detection(9, EnumEventType.Fault));
        Assert.Empty(manager.Requests);

        manager.CurrentSettings = new CameraPopupSettings { EventWindowOnMalfunction = true }.Normalize();
        await RunAsync(trigger, Detection(9, EnumEventType.Fault));

        var request = Assert.Single(manager.Requests);
        Assert.Equal(EventWindowKind.Malfunction, request.Kind);
        Assert.Equal("장애", request.Header.KindLabel);
    }

    [Theory]
    [InlineData(EnumEventType.Intrusion, true)]
    [InlineData(EnumEventType.ContactOn, true)]
    [InlineData(EnumEventType.Alert, true)]
    [InlineData(EnumEventType.ContactOff, false)]
    [InlineData(EnumEventType.Connection, false)]
    [InlineData(EnumEventType.Action, false)]
    public void should_decide_by_event_type_when_detection_windows_are_on(EnumEventType type, bool expected)
        => Assert.Equal(expected, EventWindowPlanning.ShouldOpen(new CameraPopupSettings().Normalize(), type));

    // ───────────────────────── 같은 이벤트 · 중복 ─────────────────────────

    [Fact]
    public async Task should_bring_to_front_without_fetching_when_same_event_is_already_open()
    {
        var (trigger, manager, source, _) = NewTrigger();
        source.Entries = new() { Wire(1, 1) };
        await RunAsync(trigger, Detection(3));

        await RunAsync(trigger, Detection(3));

        Assert.Single(manager.Requests);
        Assert.Equal(1, source.Calls);
        Assert.Equal(new[] { EventKeys.Build(EventWindowKind.Detection, "3") }, manager.Fronts);
    }

    [Fact]
    public async Task should_fetch_and_open_once_when_same_event_arrives_twice_while_building()
    {
        var (trigger, manager, source, _) = NewTrigger();
        source.Entries = new() { Wire(1, 1) };
        source.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = trigger.TryStart(Detection(4));
        var second = trigger.TryStart(Detection(4));
        source.Gate.SetResult();
        await first!;

        Assert.Null(second);
        Assert.Equal(1, source.Calls);
        Assert.Single(manager.Requests);
    }

    [Fact]
    public async Task should_open_once_and_bring_front_on_repeat_when_wired_to_real_event_queue()
    {
        var (trigger, manager, source, _) = NewTrigger();
        source.Entries = new() { Wire(1, 1) };
        var started = new ConcurrentQueue<Task>();
        using var eqm = new EventQueueManager();
        eqm.OnEntryEnqueued += e => { if (trigger.TryStart(e) is { } t) started.Enqueue(t); };

        eqm.Enqueue(Detection(21), "env-1");
        await Task.WhenAll(started);
        eqm.Enqueue(Detection(21), "env-2");   // 매니저가 새 봉투로 다시 보낸 같은 이벤트(서비스 필터를 지나온 경우)
        await Task.WhenAll(started);

        Assert.Single(manager.Requests);
        Assert.Single(manager.Fronts);
    }

    // ───────────────────────── 카메라 고르기 ─────────────────────────

    [Fact]
    public async Task should_order_by_priority_take_per_window_and_count_extra_when_more_cameras_are_mapped()
    {
        var (trigger, manager, source, directory) = NewTrigger();
        manager.CurrentSettings = new CameraPopupSettings { CamerasPerWindow = 3 }.Normalize();
        directory.Cameras.Remove(9);   // 장비 캐시에 없는 카메라는 건너뛴다
        source.Entries = new()
        {
            Wire(5, null), Wire(4, 3), Wire(9, 0), Wire(1, 1), Wire(2, 2),
            Wire(1, 5, configId: 99),                                   // 같은 카메라 두 번 — 앞 것만
            new MappingCameraEntry(6, 0, 6, 0, IsEnable: false),        // 꺼진 배선
        };

        await RunAsync(trigger, Detection(8));

        var request = Assert.Single(manager.Requests);
        Assert.Equal(new[] { "1", "2", "4" }, request.Cameras.Select(c => c.CameraId));
        Assert.Equal(1, request.ExtraCameraCount);   // 5번이 꼬리 "+1"
    }

    [Fact]
    public async Task should_not_open_window_when_no_mapped_camera_resolves()
    {
        var (trigger, manager, source, _) = NewTrigger();
        source.Entries = new() { Wire(42, 1) };   // 캐시에 없는 카메라

        await RunAsync(trigger, Detection(8));

        Assert.Empty(manager.Requests);
    }

    [Fact]
    public async Task should_mark_ptz_not_allowed_when_user_lacks_camera_control()
    {
        var (trigger, manager, source, _) = NewTrigger(ptzAllowed: false);
        source.Entries = new() { Wire(1, 1, target: 2, delay: 5) };

        await RunAsync(trigger, Detection(8));

        var camera = Assert.Single(Assert.Single(manager.Requests).Cameras);
        Assert.True(camera.IsPtz);
        Assert.False(camera.PtzAllowed);
    }

    [Fact]
    public void should_build_ptz_tile_with_presets_and_delay_when_mapping_has_target_preset()
    {
        var entry = new MappingCameraEntry(3, 1, 11, 7, true, TargetPresetIndex: 2, TargetPresetName: "정문", HomePresetIndex: 1);

        var tile = EventWindowPlanning.BuildCamera(Camera(3), entry, new CameraPopupSettings().Normalize(), ptzAllowed: true);

        Assert.Equal(("3", "카메라 3", true, true), (tile.CameraId, tile.Name, tile.IsPtz, tile.PtzAllowed));
        Assert.Equal(("2", "정문", "1", 7), (tile.TargetPresetToken, tile.TargetPresetName, tile.HomePresetToken, tile.DelaySeconds));
        Assert.Equal(ContractProviderKind.Onvif, tile.Provider.Kind);
        Assert.Equal("http://10.0.0.3:80/onvif/device_service", tile.Provider.Uri);
        Assert.Equal(("admin", "pw"), (tile.Provider.Username, tile.Provider.Password));
    }

    [Fact]
    public void should_drop_presets_and_use_rtsp_address_when_camera_is_fixed_and_provider_is_rtsp()
    {
        var entry = new MappingCameraEntry(3, 1, 11, 7, true, TargetPresetIndex: 2);
        var settings = new CameraPopupSettings { Provider = SettingsProviderKind.RtspUrl }.Normalize();

        var tile = EventWindowPlanning.BuildCamera(Camera(3, ptz: false), entry, settings, ptzAllowed: true);

        Assert.False(tile.IsPtz);
        Assert.Null(tile.TargetPresetToken);
        Assert.Equal(0, tile.DelaySeconds);
        Assert.Equal(ContractProviderKind.Rtsp, tile.Provider.Kind);
        Assert.Equal("rtsp://10.0.0.3/main", tile.Provider.Uri);
    }

    // ───────────────────────── 조치보고 → 닫기 ─────────────────────────

    [Fact]
    public void should_close_only_same_kind_window_when_action_reported()
    {
        var (trigger, manager, _, _) = NewTrigger();

        trigger.OnActionReported("malfunction", 7);
        trigger.OnActionReported("detection", 8);
        trigger.OnActionReported("unknown", 9);

        Assert.Equal(new[] { (EventWindowKind.Malfunction, "7"), (EventWindowKind.Detection, "8") }, manager.Closes);
    }

    [Fact]
    public async Task should_not_open_window_when_action_report_arrives_while_mapping_is_loading()
    {
        var (trigger, manager, source, _) = NewTrigger();
        source.Entries = new() { Wire(1, 1) };
        source.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var build = trigger.TryStart(Detection(12));
        trigger.OnActionReported("detection", 12);
        source.Gate.SetResult();
        await build!;

        Assert.Empty(manager.Requests);
        Assert.Null(trigger.TryStart(Detection(12)));   // 이미 조치된 이벤트는 다시 와도 창을 열지 않는다
    }

    // ───────────────────────── 비차단 · 예외 ─────────────────────────

    [Fact]
    public void should_return_within_budget_when_mapping_source_is_slow()
    {
        var (trigger, _, source, _) = NewTrigger();
        source.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);   // 끝나지 않는 조회

        var sw = Stopwatch.StartNew();
        for (var i = 1; i <= 20; i++) trigger.OnEntryEnqueued(Detection(100 + i));
        sw.Stop();
        source.Gate.SetResult();

        Assert.True(sw.ElapsedMilliseconds < 50, $"20건에 {sw.ElapsedMilliseconds} ms — NATS 처리 줄을 붙잡았다");
    }

    [Fact]
    public async Task should_not_throw_when_directory_and_permission_throw()
    {
        var manager = new FakeManager();
        var source = new FakeSource { Entries = new() { Wire(1, 1) } };
        var directory = new Mock<IEventWindowDeviceDirectory>();
        directory.Setup(d => d.FindCamera(It.IsAny<int>())).Throws(new InvalidOperationException("cache broken"));
        var trigger = new EventWindowTrigger(manager, source, directory.Object, () => throw new InvalidOperationException("perm broken"));

        var ex = await Record.ExceptionAsync(() => RunAsync(trigger, Detection(1)));

        Assert.Null(ex);
        Assert.Empty(manager.Requests);
    }

    [Fact]
    public void should_do_nothing_when_popup_module_is_not_registered()
    {
        var source = new FakeSource();
        var trigger = new EventWindowTrigger(null, source, new FakeDirectory(), () => true);

        trigger.OnEntryEnqueued(Detection(1));
        trigger.OnActionReported("detection", 1);

        Assert.False(trigger.IsEnabled);
        Assert.Equal(0, source.Calls);
    }

    // ───────────────────────── 매핑 캐시 ─────────────────────────

    private static EventMappingReadDto Mapping(int id, int? group, bool status = true, string category = "FENCE_SENSOR_ONLY")
        => new() { Id = id, DeviceGroupId = group, Status = status, CategoryEventMapping = category };

    private static MappingCameraReadDto CameraRow(int configId, int cameraId, int? priority, int? targetIndex = null)
        => new()
        {
            ConfigId = configId,
            Camera = new MappingDeviceRefDto { Id = cameraId, CategoryDevice = "camera" },
            Priority = priority,
            DelayTime = 3,
            TargetPreset = targetIndex is null ? null : new MappingPresetRefDto { Id = 900 + configId, CameraId = cameraId, PresetIndex = targetIndex.Value, PresetName = "P" + targetIndex },
        };

    private static Mock<IMappingWorkbenchGateway> Gateway()
    {
        var gateway = new Mock<IMappingWorkbenchGateway>();
        gateway.Setup(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Ok(new[]
               {
                   Mapping(10, 7), Mapping(11, 7, status: false), Mapping(12, 8), Mapping(13, 7, category: "OPERATION_ONLY"),
               }));
        gateway.Setup(g => g.ListCamerasAsync(10, It.IsAny<CancellationToken>()))
               .ReturnsAsync(MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(new[] { CameraRow(1, 3, 2, targetIndex: 4), CameraRow(2, 5, 1) }));
        gateway.Setup(g => g.ListCamerasAsync(It.Is<int>(id => id != 10), It.IsAny<CancellationToken>()))
               .ReturnsAsync(MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(new[] { CameraRow(9, 99, 0) }));
        return gateway;
    }

    [Fact]
    public async Task should_read_only_active_mappings_of_event_groups_and_cache_them()
    {
        var gateway = Gateway();
        var cache = new EventMappingCameraCache(gateway.Object);

        var first = await cache.GetCamerasForGroupsAsync(new[] { 7 });
        var second = await cache.GetCamerasForGroupsAsync(new[] { 7 });

        Assert.Equal(new[] { 3, 5 }, first.Select(e => e.CameraId));
        Assert.Equal((4, "P4", 3), (first[0].TargetPresetIndex, first[0].TargetPresetName, first[0].DelaySeconds));
        Assert.Equal(first, second);
        gateway.Verify(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()), Times.Once);
        gateway.Verify(g => g.ListCamerasAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        gateway.Verify(g => g.ListCamerasAsync(11, It.IsAny<CancellationToken>()), Times.Never);   // 꺼진 매핑
        gateway.Verify(g => g.ListCamerasAsync(13, It.IsAny<CancellationToken>()), Times.Never);   // 운영 전용
    }

    [Fact]
    public async Task should_refetch_when_sync_event_mapping_invalidates_cache()
    {
        var gateway = Gateway();
        var cache = new EventMappingCameraCache(gateway.Object);
        var trigger = new EventWindowTrigger(new FakeManager(), cache, new FakeDirectory(), () => true);
        await cache.GetCamerasForGroupsAsync(new[] { 7 });

        await trigger.HandleAsync(new EventMappingsChangedMessage("UPDATED", 10), CancellationToken.None);
        await cache.GetCamerasForGroupsAsync(new[] { 7 });

        Assert.Equal(1, cache.Generation);
        gateway.Verify(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        gateway.Verify(g => g.ListCamerasAsync(10, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task should_retry_next_time_when_mapping_read_fails()
    {
        var gateway = new Mock<IMappingWorkbenchGateway>();
        gateway.SetupSequence(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Fail("서버 오류", "500", 500))
               .ReturnsAsync(MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Ok(new[] { Mapping(10, 7) }));
        gateway.Setup(g => g.ListCamerasAsync(10, It.IsAny<CancellationToken>()))
               .ReturnsAsync(MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(new[] { CameraRow(1, 3, 1) }));
        var cache = new EventMappingCameraCache(gateway.Object);

        var failed = await cache.GetCamerasForGroupsAsync(new[] { 7 });
        var retried = await cache.GetCamerasForGroupsAsync(new[] { 7 });

        Assert.Empty(failed);
        Assert.Single(retried);
    }

    [Fact]
    public async Task should_use_legacy_api_without_presets_when_server_contract_is_older_than_7()
    {
        var legacy = new Mock<IEventApiService>();
        legacy.Setup(a => a.GetEventMappingsAsync(7, true, 1, 100, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiListResponse<EventMappingDto>
              {
                  Success = true,
                  Data = new() { new EventMappingDto { Id = 1, DeviceGroupId = 7, Status = true, Cameras = new() { new EventMappingCameraDto { CameraId = 3, Priority = 1, TargetPresetId = 55, DelayTime = 4 } } } },
              });
        var cache = new EventMappingCameraCache(gateway: null, legacy.Object);

        var entries = await cache.GetCamerasForGroupsAsync(new[] { 7 });

        var entry = Assert.Single(entries);
        Assert.Equal(3, entry.CameraId);
        Assert.Null(entry.TargetPresetIndex);   // 옛 응답의 프리셋은 DB id 뿐 — 자동 이동 없음
    }
}
