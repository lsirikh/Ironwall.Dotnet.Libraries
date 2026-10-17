using System.Collections.Concurrent;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 이벤트 → 이벤트 창(카메라 팝업 자동 열림 요청) · 카드 · SYNC_DETECTION · 억제 헤드리스 시나리오
                  (카탈로그 gis-map-event-scenario-catalog §2 EVM-* 의 남은 라이브러리 쪽 항목, 2026-10-11)
                  - EventMapRig(가짜 서버 → 실제 NATS 수신 서비스 → 실제 큐 → 실제 심볼 관리자 · 카드 패널)에
                    실제 EventWindowTrigger 를 호스트 배선(EventUiModule.WireEventWindows)과 같게 잇는다:
                    큐 OnEntryEnqueued → 트리거, 카드 패널 ActionReported → 트리거.OnActionReported.
                  - 창 관리자는 가짜(요청만 기록) — 창을 띄우지 않는다(Window.Show 없음).
                  - 이벤트는 "가짜 API 기록 먼저 → 같은 번호의 NATS 봉투" 순서. 탐지 · 장애는 조치보고 전 같은 대상 중복 송신 없음.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>로그 줄을 모으는 가짜 — 앱 로그 단언(「매핑 카메라 없음」 등)용.</summary>
internal sealed class RecordingLog : ILogService
{
    public ConcurrentQueue<string> Lines { get; } = new();
    public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Enqueue("ERROR " + msg);
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Enqueue("INFO " + msg);
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Enqueue("WARN " + msg);
}

/// <summary>창 관리자 가짜 — 여는 요청 · 닫는 요청 · 앞으로 가져오기만 기록한다.</summary>
internal sealed class RecordingWindowManager : IEventWindowManager
{
    public ConcurrentQueue<EventWindowRequest> Requests { get; } = new();
    public ConcurrentQueue<(EventWindowKind Kind, string Id)> Closes { get; } = new();
    public HashSet<string> Open { get; } = new();
    public CameraPopupSettings CurrentSettings { get; set; } = new CameraPopupSettings().Normalize();
    public int OpenCount { get { lock (Open) return Open.Count; } }
    public event EventHandler<string>? NoticeRaised { add { } remove { } }

    EventWindowOpenResult IEventWindowManager.Open(EventWindowRequest request)
    {
        Requests.Enqueue(request);
        lock (Open) Open.Add(request.EventKey);
        return EventWindowOpenResult.Opened;
    }

    public bool BringToFront(EventWindowKind kind, string eventId) => true;

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

/// <summary>카메라 장비 · 구역 이름 — GIS 장비 캐시 대역.</summary>
internal sealed class FixedWindowDirectory : IEventWindowDeviceDirectory
{
    public Dictionary<int, ICameraDeviceModel> Cameras { get; } = new();
    public ICameraDeviceModel? FindCamera(int cameraId) => Cameras.TryGetValue(cameraId, out var c) ? c : null;
    public string? DeviceName(int deviceId, EnumDeviceType deviceType) => $"{deviceType} #{deviceId}";
    public string? GroupName(int groupId) => $"구역-{groupId:00}";

    public static CameraDeviceModel Camera(int id) => new(id)
    {
        DeviceName = $"카메라 {id}",
        DeviceType = EnumDeviceType.IpCamera,
        Category = EnumCameraType.PTZ,
        IpAddress = $"10.0.0.{id}",
        IpPort = 80,
        UserName = "admin",
        UserPassword = "pw",
        Urls = new CameraUrlsModel { RtspMain = $"rtsp://10.0.0.{id}/main" },
    };
}

/// <summary>서버 이벤트 매핑 가짜 — 그룹 → 매핑 1개 → 카메라 배선(바꿀 수 있음).</summary>
internal sealed class FakeMappingServer
{
    public Dictionary<int, List<int>> CamerasByGroup { get; } = new();
    public Mock<IMappingWorkbenchGateway> Gateway { get; } = new();

    public FakeMappingServer()
    {
        Gateway.Setup(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Ok(
                   CamerasByGroup.Keys.Select(g => new EventMappingReadDto { Id = 1000 + g, DeviceGroupId = g, Status = true, CategoryEventMapping = "FENCE_SENSOR_ONLY" }).ToList()));
        Gateway.Setup(g => g.ListCamerasAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((int mappingId, CancellationToken _) => MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(
                   (CamerasByGroup.TryGetValue(mappingId - 1000, out var cams) ? cams : new List<int>())
                       .Select((cameraId, i) => new MappingCameraReadDto
                       {
                           ConfigId = mappingId * 10 + i,
                           Camera = new MappingDeviceRefDto { Id = cameraId, CategoryDevice = "camera" },
                           Priority = i + 1,
                           DelayTime = 0,
                       }).ToList()));
    }
}

/// <summary>EventMapRig + 실제 이벤트 창 트리거(호스트 배선과 같게).</summary>
internal sealed class EventPopupRig : IDisposable
{
    private readonly List<Task> _started = new();

    public EventMapRig Map { get; }
    public RecordingWindowManager Windows { get; } = new();
    public FixedWindowDirectory Directory { get; } = new();
    public FakeMappingServer Mappings { get; } = new();
    public EventMappingCameraCache Cache { get; }
    public RecordingLog Log { get; } = new();
    public EventWindowTrigger Trigger { get; }

    public EventPopupRig()
    {
        Map = new EventMapRig();
        for (var i = 1; i <= 9; i++) Directory.Cameras[i] = FixedWindowDirectory.Camera(i);
        Cache = new EventMappingCameraCache(Mappings.Gateway.Object, log: Log);
        Trigger = new EventWindowTrigger(Windows, Cache, Directory, () => true, Log);
        // 호스트 배선(EventUiModule.WireEventWindows :417-418)과 같다 — 다만 시험은 시작된 작업을 기다린다.
        Map.Queue.OnEntryEnqueued += e => { if (Trigger.TryStart(e) is { } t) lock (_started) _started.Add(t); };
        Map.Panel.ActionReported += Trigger.OnActionReported;
    }

    /// <summary>트리거가 시작한 배경 작업(매핑 조회 · 창 명령)을 모두 기다린다.</summary>
    public async Task SettleAsync()
    {
        Task[] pending;
        lock (_started) { pending = _started.ToArray(); _started.Clear(); }
        await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(10));
    }

    public void Dispose() => Map.Dispose();
}

[Collection("IoC-Dependent")]   // 카드 패널 · 썸네일 주소가 IoC.GetInstance(전역 정적)를 읽는다
public class EventPopupScenarioTests
{
    // ───────────────────────── A. 카드 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-001")]
    public async Task should_list_detection_card_named_by_kind_and_id_when_detect_arrives_after_api_record_evm001()
    {
        using var rig = new EventMapRig();
        rig.Sensor(2001, groups: 1);

        var id = await rig.DetectAsync(2001);
        var card = await rig.ShowDetectionCardAsync(id);

        Assert.Same(card, Assert.Single(rig.Panel.ViewModelProvider));
        Assert.Equal($"Events.Card.Detection.{id}", card.AutomationKey);
        Assert.NotNull(rig.Queue.FindEntryByEventId(id, EnumEventType.Intrusion));   // 카드와 같은 번호의 큐 항목

        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Empty(rig.Panel.ViewModelProvider);
    }

    [Fact]
    [Trait("Scenario", "EVM-002")]
    public async Task should_put_newest_card_on_top_when_two_detections_arrive_on_distinct_sensors_evm002()
    {
        using var rig = new EventMapRig();
        rig.Sensor(2011);
        rig.Sensor(2012);

        var first = await rig.DetectAsync(2011);
        await rig.ShowDetectionCardAsync(first);
        var second = await rig.DetectAsync(2012);
        await rig.ShowDetectionCardAsync(second);

        Assert.Equal(new[] { second, first }, rig.Panel.ViewModelProvider.Select(c => c.Model!.Id));   // 목록 0번 = 새 카드

        rig.RemoteReport(ActionReportKind.Detection, first);
        rig.RemoteReport(ActionReportKind.Detection, second);
    }

    [Theory]
    [Trait("Scenario", "EVM-020")]
    [InlineData("\"SENSOR\"")]
    [InlineData("\"AI\"")]
    [InlineData("\"\"")]
    [InlineData("\"WHATEVER\"")]
    [InlineData("null")]
    [InlineData("12")]
    public async Task should_queue_and_paint_without_crash_when_detection_result_varies_evm020(string resultJson)
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(2021, groups: 2);
        var id = rig.RecordDetection(2021);

        await rig.PublishRawAsync($$"""
            { "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "DETECT", "from": "PidsProxy",
              "body": { "id": {{id}}, "category_event": "detection", "type_event": "Intrusion", "action_reported": false,
                        "device": { "id": 2021, "category_device": "sensor" }, "result": {{resultJson}} } }
            """);

        Assert.NotNull(rig.Queue.FindEntryByEventId(id, EnumEventType.Intrusion));
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
    }

    [Fact]
    [Trait("Scenario", "EVM-052,EVM-073")]
    public async Task should_list_malfunction_card_apart_from_detection_card_when_both_arrive_after_api_record_evm052_073()
    {
        using var rig = new EventMapRig();
        rig.Sensor(2041);
        rig.Sensor(2042);

        var detection = await rig.DetectAsync(2041);
        await rig.ShowDetectionCardAsync(detection);
        var fault = await rig.MalfunctionAsync(2042, "FAULT_FENCE");
        var card = await rig.ShowMalfunctionCardAsync(fault);

        Assert.Equal($"Events.Card.Malfunction.{fault}", card.AutomationKey);
        Assert.Single(rig.Panel.ViewModelProvider.OfType<MalfunctionEventCardViewModel>());   // 카드 종류가 따로 선다
        Assert.Single(rig.Panel.ViewModelProvider.OfType<DetectionEventCardViewModel>());
        Assert.Equal((1, 1), rig.Counts);

        rig.RemoteReport(ActionReportKind.Malfunction, fault);
        rig.RemoteReport(ActionReportKind.Detection, detection);
        Assert.Empty(rig.Panel.ViewModelProvider);
    }

    [Theory]
    [Trait("Scenario", "EVM-053")]
    [InlineData(EnumFaultType.FAULT_CONTROLLER, "제어기 장애")]
    [InlineData(EnumFaultType.FAULT_FENCE, "펜스 장애")]
    [InlineData(EnumFaultType.FAULT_MULTI, "복합센서 장애")]
    [InlineData(EnumFaultType.FAULT_CABLE_CUTTING, "케이블 절단")]
    [InlineData(EnumFaultType.FAULT_ETC, "기타 장애")]
    public void should_show_korean_fault_type_for_each_of_five_malfunction_reasons_evm053(EnumFaultType reason, string expected)
    {
        // 카드 · 창 글이 쓰는 같은 표(EnumKoreanMap) — 영문 코드가 화면에 새지 않는다.
        Assert.Equal(expected, Ironwall.Dotnet.Libraries.Events.Ui.Converters.EnumKoreanMap.To(reason));
    }

    [Fact]
    [Trait("Scenario", "EVM-095")]
    public async Task should_keep_alarm_until_last_of_two_detections_is_reported_evm095()
    {
        using var rig = new EventMapRig();
        rig.Sensor(2031);
        rig.Sensor(2032);
        var a = await rig.DetectAsync(2031);
        var b = await rig.DetectAsync(2032);

        rig.RemoteReport(ActionReportKind.Detection, a);
        Assert.Equal(0, rig.SoundStops);     // 아직 1건 남음 — 경보 유지
        Assert.Equal((1, 0), rig.Counts);

        rig.RemoteReport(ActionReportKind.Detection, b);
        Assert.Equal(1, rig.SoundStops);     // 0 건에서 멈춤
        Assert.Equal((0, 0), rig.Counts);
    }

    // ───────────────────────── A · B · C. 이벤트 창(카메라 팝업 자동 열림 요청) ─────────────────────────

    [Theory]
    [Trait("Scenario", "EVM-012,EVM-021,EVM-051")]
    [InlineData("Intrusion", EnumDeviceType.Fence)]
    [InlineData("Alert", EnumDeviceType.Fence)]        // EVM-021 사전 경보도 창
    [InlineData("ContactOn", EnumDeviceType.Contact)]  // EVM-051 접점 센서
    public async Task should_request_event_window_with_mapped_cameras_when_mapped_sensor_detects_evm012(string typeEvent, EnumDeviceType type)
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[7] = new() { 3, 5 };
        rig.Map.Sensor(2101, type, groups: 7);

        var id = await rig.Map.DetectAsync(2101, typeEvent);
        await rig.SettleAsync();

        var request = Assert.Single(rig.Windows.Requests);
        Assert.Equal(EventWindowKind.Detection, request.Kind);
        Assert.Equal(id.ToString(), request.EventId);
        Assert.Equal(new[] { "3", "5" }, request.Cameras.Select(c => c.CameraId));
        Assert.Equal("구역-07", request.Header.ZoneName);
        Assert.Equal($"detection-{id}", request.EventKey);

        rig.Map.RemoteReport(ActionReportKind.Detection, id);
    }

    [Fact]
    [Trait("Scenario", "EVM-046")]
    public async Task should_request_event_window_when_ai_detection_arrives_for_camera_in_mapped_group_evm046()
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[6] = new() { 4 };
        rig.Map.Device(2111, EnumDeviceType.IpCamera, groups: 6);

        var id = await rig.Map.DetectAsync(2111);
        await rig.SettleAsync();

        var request = Assert.Single(rig.Windows.Requests);
        Assert.Equal(new[] { "4" }, request.Cameras.Select(c => c.CameraId));
        rig.Map.RemoteReport(ActionReportKind.Detection, id);
    }

    [Fact]
    [Trait("Scenario", "EVM-013")]
    public async Task should_not_request_window_and_log_no_mapped_camera_when_group_has_no_mapping_evm013()
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[9] = new() { 2 };     // 다른 그룹에만 매핑
        var symbol = rig.Map.Sensor(2121, groups: 8);

        var id = await rig.Map.DetectAsync(2121);
        await rig.SettleAsync();

        Assert.Empty(rig.Windows.Requests);
        Assert.Contains(rig.Log.Lines, l => l.Contains("매핑 카메라 없음") && l.Contains($"detection-{id}"));
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);   // 창이 없어도 지도 · 카드 흐름은 그대로
        rig.Map.RemoteReport(ActionReportKind.Detection, id);
    }

    [Fact]
    [Trait("Scenario", "EVM-034")]
    public async Task should_not_open_window_or_fetch_mapping_when_action_report_arrived_before_detection_evm034()
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[7] = new() { 3 };
        var symbol = rig.Map.Sensor(2131, groups: 7);
        var id = rig.Map.RecordDetection(2131);

        rig.Map.RemoteReport(ActionReportKind.Detection, id);   // ACTION_REPORT 가 먼저 닿음
        await rig.Map.PublishDetectAsync(id);
        await rig.SettleAsync();

        Assert.Empty(rig.Windows.Requests);
        rig.Mappings.Gateway.Verify(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal((0, 0), rig.Map.Counts);   // 신호등도 0(EVM-034 신호등)
    }

    [Theory]
    [Trait("Scenario", "EVM-059,EVM-060")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task should_request_malfunction_window_only_when_event_window_on_malfunction_is_on_evm059_060(bool onMalfunction)
    {
        using var rig = new EventPopupRig();
        rig.Windows.CurrentSettings = new CameraPopupSettings { EventWindowOnMalfunction = onMalfunction }.Normalize();
        rig.Mappings.CamerasByGroup[11] = new() { 6 };
        rig.Map.Sensor(2141, groups: 11);

        var id = await rig.Map.MalfunctionAsync(2141);
        await rig.SettleAsync();

        if (onMalfunction)
        {
            var request = Assert.Single(rig.Windows.Requests);
            Assert.Equal(EventWindowKind.Malfunction, request.Kind);
            Assert.Equal($"malfunction-{id}", request.EventKey);
        }
        else
        {
            Assert.Empty(rig.Windows.Requests);   // 기본값(false) — 창 없음
        }
        rig.Map.RemoteReport(ActionReportKind.Malfunction, id);
    }

    [Fact]
    [Trait("Scenario", "EVM-088")]
    public async Task should_send_close_for_same_kind_window_when_remote_action_report_arrives_evm088()
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[7] = new() { 3 };
        rig.Map.Sensor(2151, groups: 7);
        var id = await rig.Map.DetectAsync(2151);
        await rig.SettleAsync();
        Assert.True(rig.Windows.IsOpen($"detection-{id}"));

        rig.Map.RemoteReport(ActionReportKind.Detection, id);

        Assert.Equal(new[] { (EventWindowKind.Detection, id.ToString()) }, rig.Windows.Closes);
        Assert.False(rig.Windows.IsOpen($"detection-{id}"));
    }

    [Fact]
    [Trait("Scenario", "EVM-091")]
    public async Task should_close_window_card_and_symbol_together_when_local_report_dialog_ok_evm091()
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[7] = new() { 3 };
        var symbol = rig.Map.Sensor(2161, groups: 7);
        var id = await rig.Map.DetectAsync(2161);
        await rig.SettleAsync();
        var card = await rig.Map.ShowDetectionCardAsync(id);

        await rig.Map.ReportLocallyAsync(card);

        Assert.Empty(rig.Map.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Contains(id, rig.Map.ServerActions);                      // ACTION_REPORT 1건(서버 POST)
        Assert.Equal(1, rig.Map.SoundStops);                              // 소리 멈춤
        Assert.Contains((EventWindowKind.Detection, id.ToString()), rig.Windows.Closes);
    }

    [Fact]
    [Trait("Scenario", "EVM-138")]
    public async Task should_use_new_camera_for_next_window_when_sync_event_mapping_changes_cameras_evm138()
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[7] = new() { 3 };
        rig.Map.Sensor(2171, groups: 7);
        rig.Map.Sensor(2172, groups: 7);
        var first = await rig.Map.DetectAsync(2171);
        await rig.SettleAsync();

        rig.Mappings.CamerasByGroup[7] = new() { 8 };   // 서버에서 매핑 카메라를 바꿈 → SYNC_EVENT_MAPPING
        await rig.Trigger.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1007), CancellationToken.None);
        var second = await rig.Map.DetectAsync(2172);
        await rig.SettleAsync();

        var requests = rig.Windows.Requests.ToArray();
        Assert.Equal(2, requests.Length);
        Assert.Equal(new[] { "3" }, requests[0].Cameras.Select(c => c.CameraId));
        Assert.Equal(new[] { "8" }, requests[1].Cameras.Select(c => c.CameraId));
        rig.Map.RemoteReport(ActionReportKind.Detection, first);
        rig.Map.RemoteReport(ActionReportKind.Detection, second);
    }

    // ───────────────────────── 썸네일 주소(카드 사진) ─────────────────────────

    [Theory]
    [Trait("Scenario", "EVM-004,EVM-043")]
    [InlineData("/api/thumbnails/images/det_1.jpg", "https://gop-api.test:8136/api/thumbnails/images/det_1.jpg")]                         // EVM-004 상대 URL
    [InlineData("https://192.168.202.151:8000/api/thumbnails/images/ai_2.jpg", "https://gop-api.test:8136/api/thumbnails/images/ai_2.jpg")] // EVM-043 다른 호스트 절대 URL
    public async Task should_resolve_card_photo_against_app_api_host_when_detection_carries_thumbnail_evm004_043(string raw, string expected)
    {
        using var rig = new EventMapRig();
        var api = new Mock<IApiSetupModel>();
        api.SetupGet(a => a.Url).Returns("https://gop-api.test:8136/api");
        var inner = IoC.GetInstance;
        IoC.GetInstance = (type, key) => type == typeof(IApiSetupModel) ? api.Object : inner(type, key);
        rig.Sensor(2201);
        var id = await rig.DetectAsync(2201);
        var model = new DetectionEventModel { Id = id, MessageType = EnumEventType.Intrusion, Device = rig.ServerDevices[2201], Thumbnail = raw };
        var card = new DetectionEventCardViewModel(model);
        rig.Panel.EnqueueCard(card);
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.True(card.HasThumbnail);
        Assert.Equal(expected, card.ThumbnailUri!.AbsoluteUri);
        rig.RemoteReport(ActionReportKind.Detection, id);
    }

    // ───────────────────────── H. SYNC_DETECTION ─────────────────────────

    /// <summary>실제 DetectionSyncNatsService 를 같은 큐에 잇는다 — 발행은 카드 패널 처리기로 곧바로(호스트 EA 대역).</summary>
    private static (DetectionSyncNatsService Service, Func<MessageArgsModel, Task> Handler, Mock<IEventApiService> Api) SyncService(EventMapRig rig)
    {
        Func<MessageArgsModel, Task>? handler = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handler = h);
        var api = new Mock<IEventApiService>();
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Returns((object message, Func<Func<Task>, Task> _, CancellationToken _) => message switch
          {
              DetectionThumbnailSyncedMessage m => rig.Panel.HandleAsync(m, CancellationToken.None),
              DetectionDeletedOnServerMessage m => rig.Panel.HandleAsync(m, CancellationToken.None),
              _ => Task.CompletedTask,
          });
        var service = new DetectionSyncNatsService(null, nats.Object, rig.Queue, api.Object, ea.Object, null);
        service.StartService().GetAwaiter().GetResult();
        return (service, handler!, api);
    }

    private static MessageArgsModel Sync(string action, int resourceId) => new(
        subject: "sensorway.unit001.all.sync.detection", subscriptionSubject: "sensorway.unit001.all.>",
        data: $$"""{ "id": "sync-{{action}}-{{resourceId}}", "m_type": "PUB", "cmd": "SYNC_DETECTION", "from": "DBApi", "body": { "action": "{{action}}", "resource_id": {{resourceId}} } }""");

    [Fact]
    [Trait("Scenario", "EVM-123")]
    public async Task should_replace_card_photo_when_server_patches_thumbnail_and_sync_detection_updated_arrives_evm123()
    {
        using var rig = new EventMapRig();
        rig.Sensor(2301);
        var id = await rig.DetectAsync(2301);
        var model = new DetectionEventModel { Id = id, MessageType = EnumEventType.Intrusion, Device = rig.ServerDevices[2301], Thumbnail = "/api/thumbnails/images/before.jpg" };
        var card = new DetectionEventCardViewModel(model);
        rig.Panel.EnqueueCard(card);
        await rig.Panel.FlushPendingCardsNowAsync();
        var (service, handler, api) = SyncService(rig);
        api.Setup(a => a.GetDetectionEventByIdAsync(id, It.IsAny<CancellationToken>()))   // 서버 PATCH thumbnail 뒤의 GET
           .ReturnsAsync(new ApiResponse<DetectionEventDto> { Success = true, Data = new DetectionEventDto { Id = id, Detail = new DetectionDetailDto { Thumbnail = "/api/thumbnails/images/after.jpg", FrameWidth = 1920, FrameHeight = 1080 } } });

        await handler(Sync("UPDATED", id));
        await service.LastProcessingTask!;

        Assert.Equal("/api/thumbnails/images/after.jpg", model.Thumbnail);
        Assert.Equal((1920, 1080), (model.FrameWidth, model.FrameHeight));
        rig.RemoteReport(ActionReportKind.Detection, id);
    }

    [Fact]
    [Trait("Scenario", "EVM-124")]
    public async Task should_not_revive_card_or_fetch_when_sync_detection_arrives_for_reported_event_evm124()
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(2311);
        var id = await rig.DetectAsync(2311);
        await rig.ShowDetectionCardAsync(id);
        rig.RemoteReport(ActionReportKind.Detection, id);   // 조치 → 카드 닫힘
        var (_, handler, api) = SyncService(rig);

        await handler(Sync("UPDATED", id));

        api.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        var after = await rig.DetectAsync(2311);            // 뒤 처리는 계속된다
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        rig.RemoteReport(ActionReportKind.Detection, after);
    }

    [Fact]
    [Trait("Scenario", "EVM-125")]
    public async Task should_close_card_lower_lamp_restore_symbol_and_close_window_when_sync_detection_deleted_arrives_evm125()
    {
        using var rig = new EventPopupRig();
        rig.Mappings.CamerasByGroup[7] = new() { 3 };
        var symbol = rig.Map.Sensor(2321, groups: 7);
        var id = await rig.Map.DetectAsync(2321);
        await rig.SettleAsync();
        await rig.Map.ShowDetectionCardAsync(id);
        var (service, handler, _) = SyncService(rig.Map);

        await handler(Sync("DELETED", id));   // 서버에서 탐지 DELETE → SYNC_DETECTION{DELETED}
        await service.LastProcessingTask!;

        Assert.Empty(rig.Map.Panel.ViewModelProvider);                                    // 카드 닫힘
        Assert.Equal((0, 0), rig.Map.Counts);                                              // 신호등 −1
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);                          // 심볼 재계산
        Assert.Contains((EventWindowKind.Detection, id.ToString()), rig.Windows.Closes);   // 이벤트 창 닫힘
        Assert.Contains(rig.Map.Published, m => m is EventDeletedOnServerNotice n && n.EventId == id);   // 토스트 한 줄
    }

    // ───────────────────────── H. 억제 ─────────────────────────

    private sealed class SpyMonitor : ISuppressionActiveMonitor
    {
        public List<string> Requests { get; } = new();
        public IReadOnlyList<EventSuppressionScheduleDto> Active { get; } = new List<EventSuppressionScheduleDto>();
        public event System.Action? ActiveChanged { add { } remove { } }
        public DateTime? LastSuccessAt => null;
        public bool IsStale => false;
        public string LastSuccessAgeText => "확인 안 됨";
        public void RequestImmediatePoll(string reason = "") => Requests.Add(reason);
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    }

    [Fact]
    [Trait("Scenario", "EVM-131")]
    public async Task should_poll_suppression_panel_three_times_and_leave_map_untouched_when_suppression_created_updated_deleted_evm131()
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(2401, groups: 4);
        var zone = rig.Zone(4);
        Func<MessageArgsModel, Task>? handler = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handler = h);
        var spy = new SpyMonitor();
        _ = new EventSuppressionSyncNatsService(null, nats.Object, spy, null).StartService();

        foreach (var action in new[] { "CREATED", "UPDATED", "DELETED" })
        {
            var json = $$"""{ "id": "sup-{{action}}", "m_type": "PUB", "cmd": "SYNC_EVENT_SUPPRESSION", "body": { "action": "{{action}}", "resource_id": 86, "device_id": 2401, "suppressing": true } }""";
            await handler!(new MessageArgsModel("sensorway.unit001.all.sync.suppression", "sensorway.unit001.all.>", json));
            await rig.PublishRawAsync(json, "sensorway.unit001.all.sync.suppression");   // 이벤트 수신 서비스도 같은 봉투를 본다
        }

        Assert.Equal(3, spy.Requests.Count);                                  // 알림 3 → 패널 폴링 3
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());                      // 지도 표시 없음
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal(-1, rig.Counts.Detection);
    }
}
