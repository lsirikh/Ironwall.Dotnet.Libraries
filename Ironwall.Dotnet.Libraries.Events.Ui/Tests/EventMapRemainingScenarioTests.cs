using System.Diagnostics;
using System.IO;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 시나리오 상태표의 남은 이벤트 · 지도 심볼 항목(MAP-140 ~ 145 · EVM-022/023/031/037/068/069/097/127/128/129)
                  을 헤드리스로 덮는다(2026-10-11, W6).
                  - 장치는 EventMapRig(가짜 서버 → 실제 NATS 수신 서비스 → 실제 큐 · 심볼 관리자 · 카드 패널) 그대로.
                  - 이벤트는 "가짜 API 기록 먼저 → 같은 번호의 NATS 봉투". 탐지 · 장애는 조치보고 전에 같은 이벤트를 다시 보내지 않는다.
                  - 심볼 UIA 이름 · ItemStatus 글(「탐지 중」 · 「장비 오류」 · 「문 열림」)은 GMaps.Ui 의 SymbolStatusText · DoorIndicatorRules 가
                    심볼 모델의 축(EventStatus · OperationState · DoorState)에서 만든다. 이 프로젝트는 GMaps.Ui 를 참조할 수 없으므로
                    (GMaps.Ui → Events.Ui 참조, 거꾸로 걸면 순환) 여기서는 그 글의 원천인 축 값을 단언한다.
                    축 → 글 대응은 tests/GMaps.Housing.Tests(EventMarkerPaintScenarioTests · MarkerComponentBadgeTests) 몫이다.
                  - 창을 띄우지 않는다(Window.Show 없음). 실제 NATS · Redis · 서버 · 지도 DB 없음.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // 카드 패널 · 행 뷰모델이 IoC.GetInstance(전역 정적)를 읽는다
public class EventMapRemainingScenarioTests
{
    // ───────────────────────── 장치 보조 ─────────────────────────

    /// <summary>호스트(NatsDomainService)가 하듯 카드를 묶음 버퍼에만 넣는다 — 목록에 올리는 것은 패널 비우기 몫.</summary>
    private static DetectionEventCardViewModel NewDetectionCard(EventMapRig rig, int eventId)
    {
        var (deviceId, _) = rig.ServerEvents[(ActionReportKind.Detection, eventId)];
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(eventId);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        model.SetupGet(m => m.Device).Returns(rig.ServerDevices[deviceId]);
        model.SetupProperty(m => m.Status);
        return new DetectionEventCardViewModel(model.Object);
    }

    /// <summary>SYNC_DETECTION 수신 서비스(실물) — 발행은 카드 패널로 곧바로 잇는다(호스트 EA 대역). REST 는 가짜.</summary>
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
        data: $$"""{ "id": "sync-{{action}}-{{resourceId}}-{{Guid.NewGuid():N}}", "m_type": "PUB", "cmd": "SYNC_DETECTION", "from": "DBApi", "body": { "action": "{{action}}", "resource_id": {{resourceId}} } }""");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("저장소 루트(sln)를 찾지 못함");
    }

    // ───────────────────────── MAP-140 · 141 · 143 · 144 심볼 축 ─────────────────────────

    [Theory]
    [InlineData(EnumDeviceType.Fence)]
    [InlineData(EnumDeviceType.Underground)]
    [InlineData(EnumDeviceType.PIR)]
    [InlineData(EnumDeviceType.SmartSensor2)]
    [InlineData(EnumDeviceType.SmartMultisensor2)]
    [Trait("Scenario", "MAP-140")]
    public async Task should_set_detecting_axis_on_that_sensor_symbol_only_when_map_sensor_detects_map140(EnumDeviceType type)
    {
        // 「탐지 중」 = SymbolStatusText.EventText(EnumEventStatus.Detecting). 지도의 다른 센서 심볼은 그대로여야 한다.
        using var rig = new EventMapRig();
        var target = rig.Sensor(14001, type, groups: 14);
        var neighbour = rig.Sensor(14002, type, groups: 14);
        var zone = rig.Zone(14);

        var id = await rig.DetectAsync(14001);

        Assert.Equal(EnumEventStatus.Detecting, target.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, target.CompositeStatus);
        Assert.Equal(EnumOperationState.ACTIVATED, target.OperationState);    // 「장비 활성」 그대로 — 탐지는 운영 축을 건드리지 않는다
        Assert.Equal(EnumEventStatus.Normal, neighbour.EventStatus);          // 이웃 센서는 「정상」
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);

        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, target.EventStatus);
    }

    [Fact]
    [Trait("Scenario", "MAP-141")]
    public async Task should_follow_sync_device_error_and_activated_on_operation_axis_while_keeping_detecting_when_server_changes_status_map141()
    {
        // 호스트 SYNC_DEVICE 처리 = SymbolEventManager.SyncDeviceStatus(라이브러리). 「장비 오류」 ↔ 「장비 활성」 은 운영 축, 「탐지 중」 은 큐가 정한다.
        using var rig = new EventMapRig();
        var busy = rig.Sensor(14101);
        var idle = rig.Sensor(14102);
        var id = await rig.DetectAsync(14101);

        rig.Symbols.SyncDeviceStatus(14101, EnumDeviceType.Fence, EnumDeviceStatus.ERROR);
        rig.Symbols.SyncDeviceStatus(14102, EnumDeviceType.Fence, EnumDeviceStatus.ERROR);

        Assert.Equal(EnumOperationState.ERROR, busy.OperationState);          // 「장비 오류」
        Assert.Equal(EnumEventStatus.Detecting, busy.EventStatus);            // 「탐지 중」 유지
        Assert.Equal(EnumOperationState.ERROR, idle.OperationState);
        Assert.Equal(EnumEventStatus.Normal, idle.EventStatus);

        rig.Symbols.SyncDeviceStatus(14101, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);
        rig.Symbols.SyncDeviceStatus(14102, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);

        Assert.Equal(EnumOperationState.ACTIVATED, busy.OperationState);      // 「장비 활성」
        Assert.Equal(EnumEventStatus.Detecting, busy.EventStatus);            // ACTIVATED 재계산도 큐에 살아 있는 탐지는 지우지 않는다
        Assert.Equal(EnumOperationState.ACTIVATED, idle.OperationState);
        Assert.Equal(EnumEventStatus.Normal, idle.EventStatus);
        Assert.Equal((1, 0), rig.Counts);                                     // SYNC_DEVICE 는 신호등 건수를 바꾸지 않는다

        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, busy.EventStatus);
        Assert.Equal(EnumOperationState.ACTIVATED, busy.OperationState);
    }

    [Fact]
    [Trait("Scenario", "MAP-143")]
    public async Task should_black_out_member_zone_keep_member_sensors_and_auto_recover_when_controller_faults_then_becomes_activated_map143()
    {
        using var rig = new EventMapRig();
        var (controller, ctrlSymbol) = rig.Controller(14300);
        var memberA = rig.Sensor(14301, controller: controller, groups: 43);
        var memberB = rig.Sensor(14302, controller: controller, groups: 43);
        var memberZone = rig.Zone(43);
        var otherZone = rig.Zone(44);
        rig.Sensor(14303, groups: 44);   // 다른 제어기 소속

        var fault = await rig.MalfunctionAsync(14300, "FAULT_CONTROLLER");
        await rig.ShowMalfunctionCardAsync(fault);
        rig.Symbols.SyncDeviceStatus(14300, EnumDeviceType.Controller, EnumDeviceStatus.ERROR);   // 서버가 제어기를 ERROR 로(SYNC_DEVICE)

        // 구역선 「무통신」(Blackout) · 제어기 자기 심볼도 무통신 · 소속 센서는 이벤트 · 운영 축 모두 그대로(설계)
        Assert.Equal(EnumCompositeEventStatus.Blackout, memberZone.CompositeStatus);
        Assert.Equal(EnumEventStatus.Blackout, ctrlSymbol.EventStatus);
        Assert.Equal(EnumOperationState.ERROR, ctrlSymbol.OperationState);
        Assert.All(new[] { memberA, memberB }, s =>
        {
            Assert.Equal(EnumEventStatus.Normal, s.EventStatus);
            Assert.Equal(EnumOperationState.ACTIVATED, s.OperationState);
        });
        Assert.Equal(EnumCompositeEventStatus.Normal, otherZone.CompositeStatus);
        Assert.Equal((0, 1), rig.Counts);

        // 서버 SYNC_DEVICE ACTIVATED — 호스트 처리 순서와 같다: 자동복구 → 상태 동기
        Assert.True(rig.Queue.TryAutoRecoverController(14300));
        rig.Symbols.SyncDeviceStatus(14300, EnumDeviceType.Controller, EnumDeviceStatus.ACTIVATED);
        await TestWait.UntilAsync(() => rig.ServerActions.Contains(fault) && rig.Panel.ViewModelProvider.Count == 0, "자동복구 조치보고가 끝나지 않았다");

        Assert.Equal(EnumCompositeEventStatus.Normal, memberZone.CompositeStatus);   // 자동 복구
        Assert.Equal(EnumEventStatus.Normal, ctrlSymbol.EventStatus);
        Assert.Equal(EnumOperationState.ACTIVATED, ctrlSymbol.OperationState);
        Assert.All(new[] { memberA, memberB }, s => Assert.Equal(EnumEventStatus.Normal, s.EventStatus));
        Assert.Equal((0, 0), rig.Counts);
    }

    [Theory]
    [InlineData(EnumDeviceType.Enclosure, "DOOR_OPEN", "OPEN", "DOOR_CLOSED", "CLOSED")]
    [InlineData(EnumDeviceType.Gate, "GATE_OPEN", null, "GATE_CLOSED", null)]
    [Trait("Scenario", "MAP-144")]
    public async Task should_flip_door_axis_open_then_closed_without_touching_event_operation_or_counts_when_operation_event_arrives_map144(
        EnumDeviceType type, string openReason, string? openStatus, string closeReason, string? closeStatus)
    {
        // 「문 열림 / 닫힘」 = DoorIndicatorRules.Text(DoorState). 서버 OPERATION_EVENT(component-status PATCH 뒤)만으로 바뀐다.
        using var rig = new EventMapRig();
        var symbol = rig.Device(14401, type);

        await rig.PublishOperationAsync(14401, openReason, openStatus);
        Assert.Equal(EnumDoorState.Open, symbol.DoorState);

        await rig.PublishOperationAsync(14401, closeReason, closeStatus);
        Assert.Equal(EnumDoorState.Closed, symbol.DoorState);

        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);            // 이벤트 색 없음
        Assert.Equal(EnumOperationState.ACTIVATED, symbol.OperationState);   // 운영 축 그대로
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());                     // 카드 · 큐 · 신호등 영향 없음
        Assert.Equal(-1, rig.Counts.Detection);
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Empty(rig.SoundPlays);
    }

    // ───────────────────────── EVM-023 · MAP-145 재구성(편집 모드) ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-023,MAP-145")]
    public async Task should_show_detecting_again_after_symbol_table_is_rebuilt_and_still_release_on_report_when_edit_mode_is_toggled_evm023_map145()
    {
        // 편집 모드를 끄면 지도(MapViewModel.InitializeDeviceSymbolIntegration)가 조회표를 비우고(BeginRebuild = SymbolEventManager.Dispose)
        //   같은 심볼 모델로 다시 등록한 뒤(RegisterDeviceSymbol · RegisterGroupSymbol) 큐 기준으로 다시 칠한다(CompleteRebuild).
        //   라이브러리 몫 = 재등록 · 큐 재계산(Refresh*) · 재등록 뒤 전이가 새 조회표로 닿는 것. 재등록 사이에 온 탐지도 큐에서 되살아난다.
        using var rig = new EventMapRig();
        var first = rig.Sensor(2301, groups: 23);
        var during = rig.Sensor(2302, groups: 24);
        var zoneA = rig.Zone(23);
        var zoneB = rig.Zone(24);
        var a = await rig.DetectAsync(2301);

        rig.Symbols.Dispose();                                  // 편집 끔 → 재구성 시작(조회표 비움)
        var b = await rig.DetectAsync(2302);                    // 재구성 사이에 온 다른 센서 탐지(같은 대상 중복 아님)
        rig.Symbols.RegisterDeviceSymbol(rig.ServerDevices[2301], first);
        rig.Symbols.RegisterDeviceSymbol(rig.ServerDevices[2302], during);
        rig.Symbols.RegisterGroupSymbol(23, rig.ServerDevices[2301], zoneA);
        rig.Symbols.RegisterGroupSymbol(24, rig.ServerDevices[2302], zoneB);
        rig.Symbols.RefreshGroupSymbol(23);                     // 마무리 재칠(지도 CompleteRebuild 가 구역선마다 하는 것)
        rig.Symbols.RefreshGroupSymbol(24);

        Assert.Equal(EnumEventStatus.Detecting, first.EventStatus);   // 재구성 뒤 「탐지 중」
        Assert.Equal(EnumEventStatus.Detecting, during.EventStatus);  // 사이에 버려진 전이도 큐에서 복원
        Assert.Equal(EnumCompositeEventStatus.Detecting, zoneA.CompositeStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, zoneB.CompositeStatus);
        Assert.Equal((2, 0), rig.Counts);

        rig.RemoteReport(ActionReportKind.Detection, a);              // 재구성 뒤 조치가 새 조회표로 닿는다
        Assert.Equal(EnumEventStatus.Normal, first.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zoneA.CompositeStatus);
        Assert.Equal(EnumEventStatus.Detecting, during.EventStatus);
        rig.RemoteReport(ActionReportKind.Detection, b);
        Assert.Equal(EnumEventStatus.Normal, during.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zoneB.CompositeStatus);
    }

    [Fact]
    [Trait("Scenario", "MAP-145")]
    public async Task should_keep_detecting_fault_and_error_axes_when_symbol_is_rebuilt_from_a_stale_model_of_an_error_device_map145()
    {
        // 다시 불러온 심볼 모델(영속된 옛 색 = Normal)로 재구성 — ERROR 장비는 등록만으로는 재계산하지 않으므로(RegisterDeviceSymbol 은 ACTIVATED 만)
        //   지도의 마무리 재칠(CompleteRebuild → 큐 읽기)이 상태를 되살린다. 여기서는 그 재칠과 같은 라이브러리 호출(RefreshDeviceSymbol)을 쓴다.
        using var rig = new EventMapRig();
        var device = new SensorDeviceModel { Id = 14501, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor, Status = EnumDeviceStatus.ERROR, DeviceGroups = new() { 45 } };
        rig.Unplaced(device);
        var original = new PidsSymbolModel { Title = "펜스 14501", DeviceType = EnumDeviceType.Fence };
        rig.Symbols.RegisterDeviceSymbol(device, original);
        var detection = await rig.DetectAsync(14501);
        var fault = await rig.MalfunctionAsync(14501);
        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting, original.CompositeStatus);

        rig.Symbols.Dispose();
        var reloaded = new PidsSymbolModel { Title = "펜스 14501", DeviceType = EnumDeviceType.Fence };   // 옛 값 Normal
        rig.Symbols.RegisterDeviceSymbol(device, reloaded);
        rig.Symbols.RefreshDeviceSymbol(14501, EnumDeviceType.Fence);

        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting, reloaded.CompositeStatus);   // 탐지 + 장애 그대로
        Assert.Equal(EnumEventStatus.Fault, reloaded.EventStatus);                           // 마커 점은 장애 우선(설계)
        Assert.Equal(EnumOperationState.ERROR, reloaded.OperationState);                     // 「장비 오류」 그대로

        rig.RemoteReport(ActionReportKind.Detection, detection);
        Assert.Equal(EnumCompositeEventStatus.Faulted, reloaded.CompositeStatus);
        rig.RemoteReport(ActionReportKind.Malfunction, fault);
        Assert.Equal(EnumCompositeEventStatus.Normal, reloaded.CompositeStatus);
    }

    // ───────────────────────── EVM-022 같은 센서 20건 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-022")]
    public async Task should_go_from_twenty_cards_to_nineteen_and_keep_symbol_detecting_when_one_of_twenty_detections_on_same_sensor_is_reported_evm022()
    {
        // 카탈로그 정의상 "같은 센서 ×20" — 서로 다른 이벤트 번호 20개(같은 번호 재송신 아님). 각 건은 서버 기록 뒤 NATS.
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(2201, groups: 22);
        var zone = rig.Zone(22);
        var ids = new List<int>();
        for (var i = 0; i < 20; i++)
        {
            var id = await rig.DetectAsync(2201);
            await rig.ShowDetectionCardAsync(id);
            ids.Add(id);
        }
        Assert.Equal(20, ids.Distinct().Count());
        Assert.Equal(20, rig.Panel.ViewModelProvider.Count);
        Assert.Equal((20, 0), rig.Counts);

        rig.RemoteReport(ActionReportKind.Detection, ids[7]);

        Assert.Equal(19, rig.Panel.ViewModelProvider.Count);                                   // 20 → 19
        Assert.DoesNotContain(rig.Panel.ViewModelProvider, c => c.Model!.Id == ids[7]);
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);                           // 「탐지 중」 유지
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);
        Assert.Equal((19, 0), rig.Counts);
        Assert.Equal(0, rig.SoundStops);                                                       // 소리도 유지

        foreach (var id in ids.Where(x => x != ids[7])) rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(1, rig.SoundStops);
    }

    // ───────────────────────── EVM-031 Redis EVENT_CALL 없음 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-031")]
    public async Task should_publish_no_event_call_and_carry_no_redis_dependency_when_detection_is_raised_and_reported_evm031()
    {
        // camera-popup-modes FR-19: Redis RTSP EVENT_CALL 경로 제거. 라이브러리 몫 = 탐지 → 조치 흐름 어디에서도 EVENT_CALL 류를 내지 않고,
        //   이벤트 라이브러리가 Redis · RTSP 메시지 어셈블리에 기대지 않는다. (호스트 쪽은 Bootstrapper 의 Redis 모듈 제거 — 실창 E2E 몫)
        using var rig = new EventMapRig();
        rig.Sensor(3101);
        var id = await rig.DetectAsync(3101);
        var card = await rig.ShowDetectionCardAsync(id);
        await rig.ReportLocallyAsync(card);

        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Contains(id, rig.ServerActions);
        Assert.DoesNotContain(rig.Published, m =>
            m.GetType().Name.Contains("EventCall", StringComparison.OrdinalIgnoreCase)
            || (m.GetType().Namespace ?? "").Contains("Rtsp", StringComparison.OrdinalIgnoreCase)
            || (m.GetType().Namespace ?? "").Contains("Redis", StringComparison.OrdinalIgnoreCase));
        var references = typeof(EventQueueManager).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.DoesNotContain(references, n => n.Contains("Redis", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, n => n.Contains("RtspMessage", StringComparison.OrdinalIgnoreCase));
    }

    // ───────────────────────── EVM-037 짧은 시간 50건 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-037")]
    public async Task should_list_fifty_cards_lose_none_and_finish_within_bound_when_fifty_distinct_detections_arrive_in_a_short_burst_evm037()
    {
        // 카탈로그 "5초 50건" 을 짧게 눌러(약 10 ms 간격, 1초 남짓) 보낸다 — 서로 다른 센서 50대(같은 대상 중복 없음).
        //   카드는 호스트처럼 메시지마다 버퍼에 넣고, 패널 타이머처럼 10건마다 비운다. 처리 시간(대기 제외) 상한 1 s.
        const int count = 50;
        using var rig = new EventMapRig();
        var symbols = Enumerable.Range(0, count).Select(i => rig.Sensor(37000 + i, groups: 37 + i % 5)).ToList();
        var zones = Enumerable.Range(37, 5).Select(g => rig.Zone(g)).ToList();
        var ids = Enumerable.Range(0, count).Select(i => rig.RecordDetection(37000 + i)).ToList();   // API 기록 먼저
        var reactions = new List<int>();
        rig.Panel.UpdateAction += () => reactions.Add(rig.Panel.ViewModelProvider.Count);

        var busy = new Stopwatch();
        var wall = Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            busy.Start();
            await rig.PublishDetectAsync(ids[i]);
            rig.Panel.EnqueueCard(NewDetectionCard(rig, ids[i]));
            if (i % 10 == 9) await rig.Panel.FlushPendingCardsNowAsync();
            busy.Stop();
            await Task.Delay(10);
        }
        busy.Start();
        await rig.Panel.FlushPendingCardsNowAsync();
        busy.Stop();
        wall.Stop();

        Assert.Equal(count, rig.Queue.GetTotalQueueCount());
        Assert.Equal(count, rig.Panel.ViewModelProvider.Count);                                          // 카드 50
        Assert.Equal(ids.OrderBy(x => x), rig.Panel.ViewModelProvider.Select(c => c.Model!.Id).OrderBy(x => x));   // 누락 · 중복 0
        Assert.All(rig.Panel.ViewModelProvider, c => Assert.NotNull(c.EntryId));                         // 카드 ↔ 큐 엔트리 이어짐
        Assert.Equal((count, 0), rig.Counts);
        Assert.All(symbols, s => Assert.Equal(EnumEventStatus.Detecting, s.EventStatus));
        Assert.All(zones, z => Assert.Equal(EnumCompositeEventStatus.Detecting, z.CompositeStatus));
        Assert.NotEmpty(reactions);                                                                      // 패널이 묶음마다 반응
        Assert.Equal(count, reactions.Last());
        Assert.True(busy.ElapsedMilliseconds < 1000, $"50건 처리(대기 제외) {busy.ElapsedMilliseconds} ms ≥ 1000 ms");
        Assert.True(wall.ElapsedMilliseconds < 5000, $"50건 전체 {wall.ElapsedMilliseconds} ms ≥ 5 s(카탈로그 창)");

        foreach (var id in ids) rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.All(symbols, s => Assert.Equal(EnumEventStatus.Normal, s.EventStatus));
        Assert.Equal((0, 0), rig.Counts);
    }

    // ───────────────────────── EVM-068 · 069 고장 구간은 지도에 그리지 않는다 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-068,EVM-069")]
    public async Task should_paint_only_fault_color_on_map_and_show_first_and_second_section_as_text_when_malfunction_carries_sections_evm068_069()
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(6801, groups: 68);
        var zone = rig.Zone(68);
        var id = rig.RecordMalfunction(6801, "FAULT_FENCE");
        var json = $$"""
            { "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "MALFUNCTION", "from": "PidsProxy",
              "body": { "id": {{id}}, "category_event": "malfunction", "type_event": "Fault", "action_reported": false,
                        "reason": "FAULT_FENCE", "device": { "id": 6801, "category_device": "sensor" },
                        "detail": { "first_start": 3, "first_end": 7, "second_start": 12, "second_end": 18 } } }
            """;

        await rig.PublishRawAsync(json);

        // 지도: 색 축만 바뀐다 — 구간(1차 · 2차)은 큐 엔트리에도, 지도로 가는 알림에도 실리지 않는다.
        Assert.Equal(EnumEventStatus.Fault, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Faulted, zone.CompositeStatus);
        Assert.NotNull(rig.Queue.FindEntryByEventId(id, EnumEventType.Fault));
        string[] sectionNames = { "FirstStart", "FirstEnd", "SecondStart", "SecondEnd" };
        Assert.DoesNotContain(typeof(EventEntry).GetProperties(), p => sectionNames.Contains(p.Name));
        Assert.DoesNotContain(rig.Published, m => m.GetType().GetProperties().Any(p => sectionNames.Contains(p.Name)));
        // 지도 라이브러리에는 구간을 읽는 코드가 없다(GAP-N — 그리지 않음이 현재 설계). 그리기를 넣으면 이 시험과 카탈로그를 함께 고칠 것.
        var gmapsUi = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui");
        var consumers = Directory.EnumerateFiles(gmapsUi, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => { var text = File.ReadAllText(f); return sectionNames.Any(text.Contains) || text.Contains("first_start") || text.Contains("second_start"); })
            .ToList();
        Assert.True(consumers.Count == 0, "지도(GMaps.Ui)가 고장 구간을 읽는다: " + string.Join(", ", consumers));

        // 글: 호스트(ProcessFault)가 같은 body 로 만드는 카드 모델 · 콘솔 상세에는 구간이 나온다.
        var dto = JObject.Parse(json)["body"]!.ToObject<MalfunctionEventDto>()!;
        var model = dto.ToMalfunctionEventModel(rig.Devices);
        var card = new MalfunctionEventCardViewModel(model);
        Assert.Equal((3, 7, 12, 18), (card.FirstStart, card.FirstEnd, card.SecondStart, card.SecondEnd));
        var detail = new EventDetailViewModel(new ConsoleDetailPresenter());
        detail.Load(EventDetailKind.Malfunction, new object[] { new MalfunctionEventViewModel(model) }, canEdit: true, canReport: true, actionCount: 0);
        var fields = detail.Sections.Single(s => s.Title == "고장 구간").Fields;
        Assert.Equal("3 → 7", fields.Single(f => f.Label == "1차 선").Text);     // EVM-068
        Assert.Equal("12 → 18", fields.Single(f => f.Label == "2차 선").Text);   // EVM-069

        rig.RemoteReport(ActionReportKind.Malfunction, id);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
    }

    // ───────────────────────── EVM-097 다른 부대 조치보고 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-097")]
    public async Task should_keep_card_symbol_and_lamp_when_action_report_of_another_unit_reaches_the_library_evm097()
    {
        // NATS 구독은 자기 부대 · global 만 덮는다(Nats/Tests/NatsUnitSubjectScenarioTests). 여기서는 그 다음 층 —
        //   혹시 다른 부대(unit002) 조치보고 봉투가 라이브러리 수신 서비스에 닿아도 카드 · 큐 · 심볼 · 신호등을 건드리지 않는다.
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(9701, groups: 97);
        var zone = rig.Zone(97);
        var id = await rig.DetectAsync(9701);
        var card = await rig.ShowDetectionCardAsync(id);
        var report = $$"""
            { "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "ACTION_REPORT", "from": "GIS",
              "body": { "id": 97001, "type_event": "Action", "content": "현장 확인", "user": "unit002-kim",
                        "from_event": { "id": {{id}}, "category_event": "detection", "action_reported": true,
                                        "device": { "id": 9701, "category_device": "sensor" } } } }
            """;

        await rig.PublishRawAsync(report, subject: "sensorway.unit002.all.event.action-report");

        Assert.Same(card, Assert.Single(rig.Panel.ViewModelProvider));   // 카드 유지
        Assert.Equal(1, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Detecting, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Detecting, zone.CompositeStatus);
        Assert.Equal((1, 0), rig.Counts);
        Assert.Empty(rig.ActionReported);                                 // 이벤트 창 닫기 신호도 없다
        Assert.Equal(0, rig.SoundStops);

        rig.RemoteReport(ActionReportKind.Detection, id);                 // 자기 부대 조치로 닫는다
        Assert.Empty(rig.Panel.ViewModelProvider);
    }

    // ───────────────────────── EVM-127 · 128 조치 뒤 SYNC_DETECTION · 조치 삭제 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-127")]
    public async Task should_refresh_photo_before_report_and_ignore_updated_and_deleted_sync_after_report_when_server_sync_detection_follows_action_evm127()
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(12701);
        var id = await rig.DetectAsync(12701);
        var model = new DetectionEventModel { Id = id, MessageType = EnumEventType.Intrusion, Device = rig.ServerDevices[12701], Thumbnail = "/api/thumbnails/images/before.jpg" };
        var card = new DetectionEventCardViewModel(model);
        rig.Panel.EnqueueCard(card);
        await rig.Panel.FlushPendingCardsNowAsync();
        var (service, handler, api) = SyncService(rig);
        api.Setup(a => a.GetDetectionEventByIdAsync(id, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<DetectionEventDto> { Success = true, Data = new DetectionEventDto { Id = id, ActionReported = "False", Detail = new DetectionDetailDto { Thumbnail = "/api/thumbnails/images/after.jpg", FrameWidth = 1280, FrameHeight = 720 } } });

        await handler(Sync("UPDATED", id));                  // 조치 전 — PTZ 회전 뒤 썸네일 갱신
        await service.LastProcessingTask!;
        Assert.Equal("/api/thumbnails/images/after.jpg", model.Thumbnail);   // 카드 사진 갱신

        await rig.ReportLocallyAsync(card);                  // 조치 → 서버가 action_reported 를 참으로 다시 계산하고 SYNC_DETECTION 을 낸다
        Assert.Empty(rig.Panel.ViewModelProvider);
        var noticesBefore = rig.Published.OfType<EventDeletedOnServerNotice>().Count();

        await handler(Sync("UPDATED", id));                  // 조치 뒤 UPDATED — 큐에 없으므로 재조회 없음
        await handler(Sync("DELETED", id));                  // 조치 뒤 DELETED — 닫을 카드가 없으므로 토스트 없음
        if (service.LastProcessingTask is { } last) await last;

        api.Verify(a => a.GetDetectionEventByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);   // 조치 전 1회뿐
        Assert.Empty(rig.Panel.ViewModelProvider);                                                        // 카드가 되살아나지 않는다
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(noticesBefore, rig.Published.OfType<EventDeletedOnServerNotice>().Count());
    }

    [Fact]
    [Trait("Scenario", "EVM-128")]
    public async Task should_not_revive_card_or_symbol_when_the_only_action_is_deleted_and_server_resyncs_detection_unreported_evm128()
    {
        using var rig = new EventMapRig();
        var symbol = rig.Sensor(12801, groups: 28);
        var zone = rig.Zone(28);
        var id = await rig.DetectAsync(12801);
        var card = await rig.ShowDetectionCardAsync(id);
        await rig.ReportLocallyAsync(card);
        Assert.Equal(new[] { id }, rig.ServerActions);
        var plays = rig.SoundPlays.Count;
        var (service, handler, api) = SyncService(rig);

        // 서버: 하나뿐인 조치 삭제 → 원본 action_reported 를 거짓으로 다시 계산(서버 몫) → 그 탐지 행 UPDATED 알림
        rig.ServerActions.Remove(id);
        api.Setup(a => a.GetDetectionEventByIdAsync(id, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<DetectionEventDto> { Success = true, Data = new DetectionEventDto { Id = id, ActionReported = "False" } });
        await handler(Sync("UPDATED", id));
        if (service.LastProcessingTask is { } last) await last;

        Assert.Empty(rig.Panel.ViewModelProvider);                       // 카드 안 살아남
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);
        Assert.Equal(plays, rig.SoundPlays.Count);                       // 다시 울리지 않는다
        api.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);   // 활성 아님 → 재조회도 없다
    }

    // ───────────────────────── EVM-129 억제 장비(서버 202 · 발행 없음) ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-129")]
    public async Task should_change_nothing_on_card_symbol_window_lamp_or_sound_when_server_accepts_suppressed_detection_with_202_and_publishes_nothing_evm129()
    {
        // 억제 차단은 서버 REST 게이트 하나다 — POST events/detections 가 202 로 받고 DETECT 를 내지 않는다.
        //   GIS 는 스스로 거르는 길이 없다(EVM-130: 직접 온 NATS 는 칠한다). 그래서 GIS 쪽 기대 = "아무것도 오지 않으니 아무것도 바뀌지 않는다".
        using var popup = new EventPopupRig();
        var rig = popup.Map;
        popup.Mappings.CamerasByGroup[29] = new() { 3 };
        var suppressed = rig.Sensor(12901, groups: 29);
        var zone = rig.Zone(29);

        // 억제 일정 알림(SYNC_EVENT_SUPPRESSION)은 지도 수신 서비스가 보더라도 아무것도 칠하지 않는다
        await rig.PublishRawAsync("""{ "id": "sup-129", "m_type": "PUB", "cmd": "SYNC_EVENT_SUPPRESSION", "body": { "action": "CREATED", "resource_id": 129, "device_id": 12901, "suppressing": true } }""",
                                  "sensorway.unit001.all.sync.suppression");
        // R-det → 서버 202(억제) — 이벤트 행도 NATS 발행도 없다(가짜 서버 표에 기록하지 않음)
        await popup.SettleAsync();

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(0, rig.Panel.PendingCardCount);
        Assert.Equal(EnumEventStatus.Normal, suppressed.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((-1, -1), rig.Counts);                              // 신호등 통지 자체가 없다
        Assert.Empty(rig.SoundPlays);
        Assert.Empty(popup.Windows.Requests);                             // 이벤트 창 없음
        Assert.DoesNotContain(rig.Published, m => m is EventEntryEnqueuedMessage);

        // 대조: 억제가 풀린 뒤(서버가 다시 발행) 같은 센서 탐지는 평소대로 전부 반응한다
        var id = await rig.DetectAsync(12901);
        await popup.SettleAsync();
        Assert.Equal(EnumEventStatus.Detecting, suppressed.EventStatus);
        Assert.Equal((1, 0), rig.Counts);
        Assert.Single(popup.Windows.Requests);
        rig.RemoteReport(ActionReportKind.Detection, id);
        Assert.Equal(EnumEventStatus.Normal, suppressed.EventStatus);
    }
}

/// <summary>배경 작업(자동복구 조치보고 등)을 짧게 기다리는 도우미.</summary>
internal static class TestWait
{
    public static async Task UntilAsync(Func<bool> condition, string what, int timeoutMs = 5000)
    {
        var ok = await Task.Run(() => SpinWait.SpinUntil(condition, TimeSpan.FromMilliseconds(timeoutMs)));
        Assert.True(ok, what);
    }
}
