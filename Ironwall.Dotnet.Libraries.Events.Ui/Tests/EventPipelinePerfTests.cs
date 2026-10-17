using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Events.Ui.Modules;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Utils.Perf;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 이벤트 파이프라인 처리량 · 순서 · 누락 · 중복 헤드리스 시험 + 계측(PerfTrace) 단계 순서
                  (카탈로그 §7 PERF-014 · PERF-015, 단계 계측 PERF-001 ~ 006)
                  - 실제 큐(EQM) · 심볼 관리자(SEM) · NATS 수신 서비스 · 카드 패널 배선(EventMapRig) 에 가짜 NATS 봉투를 초당 1 · 20 · 200 건,
                    순간 100 건으로 넣는다. 카드는 호스트처럼 메시지마다 EnqueueCard 하고, 패널의 묶음 타이머처럼 150 ms 마다 비운다.
                  - "UI 스레드" 일(카드 패널)은 호스트에서 한 스레드라, 시험도 보내기 · 비우기를 한 흐름에서 차례로 한다(동시 접근 없음).
                    실제 스레드 넘김 지연은 호스트 실측(계측 jsonl)이 잰다.
                  - 탐지는 서로 다른 센서로 보낸다(조치보고 전 같은 대상 중복 송신 없음 — 운영 전제). 중복은 견딤(PERF-015)에서만.
                  - 창을 띄우지 않는다(Window.Show · 화면 밖 창 없음). 지도 DB · 운영 서버 · 실제 NATS 에 붙지 않는다.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // EventMapRig 가 IoC.GetInstance(전역 정적)를 바꾼다
public class EventPipelinePerfTests
{
    private const int ZoneCount = 20;
    private const int FlushIntervalMs = 150;   // EventCardListPanelViewModel.BATCH_INTERVAL_MS 와 같다
    private static int s_nextId = 700_000_000 + Random.Shared.Next(0, 1000) * 100_000;   // 다른 시험의 이벤트 번호와 겹치지 않게
    private static int s_nextDevice = 600_000;
    private static readonly EventCardBatchBuffer<object> s_intervalRule = new();   // 간격 규칙만 쓴다(버퍼로 쓰지 않음)

    private readonly ITestOutputHelper _output;

    public EventPipelinePerfTests(ITestOutputHelper output) => _output = output;

    // ───────────────────────── 장치 ─────────────────────────

    private sealed record Planned(int EventId, int DeviceId, int Group, PidsSymbolModel Symbol, DetectionEventCardViewModel Card);

    private static int NextIds(int count) => Interlocked.Add(ref s_nextId, count) - count + 1;

    /// <summary>서로 다른 센서 <paramref name="count"/> 개(구역 20 개에 고루) + 서버 기록 + 카드(호스트가 만들 것)를 미리 만든다.</summary>
    private static List<Planned> Plan(EventMapRig rig, int count, Dictionary<int, PidsGroupSymbolModel> zones)
    {
        int firstId = NextIds(count);
        int firstDevice = Interlocked.Add(ref s_nextDevice, count) - count + 1;
        var list = new List<Planned>(count);
        for (int i = 0; i < count; i++)
        {
            int deviceId = firstDevice + i;
            int group = 1 + i % ZoneCount;
            if (!zones.ContainsKey(group)) zones[group] = rig.Zone(group);
            var symbol = rig.Sensor(deviceId, groups: group);
            int eventId = firstId + i;
            rig.ServerEvents[(ActionReportKind.Detection, eventId)] = (deviceId, "Intrusion");   // API 기록 먼저(가짜 서버)
            list.Add(new Planned(eventId, deviceId, group, symbol, Card(rig, eventId)));
        }
        return list;
    }

    private static DetectionEventCardViewModel Card(EventMapRig rig, int eventId)
    {
        var (deviceId, _) = rig.ServerEvents[(ActionReportKind.Detection, eventId)];
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(eventId);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        model.SetupGet(m => m.Device).Returns(rig.ServerDevices[deviceId]);
        model.SetupProperty(m => m.Status);
        return new DetectionEventCardViewModel(model.Object);
    }

    private static MalfunctionEventCardViewModel MalfunctionCard(EventMapRig rig, int eventId)
    {
        var (deviceId, _) = rig.ServerEvents[(ActionReportKind.Malfunction, eventId)];
        var model = new Mock<IMalfunctionEventModel>();
        model.SetupGet(m => m.Id).Returns(eventId);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Fault);
        model.SetupGet(m => m.Device).Returns(rig.ServerDevices[deviceId]);
        model.SetupProperty(m => m.Status);
        return new MalfunctionEventCardViewModel(model.Object);
    }

    /// <summary>JIT · 첫 호출 비용을 시험 측정 밖으로 — 한 건 보내고 조치로 닫는다.</summary>
    private static async Task WarmUpAsync(EventMapRig rig, Dictionary<int, PidsGroupSymbolModel> zones)
    {
        var warm = Plan(rig, 1, zones)[0];
        await rig.PublishDetectAsync(warm.EventId);
        rig.Panel.EnqueueCard(warm.Card);
        await rig.Panel.FlushPendingCardsNowAsync();
        rig.RemoteReport(ActionReportKind.Detection, warm.EventId);
    }

    private sealed record RunResult(double ElapsedMs, double BusyMs, int Messages)
    {
        /// <summary>파이프라인이 실제로 일한 시간 기준 처리 능력(건/초).</summary>
        public double Capacity => BusyMs <= 0 ? double.PositiveInfinity : Messages / (BusyMs / 1000d);
    }

    /// <summary>
    /// 초당 <paramref name="ratePerSecond"/> 건(0 = 한꺼번에)으로 보낸다. 메시지마다 NATS DETECT → 카드 EnqueueCard(호스트),
    /// 150 ms 마다 묶음 비우기(패널 타이머). 끝나면 한 번 더 비운다.
    /// </summary>
    private static async Task<RunResult> RunAsync(EventMapRig rig, IReadOnlyList<Planned> plan, int ratePerSecond)
    {
        var wall = Stopwatch.StartNew();
        var busy = new Stopwatch();
        double nextFlushAt = FlushIntervalMs;
        int sent = 0;
        while (sent < plan.Count)
        {
            int due = ratePerSecond <= 0 ? plan.Count : Math.Min(plan.Count, (int)(wall.Elapsed.TotalSeconds * ratePerSecond) + 1);
            busy.Start();
            while (sent < due)
            {
                var p = plan[sent++];
                await rig.PublishDetectAsync(p.EventId);
                rig.Panel.EnqueueCard(p.Card);
            }
            if (wall.Elapsed.TotalMilliseconds >= nextFlushAt)
            {
                // 패널 타이머와 같은 적응형 간격 — 묶음이 크면 150 → 300 → 500 ms 로 늘린다(EventCardBatchBuffer.CalculateInterval)
                int pending = rig.Panel.PendingCardCount;
                await rig.Panel.FlushPendingCardsNowAsync();
                nextFlushAt = wall.Elapsed.TotalMilliseconds + (pending == 0 ? FlushIntervalMs : s_intervalRule.CalculateInterval(pending));
            }
            busy.Stop();
            if (sent < plan.Count) await Task.Delay(5);
        }
        busy.Start();
        await rig.Panel.FlushPendingCardsNowAsync();
        busy.Stop();
        return new RunResult(wall.Elapsed.TotalMilliseconds, busy.Elapsed.TotalMilliseconds, plan.Count);
    }

    /// <summary>내 이벤트(종류 + id)만 — 같은 프로세스의 다른 시험 기록을 뺀다.</summary>
    private static List<PerfTraceRecord> Mine(PerfTraceCapture capture, IEnumerable<int> eventIds, string kind = PerfKinds.Detection)
    {
        var ids = new HashSet<string>(eventIds.Select(i => i.ToString()));
        return capture.Records.Where(r => r.Kind == kind && r.Id is not null && ids.Contains(r.Id)).ToList();
    }

    // ───────────────────────── PERF-014 처리량 · 순서 · 누락 0 ─────────────────────────

    [Fact]
    [Trait("Scenario", "PERF-014,PERF-007")]
    public Task should_keep_order_and_lose_nothing_when_one_detection_per_second_arrives() => AssertThroughputAsync(ratePerSecond: 1, count: 3, assertCapacity: false);

    [Fact]
    [Trait("Scenario", "PERF-014,PERF-009")]
    public Task should_keep_order_and_lose_nothing_when_twenty_detections_per_second_arrive() => AssertThroughputAsync(ratePerSecond: 20, count: 40, assertCapacity: true);

    [Fact]
    [Trait("Scenario", "PERF-014")]
    public Task should_process_two_hundred_per_second_in_order_without_loss_when_high_rate_arrives() => AssertThroughputAsync(ratePerSecond: 200, count: 400, assertCapacity: true);

    [Fact]
    [Trait("Scenario", "PERF-014,PERF-010")]
    public Task should_absorb_burst_of_hundred_in_order_without_loss_when_all_arrive_at_once() => AssertThroughputAsync(ratePerSecond: 0, count: 100, assertCapacity: true);

    private async Task AssertThroughputAsync(int ratePerSecond, int count, bool assertCapacity)
    {
        using var rig = new EventMapRig();
        var zones = new Dictionary<int, PidsGroupSymbolModel>();
        await WarmUpAsync(rig, zones);
        var plan = Plan(rig, count, zones);
        using var capture = PerfTrace.Capture();

        var run = await RunAsync(rig, plan, ratePerSecond);

        // 누락 0 — 큐 · 카드 · 심볼 · 구역선 · 신호등
        Assert.Equal(count, rig.Queue.GetTotalQueueCount());
        Assert.Equal(count, rig.Panel.ViewModelProvider.Count);
        Assert.Equal((count, 0), rig.Counts);
        Assert.All(plan, p => Assert.Equal(EnumEventStatus.Detecting, p.Symbol.EventStatus));
        Assert.All(zones.Values, z => Assert.Equal(EnumCompositeEventStatus.Detecting, z.CompositeStatus));
        Assert.All(plan, p => Assert.NotNull(p.Card.EntryId));   // 카드 ↔ 큐 엔트리 1:1 매칭(자동 조치보고 · 원격 해제가 닿는다)

        // 순서 보존 — 최신이 맨 위이므로 아래에서 위로 읽으면 보낸 순서
        Assert.Equal(plan.Select(p => p.EventId), rig.Panel.ViewModelProvider.Select(c => c.Model!.Id).Reverse());

        // 계측 대조 — 보낸 모든 이벤트가 수신 → 큐 → 심볼 → 카드 단계를 남기고 순서가 뒤집히지 않는다
        var records = Mine(capture, plan.Select(p => p.EventId));
        var report = PerfTraceAnalyzer.Analyze(records);
        _output.WriteLine($"rate={ratePerSecond}/s count={count} wall={run.ElapsedMs:0} ms busy={run.BusyMs:0} ms capacity={run.Capacity:0}/s");
        _output.WriteLine(report.ToMarkdown());
        Assert.Equal(count, report.ReceivedCount);
        Assert.Empty(report.MissingEnqueued);
        Assert.Empty(report.MissingCards);
        Assert.DoesNotContain(records, r => r.Stage == PerfStages.CardDropped);   // 묶음 버퍼 상한(200)에 닿지 않았다
        Assert.Empty(report.DuplicateCards);
        Assert.Empty(report.DuplicateEnqueues);
        Assert.Equal(0, report.EnqueueOrderInversions);
        Assert.Equal(0, report.CardOrderInversions);
        Assert.Equal(count, report.Latency("수신→심볼")!.Count);   // 센서 심볼 칠하기가 이벤트 문맥으로 남는다
        var toCard = report.Latency("수신→카드")!;
        Assert.Equal(count, toCard.Count);
        Assert.True(toCard.P95Ms <= 1000, $"수신→카드 p95 {toCard.P95Ms:0} ms > 1 s (PERF-001 기준)");
        Assert.True(report.CatchUpMs <= 1000, $"마지막 수신 → 마지막 카드 {report.CatchUpMs:0} ms");
        if (assertCapacity)
            Assert.True(run.Capacity >= 200, $"처리 능력 {run.Capacity:0}건/초 < 200건/초 (busy {run.BusyMs:0} ms / {count}건)");

        // 끝 — 전부 조치로 닫으면 큐 0 · 심볼 정상 · 조치보고→정상 복귀가 이벤트마다 남는다(PERF-006 단계)
        foreach (var p in plan) rig.RemoteReport(ActionReportKind.Detection, p.EventId);
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.All(plan, p => Assert.Equal(EnumEventStatus.Normal, p.Symbol.EventStatus));
        Assert.All(zones.Values, z => Assert.Equal(EnumCompositeEventStatus.Normal, z.CompositeStatus));
        var closed = PerfTraceAnalyzer.Analyze(Mine(capture, plan.Select(p => p.EventId)));
        Assert.Equal(count, closed.Latency("조치보고→심볼정상")!.Count);
    }

    // ───────────────────────── PERF-015 중복 · 순서 뒤집힘 ─────────────────────────

    [Fact]
    [Trait("Scenario", "PERF-015")]
    public async Task should_keep_one_entry_one_card_and_record_dedup_when_same_event_is_received_again()
    {
        using var rig = new EventMapRig();
        var zones = new Dictionary<int, PidsGroupSymbolModel>();
        var p = Plan(rig, 1, zones)[0];
        using var capture = PerfTrace.Capture();

        var json = rig.DetectJson(p.EventId, envelopeId: $"env-dup-{p.EventId}");
        await rig.PublishRawAsync(json);
        await rig.PublishRawAsync(json);                                       // 같은 봉투 다시(이중 subject · 재전송)
        await rig.PublishDetectAsync(p.EventId, envelopeId: $"env-new-{p.EventId}");   // 같은 이벤트를 새 봉투로(매니저 재전송)
        rig.Panel.EnqueueCard(p.Card);
        rig.Panel.EnqueueCard(Card(rig, p.EventId));                           // 호스트가 카드도 두 번
        await rig.Panel.FlushPendingCardsNowAsync();
        rig.Panel.EnqueueCard(Card(rig, p.EventId));                           // 다음 묶음에 또
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.Equal(1, rig.Queue.GetTotalQueueCount());
        Assert.Single(rig.Panel.ViewModelProvider);
        Assert.Equal((1, 0), rig.Counts);
        Assert.Equal(EnumEventStatus.Detecting, p.Symbol.EventStatus);
        Assert.Equal(new[] { EnumEventType.Intrusion }, rig.SoundPlays);       // 알람도 한 번

        var mine = Mine(capture, new[] { p.EventId });
        var where = mine.Where(r => r.Stage == PerfStages.DedupSkipped).Select(r => r.ExtraString("where")).ToList();
        Assert.Equal(new[] { "queue_envelope", "queue_event", "card_duplicate", "card_duplicate" }, where);
        var report = PerfTraceAnalyzer.Analyze(mine);
        Assert.Empty(report.DuplicateCards);
        Assert.Empty(report.DuplicateEnqueues);
        Assert.Equal(1, report.DuplicateReceives);
        Assert.Equal(4, report.DedupSkipped);

        rig.RemoteReport(ActionReportKind.Detection, p.EventId);
        Assert.Equal(EnumEventStatus.Normal, p.Symbol.EventStatus);
    }

    [Fact]
    [Trait("Scenario", "PERF-015")]
    public async Task should_end_normal_without_card_when_action_report_overtakes_its_detection()
    {
        using var rig = new EventMapRig();
        var zones = new Dictionary<int, PidsGroupSymbolModel>();
        var p = Plan(rig, 1, zones)[0];
        var zone = zones[p.Group];
        using var capture = PerfTrace.Capture();

        rig.RemoteReport(ActionReportKind.Detection, p.EventId);   // 조치보고가 먼저 닿았다(다른 GIS 가 바로 조치 · 늦은 DETECT)
        await rig.PublishDetectAsync(p.EventId);
        rig.Panel.EnqueueCard(p.Card);
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
        Assert.Empty(rig.Panel.ViewModelProvider);
        Assert.Equal(EnumEventStatus.Normal, p.Symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);
        Assert.Equal((0, 0), rig.Counts);

        var mine = Mine(capture, new[] { p.EventId });
        var stages = mine.OrderBy(r => r.T).Select(r => r.Stage + (r.ExtraString("cmd") is { } c ? ":" + c : "")).ToList();
        Assert.Equal("nats_received:ACTION_REPORT", stages[0]);
        Assert.Contains("nats_received:DETECT", stages);
        Assert.Contains(mine, r => r.Stage == PerfStages.DedupSkipped && r.ExtraString("where") == "card_already_reported");
        var last = mine.Where(r => r.Stage == PerfStages.SymbolState && r.ExtraString("target")!.StartsWith("device:")).OrderBy(r => r.T).Last();
        Assert.Equal("Normal", last.ExtraString("state"));            // 잠깐 칠했다가도 마지막은 정상
        var report = PerfTraceAnalyzer.Analyze(mine);
        Assert.Empty(report.MissingCards);                            // 일부러 거른 카드는 누락이 아니다
    }

    [Fact]
    [Trait("Scenario", "PERF-015,PERF-005")]
    public async Task should_keep_fault_color_when_late_sync_device_activated_arrives_before_report()
    {
        using var rig = new EventMapRig();
        int deviceId = Interlocked.Increment(ref s_nextDevice);
        var symbol = rig.Sensor(deviceId, groups: 3);
        var zone = rig.Zone(3);
        int eventId = NextIds(1);
        rig.ServerEvents[(ActionReportKind.Malfunction, eventId)] = (deviceId, "FAULT_FENCE");
        using var capture = PerfTrace.Capture();

        await rig.PublishMalfunctionAsync(eventId);
        rig.Panel.EnqueueCard(MalfunctionCard(rig, eventId));
        await rig.Panel.FlushPendingCardsNowAsync();
        // 늦은 SYNC_DEVICE — 장애 전에 찍힌 ACTIVATED 가 장애 뒤에 닿았다(호스트 라우터 → SyncDeviceStatus). 큐가 권위라 장애색 유지.
        rig.Symbols.SyncDeviceStatus(deviceId, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);

        Assert.Equal(EnumEventStatus.Fault, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Faulted, zone.CompositeStatus);

        rig.RemoteReport(ActionReportKind.Malfunction, eventId);
        Assert.Equal(EnumEventStatus.Normal, symbol.EventStatus);
        Assert.Equal(EnumCompositeEventStatus.Normal, zone.CompositeStatus);

        var report = PerfTraceAnalyzer.Analyze(Mine(capture, new[] { eventId }, PerfKinds.Malfunction));
        Assert.Equal(1, report.Latency("수신→큐")!.Count);
        Assert.Equal(1, report.Latency("수신→심볼")!.Count);
        Assert.Equal(1, report.Latency("수신→카드")!.Count);
        Assert.Equal(1, report.Latency("조치보고→심볼정상")!.Count);
        Assert.Contains(capture.Records, r => r.Stage == PerfStages.SymbolState && r.Kind == PerfKinds.Device
                                              && r.Id == $"device:Fence:{deviceId}" && r.ExtraString("state") == "Faulted");   // 문맥 없는 재칠은 대상 id 로
    }

    [Fact]
    [Trait("Scenario", "PERF-015")]
    public async Task should_keep_zone_detecting_when_report_for_one_sensor_and_detection_of_another_interleave()
    {
        using var rig = new EventMapRig();
        var zones = new Dictionary<int, PidsGroupSymbolModel>();
        var plan = Plan(rig, ZoneCount * 2, zones);   // 구역마다 센서 2개(a = i, b = i + 20)
        using var capture = PerfTrace.Capture();

        // 구역마다 a 탐지 → (b 탐지, a 조치) 를 번갈아 순서를 뒤집어 넣는다
        for (int i = 0; i < ZoneCount; i++) await rig.PublishDetectAsync(plan[i].EventId);
        for (int i = 0; i < ZoneCount; i++)
        {
            if (i % 2 == 0)
            {
                await rig.PublishDetectAsync(plan[i + ZoneCount].EventId);
                rig.RemoteReport(ActionReportKind.Detection, plan[i].EventId);
            }
            else
            {
                rig.RemoteReport(ActionReportKind.Detection, plan[i].EventId);
                await rig.PublishDetectAsync(plan[i + ZoneCount].EventId);
            }
        }

        Assert.Equal(ZoneCount, rig.Queue.GetTotalQueueCount());
        Assert.All(zones.Values, z => Assert.Equal(EnumCompositeEventStatus.Detecting, z.CompositeStatus));   // b 가 살아 있다
        Assert.All(plan.Take(ZoneCount), p => Assert.Equal(EnumEventStatus.Normal, p.Symbol.EventStatus));
        Assert.All(plan.Skip(ZoneCount), p => Assert.Equal(EnumEventStatus.Detecting, p.Symbol.EventStatus));
        var report = PerfTraceAnalyzer.Analyze(Mine(capture, plan.Select(p => p.EventId)));
        Assert.Equal(ZoneCount, report.Latency("조치보고→심볼정상")!.Count);
        Assert.Empty(report.DuplicateEnqueues);
    }

    [Fact]
    [Trait("Scenario", "PERF-015")]
    public async Task should_ignore_sync_detection_before_its_detect_and_refetch_once_after()
    {
        using var rig = new EventMapRig();
        var zones = new Dictionary<int, PidsGroupSymbolModel>();
        var p = Plan(rig, 1, zones)[0];
        Func<MessageArgsModel, Task>? handler = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handler = h);
        var api = new Mock<IEventApiService>();
        api.Setup(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<DetectionEventDto> { Success = false });
        await new DetectionSyncNatsService(null, nats.Object, rig.Queue, api.Object).StartService();
        using var capture = PerfTrace.Capture();
        string Sync() => $$"""{ "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "SYNC_DETECTION", "from": "server", "body": { "action": "UPDATED", "resource_id": {{p.EventId}} } }""";

        await handler!(new MessageArgsModel("sensorway.unit001.all.sync.detection", null, Sync()));   // DETECT 보다 먼저 닿은 SYNC
        await rig.PublishDetectAsync(p.EventId);
        await handler!(new MessageArgsModel("sensorway.unit001.all.sync.detection", null, Sync()));
        await Task.Delay(50);

        api.Verify(a => a.GetDetectionEventByIdAsync(p.EventId, It.IsAny<CancellationToken>()), Times.Once);   // 큐에 들어간 뒤 것만 재조회
        Assert.Equal(EnumEventStatus.Detecting, p.Symbol.EventStatus);
        var cmds = Mine(capture, new[] { p.EventId }).Where(r => r.Stage == PerfStages.NatsReceived).OrderBy(r => r.T).Select(r => r.ExtraString("cmd"));
        Assert.Equal(new[] { "SYNC_DETECTION", "DETECT", "SYNC_DETECTION" }, cmds);
    }

    // ───────────────────────── 계측 단계 순서 ─────────────────────────

    private sealed class SilentHost : ICameraPopupHost
    {
        public CameraPopupHostState State => CameraPopupHostState.Running;
        public string? StateReason => null;
        public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived { add { } remove { } }
        public int OpenedCount;
        public void Start() { }
        public void Restart() { }
        public IFrameSource? OpenOverlay(OverlayStreamRequest request) => null;
        public void CloseOverlay(string streamId) { }
        public void OpenEventWindow(OpenEventWindow request) => Interlocked.Increment(ref OpenedCount);
        public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) { }
        public void BringEventWindowToFront(string eventKey) { }
        public void SetTheme(string theme) { }
        public void SendPtz(PtzCommand command) { }
        public void Dispose() { }
    }

    private sealed class OneCameraSource : IEventMappingCameraSource
    {
        public Task<IReadOnlyList<MappingCameraEntry>> GetCamerasForGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken token = default)
            => Task.FromResult<IReadOnlyList<MappingCameraEntry>>(new[] { new MappingCameraEntry(77, 1, 77, 0, true, null, null, null) });
        public void Invalidate() { }
    }

    private sealed class OneCameraDirectory : IEventWindowDeviceDirectory
    {
        public ICameraDeviceModel? FindCamera(int cameraId) => cameraId == 77 ? new CameraDeviceModel(77)
        {
            DeviceName = "카메라 77", DeviceType = EnumDeviceType.IpCamera, Category = EnumCameraType.FIXED,
            IpAddress = "10.0.0.77", IpPort = 80, Urls = new CameraUrlsModel { RtspMain = "rtsp://10.0.0.77/main" },
        } : null;
        public string? DeviceName(int deviceId, EnumDeviceType deviceType) => $"펜스 센서 #{deviceId}";
        public string? GroupName(int groupId) => $"구역-{groupId:00}";
    }

    private sealed class OneMonitor : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors()
            => new[] { new DisplayMonitorInfo(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true) };
    }

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-003,PERF-006")]
    public async Task should_trace_detection_stages_in_pipeline_order_with_same_event_id()
    {
        using var rig = new EventMapRig();
        var zones = new Dictionary<int, PidsGroupSymbolModel>();
        var p = Plan(rig, 1, zones)[0];
        var host = new SilentHost();
        using var windows = new EventWindowManager(host, new DelegateCameraPopupSettingsSource(() => new CameraPopupSettings { Mode = CameraPopupMode.Self }),
                                                   new OneMonitor());
        var trigger = new EventWindowTrigger(windows, new OneCameraSource(), new OneCameraDirectory(), () => false);
        EventUiModule.WireEventWindows(rig.Queue, rig.Panel, trigger, null, null);   // 호스트 배선과 같다(큐 적재 → 창, 조치보고 → 닫기)
        using var capture = PerfTrace.Capture();

        await rig.PublishDetectAsync(p.EventId);
        rig.Panel.EnqueueCard(p.Card);
        await rig.Panel.FlushPendingCardsNowAsync();
        var sw = Stopwatch.StartNew();
        while (!Mine(capture, new[] { p.EventId }).Any(r => r.Stage == PerfStages.PopupOpenSent) && sw.ElapsedMilliseconds < 5000) await Task.Delay(10);
        rig.RemoteReport(ActionReportKind.Detection, p.EventId);

        var mine = Mine(capture, new[] { p.EventId }).OrderBy(r => r.T).ToList();
        int At(string stage, Func<PerfTraceRecord, bool>? also = null) => mine.FindIndex(r => r.Stage == stage && (also?.Invoke(r) ?? true));
        int received = At(PerfStages.NatsReceived, r => r.ExtraString("cmd") == "DETECT");
        int enqueued = At(PerfStages.Enqueued);
        int deviceSymbol = At(PerfStages.SymbolState, r => r.ExtraString("target") == $"device:Fence:{p.DeviceId}" && r.ExtraString("state") == "Detecting");
        int zoneSymbol = At(PerfStages.SymbolState, r => r.ExtraString("target") == $"group:{p.Group}" && r.ExtraString("state") == "Detecting");
        int requested = At(PerfStages.PopupRequested);
        int sent = At(PerfStages.PopupOpenSent);
        int card = At(PerfStages.CardAdded);
        int report = At(PerfStages.NatsReceived, r => r.ExtraString("cmd") == "ACTION_REPORT");
        int normal = At(PerfStages.SymbolState, r => r.ExtraString("target") == $"device:Fence:{p.DeviceId}" && r.ExtraString("state") == "Normal");
        _output.WriteLine(string.Join("\n", mine.Select(r => $"{r.T:HH:mm:ss.fffffff} {r.Stage} {r.ExtraString("cmd") ?? r.ExtraString("target")} {r.ExtraString("state")}")));

        Assert.True(received >= 0 && enqueued > received, "수신 → 큐");
        Assert.True(deviceSymbol > enqueued && zoneSymbol > enqueued, "큐 → 심볼 · 구역선(이벤트 문맥으로)");
        Assert.True(requested > enqueued && sent > requested, "큐 → 팝업 요청 → 호스트로 보냄");
        Assert.True(card > enqueued, "큐 → 카드");
        Assert.True(report > card && normal > report, "조치보고 수신 → 심볼 정상");
        Assert.Equal(1, host.OpenedCount);
        var stats = PerfTraceAnalyzer.Analyze(mine);
        Assert.Equal(1, stats.Latency("수신→팝업요청")!.Count);
        Assert.Equal(1, stats.Latency("팝업요청→보냄")!.Count);
    }

    [Fact]
    [Trait("Scenario", "PERF-005,PERF-006")]
    public async Task should_trace_malfunction_stages_from_receive_to_fault_color_and_back_to_normal()
    {
        using var rig = new EventMapRig();
        int deviceId = Interlocked.Increment(ref s_nextDevice);
        rig.Sensor(deviceId, groups: 9);
        rig.Zone(9);
        int eventId = NextIds(1);
        rig.ServerEvents[(ActionReportKind.Malfunction, eventId)] = (deviceId, "FAULT_FENCE");
        using var capture = PerfTrace.Capture();

        await rig.PublishMalfunctionAsync(eventId);
        rig.Panel.EnqueueCard(MalfunctionCard(rig, eventId));
        await rig.Panel.FlushPendingCardsNowAsync();
        rig.RemoteReport(ActionReportKind.Malfunction, eventId);

        var mine = Mine(capture, new[] { eventId }, PerfKinds.Malfunction).OrderBy(r => r.T).ToList();
        var order = mine.Select(r => r.Stage switch
        {
            PerfStages.NatsReceived => "recv:" + r.ExtraString("cmd"),
            PerfStages.SymbolState => "symbol:" + r.ExtraString("state"),
            _ => r.Stage,
        }).Distinct().ToList();
        Assert.Equal(new[] { "recv:MALFUNCTION", PerfStages.Enqueued, "symbol:Faulted", PerfStages.CardAdded, "recv:ACTION_REPORT", "symbol:Normal" }, order);
        Assert.DoesNotContain(mine, r => r.Kind == PerfKinds.Detection);   // 장애 7번은 탐지 7번과 섞이지 않는다
    }
}
