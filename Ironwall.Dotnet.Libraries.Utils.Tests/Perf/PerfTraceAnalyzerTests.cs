using System.Globalization;
using System.Text.Json;
using Ironwall.Dotnet.Libraries.Utils.Perf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests.Perf;

/****************************************************************************
   Purpose      : 계측 분석기 — 알려진 입력 → 기대 백분위 · 누락 · 중복 · 순서 · 따라잡기 · UI 지연 · 메모리 증가 · md/json
                  (카탈로그 §7 PERF-001 ~ 013 의 "대조" 수단. 호스트 실측 시험이 이 분석기를 쓴다)
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class PerfTraceAnalyzerTests
{
    private static readonly DateTime T0 = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

    private static PerfTraceRecord R(double atMs, string stage, string kind, string? id, params (string Key, object? Value)[] extra) => new()
    {
        T = T0.AddTicks((long)(atMs * TimeSpan.TicksPerMillisecond)),
        Wall = T0.AddTicks((long)(atMs * TimeSpan.TicksPerMillisecond)),
        MonoMs = atMs, Pid = 1, Tid = 1, Stage = stage, Kind = kind, Id = id,
        Extra = extra.Length == 0 ? null : PerfTrace.Extra(extra),
    };

    private static PerfTraceRecord Detect(double atMs, int id) => R(atMs, PerfStages.NatsReceived, PerfKinds.Detection, id.ToString(), ("cmd", "DETECT"));

    // ───────────────────────── 백분위 ─────────────────────────

    [Fact]
    [Trait("Scenario", "PERF-007,PERF-008,PERF-009,PERF-010")]
    public void should_return_nearest_rank_percentiles_when_values_are_one_to_hundred()
    {
        var sorted = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();

        Assert.Equal(50, PerfTraceAnalyzer.Percentile(sorted, 50));
        Assert.Equal(95, PerfTraceAnalyzer.Percentile(sorted, 95));
        Assert.Equal(99, PerfTraceAnalyzer.Percentile(sorted, 99));
        Assert.Equal(100, PerfTraceAnalyzer.Percentile(sorted, 100));
        Assert.Equal(1, PerfTraceAnalyzer.Percentile(sorted, 0));
    }

    [Theory]
    [Trait("Scenario", "PERF-007,PERF-008,PERF-009,PERF-010")]
    [InlineData(new double[] { 7 }, 50, 7)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 50, 5)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 95, 10)]   // ceil(9.5) = 10 번째
    [InlineData(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 90, 9)]
    [InlineData(new double[0], 95, 0)]
    public void should_pick_ceil_rank_value_when_percentile_requested(double[] sorted, double p, double expected)
        => Assert.Equal(expected, PerfTraceAnalyzer.Percentile(sorted, p));

    [Fact]
    [Trait("Scenario", "PERF-007,PERF-008,PERF-009,PERF-010")]
    public void should_sort_before_ranking_when_summarizing_unordered_samples()
    {
        var shuffled = Enumerable.Range(1, 100).Select(i => (double)i).OrderBy(i => (i * 37) % 101).ToList();

        var stats = PerfTraceAnalyzer.Summarize("x", "a", "b", shuffled);

        Assert.Equal((100, 50d, 95d, 99d, 100d, 1d, 50.5d), (stats.Count, stats.P50Ms, stats.P95Ms, stats.P99Ms, stats.MaxMs, stats.MinMs, stats.MeanMs));
    }

    // ───────────────────────── 구간 지연 ─────────────────────────

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-003,PERF-004,PERF-006")]
    public void should_measure_each_stage_pair_per_event_when_trace_has_known_latencies()
    {
        // 이벤트 i(1..100): 수신 t=i*1000, 큐 +1, 심볼 +2, 카드 +i, 팝업요청 +3, 보냄 +4, 열림 +4+i, 첫영상 열림+2i, 조치보고 수신 → 정상 +i
        var records = new List<PerfTraceRecord>();
        for (int i = 1; i <= 100; i++)
        {
            double t = i * 1000;
            var id = i.ToString();
            records.Add(Detect(t, i));
            records.Add(R(t + 1, PerfStages.Enqueued, PerfKinds.Detection, id));
            records.Add(R(t + 2, PerfStages.SymbolState, PerfKinds.Detection, id, ("state", "Detecting")));
            records.Add(R(t + i, PerfStages.CardAdded, PerfKinds.Detection, id));
            records.Add(R(t + 3, PerfStages.PopupRequested, PerfKinds.Detection, id));
            records.Add(R(t + 4, PerfStages.PopupOpenSent, PerfKinds.Detection, id));
            records.Add(R(t + 4 + i, PerfStages.PopupOpened, PerfKinds.Detection, id));
            records.Add(R(t + 4 + 3 * i, PerfStages.FirstFrame, PerfKinds.Detection, id));
            records.Add(R(t + 500, PerfStages.NatsReceived, PerfKinds.Detection, id, ("cmd", "ACTION_REPORT")));
            records.Add(R(t + 500 + i, PerfStages.SymbolState, PerfKinds.Detection, id, ("state", "Normal")));
        }

        var report = PerfTraceAnalyzer.Analyze(records);

        AssertStats(report.Latency("수신→카드"), 100, 50, 95, 99, 100);
        AssertStats(report.Latency("수신→큐"), 100, 1, 1, 1, 1);
        AssertStats(report.Latency("수신→심볼"), 100, 2, 2, 2, 2);          // Normal 은 빼고 첫 칠하기
        AssertStats(report.Latency("보냄→열림"), 100, 50, 95, 99, 100);
        AssertStats(report.Latency("수신→팝업열림"), 100, 54, 99, 103, 104);
        AssertStats(report.Latency("열림→첫영상"), 100, 100, 190, 198, 200);
        AssertStats(report.Latency("조치보고→심볼정상"), 100, 50, 95, 99, 100);
        Assert.Equal(0, report.Latency("발행→수신")!.Count);                // 발행 기록 없음
        Assert.Empty(report.MissingCards);
        Assert.Equal(100, report.EventCount);
    }

    private static void AssertStats(PerfLatencyStats? s, int count, double p50, double p95, double p99, double max)
    {
        Assert.NotNull(s);
        Assert.Equal((count, p50, p95, p99, max), (s!.Count, Math.Round(s.P50Ms, 3), Math.Round(s.P95Ms, 3), Math.Round(s.P99Ms, 3), Math.Round(s.MaxMs, 3)));
    }

    [Fact]
    [Trait("Scenario", "PERF-007,PERF-008,PERF-009")]
    public void should_join_injector_publish_lines_when_publish_jsonl_given()
    {
        var lines = new[]
        {
            """{"kind":"detection","id":1,"api_created_at":"2026-10-11T00:00:00.000Z","nats_published_at":"2026-10-11T00:00:00.010Z"}""",
            """{"kind":"detection","id":"2","api_created_at":"2026-10-11T09:00:01.000+09:00","nats_published_at":"2026-10-11T00:00:01.010Z"}""",
            """{"kind":"detection","id":3,"nats_published_at":"2026-10-11T00:00:02.010Z"}""",
            "broken",
            """{"id":4}""",
        };
        var published = PerfTraceAnalyzer.ParsePublished(lines);
        Assert.Equal(3, published.Count);
        Assert.Equal(T0.AddSeconds(1), published[1].ApiCreatedAt);   // +09:00 → UTC

        var gis = new[]
        {
            Detect(30, 1), R(130, PerfStages.CardAdded, PerfKinds.Detection, "1"),
            Detect(1050, 2), R(1250, PerfStages.CardAdded, PerfKinds.Detection, "2"),
            // 3 은 수신 안 됨
        };

        var report = PerfTraceAnalyzer.Analyze(gis, published);

        AssertStats(report.Latency("발행→수신"), 2, 20, 40, 40, 40);
        AssertStats(report.Latency("API→수신"), 2, 30, 50, 50, 50);
        AssertStats(report.Latency("발행→카드"), 2, 120, 240, 240, 240);
        Assert.Equal(new[] { "detection:3" }, report.MissingReceived);
        Assert.Equal((3, 2, 2), (report.PublishedCount, report.ReceivedCount, report.CardCount));
        Assert.Equal(1250 - 2010, report.CatchUpMs!.Value, 3);   // 마지막 발행(3번, 2010) 뒤 마지막 카드(1250) — 음수 = 따라잡을 것 없음
    }

    // ───────────────────────── 누락 · 중복 · 순서 ─────────────────────────

    [Fact]
    [Trait("Scenario", "PERF-014,PERF-015")]
    public void should_report_missing_duplicate_and_purposely_filtered_events_separately()
    {
        var gis = new List<PerfTraceRecord>
        {
            // 1: 정상
            Detect(0, 1), R(1, PerfStages.Enqueued, PerfKinds.Detection, "1"), R(2, PerfStages.CardAdded, PerfKinds.Detection, "1"),
            // 2: 큐는 됐는데 카드 없음(누락)
            Detect(10, 2), R(11, PerfStages.Enqueued, PerfKinds.Detection, "2"),
            // 3: 조치가 먼저 와 카드 쪽이 일부러 거름 — 누락 아님
            Detect(20, 3), R(21, PerfStages.Enqueued, PerfKinds.Detection, "3"),
            R(22, PerfStages.DedupSkipped, PerfKinds.Detection, "3", ("where", "card_already_reported")),
            // 4: 카드 두 번(중복) · 수신 두 번(재전송) · 큐에서 거름
            Detect(30, 4), R(31, PerfStages.Enqueued, PerfKinds.Detection, "4"), R(32, PerfStages.CardAdded, PerfKinds.Detection, "4"),
            Detect(33, 4), R(34, PerfStages.DedupSkipped, PerfKinds.Detection, "4", ("where", "queue_envelope")),
            R(35, PerfStages.CardAdded, PerfKinds.Detection, "4"),
            // 5: 수신했는데 큐도 카드도 없음, 카드 버퍼 포화로 버림 — 누락으로 센다
            Detect(40, 5), R(41, PerfStages.CardDropped, PerfKinds.Detection, "5", ("where", "buffer_full")),
            // 6: 장애 — 큐 두 번(중복 큐)
            R(50, PerfStages.NatsReceived, PerfKinds.Malfunction, "6", ("cmd", "MALFUNCTION")),
            R(51, PerfStages.Enqueued, PerfKinds.Malfunction, "6"), R(52, PerfStages.Enqueued, PerfKinds.Malfunction, "6"),
            R(53, PerfStages.CardAdded, PerfKinds.Malfunction, "6"),
            // 표본(이벤트 아님)
            R(60, PerfStages.SymbolState, PerfKinds.Device, "group:1", ("state", "Normal")),
        };

        var report = PerfTraceAnalyzer.Analyze(gis);

        Assert.Equal(new[] { "detection:2", "detection:5" }, report.MissingCards.OrderBy(k => k));
        Assert.Equal(new[] { "detection:5" }, report.MissingEnqueued);
        Assert.Equal(new[] { "detection:4" }, report.DuplicateCards);
        Assert.Equal(new[] { "malfunction:6" }, report.DuplicateEnqueues);
        Assert.Equal(1, report.DuplicateReceives);
        Assert.Equal(2, report.DedupSkipped);
        Assert.Equal(6, report.EventCount);   // kind=device 는 이벤트가 아니다
    }

    [Fact]
    [Trait("Scenario", "PERF-014")]
    public void should_count_neighbour_inversions_when_cards_land_out_of_receive_order()
    {
        var gis = new List<PerfTraceRecord>();
        double[] cardAt = { 100, 300, 200, 400, 350 };   // 2↔3, 4↔5 뒤집힘
        for (int i = 0; i < cardAt.Length; i++)
        {
            gis.Add(Detect(i * 10, i + 1));
            gis.Add(R(i * 10 + 1, PerfStages.Enqueued, PerfKinds.Detection, (i + 1).ToString()));
            gis.Add(R(cardAt[i], PerfStages.CardAdded, PerfKinds.Detection, (i + 1).ToString()));
        }

        var report = PerfTraceAnalyzer.Analyze(gis);

        Assert.Equal(2, report.CardOrderInversions);
        Assert.Equal(0, report.EnqueueOrderInversions);
        Assert.Equal(400 - 40, report.CatchUpMs!.Value, 3);   // 발행 없음 → 마지막 수신(40) → 마지막 카드(400)
    }

    // ───────────────────────── UI · 자원 ─────────────────────────

    [Fact]
    [Trait("Scenario", "PERF-011,PERF-012,PERF-013")]
    public void should_summarize_ui_lag_cpu_and_memory_growth_when_samples_present()
    {
        const long MiB = 1024 * 1024;
        var gis = new List<PerfTraceRecord>
        {
            R(1000, PerfStages.UiLag, PerfKinds.Gis, null, ("max_ms", 12.0), ("p95_ms", 8.0)),
            R(2000, PerfStages.UiLag, PerfKinds.Gis, null, ("max_ms", 480.5), ("p95_ms", 120.0)),
            R(3000, PerfStages.UiLag, PerfKinds.Gis, null, ("max_ms", 30.0), ("p95_ms", 20.0)),
            R(1000, PerfStages.Process, PerfKinds.Gis, null, ("cpu_pct", 10.0), ("private_bytes", 400 * MiB), ("working_set", 500 * MiB)),
            R(2000, PerfStages.Process, PerfKinds.Gis, null, ("cpu_pct", 40.0), ("private_bytes", 520 * MiB), ("working_set", 560 * MiB)),
            R(3000, PerfStages.Process, PerfKinds.Gis, null, ("cpu_pct", 25.0), ("private_bytes", 450 * MiB), ("working_set", 540 * MiB)),
            R(1500, PerfStages.HostHeartbeat, PerfKinds.CameraHost, null, ("ui_stall_ms", 0), ("private_bytes", 200 * MiB)),
            R(2500, PerfStages.HostHeartbeat, PerfKinds.CameraHost, null, ("ui_stall_ms", 1500), ("private_bytes", 260 * MiB)),
            R(2600, PerfStages.TraceDropped, PerfKinds.Gis, null, ("count", 7)),
        };

        var report = PerfTraceAnalyzer.Analyze(gis);

        Assert.Equal((3, 480.5, 120.0), (report.UiLagSamples, report.UiLagMaxMs, report.UiLagP95MaxMs));
        Assert.Equal(40.0, report.GisCpuMaxPercent);
        Assert.Equal(25.0, report.GisCpuMeanPercent);
        Assert.Equal((400 * MiB, 450 * MiB, 520 * MiB, 50 * MiB),
                     (report.GisPrivateBytes!.FirstBytes, report.GisPrivateBytes.LastBytes, report.GisPrivateBytes.MaxBytes, report.GisPrivateBytes.GrowthBytes));
        Assert.Equal(40 * MiB, report.GisWorkingSet!.GrowthBytes);
        Assert.Equal(60 * MiB, report.HostPrivateBytes!.GrowthBytes);
        Assert.Equal(1500, report.HostUiStallMaxMs);
        Assert.Equal(7, report.TraceDropped);
        Assert.Equal(0, report.EventCount);
    }

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-007,PERF-011,PERF-013")]
    public void should_render_markdown_table_and_json_with_same_numbers()
    {
        var gis = new List<PerfTraceRecord>();
        for (int i = 1; i <= 20; i++)
        {
            gis.Add(Detect(i * 100, i));
            gis.Add(R(i * 100 + i * 5, PerfStages.CardAdded, PerfKinds.Detection, i.ToString()));
        }
        gis.Add(R(5000, PerfStages.UiLag, PerfKinds.Gis, null, ("max_ms", 33.0), ("p95_ms", 21.0)));

        var report = PerfTraceAnalyzer.Analyze(gis);
        var md = report.ToMarkdown();
        var json = report.ToJson();

        Assert.Contains("| 수신→카드 | `nats_received[cmd=DETECT\\|MALFUNCTION]` | `card_added` | 20 | 50.0 | 95.0 | 100.0 | 100.0 |", md);
        Assert.Contains("33.0 · 21.0 (1초)", md);
        using var doc = JsonDocument.Parse(json);
        var card = doc.RootElement.GetProperty("latencies").EnumerateArray().Single(e => e.GetProperty("name").GetString() == "수신→카드");
        Assert.Equal(95, card.GetProperty("p95_ms").GetDouble());
        Assert.Equal(20, card.GetProperty("count").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("missing_cards").GetArrayLength());
        Assert.Equal(33, doc.RootElement.GetProperty("ui_lag_max_ms").GetDouble());
    }

    [Fact]
    [Trait("Scenario", "PERF-007")]
    public void should_parse_lines_written_by_record_serializer_when_reading_trace_text()
    {
        var lines = Enumerable.Range(1, 5).Select(i => Detect(i, i).ToJsonLine()).Append("").Append("{garbage").ToList();

        var records = PerfTraceAnalyzer.ParseRecords(lines);

        Assert.Equal(5, records.Count);
        Assert.All(records, r => Assert.Equal("DETECT", r.ExtraString("cmd")));
        Assert.Equal("3", records[2].Id);
        Assert.Equal(T0.AddMilliseconds(3).ToString("O", CultureInfo.InvariantCulture), records[2].T.ToString("O", CultureInfo.InvariantCulture));
    }
}
