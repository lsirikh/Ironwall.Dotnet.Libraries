using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using DummyCameras.Control;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using DummyCameras.Onvif;
using DummyCameras.Ptz;
using DummyCameras.Streams;

namespace DummyCameras;

public sealed class FarmOptions
{
    public int Count { get; set; } = 8;
    public int PtzCount { get; set; } = 4;              // cameras 1..PtzCount are PTZ, the rest fixed
    public int OnvifBasePort { get; set; } = 8180;      // cam N → base + N (0 = free ports, tests)
    public int RtspPort { get; set; } = 8554;
    public int RtpPort { get; set; } = 18000;           // UDP RTP/RTCP (MediaMTX default 8000 clashes with the API server's number)
    public int MediaMtxApiPort { get; set; } = 9997;
    public int ControlPort { get; set; } = 8180;        // base port itself (cam N = base + N) · -1 = off
    public string User { get; set; } = "dummy";
    public string Password { get; set; } = "dummy1234";
    public double GotoSeconds { get; set; } = 3.0;
    public double DefaultPtzTimeoutSeconds { get; set; } = 5.0;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public int SubWidth { get; set; } = 640;
    public int SubHeight { get; set; } = 360;
    public int Fps { get; set; } = 15;
    public bool Sub { get; set; } = true;
    public string? Video { get; set; }                 // file or folder
    public bool Transcode { get; set; }                 // force overlay transcode for user video
    public bool EnableRtsp { get; set; } = true;        // false = ONVIF only (in-process tests)
    public bool AnonymousRtsp { get; set; }
    public string? FfmpegPath { get; set; }
    public string? FfprobePath { get; set; }
    public string? MediaMtxPath { get; set; }
    public string OutDir { get; set; } = Path.Combine(AppContext.BaseDirectory, "run");
    public Dictionary<int, Action<CameraFaults>> FaultSetters { get; } = new();
}

/// <summary>Runtime of one camera.</summary>
public sealed class CameraRuntime
{
    public required CameraSpec Spec { get; init; }
    public PtzSimulator? Ptz { get; init; }
    public required OnvifResponder Onvif { get; init; }
    public StreamPublisher? Publisher { get; set; }
}

/// <summary>
/// The whole dummy farm: MediaMTX (RTSP server) + one ffmpeg publisher per camera + one ONVIF responder per camera
/// + overlay writer + control endpoint. Dispose stops everything (children also die with the job object).
/// </summary>
public sealed class Farm : IAsyncDisposable
{
    private readonly List<CameraRuntime> _cameras = new();
    private ManagedProcess? _mediamtx;
    private OverlayWriter? _overlay;
    private ControlServer? _control;

    private Farm(FarmOptions options)
    {
        Options = options;
        Directory.CreateDirectory(options.OutDir);
        LogDir = Path.Combine(options.OutDir, "logs");
        Directory.CreateDirectory(LogDir);
        OnvifLog = new JsonlLog(Path.Combine(LogDir, "onvif-requests.jsonl"));
        PtzLog = new JsonlLog(Path.Combine(LogDir, "ptz-commands.jsonl"));
        StreamLog = new JsonlLog(Path.Combine(LogDir, "stream-events.jsonl"));
    }

    public FarmOptions Options { get; }
    public string LogDir { get; }
    public JsonlLog OnvifLog { get; }
    public JsonlLog PtzLog { get; }
    public JsonlLog StreamLog { get; }
    public IReadOnlyList<CameraRuntime> Cameras => _cameras;
    public int? ControlPort => _control?.Port;

    public static async Task<Farm> StartAsync(FarmOptions o, TextWriter log, CancellationToken ct)
    {
        if (o.Count < 1 || o.Count > 32) throw new ArgumentOutOfRangeException(nameof(o), "count must be 1..32");
        var farm = new Farm(o);
        try
        {
            await farm.StartCoreAsync(log, ct).ConfigureAwait(false);
            return farm;
        }
        catch
        {
            await farm.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task StartCoreAsync(TextWriter log, CancellationToken ct)
    {
        var o = Options;
        var videos = ResolveVideos(o.Video);
        var ffmpeg = o.EnableRtsp ? (o.FfmpegPath ?? ToolPaths.FindOnPath("ffmpeg") ?? throw new InvalidOperationException("ffmpeg not found on PATH (use --ffmpeg)")) : null;
        var ffprobe = o.FfprobePath ?? ToolPaths.FindOnPath("ffprobe");

        // 1) camera specs
        var probeCache = new Dictionary<string, VideoProbe?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i <= o.Count; i++)
        {
            var spec = new CameraSpec
            {
                Index = i,
                IsPtz = i <= o.PtzCount,
                OnvifPort = o.OnvifBasePort == 0 ? 0 : o.OnvifBasePort + i,
                RtspPort = o.RtspPort,
                MainWidth = o.Width, MainHeight = o.Height,
                SubWidth = o.SubWidth, SubHeight = o.SubHeight,
                Fps = o.Fps,
                HasSub = o.Sub,
            };
            if (videos.Count > 0)
            {
                var file = videos[(i - 1) % videos.Count];
                if (!probeCache.TryGetValue(file, out var probe))
                    probeCache[file] = probe = ffprobe is null ? null : await VideoProbe.RunAsync(ffprobe, file, ct).ConfigureAwait(false);
                int sharing = Enumerable.Range(1, o.Count).Count(k => (k - 1) % videos.Count == (i - 1) % videos.Count);
                int slot = (i - 1) / videos.Count;
                spec.VideoFile = file;
                spec.VideoOffsetSec = probe?.DurationSec is double d && d > 1 ? Math.Round(d * slot / Math.Max(1, sharing), 2) : 0;
                spec.CopyVideo = !o.Transcode && probe is { Codec: "h264" };
                if (spec.CopyVideo)
                {
                    spec.HasSub = false;
                    if (probe!.Width > 0) { spec.MainWidth = probe.Width; spec.MainHeight = probe.Height; }
                }
            }
            if (o.FaultSetters.TryGetValue(i, out var set)) set(spec.Faults);
            var ptz = spec.IsPtz
                ? new PtzSimulator(TimeSpan.FromSeconds(o.GotoSeconds), TimeSpan.FromSeconds(o.DefaultPtzTimeoutSeconds))
                : null;
            var onvif = new OnvifResponder(spec, o.User, o.Password, ptz, OnvifLog, PtzLog);
            onvif.Start();
            _cameras.Add(new CameraRuntime { Spec = spec, Ptz = ptz, Onvif = onvif });
        }

        // 2) control endpoint (loopback)
        if (o.ControlPort >= 0) _control = new ControlServer(this, o.ControlPort);

        if (!o.EnableRtsp) return;

        // 3) MediaMTX
        var mtxExe = o.MediaMtxPath ?? await MediaMtxInstaller.EnsureAsync(log, ct).ConfigureAwait(false);
        var cfgPath = Path.Combine(o.OutDir, "mediamtx.yml");
        await File.WriteAllTextAsync(cfgPath, MediaMtxConfig.Build(
            new MediaMtxConfig.Settings(o.RtspPort, o.RtpPort, o.MediaMtxApiPort, o.User, o.Password, o.AnonymousRtsp),
            _cameras.Select(c => c.Spec)), ct).ConfigureAwait(false);
        _mediamtx = ManagedProcess.Start(mtxExe, new[] { cfgPath }, o.OutDir, Path.Combine(LogDir, "mediamtx.log"));
        if (!await WaitTcpAsync(o.RtspPort, TimeSpan.FromSeconds(15), ct).ConfigureAwait(false))
            throw new InvalidOperationException($"MediaMTX did not open 127.0.0.1:{o.RtspPort}. Last output:\n  " + string.Join("\n  ", _mediamtx.Tail));

        // 4) overlay files + publishers
        var overlayDir = Path.Combine(o.OutDir, "overlay");
        Directory.CreateDirectory(overlayDir);
        bool font = CopyFont(overlayDir);
        _overlay = new OverlayWriter(overlayDir, _cameras.Select(c => (c.Spec, c.Ptz)).ToList());
        foreach (var c in _cameras)
        {
            c.Publisher = new StreamPublisher(c.Spec, ffmpeg!, overlayDir, LogDir, font, StreamLog);
            c.Publisher.Start();
        }
    }

    /// <summary>Waits until every non-killed camera's paths are ready in MediaMTX (API /v3/paths/list).</summary>
    public async Task<bool> WaitStreamsReadyAsync(TimeSpan timeout, CancellationToken ct)
    {
        if (!Options.EnableRtsp) return true;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var want = _cameras.Where(c => c.Publisher is { IsKilled: false }).SelectMany(c => c.Spec.Paths).ToHashSet();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
        {
            try
            {
                var json = await http.GetStringAsync($"http://127.0.0.1:{Options.MediaMtxApiPort}/v3/paths/list", ct).ConfigureAwait(false);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var ready = doc.RootElement.GetProperty("items").EnumerateArray()
                    .Where(i => i.TryGetProperty("ready", out var r) && r.ValueKind == System.Text.Json.JsonValueKind.True)
                    .Select(i => i.GetProperty("name").GetString())
                    .ToHashSet();
                if (want.All(ready.Contains)) return true;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            await Task.Delay(300, ct).ConfigureAwait(false);
        }
        return false;
    }

    public CameraRuntime? Find(int index) => _cameras.FirstOrDefault(c => c.Spec.Index == index);

    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"  {"cam",-6}{"type",-7}{"ONVIF device_service",-48}{"RTSP main / sub",-60}faults");
        foreach (var c in _cameras)
        {
            var s = c.Spec;
            var rtsp = Options.EnableRtsp ? s.MainRtspUrl + (s.HasSub ? " / .../" + s.SubPath : string.Empty) : "(rtsp off)";
            sb.AppendLine($"  {s.Id,-6}{(s.IsPtz ? "PTZ" : "FIXED"),-7}{s.DeviceServiceUrl,-48}{rtsp,-60}{s.Faults}");
            if (s.VideoFile is not null)
                sb.AppendLine($"        video={Path.GetFileName(s.VideoFile)} offset={s.VideoOffsetSec.ToString(CultureInfo.InvariantCulture)}s {(s.CopyVideo ? "copy" : "transcode")}");
        }
        sb.AppendLine($"  logs: {LogDir}");
        if (_control is not null) sb.AppendLine($"  control: http://127.0.0.1:{_control.Port}/status");
        return sb.ToString();
    }

    private static List<string> ResolveVideos(string? video)
    {
        if (string.IsNullOrWhiteSpace(video)) return new List<string>();
        if (File.Exists(video)) return new List<string> { Path.GetFullPath(video) };
        if (Directory.Exists(video))
        {
            var ext = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".avi", ".ts", ".m4v", ".webm" };
            var files = Directory.EnumerateFiles(video).Where(f => ext.Contains(Path.GetExtension(f))).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0) throw new InvalidOperationException($"no video files in {video}");
            return files;
        }
        throw new FileNotFoundException("video not found", video);
    }

    private static bool CopyFont(string dir)
    {
        foreach (var candidate in new[] { "consola.ttf", "arial.ttf", "segoeui.ttf" })
        {
            var src = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), candidate);
            if (!File.Exists(src)) continue;
            try { File.Copy(src, Path.Combine(dir, FfmpegArgs.FontFile), overwrite: true); return true; }
            catch (IOException) { return File.Exists(Path.Combine(dir, FfmpegArgs.FontFile)); }
        }
        return false;
    }

    private static async Task<bool> WaitTcpAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                using var c = new TcpClient();
                await c.ConnectAsync("127.0.0.1", port, ct).ConfigureAwait(false);
                return true;
            }
            catch (SocketException) { await Task.Delay(200, ct).ConfigureAwait(false); }
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_control is not null) await _control.DisposeAsync().ConfigureAwait(false);
        foreach (var c in _cameras)
        {
            if (c.Publisher is not null) await c.Publisher.DisposeAsync().ConfigureAwait(false);
            await c.Onvif.DisposeAsync().ConfigureAwait(false);
        }
        _overlay?.Dispose();
        _mediamtx?.Dispose();
        OnvifLog.Dispose();
        PtzLog.Dispose();
        StreamLog.Dispose();
    }
}

/// <summary>ffprobe result for a user video (codec · size · duration).</summary>
public sealed record VideoProbe(string? Codec, int Width, int Height, double? DurationSec)
{
    public static async Task<VideoProbe?> RunAsync(string ffprobe, string file, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(ffprobe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height:format=duration", "-of", "json", file })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var json = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        if (p.ExitCode != 0) return null;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        string? codec = null; int w = 0, h = 0; double? dur = null;
        if (root.TryGetProperty("streams", out var st) && st.GetArrayLength() > 0)
        {
            var s0 = st[0];
            codec = s0.TryGetProperty("codec_name", out var c) ? c.GetString() : null;
            w = s0.TryGetProperty("width", out var wv) ? wv.GetInt32() : 0;
            h = s0.TryGetProperty("height", out var hv) ? hv.GetInt32() : 0;
        }
        if (root.TryGetProperty("format", out var f) && f.TryGetProperty("duration", out var dv)
            && double.TryParse(dv.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) dur = d;
        return new VideoProbe(codec, w, h, dur);
    }
}
