using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Diagnostics;

/****************************************************************************
   Purpose      : RTSP 스트리밍 단계별 계측(기본 꺼짐) — RTSP · RTP · NAL · Decode · Dispatcher · 렌더 단계의
                  지연(ms) · 처리량(fps · kbps) · 대기열 · CPU 를 5초마다 요약 한 줄로 전용 파일에 남긴다.
   Created By   : Claude
   Created On   : 2026-10-16
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 스트림 단계 계측. 켜는 법: 환경변수 <c>IRONWALL_STREAM_TRACE=1</c>(1 · true · yes · on) 또는
/// appsettings.json(호스트 폴더 · 그 위 GIS 폴더)의 <c>"CameraHostStreamTrace": true</c>(루트 또는 <c>"AppSettings"</c> 안).
/// 기록: <c>{호스트 로그 폴더}\camera-stream-trace-yyyyMMdd.log</c> — 줄마다 즉시 flush(버퍼링 없음).
/// <para>LibVLC 안쪽(RTSP 협상 · RTP · NAL 조립 · 디코드)은 우리 코드가 아니라 <c>media.Statistics</c> 누적값의 차이와
/// 콜백 시각으로 나눈다: RTSP = Play → ESAdded 이벤트(협상 끝) · RTP = 입력 바이트 · NAL = 디먹스 바이트 · 깨짐 · 끊김 ·
/// Decode = 디코드 프레임 · lock→display 간격 · 표시 = 표시 · 버린 그림. 우리 코드(복사 · 디스패처 · WritePixels)는 직접 잰다.</para>
/// 꺼져 있으면 <see cref="IsOn"/> 읽기 한 번으로 끝난다. 어떤 메서드도 예외를 밖으로 던지지 않는다.
/// </summary>
internal static class StreamTrace
{
    public const string EnabledVariable = "IRONWALL_STREAM_TRACE";
    public const string SettingsKey = "CameraHostStreamTrace";
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private static volatile bool s_on;
    private static readonly ConcurrentDictionary<StreamCounters, byte> s_streams = new();
    private static StreamWriter? s_writer;
    private static Timer? s_timer;
    private static long s_lastTick;
    private static TimeSpan s_lastCpu;

    // 디스패처 · 렌더(모든 타일 합계) — BitmapFrameSink · SharedMemoryFrameSink 가 올린다.
    private static long s_sinkWrites, s_sinkCopyTicks, s_sinkCopyMax;
    private static long s_coalesced;
    private static long s_flushes, s_dispatchWaitTicks, s_dispatchWaitMax;
    private static long s_renderTicks, s_renderMax;
    private static long s_pending;

    // UI 넘김 탐침 — 5초마다 우선순위별로 하나씩 넣고 처리까지 걸린 시간을 다음 요약에 적는다(-1 = 아직 처리 안 됨).
    private static readonly DispatcherPriority[] s_probePriorities = { DispatcherPriority.Send, DispatcherPriority.Normal, DispatcherPriority.Render, DispatcherPriority.Background };
    private static readonly long[] s_probePosted = new long[4];
    private static readonly long[] s_probeMs = { -1, -1, -1, -1 };
    private static Dispatcher? s_dispatcher;

    public static bool IsOn => s_on;

    /// <summary>UI 디스패처를 붙인다(우선순위별 넘김 지연 탐침).</summary>
    public static void AttachDispatcher(Dispatcher dispatcher) => s_dispatcher = dispatcher;

    private static string ProbeUi()
    {
        var sb = new StringBuilder("ui-probe");
        long now = Stopwatch.GetTimestamp();
        for (int i = 0; i < s_probePriorities.Length; i++)
        {
            long ms = Interlocked.Read(ref s_probeMs[i]);
            long posted = Interlocked.Read(ref s_probePosted[i]);
            string name = s_probePriorities[i].ToString().ToLowerInvariant();
            if (posted != 0 && ms < 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $" {name}=>{Ms(now - posted):0}ms(pending)");
                continue; // 앞 탐침이 아직 — 줄을 불리지 않는다
            }
            if (posted != 0) sb.Append(CultureInfo.InvariantCulture, $" {name}={ms}ms");
            var dispatcher = s_dispatcher;
            if (dispatcher is null) continue;
            int slot = i;
            Interlocked.Exchange(ref s_probeMs[slot], -1);
            Interlocked.Exchange(ref s_probePosted[slot], Stopwatch.GetTimestamp());
            try
            {
                dispatcher.BeginInvoke(s_probePriorities[slot], new Action(() =>
                    Interlocked.Exchange(ref s_probeMs[slot], (long)Ms(Stopwatch.GetTimestamp() - Interlocked.Read(ref s_probePosted[slot])))));
            }
            catch (InvalidOperationException) { }
        }
        return sb.ToString();
    }

    /// <summary>환경변수 · 설정을 읽어 켜고 5초 요약 타이머를 건다(호스트 시작 때 한 번).</summary>
    public static void Start(HostLog log)
    {
        try
        {
            if (!ReadEnabled(Environment.GetEnvironmentVariable(EnabledVariable)) && !ReadSettings(SettingsCandidates())) return;
            var dir = Path.GetDirectoryName(log.FilePath) ?? Path.GetTempPath();
            var path = Path.Combine(dir, $"camera-stream-trace-{DateTime.Now:yyyyMMdd}.log");
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            s_writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using (var p = Process.GetCurrentProcess()) s_lastCpu = p.TotalProcessorTime;
            s_lastTick = Stopwatch.GetTimestamp();
            s_on = true;
            Write($"trace start pid={Environment.ProcessId} cores={Environment.ProcessorCount} interval={Interval.TotalSeconds:0}s");
            s_timer = new Timer(_ => Flush(), null, Interval, Interval);
            log.Info($"stream trace on → {path}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warn($"stream trace start failed: {ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>값 해석(순수) — 1 · true · yes · on.</summary>
    public static bool ReadEnabled(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || v.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>appsettings JSON 에서 <see cref="SettingsKey"/>(루트 또는 <c>AppSettings</c> 안)를 읽는다(순수). 없거나 깨졌으면 false.</summary>
    public static bool ReadSettingsJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (root.TryGetProperty(SettingsKey, out var v) || (root.TryGetProperty("AppSettings", out var app)
                                                               && app.ValueKind == JsonValueKind.Object && app.TryGetProperty(SettingsKey, out v)))
            {
                return v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && ReadEnabled(v.GetString()));
            }
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<string> SettingsCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "appsettings.json");
        var parent = Directory.GetParent(baseDir.TrimEnd(Path.DirectorySeparatorChar));
        if (parent is not null) yield return Path.Combine(parent.FullName, "appsettings.json");
    }

    private static bool ReadSettings(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            try
            {
                if (File.Exists(file) && ReadSettingsJson(File.ReadAllText(file))) return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return false;
    }

    // ───────── 스트림 등록 ─────────

    /// <summary>켜져 있으면 스트림 계수기를 만들어 등록한다. 꺼져 있으면 null.</summary>
    public static StreamCounters? Register(string name, Func<MediaStats?> stats)
    {
        if (!s_on) return null;
        var c = new StreamCounters(name, stats);
        s_streams[c] = 0;
        return c;
    }

    public static void Unregister(StreamCounters? counters)
    {
        if (counters is not null) s_streams.TryRemove(counters, out _);
    }

    // ───────── 싱크(디스패처 · 렌더) ─────────

    /// <summary>싱크가 프레임을 받아 복사했다. <paramref name="coalesced"/> = 앞 프레임이 아직 그려지지 않아 덮어썼다(그리지 않고 버림).</summary>
    public static void SinkWrite(long copyTicks, bool coalesced)
    {
        Interlocked.Increment(ref s_sinkWrites);
        Interlocked.Add(ref s_sinkCopyTicks, copyTicks);
        Max(ref s_sinkCopyMax, copyTicks);
        if (coalesced) Interlocked.Increment(ref s_coalesced);
        else Interlocked.Increment(ref s_pending);
    }

    /// <summary>UI 스레드에서 그렸다 — 넘김 대기(예약 → 실행) · 그리기(WritePixels) 시간.</summary>
    public static void Rendered(long waitTicks, long renderTicks)
    {
        Interlocked.Decrement(ref s_pending);
        Interlocked.Increment(ref s_flushes);
        Interlocked.Add(ref s_dispatchWaitTicks, waitTicks);
        Max(ref s_dispatchWaitMax, waitTicks);
        Interlocked.Add(ref s_renderTicks, renderTicks);
        Max(ref s_renderMax, renderTicks);
    }

    internal static void Max(ref long field, long value)
    {
        long current;
        while (value > (current = Volatile.Read(ref field)))
            if (Interlocked.CompareExchange(ref field, value, current) == current) break;
    }

    internal static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    // ───────── 5초 요약 ─────────

    private static void Flush()
    {
        try
        {
            long now = Stopwatch.GetTimestamp();
            double seconds = Math.Max(0.001, (now - s_lastTick) / (double)Stopwatch.Frequency);
            s_lastTick = now;
            TimeSpan cpu;
            using (var p = Process.GetCurrentProcess()) cpu = p.TotalProcessorTime;
            double cpuPct = (cpu - s_lastCpu).TotalSeconds / seconds / Environment.ProcessorCount * 100.0;
            s_lastCpu = cpu;

            var total = new StreamSample();
            int streams = 0, playing = 0;
            foreach (var c in s_streams.Keys)
            {
                var s = c.Sample(seconds);
                streams++;
                if (s.DisplayFps > 0) playing++;
                total.Add(s);
                Write(c.Format(s));
            }

            long writes = Interlocked.Exchange(ref s_sinkWrites, 0);
            long copy = Interlocked.Exchange(ref s_sinkCopyTicks, 0);
            long copyMax = Interlocked.Exchange(ref s_sinkCopyMax, 0);
            long coalesced = Interlocked.Exchange(ref s_coalesced, 0);
            long flushes = Interlocked.Exchange(ref s_flushes, 0);
            long wait = Interlocked.Exchange(ref s_dispatchWaitTicks, 0);
            long waitMax = Interlocked.Exchange(ref s_dispatchWaitMax, 0);
            long render = Interlocked.Exchange(ref s_renderTicks, 0);
            long renderMax = Interlocked.Exchange(ref s_renderMax, 0);
            long pending = Volatile.Read(ref s_pending);
            string probe = ProbeUi();

            Write(string.Create(CultureInfo.InvariantCulture,
                $"TOTAL streams={streams} playing={playing} cpu={cpuPct:0.0}% | " +
                $"rtsp play→es={total.PlayingMsAvg:0}ms first-frame={total.FirstFrameMsAvg:0}ms queue={total.QueueMsAvg:0}ms | " +
                $"rtp in={total.InKbps:0}kbps | nal demux={total.DemuxKbps:0}kbps corrupt={total.Corrupted} discont={total.Discontinuity} | " +
                $"decode={total.DecodedFps:0.0}fps lock→display avg={total.LockToDisplayAvgMs:0.0}ms max={total.LockToDisplayMaxMs:0.0}ms | " +
                $"vout displayed={total.DisplayFps:0.0}fps lost={total.Lost} callback avg={total.CallbackAvgMs:0.00}ms max={total.CallbackMaxMs:0.00}ms | " +
                $"sink writes={writes / seconds:0.0}/s copy avg={(writes == 0 ? 0 : Ms(copy) / writes):0.00}ms max={Ms(copyMax):0.00}ms | " +
                $"dispatcher flush={flushes / seconds:0.0}/s wait avg={(flushes == 0 ? 0 : Ms(wait) / flushes):0.0}ms max={Ms(waitMax):0.0}ms queue={pending} coalesced={coalesced / seconds:0.0}/s | " +
                $"render writepixels avg={(flushes == 0 ? 0 : Ms(render) / flushes):0.00}ms max={Ms(renderMax):0.00}ms ui={Ms(render) / seconds / 10:0.0}% | {probe}"));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Write($"trace flush failed: {ex.GetType().Name} {ex.Message}");
        }
    }

    private static void Write(string message)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        lock (s_streams)
        {
            try { s_writer?.WriteLine(line); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
    }
}

/// <summary>스트림 하나의 단계 계수기(생산자가 올린다). 모든 메서드는 어느 스레드에서나.</summary>
internal sealed class StreamCounters
{
    private readonly string _name;
    private readonly Func<MediaStats?> _stats;
    private MediaStats _lastStats;
    private bool _hasStats;

    private long _queueMs = -1, _playingMs = -1, _firstFrameMs = -1;
    private long _playTicks;
    private long _displays, _lockToDisplayTicks, _lockToDisplayMax, _callbackTicks, _callbackMax;

    public StreamCounters(string name, Func<MediaStats?> stats)
    {
        _name = name;
        _stats = stats;
    }

    /// <summary>연결 줄에서 기다린 시간(ms).</summary>
    public void Queued(long ms) => Interlocked.Exchange(ref _queueMs, ms);

    /// <summary>Play 호출 시각.</summary>
    public void PlayCalled() => Interlocked.Exchange(ref _playTicks, Stopwatch.GetTimestamp());

    /// <summary>LibVLC ESAdded 이벤트(첫 번) — RTSP 연결 · DESCRIBE(SDP) · SETUP 이 끝나 영상 트랙이 생겼다.
    /// (Playing 이벤트는 Play 직후 곧바로 와 협상 시간을 담지 못한다.)</summary>
    public void Negotiated() => Interlocked.CompareExchange(ref _playingMs, SincePlay(), -1);

    public void FirstFrame() => Interlocked.CompareExchange(ref _firstFrameMs, SincePlay(), -1);

    /// <summary>display 콜백 — lock(디코드 버퍼 받음)부터 표시까지 · 우리 콜백(복사 · 싱크) 시간.</summary>
    public void Displayed(long lockToDisplayTicks, long callbackTicks)
    {
        Interlocked.Increment(ref _displays);
        if (lockToDisplayTicks > 0)
        {
            Interlocked.Add(ref _lockToDisplayTicks, lockToDisplayTicks);
            StreamTrace.Max(ref _lockToDisplayMax, lockToDisplayTicks);
        }
        Interlocked.Add(ref _callbackTicks, callbackTicks);
        StreamTrace.Max(ref _callbackMax, callbackTicks);
    }

    private long SincePlay()
    {
        long play = Volatile.Read(ref _playTicks);
        return play == 0 ? 0 : (long)StreamTrace.Ms(Stopwatch.GetTimestamp() - play);
    }

    /// <summary>타이머 스레드 — 지난 구간의 차이를 뽑고 0 으로 돌린다.</summary>
    public StreamSample Sample(double seconds)
    {
        long displays = Interlocked.Exchange(ref _displays, 0);
        long l2d = Interlocked.Exchange(ref _lockToDisplayTicks, 0);
        long l2dMax = Interlocked.Exchange(ref _lockToDisplayMax, 0);
        long cb = Interlocked.Exchange(ref _callbackTicks, 0);
        long cbMax = Interlocked.Exchange(ref _callbackMax, 0);
        var s = new StreamSample
        {
            Count = 1,
            QueueMs = Volatile.Read(ref _queueMs),
            PlayingMs = Volatile.Read(ref _playingMs),
            FirstFrameMs = Volatile.Read(ref _firstFrameMs),
            DisplayFps = displays / seconds,
            LockToDisplaySumMs = StreamTrace.Ms(l2d),
            LockToDisplayMaxMs = StreamTrace.Ms(l2dMax),
            CallbackSumMs = StreamTrace.Ms(cb),
            CallbackMaxMs = StreamTrace.Ms(cbMax),
            Displays = displays,
        };
        MediaStats? now = null;
        try { now = _stats(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
        if (now is { } n)
        {
            if (_hasStats)
            {
                s.InKbps = Math.Max(0, n.ReadBytes - _lastStats.ReadBytes) * 8.0 / 1000.0 / seconds;
                s.DemuxKbps = Math.Max(0, n.DemuxReadBytes - _lastStats.DemuxReadBytes) * 8.0 / 1000.0 / seconds;
                s.Corrupted = Math.Max(0, n.DemuxCorrupted - _lastStats.DemuxCorrupted);
                s.Discontinuity = Math.Max(0, n.DemuxDiscontinuity - _lastStats.DemuxDiscontinuity);
                s.DecodedFps = Math.Max(0, n.DecodedVideo - _lastStats.DecodedVideo) / seconds;
                s.VlcDisplayedFps = Math.Max(0, n.DisplayedPictures - _lastStats.DisplayedPictures) / seconds;
                s.Lost = Math.Max(0, n.LostPictures - _lastStats.LostPictures);
            }
            _lastStats = n;
            _hasStats = true;
        }
        return s;
    }

    public string Format(StreamSample s) => string.Create(CultureInfo.InvariantCulture,
        $"stream {_name} | rtsp queue={s.QueueMs}ms play→es={s.PlayingMs}ms first-frame={s.FirstFrameMs}ms | " +
        $"rtp in={s.InKbps:0}kbps | nal demux={s.DemuxKbps:0}kbps corrupt={s.Corrupted} discont={s.Discontinuity} | " +
        $"decode={s.DecodedFps:0.0}fps lock→display avg={(s.Displays == 0 ? 0 : s.LockToDisplaySumMs / s.Displays):0.0}ms max={s.LockToDisplayMaxMs:0.0}ms | " +
        $"vout displayed={s.DisplayFps:0.0}fps(vlc {s.VlcDisplayedFps:0.0}) lost={s.Lost} callback avg={(s.Displays == 0 ? 0 : s.CallbackSumMs / s.Displays):0.00}ms max={s.CallbackMaxMs:0.00}ms");
}

/// <summary>한 구간의 표본(스트림 하나 또는 합계).</summary>
internal sealed class StreamSample
{
    public int Count;
    public long QueueMs = -1, PlayingMs = -1, FirstFrameMs = -1;
    public double InKbps, DemuxKbps, DecodedFps, DisplayFps, VlcDisplayedFps;
    public long Corrupted, Discontinuity, Lost, Displays;
    public double LockToDisplaySumMs, LockToDisplayMaxMs, CallbackSumMs, CallbackMaxMs;

    private long _queueSum, _queueN, _playingSum, _playingN, _firstSum, _firstN;

    public double QueueMsAvg => _queueN == 0 ? 0 : (double)_queueSum / _queueN;
    public double PlayingMsAvg => _playingN == 0 ? 0 : (double)_playingSum / _playingN;
    public double FirstFrameMsAvg => _firstN == 0 ? 0 : (double)_firstSum / _firstN;
    public double LockToDisplayAvgMs => Displays == 0 ? 0 : LockToDisplaySumMs / Displays;
    public double CallbackAvgMs => Displays == 0 ? 0 : CallbackSumMs / Displays;

    public void Add(StreamSample s)
    {
        Count += s.Count;
        if (s.QueueMs >= 0) { _queueSum += s.QueueMs; _queueN++; }
        if (s.PlayingMs >= 0) { _playingSum += s.PlayingMs; _playingN++; }
        if (s.FirstFrameMs >= 0) { _firstSum += s.FirstFrameMs; _firstN++; }
        InKbps += s.InKbps;
        DemuxKbps += s.DemuxKbps;
        DecodedFps += s.DecodedFps;
        DisplayFps += s.DisplayFps;
        VlcDisplayedFps += s.VlcDisplayedFps;
        Corrupted += s.Corrupted;
        Discontinuity += s.Discontinuity;
        Lost += s.Lost;
        Displays += s.Displays;
        LockToDisplaySumMs += s.LockToDisplaySumMs;
        LockToDisplayMaxMs = Math.Max(LockToDisplayMaxMs, s.LockToDisplayMaxMs);
        CallbackSumMs += s.CallbackSumMs;
        CallbackMaxMs = Math.Max(CallbackMaxMs, s.CallbackMaxMs);
    }
}
