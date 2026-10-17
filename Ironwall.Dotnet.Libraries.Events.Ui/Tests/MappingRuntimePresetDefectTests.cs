using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 맵핑 결함 G8 · G9 — 실행 중 감시금지구역 프리셋 무시 · SYNC_PRESET 무시
                  G8: 대상 프리셋이 감시금지구역인 배선이 이벤트 창에 그대로 떠 그 프리셋으로 PTZ 를 돌렸다.
                  G9: 서버 SYNC_PRESET 을 받는 곳이 없어 매핑 카메라 캐시 · 워크벤치가 옛 프리셋을 들고 있었다.
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MappingRuntimePresetDefectTests
{
    // ───────────────────────── 가짜 ─────────────────────────

    private sealed class FakeManager : IEventWindowManager
    {
        public ConcurrentQueue<EventWindowRequest> Requests { get; } = new();
        public CameraPopupSettings CurrentSettings { get; set; } = new CameraPopupSettings().Normalize();
        public int OpenCount => 0;
        public event EventHandler<string>? NoticeRaised { add { } remove { } }

        EventWindowOpenResult IEventWindowManager.Open(EventWindowRequest request)
        {
            Requests.Enqueue(request);
            return EventWindowOpenResult.Opened;
        }

        public bool BringToFront(EventWindowKind kind, string eventId) => true;
        public bool CloseForActionReport(EventWindowKind kind, string eventId) => true;
        public bool IsOpen(string eventKey) => false;
    }

    private sealed class FakeDirectory : IEventWindowDeviceDirectory
    {
        public ICameraDeviceModel? FindCamera(int cameraId) => new CameraDeviceModel(cameraId)
        {
            DeviceName = $"카메라 {cameraId}",
            DeviceType = EnumDeviceType.IpCamera,
            Category = EnumCameraType.PTZ,
            IpAddress = $"10.0.0.{cameraId}",
            IpPort = 80,
            Urls = new CameraUrlsModel { RtspMain = $"rtsp://10.0.0.{cameraId}/main" },
        };
        public string? DeviceName(int deviceId, EnumDeviceType deviceType) => $"펜스 센서 #{deviceId}";
        public string? GroupName(int groupId) => $"구역-{groupId:00}";
    }

    private static EventEntry Detection(int eventId) => new()
    {
        EventId = eventId,
        EventType = EnumEventType.Intrusion,
        DeviceId = 104,
        DeviceType = EnumDeviceType.Fence,
        GroupIds = new List<int> { 7 },
        EnqueuedAt = new DateTime(2026, 10, 4, 9, 0, 0),
    };

    private static MappingCameraReadDto CameraRow(int configId, int cameraId, int priority, int presetIndex, bool restricted)
        => new()
        {
            ConfigId = configId,
            Camera = new MappingDeviceRefDto { Id = cameraId, CategoryDevice = "camera" },
            Priority = priority,
            IsEnable = true,
            TargetPreset = new MappingPresetRefDto
            {
                Id = 900 + configId, CameraId = cameraId, PresetIndex = presetIndex, PresetName = "P" + presetIndex, IsRestrictedZone = restricted,
            },
        };

    /// <summary>매핑 10(구역 7) — 카메라 행 목록은 <paramref name="rows"/> 를 매번 다시 읽는다(서버 상태가 바뀌는 것을 흉내).</summary>
    private static Mock<IMappingWorkbenchGateway> Gateway(Func<MappingCameraReadDto[]> rows)
    {
        var gateway = new Mock<IMappingWorkbenchGateway>();
        gateway.Setup(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Ok(new[]
               {
                   new EventMappingReadDto { Id = 10, DeviceGroupId = 7, Status = true, CategoryEventMapping = "FENCE_SENSOR_ONLY" },
               }));
        gateway.Setup(g => g.ListCamerasAsync(10, It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(rows()));
        return gateway;
    }

    private static async Task RunAsync(EventWindowTrigger trigger, EventEntry entry)
    {
        var task = trigger.TryStart(entry);
        if (task is not null) await task;
    }

    // ───────────────────────── G8 ─────────────────────────

    [Fact]
    public async Task should_carry_the_restricted_zone_flag_from_the_server_into_the_cache_entry()
    {
        var cache = new EventMappingCameraCache(Gateway(() => new[] { CameraRow(1, 3, 1, 4, restricted: true), CameraRow(2, 5, 2, 6, restricted: false) }).Object);

        var entries = await cache.GetCamerasForGroupsAsync(new[] { 7 });

        Assert.Equal(new[] { true, false }, entries.Select(e => e.TargetPresetRestricted));
    }

    [Fact]
    public void should_drop_wiring_whose_target_preset_is_a_restricted_zone()
    {
        var ordered = EventWindowPlanning.Order(new[]
        {
            new MappingCameraEntry(3, 1, 1, 0, true, 4, "금지", null, TargetPresetRestricted: true),
            new MappingCameraEntry(5, 2, 2, 0, true, 6, "정문"),
        });

        Assert.Equal(new[] { 5 }, ordered.Select(e => e.CameraId));
    }

    [Fact]
    public void should_keep_the_same_camera_through_its_other_unrestricted_wiring()
    {
        var ordered = EventWindowPlanning.Order(new[]
        {
            new MappingCameraEntry(3, 1, 1, 0, true, 4, "금지", null, TargetPresetRestricted: true),
            new MappingCameraEntry(3, 2, 2, 0, true, 6, "정문"),
        });

        var only = Assert.Single(ordered);
        Assert.Equal(6, only.TargetPresetIndex);
    }

    [Fact]
    [Trait("Scenario", "EVM-015")]
    public async Task should_not_show_or_aim_a_camera_at_a_restricted_preset_when_a_detection_arrives()
    {
        var cache = new EventMappingCameraCache(Gateway(() => new[] { CameraRow(1, 3, 1, 4, restricted: true), CameraRow(2, 5, 2, 6, restricted: false) }).Object);
        var manager = new FakeManager();
        var trigger = new EventWindowTrigger(manager, cache, new FakeDirectory(), () => true);

        await RunAsync(trigger, Detection(801));

        var request = Assert.Single(manager.Requests);
        var camera = Assert.Single(request.Cameras);
        Assert.Equal("5", camera.CameraId);
        Assert.Equal("6", camera.TargetPresetToken);
        Assert.DoesNotContain(request.Cameras, c => c.TargetPresetToken == "4");
    }

    [Fact]
    public async Task should_not_open_a_window_when_every_mapped_camera_points_at_a_restricted_preset()
    {
        var cache = new EventMappingCameraCache(Gateway(() => new[] { CameraRow(1, 3, 1, 4, restricted: true) }).Object);
        var manager = new FakeManager();
        var trigger = new EventWindowTrigger(manager, cache, new FakeDirectory(), () => true);

        await RunAsync(trigger, Detection(802));

        Assert.Empty(manager.Requests);
    }

    // ───────────────────────── G9 — 트리거 ─────────────────────────

    [Fact]
    public async Task should_pick_up_a_preset_that_became_restricted_after_sync_preset()
    {
        var restricted = false;
        var gateway = Gateway(() => new[] { CameraRow(1, 3, 1, 4, restricted) });
        var cache = new EventMappingCameraCache(gateway.Object);
        var manager = new FakeManager();
        var trigger = new EventWindowTrigger(manager, cache, new FakeDirectory(), () => true);

        await RunAsync(trigger, Detection(901));
        Assert.Single(manager.Requests);

        restricted = true;                                                    // 서버에서 프리셋을 감시금지구역으로 바꿈
        await trigger.HandleAsync(new CameraPresetsChangedMessage("UPDATED", 901, 3), CancellationToken.None);
        await RunAsync(trigger, Detection(902));

        Assert.Single(manager.Requests);                                      // 두 번째 탐지는 창을 열지 않는다
        Assert.Equal(1, cache.Generation);
        gateway.Verify(g => g.ListCamerasAsync(10, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ───────────────────────── G9 — NATS 수신 ─────────────────────────

    private sealed class Recorder : IHandle<CameraPresetsChangedMessage>
    {
        public List<CameraPresetsChangedMessage> Messages { get; } = new();

        public Task HandleAsync(CameraPresetsChangedMessage message, CancellationToken cancellationToken)
        {
            lock (Messages) Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private static (PresetSyncNatsService Service, Recorder Recorder, Func<MessageArgsModel, Task> Handler) Build(ITokenStorageService? token = null)
    {
        Func<MessageArgsModel, Task>? captured = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => captured = h);

        var events = new EventAggregator();
        var recorder = new Recorder();
        events.SubscribeOnPublishedThread(recorder);
        var service = new PresetSyncNatsService(null, nats.Object, events, token);
        service.StartService();

        Assert.NotNull(captured);
        return (service, recorder, captured!);
    }

    private static MessageArgsModel Msg(string data) => new("all.sync.preset", "all.sync.>", data);

    [Fact]
    public async Task should_publish_presets_changed_when_sync_preset_arrives()
    {
        var (_, recorder, handler) = Build();

        await handler(Msg("""{"cmd":"SYNC_PRESET","m_type":"PUB","body":{"action":"UPDATED","resource_id":6,"camera_id":109}}"""));

        var message = Assert.Single(recorder.Messages);
        Assert.Equal(new CameraPresetsChangedMessage("UPDATED", 6, 109), message);
    }

    [Fact]
    public async Task should_publish_once_per_item_of_an_array_envelope()
    {
        var (_, recorder, handler) = Build();

        await handler(Msg("""[{"cmd":"SYNC_PRESET","body":{"action":"CREATED","resource_id":1,"camera_id":3}},{"cmd":"SYNC_PRESET","body":{"action":"DELETED","resource_id":2,"camera_id":3}}]"""));

        Assert.Equal(new[] { "CREATED", "DELETED" }, recorder.Messages.Select(m => m.Action));
    }

    [Theory]
    [InlineData("""{"cmd":"SYNC_EVENT_MAPPING","body":{"action":"UPDATED","resource_id":1}}""")]
    [InlineData("""{"cmd":"DETECT","body":{"note":"SYNC_PRESET 관련"}}""")]
    [InlineData("""{"cmd":"SYNC_PRESET","m_type":"RSP","body":{"action":"UPDATED","resource_id":1}}""")]
    [InlineData("""{"cmd":"sync_preset","body":{"action":"UPDATED","resource_id":1}}""")]
    [InlineData("""{ not json SYNC_PRESET""")]
    public async Task should_ignore_other_commands_responses_and_broken_payloads(string payload)
    {
        var (_, recorder, handler) = Build();

        await handler(Msg(payload));

        Assert.Empty(recorder.Messages);
    }

    [Fact]
    public async Task should_ignore_sync_preset_while_logged_out()
    {
        var token = new Mock<ITokenStorageService>();
        token.SetupGet(t => t.IsAuthenticated).Returns(false);
        var (_, recorder, handler) = Build(token.Object);

        await handler(Msg("""{"cmd":"SYNC_PRESET","body":{"action":"UPDATED","resource_id":6,"camera_id":109}}"""));

        Assert.Empty(recorder.Messages);
    }

    [Fact]
    public async Task should_unsubscribe_on_stop()
    {
        var nats = new Mock<INatsService>();
        var service = new PresetSyncNatsService(null, nats.Object, new EventAggregator());

        await service.StartService();
        await service.StopAsync();

        nats.VerifyRemove(m => m.NatsSubscribeEventAsync -= It.IsAny<Func<MessageArgsModel, Task>>(), Times.Exactly(2));
    }

    // ───────────────────────── G9 — 열린 워크벤치 ─────────────────────────

    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();

        public Task Delay(TimeSpan window, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => tcs.TrySetCanceled());
            lock (_pending) _pending.Add(tcs);
            return tcs.Task;
        }

        public void ReleaseAll()
        {
            List<TaskCompletionSource> copy;
            lock (_pending) { copy = new(_pending); _pending.Clear(); }
            foreach (var tcs in copy) tcs.TrySetResult();
        }
    }

    [Fact]
    public async Task should_refresh_the_open_workbench_preset_marks_and_candidates_on_sync_preset()
    {
        var gateway = new CountingGateway();
        var row = new MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 0, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
            TargetPreset = new MappingPresetRefDto { Id = 3702, CameraId = 370, PresetName = "후문", IsRestrictedZone = false },
        };
        gateway.Cameras.Add(row);
        var presets = new StubPresets();
        var delay = new ManualDelay();
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), delay: delay.Delay, presets: presets);
        await vm.ReloadAsync();

        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        await vm.PresetLoadTask;
        Assert.False(vm.BoardRows[0].Row.TargetPresetRestricted);
        Assert.Equal(new[] { 370 }, presets.Calls);

        // 서버에서 프리셋만 감시금지구역으로 — 배선 행의 updated_at 은 그대로다.
        row.TargetPreset.IsRestrictedZone = true;
        await vm.HandleAsync(new CameraPresetsChangedMessage("UPDATED", 3702, 370), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.True(vm.BoardRows[0].Row.TargetPresetRestricted);

        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        await vm.PresetLoadTask;
        Assert.Equal(new[] { 370, 370 }, presets.Calls);                     // 후보도 서버에서 다시 받았다
    }

    [Fact]
    public async Task should_not_overwrite_unapplied_workbench_edits_on_sync_preset()
    {
        var gateway = new CountingGateway();
        gateway.Cameras.Add(new MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 0, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        });
        var delay = new ManualDelay();
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), delay: delay.Delay);
        await vm.ReloadAsync();
        vm.AddDevices(new[] { 375 }, -1);
        gateway.Calls.Clear();

        await vm.HandleAsync(new CameraPresetsChangedMessage("UPDATED", 1, 370), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.True(vm.IsDirty);
        Assert.Equal(0, gateway.CountOf("list-mappings"));
        Assert.Equal(MappingWorkbenchViewModel.EXTERNAL_CHANGE_NOTICE, vm.StatusText);
    }
}
