using System.Diagnostics;
using System.Globalization;
using System.IO;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Utils.Perf;

/****************************************************************************
   Purpose      : 이벤트 경로 시간 계측(카탈로그 §7 PERF-*) — 환경변수 IRONWALL_PERF_TRACE=1 일 때만 켜진다.
                  이벤트(종류 + id) 기준 단계 시각을 jsonl 로 남긴다(기록은 별도 스레드 — 수신 · UI 경로를 막지 않는다).
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>단계 이름(jsonl 의 <c>stage</c>).</summary>
public static class PerfStages
{
    /// <summary>NATS 수신 직후(탐지 · 장애 · 운영 · SYNC · 조치보고). <c>extra.cmd</c> 에 명령.</summary>
    public const string NatsReceived = "nats_received";
    /// <summary>이벤트 큐(EventQueueManager)에 들어갔다.</summary>
    public const string Enqueued = "enqueued";
    /// <summary>심볼 관리자가 장비 · 그룹 상태 전이를 심볼에 칠했다. <c>extra.target</c> · <c>extra.state</c>.</summary>
    public const string SymbolState = "symbol_state";
    /// <summary>카드 패널이 카드를 UI 컬렉션에 넣었다(UI 스레드).</summary>
    public const string CardAdded = "card_added";
    /// <summary>카드 묶음 버퍼가 가득 차 카드를 버렸다(누락 — 분석기가 카드 누락으로 센다).</summary>
    public const string CardDropped = "card_dropped";
    /// <summary>이벤트 창 트리거가 창 만들기를 시작했다.</summary>
    public const string PopupRequested = "popup_requested";
    /// <summary>창 관리자가 카메라 호스트로 열기 명령을 보냈다.</summary>
    public const string PopupOpenSent = "popup_open_sent";
    /// <summary>호스트가 창을 열었다고 답했다(WindowOpened).</summary>
    public const string PopupOpened = "popup_opened";
    /// <summary>창 타일의 첫 영상 프레임(호스트가 StreamStateChanged Playing 으로 알림). <c>extra.host_ms</c> = 호스트 쪽 열기→첫 프레임.</summary>
    public const string FirstFrame = "first_frame";
    /// <summary>중복이라 걸렀다. <c>extra.where</c> 에 거른 곳.</summary>
    public const string DedupSkipped = "dedup_skipped";
    /// <summary>1초 UI 스레드 지연 요약(max_ms · p95_ms).</summary>
    public const string UiLag = "ui_lag";
    /// <summary>1초 프로세스 표본(cpu_pct · working_set · private_bytes · threads).</summary>
    public const string Process = "process";
    /// <summary>카메라 호스트 하트비트(ui_stall_ms · private_bytes).</summary>
    public const string HostHeartbeat = "host_heartbeat";
    /// <summary>기록 파일 시작(프로세스 정보).</summary>
    public const string TraceStart = "trace_start";
    /// <summary>기록 큐가 가득 차 버린 줄 수(<c>extra.count</c>).</summary>
    public const string TraceDropped = "trace_dropped";
}

/// <summary>종류(jsonl 의 <c>kind</c>). 탐지 · 장애는 카드 · 조치보고의 종류 문자열과 같다.</summary>
public static class PerfKinds
{
    public const string Detection = "detection";
    public const string Malfunction = "malfunction";
    public const string Operation = "operation";
    public const string Connection = "connection";
    /// <summary>이벤트 문맥 없이 찍힌 장비 · 그룹 상태(id = 대상).</summary>
    public const string Device = "device";
    /// <summary>GIS 프로세스 표본 · UI 지연.</summary>
    public const string Gis = "gis";
    /// <summary>카메라 팝업 호스트 하트비트.</summary>
    public const string CameraHost = "camera_host";
}

/// <summary>
/// 계측 진입점. 꺼져 있으면 <see cref="IsEnabled"/> 읽기 한 번으로 끝난다 — 호출부는 extra 를 만들기 전에 <see cref="IsEnabled"/> 를 본다.
/// 어떤 메서드도 예외를 밖으로 던지지 않는다(계측 때문에 이벤트 경로가 멈추지 않게).
/// </summary>
public static class PerfTrace
{
    /// <summary>켜기 환경변수(<c>1</c> · <c>true</c> · <c>yes</c> · <c>on</c>).</summary>
    public const string EnabledVariable = "IRONWALL_PERF_TRACE";
    /// <summary>기록 파일 경로 환경변수. 없으면 <c>%LocalAppData%\Ironwall\perf\gis-perf-&lt;pid&gt;.jsonl</c>.</summary>
    public const string FileVariable = "IRONWALL_PERF_TRACE_FILE";

    private static readonly bool s_environmentEnabled = ReadEnabled(SafeGetEnvironment(EnabledVariable));
    private static readonly DateTime s_anchorUtc = DateTime.UtcNow;
    private static readonly long s_anchorTimestamp = Stopwatch.GetTimestamp();
    private static readonly int s_pid = Environment.ProcessId;
    private static readonly object s_sinkGate = new();
    private static volatile Action<PerfTraceRecord>[] s_sinks = Array.Empty<Action<PerfTraceRecord>>();
    private static volatile bool s_enabled = s_environmentEnabled;
    private static PerfTraceFileWriter? s_file;
    private static int s_fileState;   // 0 = 아직, 1 = 열림, 2 = 열기 실패(다시 시도하지 않음)

    [ThreadStatic] private static string? t_contextKind;
    [ThreadStatic] private static string? t_contextId;

    /// <summary>지금 계측이 켜졌는가(환경변수 또는 시험 붙잡기).</summary>
    public static bool IsEnabled => s_enabled;

    /// <summary>환경변수로 켜졌는가(파일 기록 · 표본기는 이때만).</summary>
    public static bool IsEnvironmentEnabled => s_environmentEnabled;

    /// <summary>파일 기록기(환경변수로 켜졌고 첫 기록 뒤). 없으면 null.</summary>
    public static PerfTraceFileWriter? FileWriter => Volatile.Read(ref s_file);

    /// <summary>환경변수 값 해석(순수).</summary>
    public static bool ReadEnabled(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || v.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>기록 파일 경로(순수). <paramref name="overridePath"/> 가 있으면 그것, 없으면 <c>{localAppData}\Ironwall\perf\gis-perf-{pid}.jsonl</c>.</summary>
    public static string ResolveFilePath(string? overridePath, string localAppData, int processId)
    {
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath.Trim();
        return Path.Combine(localAppData, "Ironwall", "perf", $"gis-perf-{processId.ToString(CultureInfo.InvariantCulture)}.jsonl");
    }

    /// <summary>지금 시각(UTC, Stopwatch 기준 고해상도) · 단조 ms.</summary>
    public static (DateTime Utc, double MonoMs) Now()
    {
        long elapsed = Stopwatch.GetTimestamp() - s_anchorTimestamp;
        double ms = elapsed * 1000d / Stopwatch.Frequency;
        return (s_anchorUtc.AddTicks((long)(elapsed * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)), ms);
    }

    // ───────────────────────── 찍기 ─────────────────────────

    /// <summary>단계 하나를 찍는다. 꺼져 있으면 아무것도 하지 않는다.</summary>
    public static void Mark(string stage, string kind, string? id, IReadOnlyDictionary<string, object?>? extra = null)
    {
        if (!s_enabled) return;
        try
        {
            var (utc, mono) = Now();
            Emit(new PerfTraceRecord
            {
                T = utc,
                Wall = DateTime.UtcNow,
                MonoMs = mono,
                Pid = s_pid,
                Tid = Environment.CurrentManagedThreadId,
                Stage = stage ?? string.Empty,
                Kind = kind ?? string.Empty,
                Id = id,
                Extra = extra,
            });
        }
        catch
        {
            // 계측은 절대 이벤트 경로를 깨지 않는다.
        }
    }

    /// <summary>서버 번호(정수) 판. 0 이하면 id 없음.</summary>
    public static void Mark(string stage, string kind, int id, IReadOnlyDictionary<string, object?>? extra = null)
    {
        if (!s_enabled) return;
        Mark(stage, kind, id > 0 ? id.ToString(CultureInfo.InvariantCulture) : null, extra);
    }

    /// <summary>현재 스레드의 이벤트 문맥으로 찍는다(문맥 없으면 <paramref name="fallbackKind"/> · <paramref name="fallbackId"/>).</summary>
    public static void MarkInContext(string stage, string fallbackKind, string? fallbackId, IReadOnlyDictionary<string, object?>? extra = null)
    {
        if (!s_enabled) return;
        var kind = t_contextKind;
        if (kind is null) Mark(stage, fallbackKind, fallbackId, extra);
        else Mark(stage, kind, t_contextId, extra);
    }

    /// <summary>extra 사전을 만든다(호출부는 <see cref="IsEnabled"/> 를 먼저 볼 것).</summary>
    public static IReadOnlyDictionary<string, object?> Extra(params (string Key, object? Value)[] pairs)
    {
        var map = new Dictionary<string, object?>(pairs.Length, StringComparer.Ordinal);
        foreach (var (key, value) in pairs) map[key] = value;
        return map;
    }

    // ───────────────────────── 이벤트 문맥 ─────────────────────────

    /// <summary>
    /// 현재 스레드에 이벤트 문맥을 세운다 — 이 범위 안에서 동기로 불린 처리기(심볼 관리자 전이)가 <see cref="MarkInContext"/> 로
    /// 이 이벤트의 단계를 찍는다. 꺼져 있으면 아무것도 하지 않는 빈 범위.
    /// </summary>
    public static ContextScope BeginContext(string kind, string? id)
    {
        if (!s_enabled) return default;
        var scope = new ContextScope(t_contextKind, t_contextId, active: true);
        t_contextKind = kind;
        t_contextId = id;
        return scope;
    }

    /// <summary>현재 스레드의 이벤트 문맥(없으면 null, null).</summary>
    public static (string? Kind, string? Id) CurrentContext => (t_contextKind, t_contextId);

    /// <summary><see cref="BeginContext"/> 범위 — Dispose 에서 앞 문맥으로 되돌린다.</summary>
    public readonly struct ContextScope : IDisposable
    {
        private readonly string? _previousKind;
        private readonly string? _previousId;
        private readonly bool _active;

        internal ContextScope(string? previousKind, string? previousId, bool active)
        {
            _previousKind = previousKind;
            _previousId = previousId;
            _active = active;
        }

        public void Dispose()
        {
            if (!_active) return;
            t_contextKind = _previousKind;
            t_contextId = _previousId;
        }
    }

    // ───────────────────────── 받는 곳 ─────────────────────────

    /// <summary>
    /// 기록을 받는 곳을 더한다(시험 · 호스트 진단). 받는 곳이 하나라도 있으면 환경변수와 무관하게 계측이 켜진다.
    /// 받는 곳은 찍은 스레드에서 바로 불린다 — 빨리 끝내야 한다. Dispose 하면 빠진다.
    /// </summary>
    public static IDisposable AddSink(Action<PerfTraceRecord> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (s_sinkGate)
        {
            var next = new Action<PerfTraceRecord>[s_sinks.Length + 1];
            Array.Copy(s_sinks, next, s_sinks.Length);
            next[^1] = sink;
            s_sinks = next;
            s_enabled = true;
        }
        return new SinkRegistration(sink);
    }

    /// <summary>시험용 붙잡기 — 범위 안의 모든 기록을 메모리에 모은다(다른 시험의 기록도 섞이므로 자기 id 로 거를 것).</summary>
    public static PerfTraceCapture Capture() => new();

    private static void RemoveSink(Action<PerfTraceRecord> sink)
    {
        lock (s_sinkGate)
        {
            int index = Array.IndexOf(s_sinks, sink);
            if (index < 0) return;
            var next = new Action<PerfTraceRecord>[s_sinks.Length - 1];
            if (index > 0) Array.Copy(s_sinks, 0, next, 0, index);
            if (index < s_sinks.Length - 1) Array.Copy(s_sinks, index + 1, next, index, s_sinks.Length - index - 1);
            s_sinks = next;
            s_enabled = s_environmentEnabled || next.Length > 0;
        }
    }

    private sealed class SinkRegistration : IDisposable
    {
        private Action<PerfTraceRecord>? _sink;
        public SinkRegistration(Action<PerfTraceRecord> sink) => _sink = sink;
        public void Dispose()
        {
            var sink = Interlocked.Exchange(ref _sink, null);
            if (sink is not null) RemoveSink(sink);
        }
    }

    private static void Emit(PerfTraceRecord record)
    {
        foreach (var sink in s_sinks)
        {
            try { sink(record); }
            catch { /* 받는 곳 실패는 삼킨다 */ }
        }
        if (!s_environmentEnabled) return;
        EnsureFile()?.Enqueue(record);
    }

    private static PerfTraceFileWriter? EnsureFile()
    {
        var file = Volatile.Read(ref s_file);
        if (file is not null || Volatile.Read(ref s_fileState) == 2) return file;
        lock (s_sinkGate)
        {
            if (s_file is not null || s_fileState == 2) return s_file;
            try
            {
                var path = ResolveFilePath(SafeGetEnvironment(FileVariable), LocalAppDataFolder.Path, s_pid);
                var writer = new PerfTraceFileWriter(path);
                var (utc, mono) = Now();
                writer.Enqueue(new PerfTraceRecord
                {
                    T = utc, Wall = DateTime.UtcNow, MonoMs = mono, Pid = s_pid, Tid = Environment.CurrentManagedThreadId,
                    Stage = PerfStages.TraceStart, Kind = PerfKinds.Gis,
                    Extra = Extra(("process", SafeProcessName()), ("machine", Environment.MachineName),
                                  ("stopwatch_hz", Stopwatch.Frequency), ("anchor_utc", s_anchorUtc.ToString("O", CultureInfo.InvariantCulture))),
                });
                AppDomain.CurrentDomain.ProcessExit += (_, _) => writer.Dispose();
                Volatile.Write(ref s_file, writer);
                s_fileState = 1;
                return writer;
            }
            catch
            {
                s_fileState = 2;   // 경로 · 권한 문제 — 다시 시도하지 않는다(매 기록마다 예외 비용을 내지 않게)
                return null;
            }
        }
    }

    private static string SafeProcessName()
    {
        try { using var p = System.Diagnostics.Process.GetCurrentProcess(); return p.ProcessName; }
        catch { return "?"; }
    }

    private static string? SafeGetEnvironment(string name)
    {
        try { return Environment.GetEnvironmentVariable(name); }
        catch { return null; }
    }
}

/// <summary>시험용 메모리 붙잡기(<see cref="PerfTrace.Capture"/>).</summary>
public sealed class PerfTraceCapture : IDisposable
{
    private readonly List<PerfTraceRecord> _records = new();
    private readonly IDisposable _registration;

    internal PerfTraceCapture()
    {
        _registration = PerfTrace.AddSink(r =>
        {
            lock (_records) _records.Add(r);
        });
    }

    /// <summary>지금까지 모은 기록(사본, 찍힌 순서).</summary>
    public IReadOnlyList<PerfTraceRecord> Records
    {
        get { lock (_records) return _records.ToArray(); }
    }

    /// <summary>한 이벤트(종류 + id)의 기록(시각 순).</summary>
    public IReadOnlyList<PerfTraceRecord> For(string kind, string id)
        => Records.Where(r => r.Kind == kind && r.Id == id).OrderBy(r => r.T).ToArray();

    public void Dispose() => _registration.Dispose();
}
