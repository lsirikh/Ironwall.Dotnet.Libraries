using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Ironwall.Dotnet.Libraries.Utils.Perf;

/****************************************************************************
   Purpose      : 계측 jsonl 분석기(순수) — GIS 계측(+ 선택: 주입기 발행 jsonl)을 읽어 단계 간 지연 분포(p50 · p95 · p99 · max),
                  누락(발행 ↔ 수신 ↔ 큐 ↔ 카드), 중복, 순서 뒤집힘, UI 지연 최대, 메모리 증가를 계산하고 md/json 표로 낸다.
                  호스트 실측 시험(Dotnet.Monitoring.Solution)이 이 분석기를 쓴다.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>주입기의 발행 한 줄 <c>{"kind":"detection","id":9001,"api_created_at":"…","nats_published_at":"…"}</c>.</summary>
public sealed class PerfPublishRecord
{
    public string Kind { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public DateTime? ApiCreatedAt { get; init; }
    public DateTime? NatsPublishedAt { get; init; }

    /// <summary>한 줄 읽기(던지지 않는다). kind · id 가 없으면 false.</summary>
    public static bool TryParse(string? line, out PerfPublishRecord? record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(line)) return false;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String) return false;
            if (!root.TryGetProperty("id", out var id)) return false;
            var idText = id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                JsonValueKind.Number => id.GetRawText(),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(idText)) return false;
            record = new PerfPublishRecord
            {
                Kind = kind.GetString()!,
                Id = idText!,
                ApiCreatedAt = PerfTraceRecord.TryGetTime(root, "api_created_at", out var api) ? api : null,
                NatsPublishedAt = PerfTraceRecord.TryGetTime(root, "nats_published_at", out var pub) ? pub : null,
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>단계 고르기 — 단계 이름 + (선택) <c>extra.cmd</c> 목록 · <c>extra.state</c> 목록/제외 목록.</summary>
public sealed class PerfStageSelector
{
    public PerfStageSelector(string stage, IReadOnlyCollection<string>? cmdIn = null, IReadOnlyCollection<string>? stateIn = null,
                             IReadOnlyCollection<string>? stateNotIn = null)
    {
        Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        CmdIn = cmdIn;
        StateIn = stateIn;
        StateNotIn = stateNotIn;
    }

    public string Stage { get; }
    public IReadOnlyCollection<string>? CmdIn { get; }
    public IReadOnlyCollection<string>? StateIn { get; }
    public IReadOnlyCollection<string>? StateNotIn { get; }

    public bool Matches(PerfTraceRecord record)
    {
        if (!string.Equals(record.Stage, Stage, StringComparison.Ordinal)) return false;
        if (CmdIn is not null && !Contains(CmdIn, record.ExtraString("cmd"))) return false;
        var state = record.ExtraString("state");
        if (StateIn is not null && !Contains(StateIn, state)) return false;
        if (StateNotIn is not null && Contains(StateNotIn, state)) return false;
        return true;
    }

    private static bool Contains(IReadOnlyCollection<string> set, string? value)
        => value is not null && set.Any(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>표에 쓰는 이름 — 예 <c>nats_received[cmd=DETECT|MALFUNCTION]</c>.</summary>
    public string Label
    {
        get
        {
            var parts = new List<string>();
            if (CmdIn is not null) parts.Add("cmd=" + string.Join("|", CmdIn));
            if (StateIn is not null) parts.Add("state=" + string.Join("|", StateIn));
            if (StateNotIn is not null) parts.Add("state!=" + string.Join("|", StateNotIn));
            return parts.Count == 0 ? Stage : $"{Stage}[{string.Join(",", parts)}]";
        }
    }

    public override string ToString() => Label;
}

/// <summary>지연을 잴 구간 — 같은 이벤트(종류 + id)에서 <see cref="From"/> 의 첫 기록 → 그 뒤 <see cref="To"/> 의 첫 기록.</summary>
public sealed record PerfLatencyPair(string Name, PerfStageSelector From, PerfStageSelector To);

/// <summary>한 구간의 지연 분포(ms).</summary>
public sealed record PerfLatencyStats(string Name, string From, string To, int Count, double P50Ms, double P95Ms, double P99Ms,
                                      double MaxMs, double MinMs, double MeanMs);

/// <summary>메모리 표본 요약(바이트).</summary>
public sealed record PerfMemoryStats(string Source, int Samples, long FirstBytes, long LastBytes, long MaxBytes)
{
    public long GrowthBytes => LastBytes - FirstBytes;
}

/// <summary>분석 결과.</summary>
public sealed class PerfAnalysisReport
{
    public DateTime? FirstAt { get; init; }
    public DateTime? LastAt { get; init; }
    public int RecordCount { get; init; }
    public int EventCount { get; init; }
    public IReadOnlyList<PerfLatencyStats> Latencies { get; init; } = Array.Empty<PerfLatencyStats>();

    public int PublishedCount { get; init; }
    public int ReceivedCount { get; init; }
    public int EnqueuedCount { get; init; }
    public int CardCount { get; init; }
    /// <summary>발행했는데 수신(DETECT · MALFUNCTION) 기록이 없는 이벤트(<c>kind:id</c>).</summary>
    public IReadOnlyList<string> MissingReceived { get; init; } = Array.Empty<string>();
    /// <summary>수신했는데 큐 기록이 없는 이벤트.</summary>
    public IReadOnlyList<string> MissingEnqueued { get; init; } = Array.Empty<string>();
    /// <summary>수신했는데 카드 기록이 없는 이벤트(카드 쪽에서 "이미 조치됨 · 같은 카드"로 거른 것은 뺀다).</summary>
    public IReadOnlyList<string> MissingCards { get; init; } = Array.Empty<string>();
    /// <summary>카드가 두 번 이상 올라간 이벤트.</summary>
    public IReadOnlyList<string> DuplicateCards { get; init; } = Array.Empty<string>();
    /// <summary>큐에 두 번 이상 들어간 이벤트.</summary>
    public IReadOnlyList<string> DuplicateEnqueues { get; init; } = Array.Empty<string>();
    /// <summary>수신이 두 번 이상인 이벤트 수(재전송 — 정보).</summary>
    public int DuplicateReceives { get; init; }
    public int DedupSkipped { get; init; }
    /// <summary>수신 순서와 카드 순서가 뒤집힌 이웃 쌍 수.</summary>
    public int CardOrderInversions { get; init; }
    /// <summary>수신 순서와 큐 순서가 뒤집힌 이웃 쌍 수.</summary>
    public int EnqueueOrderInversions { get; init; }
    /// <summary>마지막 발행(없으면 마지막 수신) → 마지막 카드(ms). 끝난 뒤 따라잡는 시간.</summary>
    public double? CatchUpMs { get; init; }

    public int UiLagSamples { get; init; }
    public double? UiLagMaxMs { get; init; }
    public double? UiLagP95MaxMs { get; init; }
    public double? GisCpuMaxPercent { get; init; }
    public double? GisCpuMeanPercent { get; init; }
    public PerfMemoryStats? GisPrivateBytes { get; init; }
    public PerfMemoryStats? GisWorkingSet { get; init; }
    public PerfMemoryStats? HostPrivateBytes { get; init; }
    public long? HostUiStallMaxMs { get; init; }
    public long TraceDropped { get; init; }

    /// <summary>이름으로 구간 찾기.</summary>
    public PerfLatencyStats? Latency(string name) => Latencies.FirstOrDefault(l => l.Name == name);

    /// <summary>사람이 읽는 표(md).</summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 이벤트 경로 계측 분석");
        sb.AppendLine();
        var span = FirstAt is { } f && LastAt is { } l ? (l - f).TotalSeconds : 0;
        sb.AppendLine($"- 기간: {Fmt(FirstAt)} ~ {Fmt(LastAt)} ({span.ToString("0.0", CultureInfo.InvariantCulture)} s) · 기록 {RecordCount}줄 · 이벤트 {EventCount}건");
        if (TraceDropped > 0) sb.AppendLine($"- ⚠ 기록 큐 포화로 버린 줄: {TraceDropped}");
        sb.AppendLine();
        sb.AppendLine("## 단계 간 지연 (ms)");
        sb.AppendLine();
        sb.AppendLine("| 구간 | 시작 | 끝 | 건수 | p50 | p95 | p99 | 최대 |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|---:|");
        foreach (var s in Latencies)
            sb.AppendLine($"| {s.Name} | `{Cell(s.From)}` | `{Cell(s.To)}` | {s.Count} | {Ms(s.P50Ms)} | {Ms(s.P95Ms)} | {Ms(s.P99Ms)} | {Ms(s.MaxMs)} |");
        sb.AppendLine();
        sb.AppendLine("## 누락 · 중복 · 순서");
        sb.AppendLine();
        sb.AppendLine("| 항목 | 값 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| 발행 · 수신 · 큐 · 카드 | {PublishedCount} · {ReceivedCount} · {EnqueuedCount} · {CardCount} |");
        sb.AppendLine($"| 누락 — 발행→수신 | {List(MissingReceived)} |");
        sb.AppendLine($"| 누락 — 수신→큐 | {List(MissingEnqueued)} |");
        sb.AppendLine($"| 누락 — 수신→카드 | {List(MissingCards)} |");
        sb.AppendLine($"| 중복 카드 | {List(DuplicateCards)} |");
        sb.AppendLine($"| 중복 큐 | {List(DuplicateEnqueues)} |");
        sb.AppendLine($"| 재수신(같은 id) · 거른 수 | {DuplicateReceives} · {DedupSkipped} |");
        sb.AppendLine($"| 순서 뒤집힘 — 큐 · 카드 | {EnqueueOrderInversions} · {CardOrderInversions} |");
        sb.AppendLine($"| 따라잡기(마지막 발행/수신 → 마지막 카드) | {(CatchUpMs is { } c ? Ms(c) + " ms" : "-")} |");
        sb.AppendLine();
        sb.AppendLine("## UI · 자원");
        sb.AppendLine();
        sb.AppendLine("| 항목 | 값 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| GIS UI 지연 최대 · 1초 p95 최대 (ms) | {Opt(UiLagMaxMs)} · {Opt(UiLagP95MaxMs)} ({UiLagSamples}초) |");
        sb.AppendLine($"| GIS CPU 최대 · 평균 (%) | {Opt(GisCpuMaxPercent)} · {Opt(GisCpuMeanPercent)} |");
        sb.AppendLine($"| GIS Private Bytes 처음 → 끝 (증가) | {Mem(GisPrivateBytes)} |");
        sb.AppendLine($"| GIS Working Set 처음 → 끝 (증가) | {Mem(GisWorkingSet)} |");
        sb.AppendLine($"| 카메라 호스트 Private Bytes 처음 → 끝 (증가) | {Mem(HostPrivateBytes)} |");
        sb.AppendLine($"| 카메라 호스트 UI 멈춤 최대 (ms) | {(HostUiStallMaxMs is { } h ? h.ToString(CultureInfo.InvariantCulture) : "-")} |");
        return sb.ToString();
    }

    /// <summary>기계가 읽는 결과(json).</summary>
    public string ToJson()
    {
        var dto = new Dictionary<string, object?>
        {
            ["first_at"] = FirstAt,
            ["last_at"] = LastAt,
            ["record_count"] = RecordCount,
            ["event_count"] = EventCount,
            ["latencies"] = Latencies.Select(s => new Dictionary<string, object?>
            {
                ["name"] = s.Name, ["from"] = s.From, ["to"] = s.To, ["count"] = s.Count,
                ["p50_ms"] = R(s.P50Ms), ["p95_ms"] = R(s.P95Ms), ["p99_ms"] = R(s.P99Ms), ["max_ms"] = R(s.MaxMs),
                ["min_ms"] = R(s.MinMs), ["mean_ms"] = R(s.MeanMs),
            }).ToList(),
            ["published"] = PublishedCount,
            ["received"] = ReceivedCount,
            ["enqueued"] = EnqueuedCount,
            ["cards"] = CardCount,
            ["missing_received"] = MissingReceived,
            ["missing_enqueued"] = MissingEnqueued,
            ["missing_cards"] = MissingCards,
            ["duplicate_cards"] = DuplicateCards,
            ["duplicate_enqueues"] = DuplicateEnqueues,
            ["duplicate_receives"] = DuplicateReceives,
            ["dedup_skipped"] = DedupSkipped,
            ["card_order_inversions"] = CardOrderInversions,
            ["enqueue_order_inversions"] = EnqueueOrderInversions,
            ["catch_up_ms"] = CatchUpMs is { } c ? R(c) : null,
            ["ui_lag_samples"] = UiLagSamples,
            ["ui_lag_max_ms"] = UiLagMaxMs,
            ["ui_lag_p95_max_ms"] = UiLagP95MaxMs,
            ["gis_cpu_max_pct"] = GisCpuMaxPercent,
            ["gis_cpu_mean_pct"] = GisCpuMeanPercent,
            ["gis_private_bytes"] = MemDto(GisPrivateBytes),
            ["gis_working_set"] = MemDto(GisWorkingSet),
            ["host_private_bytes"] = MemDto(HostPrivateBytes),
            ["host_ui_stall_max_ms"] = HostUiStallMaxMs,
            ["trace_dropped"] = TraceDropped,
        };
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, object?>? MemDto(PerfMemoryStats? m) => m is null ? null : new()
    {
        ["samples"] = m.Samples, ["first"] = m.FirstBytes, ["last"] = m.LastBytes, ["max"] = m.MaxBytes, ["growth"] = m.GrowthBytes,
    };

    private static double R(double v) => Math.Round(v, 3);
    /// <summary>표 칸 안의 '|' 는 칸 나눔으로 읽히므로 \| 로.</summary>
    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
    private static string Ms(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    private static string Opt(double? v) => v is { } d ? d.ToString("0.0", CultureInfo.InvariantCulture) : "-";
    private static string Fmt(DateTime? t) => t is { } d ? d.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z" : "-";
    private static string List(IReadOnlyList<string> items)
        => items.Count == 0 ? "0" : $"{items.Count} ({string.Join(", ", items.Take(10))}{(items.Count > 10 ? ", …" : "")})";
    private static string Mem(PerfMemoryStats? m)
        => m is null ? "-" : $"{MiB(m.FirstBytes)} → {MiB(m.LastBytes)} MiB ({(m.GrowthBytes >= 0 ? "+" : "")}{MiB(m.GrowthBytes)} MiB, 최대 {MiB(m.MaxBytes)})";
    private static string MiB(long bytes) => (bytes / 1024d / 1024d).ToString("0.0", CultureInfo.InvariantCulture);
}

/// <summary>분석기(순수 함수).</summary>
public static class PerfTraceAnalyzer
{
    /// <summary>발행 jsonl 을 기록으로 바꿀 때 쓰는 가짜 단계 — 주입기 NATS 발행 시각.</summary>
    public const string PublishedStage = "nats_published";
    /// <summary>가짜 단계 — 주입기 API 기록 시각.</summary>
    public const string ApiCreatedStage = "api_created";

    /// <summary>"첫 수신" 으로 보는 NATS 명령 — 이벤트를 만드는 알림(탐지 · 장애).</summary>
    public static readonly IReadOnlyCollection<string> PrimaryCommands = new[] { "DETECT", "MALFUNCTION" };

    private static readonly PerfStageSelector Received = new(PerfStages.NatsReceived, cmdIn: PrimaryCommands);
    private static readonly PerfStageSelector ReportReceived = new(PerfStages.NatsReceived, cmdIn: new[] { "ACTION_REPORT" });

    /// <summary>기본 구간 — 카탈로그 PERF-001 ~ 006 · 007 ~ 010 이 보는 것.</summary>
    public static IReadOnlyList<PerfLatencyPair> DefaultPairs { get; } = new[]
    {
        new PerfLatencyPair("발행→수신", new(PublishedStage), Received),
        new PerfLatencyPair("API→수신", new(ApiCreatedStage), Received),
        new PerfLatencyPair("수신→큐", Received, new(PerfStages.Enqueued)),
        new PerfLatencyPair("수신→심볼", Received, new(PerfStages.SymbolState, stateNotIn: new[] { "Normal" })),
        new PerfLatencyPair("수신→카드", Received, new(PerfStages.CardAdded)),
        new PerfLatencyPair("큐→카드", new(PerfStages.Enqueued), new(PerfStages.CardAdded)),
        new PerfLatencyPair("발행→카드", new(PublishedStage), new(PerfStages.CardAdded)),
        new PerfLatencyPair("수신→팝업요청", Received, new(PerfStages.PopupRequested)),
        new PerfLatencyPair("팝업요청→보냄", new(PerfStages.PopupRequested), new(PerfStages.PopupOpenSent)),
        new PerfLatencyPair("보냄→열림", new(PerfStages.PopupOpenSent), new(PerfStages.PopupOpened)),
        new PerfLatencyPair("수신→팝업열림", Received, new(PerfStages.PopupOpened)),
        new PerfLatencyPair("열림→첫영상", new(PerfStages.PopupOpened), new(PerfStages.FirstFrame)),
        // 호스트는 창을 띄우며 스트림을 시작하므로 첫 영상이 WindowOpened 보다 먼저 올 수 있다 — 보냄 기준도 함께 본다(호스트 쪽 값은 first_frame.extra.host_ms).
        new PerfLatencyPair("보냄→첫영상", new(PerfStages.PopupOpenSent), new(PerfStages.FirstFrame)),
        new PerfLatencyPair("조치보고→심볼정상", ReportReceived, new(PerfStages.SymbolState, stateIn: new[] { "Normal" })),
    };

    // ───────────────────────── 읽기 ─────────────────────────

    /// <summary>jsonl 줄들 → 기록(깨진 줄은 건너뛴다).</summary>
    public static IReadOnlyList<PerfTraceRecord> ParseRecords(IEnumerable<string> lines)
    {
        var list = new List<PerfTraceRecord>();
        foreach (var line in lines)
            if (PerfTraceRecord.TryParse(line, out var r) && r is not null) list.Add(r);
        return list;
    }

    /// <summary>발행 jsonl 줄들 → 발행 기록.</summary>
    public static IReadOnlyList<PerfPublishRecord> ParsePublished(IEnumerable<string> lines)
    {
        var list = new List<PerfPublishRecord>();
        foreach (var line in lines)
            if (PerfPublishRecord.TryParse(line, out var r) && r is not null) list.Add(r);
        return list;
    }

    /// <summary>파일들(회전본 포함 가능)을 읽는다. 다른 프로세스가 쓰는 중이어도 읽는다(공유 읽기).</summary>
    public static IReadOnlyList<PerfTraceRecord> ReadRecords(IEnumerable<string> paths)
        => ParseRecords(paths.SelectMany(ReadLinesShared));

    public static IReadOnlyList<PerfPublishRecord> ReadPublished(IEnumerable<string> paths)
        => ParsePublished(paths.SelectMany(ReadLinesShared));

    /// <summary>기록 파일과 그 회전본(<c>name.N.jsonl</c>)을 오래된 것부터.</summary>
    public static IReadOnlyList<string> WithRotations(string path, int maxFiles = PerfTraceFileWriter.DefaultMaxFiles)
    {
        var list = new List<string>();
        for (int i = Math.Max(1, maxFiles) - 1; i >= 1; i--)
        {
            var rotated = PerfTraceFileWriter.RotatedPath(path, i);
            if (File.Exists(rotated)) list.Add(rotated);
        }
        if (File.Exists(path)) list.Add(path);
        return list;
    }

    private static IEnumerable<string> ReadLinesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) is not null) yield return line;
    }

    // ───────────────────────── 계산 ─────────────────────────

    /// <summary>
    /// 백분위(nearest-rank) — 오름차순 정렬된 값에서 ceil(p/100 · n) 번째. 빈 목록이면 0.
    /// 1..100 → p50 = 50 · p95 = 95 · p99 = 99 · p100 = 100.
    /// </summary>
    public static double Percentile(IReadOnlyList<double> sortedAscending, double percentile)
    {
        ArgumentNullException.ThrowIfNull(sortedAscending);
        int n = sortedAscending.Count;
        if (n == 0) return 0;
        var p = Math.Clamp(percentile, 0, 100);
        int rank = (int)Math.Ceiling(p / 100d * n);
        rank = Math.Clamp(rank, 1, n);
        return sortedAscending[rank - 1];
    }

    /// <summary>값들 → 분포.</summary>
    public static PerfLatencyStats Summarize(string name, string from, string to, IEnumerable<double> samples)
    {
        var sorted = samples.Where(double.IsFinite).OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return new PerfLatencyStats(name, from, to, 0, 0, 0, 0, 0, 0, 0);
        return new PerfLatencyStats(name, from, to, sorted.Length,
            Percentile(sorted, 50), Percentile(sorted, 95), Percentile(sorted, 99), sorted[^1], sorted[0], sorted.Average());
    }

    /// <summary>
    /// 분석한다. <paramref name="published"/> 가 있으면 발행 시각을 가짜 단계(<see cref="PublishedStage"/> · <see cref="ApiCreatedStage"/>)로 넣어
    /// 발행→수신 · 발행→카드 · 발행 누락을 함께 잰다. <paramref name="pairs"/> 가 없으면 <see cref="DefaultPairs"/>.
    /// </summary>
    public static PerfAnalysisReport Analyze(IEnumerable<PerfTraceRecord> gis, IEnumerable<PerfPublishRecord>? published = null,
                                            IEnumerable<PerfLatencyPair>? pairs = null)
    {
        ArgumentNullException.ThrowIfNull(gis);
        var records = gis.Where(r => r is not null).ToList();
        var publishList = published?.Where(p => p is not null).ToList() ?? new List<PerfPublishRecord>();
        int recordCount = records.Count;

        foreach (var p in publishList)
        {
            if (p.NatsPublishedAt is { } pub) records.Add(new PerfTraceRecord { T = pub, Wall = pub, Stage = PublishedStage, Kind = p.Kind, Id = p.Id });
            if (p.ApiCreatedAt is { } api) records.Add(new PerfTraceRecord { T = api, Wall = api, Stage = ApiCreatedStage, Kind = p.Kind, Id = p.Id });
        }

        // 이벤트별 묶음(시각 순)
        var groups = records
            .Where(r => r.Id is not null && IsEventKind(r.Kind))
            .GroupBy(r => Key(r.Kind, r.Id!))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.T).ToList(), StringComparer.Ordinal);

        // 구간 지연
        var latencies = new List<PerfLatencyStats>();
        foreach (var pair in pairs ?? DefaultPairs)
        {
            var samples = new List<double>();
            foreach (var list in groups.Values)
            {
                var from = list.FirstOrDefault(pair.From.Matches);
                if (from is null) continue;
                var to = list.FirstOrDefault(r => r.T >= from.T && pair.To.Matches(r));
                if (to is null) continue;
                samples.Add((to.T - from.T).TotalMilliseconds);
            }
            latencies.Add(Summarize(pair.Name, pair.From.Label, pair.To.Label, samples));
        }

        // 누락 · 중복
        var publishedKeys = publishList.Select(p => Key(p.Kind, p.Id)).Distinct(StringComparer.Ordinal).ToList();
        var receivedKeys = groups.Where(g => g.Value.Any(Received.Matches)).Select(g => g.Key).ToList();
        var receivedSet = new HashSet<string>(receivedKeys, StringComparer.Ordinal);
        int Count(List<PerfTraceRecord> list, string stage) => list.Count(r => r.Stage == stage);

        var missingReceived = publishedKeys.Where(k => !receivedSet.Contains(k)).ToList();
        var missingEnqueued = receivedKeys.Where(k => Count(groups[k], PerfStages.Enqueued) == 0).ToList();
        var missingCards = receivedKeys.Where(k => Count(groups[k], PerfStages.CardAdded) == 0 && !CardFilteredOnPurpose(groups[k])).ToList();
        var duplicateCards = groups.Where(g => Count(g.Value, PerfStages.CardAdded) > 1).Select(g => g.Key).ToList();
        var duplicateEnqueues = groups.Where(g => Count(g.Value, PerfStages.Enqueued) > 1).Select(g => g.Key).ToList();
        int duplicateReceives = groups.Count(g => g.Value.Count(Received.Matches) > 1);
        int dedupSkipped = records.Count(r => r.Stage == PerfStages.DedupSkipped);

        // 순서 — 수신(발행이 있으면 발행) 순서 대비 큐 · 카드 순서
        DateTime? OrderTime(List<PerfTraceRecord> list)
            => list.FirstOrDefault(r => r.Stage == PublishedStage)?.T ?? list.FirstOrDefault(Received.Matches)?.T;
        var ordered = groups.Values.Select(l => (At: OrderTime(l), List: l)).Where(x => x.At is not null).OrderBy(x => x.At).Select(x => x.List).ToList();
        int Inversions(string stage)
        {
            int count = 0;
            DateTime? previous = null;
            foreach (var list in ordered)
            {
                var at = list.FirstOrDefault(r => r.Stage == stage)?.T;
                if (at is null) continue;
                if (previous is { } p && at < p) count++;
                previous = at;
            }
            return count;
        }

        // 따라잡기
        double? catchUp = null;
        var lastCard = groups.Values.SelectMany(l => l).Where(r => r.Stage == PerfStages.CardAdded).Select(r => (DateTime?)r.T).Max();
        var lastStart = publishList.Count > 0
            ? records.Where(r => r.Stage == PublishedStage).Select(r => (DateTime?)r.T).Max()
            : records.Where(Received.Matches).Select(r => (DateTime?)r.T).Max();
        if (lastCard is { } lc && lastStart is { } ls) catchUp = (lc - ls).TotalMilliseconds;

        // UI · 자원
        var uiLag = records.Where(r => r.Stage == PerfStages.UiLag).ToList();
        var process = records.Where(r => r.Stage == PerfStages.Process && r.Kind == PerfKinds.Gis).OrderBy(r => r.T).ToList();
        var host = records.Where(r => r.Stage == PerfStages.HostHeartbeat).OrderBy(r => r.T).ToList();
        var cpu = process.Select(r => r.ExtraNumber("cpu_pct")).Where(v => v is not null).Select(v => v!.Value).ToList();
        var hostStall = host.Select(r => r.ExtraNumber("ui_stall_ms")).Where(v => v is not null).Select(v => (long)v!.Value).ToList();

        var timed = records.Select(r => r.T).ToList();

        return new PerfAnalysisReport
        {
            FirstAt = timed.Count == 0 ? null : timed.Min(),
            LastAt = timed.Count == 0 ? null : timed.Max(),
            RecordCount = recordCount,
            EventCount = groups.Count,
            Latencies = latencies,
            PublishedCount = publishedKeys.Count,
            ReceivedCount = receivedKeys.Count,
            EnqueuedCount = groups.Count(g => Count(g.Value, PerfStages.Enqueued) > 0),
            CardCount = groups.Count(g => Count(g.Value, PerfStages.CardAdded) > 0),
            MissingReceived = missingReceived,
            MissingEnqueued = missingEnqueued,
            MissingCards = missingCards,
            DuplicateCards = duplicateCards,
            DuplicateEnqueues = duplicateEnqueues,
            DuplicateReceives = duplicateReceives,
            DedupSkipped = dedupSkipped,
            CardOrderInversions = Inversions(PerfStages.CardAdded),
            EnqueueOrderInversions = Inversions(PerfStages.Enqueued),
            CatchUpMs = catchUp,
            UiLagSamples = uiLag.Count,
            UiLagMaxMs = MaxOf(uiLag, "max_ms"),
            UiLagP95MaxMs = MaxOf(uiLag, "p95_ms"),
            GisCpuMaxPercent = cpu.Count == 0 ? null : cpu.Max(),
            GisCpuMeanPercent = cpu.Count == 0 ? null : cpu.Average(),
            GisPrivateBytes = Memory("gis.private_bytes", process, "private_bytes"),
            GisWorkingSet = Memory("gis.working_set", process, "working_set"),
            HostPrivateBytes = Memory("camera_host.private_bytes", host, "private_bytes"),
            HostUiStallMaxMs = hostStall.Count == 0 ? null : hostStall.Max(),
            TraceDropped = (long)records.Where(r => r.Stage == PerfStages.TraceDropped).Sum(r => r.ExtraNumber("count") ?? 0),
        };
    }

    /// <summary>카드 쪽이 일부러 거른 이벤트(이미 조치됨 · 같은 카드 있음) — 카드 누락으로 세지 않는다.</summary>
    private static bool CardFilteredOnPurpose(List<PerfTraceRecord> list)
        => list.Any(r => r.Stage == PerfStages.DedupSkipped && (r.ExtraString("where")?.StartsWith("card", StringComparison.Ordinal) ?? false));

    private static bool IsEventKind(string kind)
        => kind != PerfKinds.Gis && kind != PerfKinds.CameraHost && kind != PerfKinds.Device;

    private static string Key(string kind, string id) => $"{kind}:{id}";

    private static double? MaxOf(List<PerfTraceRecord> list, string key)
    {
        double? max = null;
        foreach (var r in list)
            if (r.ExtraNumber(key) is { } v && (max is null || v > max)) max = v;
        return max;
    }

    private static PerfMemoryStats? Memory(string source, List<PerfTraceRecord> sortedByTime, string key)
    {
        var values = sortedByTime.Select(r => r.ExtraNumber(key)).Where(v => v is > 0).Select(v => (long)v!.Value).ToList();
        if (values.Count == 0) return null;
        return new PerfMemoryStats(source, values.Count, values[0], values[^1], values.Max());
    }
}
