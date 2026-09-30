using System.Globalization;
using DummyCameras;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using DummyCameras.Seed;

return await Cli.MainAsync(args).ConfigureAwait(false);

internal static class Cli
{
    private const string Help = """
        dummy-cameras - loopback dummy IP cameras (RTSP + ONVIF) for headed camera-popup tests

        COMMANDS
          run              start MediaMTX + N ffmpeg publishers + N ONVIF devices (default command)
          fetch-mediamtx   download MediaMTX (pinned version + SHA-256) into tools/dummy-cameras/bin/
          seed             register the cameras on the loopback test server (DRY-RUN unless --apply)
          cleanup          delete ONLY the rows in the seed manifest (DRY-RUN unless --apply)
          help

        RUN OPTIONS
          --count 8  --ptz 4                cameras 1..ptz are PTZ, the rest fixed
          --onvif-base-port 8180            cam N ONVIF = http://127.0.0.1:(base+N)/onvif/device_service
                                            (808N collides with Docker on this PC); control/status = base port
          --rtsp-port 8554  --rtp-port 18000  --mtx-api-port 9997
          --user dummy  --password dummy1234  (camera account for ONVIF + RTSP)
          --video <file|folder>             user footage, round-robin with start offsets (h264 -> -c copy)
          --transcode                       force overlay transcode for user footage
          --size 1280x720  --sub-size 640x360  --fps 15  --no-sub
          --goto-seconds 3                  GotoPreset / Home / Absolute travel time
          --onvif-only                      no MediaMTX / ffmpeg
          --anonymous-rtsp                  RTSP read without credentials
          --out <dir>                       logs (onvif-requests.jsonl · ptz-commands.jsonl · stream-events.jsonl)
          --duration <s>                    stop after s seconds (default: until Ctrl+C)
          --ffmpeg <path>  --ffprobe <path>  --mediamtx <path>
        FAULT FLAGS (per camera; list or N=value)
          --slow-onvif 5=3000[,6=1500]      delay every ONVIF reply (ms)
          --no-onvif-reply 5[,6]            never answer ONVIF
          --auth-fail 6                     WS-Security + RTSP always rejected
          --http-digest 7|all               demand HTTP Digest on ONVIF (401 challenge)
          --kill-stream-after 7=30          stop the RTSP publisher after 30 s (camera power off)
          --corrupt-stream 8                bit-flip the H.264 packets
        SEED / CLEANUP OPTIONS
          --apply                           actually write (default is a dry-run that makes NO network call)
          --api https://127.0.0.1:8000/api  (loopback only)
          --cred <file>                     injector account: only the id= / pw= lines are read
          --ca <pem>                        local CA for the test server TLS (default: api-test-server mkcert root)
          --manifest <path>                 default tools/dummy-cameras/out/seed-manifest.json
          --sensor-id <id>[,<id>]           put EXISTING sensors in the group instead of creating LRT-DUMMY-SENSOR
          --delay 3                         mapping delay_time for PTZ cam1 (cam k: delay+k-1)
          (--count/--ptz/--onvif-base-port/--rtsp-port/--user/--password/--no-sub must match the run)
        """;

    public static async Task<int> MainAsync(string[] argv)
    {
        var args = new Args(argv);
        var cmd = args.Command ?? "run";
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        try
        {
            return cmd switch
            {
                "run" => await RunAsync(args, cts.Token).ConfigureAwait(false),
                "fetch-mediamtx" => await FetchAsync(cts.Token).ConfigureAwait(false),
                "seed" => await SeedAsync(args, cleanup: false, cts.Token).ConfigureAwait(false),
                "cleanup" => await SeedAsync(args, cleanup: true, cts.Token).ConfigureAwait(false),
                "help" or "-h" or "--help" or "/?" => PrintHelp(),
                _ => Fail($"unknown command '{cmd}'"),
            };
        }
        catch (OperationCanceledException) { return 130; }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FileNotFoundException or FormatException)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    private static int PrintHelp() { Console.WriteLine(Help); return 0; }

    private static int Fail(string msg) { Console.Error.WriteLine("error: " + msg); Console.Error.WriteLine(Help); return 2; }

    private static async Task<int> FetchAsync(CancellationToken ct)
    {
        var exe = await MediaMtxInstaller.EnsureAsync(Console.Out, ct).ConfigureAwait(false);
        Console.WriteLine($"mediamtx {MediaMtxInstaller.Version}: {exe}");
        return 0;
    }

    public static FarmOptions ParseFarm(Args a)
    {
        var o = new FarmOptions
        {
            Count = a.Int("count", 8),
            PtzCount = a.Int("ptz", 4),
            OnvifBasePort = a.Int("onvif-base-port", 8180),
            RtspPort = a.Int("rtsp-port", 8554),
            RtpPort = a.Int("rtp-port", 18000),
            MediaMtxApiPort = a.Int("mtx-api-port", 9997),
            User = a.Str("user") ?? "dummy",
            Password = a.Str("password") ?? "dummy1234",
            GotoSeconds = a.Dbl("goto-seconds", 3),
            Fps = a.Int("fps", 15),
            Sub = !a.Flag("no-sub"),
            Video = a.Str("video"),
            Transcode = a.Flag("transcode"),
            EnableRtsp = !a.Flag("onvif-only"),
            AnonymousRtsp = a.Flag("anonymous-rtsp"),
            FfmpegPath = a.Str("ffmpeg"),
            FfprobePath = a.Str("ffprobe"),
            MediaMtxPath = a.Str("mediamtx"),
        };
        o.ControlPort = a.Int("control-port", o.OnvifBasePort);
        if (a.Str("out") is { } outDir) o.OutDir = Path.GetFullPath(outDir);
        if (a.Str("size") is { } size) (o.Width, o.Height) = Size(size);
        if (a.Str("sub-size") is { } sub) (o.SubWidth, o.SubHeight) = Size(sub);

        foreach (var (cam, v) in a.PerCamera("slow-onvif", o.Count)) Add(o, cam, f => f.SlowOnvifMs = v ?? 3000);
        foreach (var (cam, _) in a.PerCamera("no-onvif-reply", o.Count)) Add(o, cam, f => f.NoOnvifReply = true);
        foreach (var (cam, _) in a.PerCamera("auth-fail", o.Count)) Add(o, cam, f => f.AuthFail = true);
        foreach (var (cam, _) in a.PerCamera("http-digest", o.Count)) Add(o, cam, f => f.HttpDigest = true);
        foreach (var (cam, v) in a.PerCamera("kill-stream-after", o.Count)) Add(o, cam, f => f.KillStreamAfterSec = v ?? 30);
        foreach (var (cam, _) in a.PerCamera("corrupt-stream", o.Count)) Add(o, cam, f => f.CorruptStream = true);
        return o;
    }

    private static void Add(FarmOptions o, int cam, Action<CameraFaults> set)
    {
        o.FaultSetters[cam] = o.FaultSetters.TryGetValue(cam, out var prev) ? prev + set : set;
    }

    private static (int, int) Size(string s)
    {
        var p = s.ToLowerInvariant().Split('x');
        if (p.Length != 2) throw new FormatException($"size must be WxH: {s}");
        return (int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture));
    }

    private static async Task<int> RunAsync(Args a, CancellationToken ct)
    {
        var o = ParseFarm(a);
        var duration = a.Dbl("duration", 0);
        Console.WriteLine($"dummy-cameras: {o.Count} cameras ({o.PtzCount} PTZ) - loopback only");
        await using var farm = await Farm.StartAsync(o, Console.Out, ct).ConfigureAwait(false);
        Console.Write(farm.Describe());
        if (o.EnableRtsp)
        {
            Console.WriteLine("  waiting for streams...");
            bool ready = await farm.WaitStreamsReadyAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            Console.WriteLine(ready ? "  all streams ready." : "  WARNING: not every stream is ready - see logs/ffmpeg-camN.log, mediamtx.log");
        }
        Console.WriteLine(duration > 0 ? $"running for {duration}s..." : "running - Ctrl+C to stop.");
        try { await Task.Delay(duration > 0 ? TimeSpan.FromSeconds(duration) : Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        Console.WriteLine("stopping...");
        return 0;
    }

    private static async Task<int> SeedAsync(Args a, bool cleanup, CancellationToken ct)
    {
        var o = new SeedOptions
        {
            Apply = a.Flag("apply"),
            CredentialFile = a.Str("cred"),
            Count = a.Int("count", 8),
            PtzCount = a.Int("ptz", 4),
            OnvifBasePort = a.Int("onvif-base-port", 8180),
            RtspPort = a.Int("rtsp-port", 8554),
            Sub = !a.Flag("no-sub"),
            CameraUser = a.Str("user") ?? "dummy",
            CameraPassword = a.Str("password") ?? "dummy1234",
            DelayBase = a.Int("delay", 3),
            GotoSeconds = a.Dbl("goto-seconds", 3),
        };
        if (a.Str("api") is { } api) o.Api = new Uri(api);
        if (a.Str("ca") is { } ca) o.LocalCaPem = ca;
        if (a.Str("manifest") is { } m) o.Manifest = Path.GetFullPath(m);
        foreach (var s in (a.Str("sensor-id") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            o.ExistingSensorIds.Add(int.Parse(s, CultureInfo.InvariantCulture));
        LoopbackGuard.Assert(o.Api);

        Console.WriteLine($"[{(cleanup ? "cleanup" : "seed")}] {(o.Apply ? "APPLY" : "DRY-RUN (no network)")} -> {o.Api}");
        using ISeedApi transport = o.Apply ? new LiveSeedApi(o.Api, o.LocalCaPem) : new DryRunSeedApi(Console.Out);
        return cleanup
            ? await new Cleaner(o, transport, Console.Out).RunAsync(ct).ConfigureAwait(false)
            : await new Seeder(o, transport, Console.Out).RunAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>Tiny argument reader: first bare word = command; options take one or two dashes.</summary>
internal sealed class Args
{
    private readonly Dictionary<string, string?> _opts = new(StringComparer.OrdinalIgnoreCase);

    public Args(string[] argv)
    {
        for (int i = 0; i < argv.Length; i++)
        {
            var t = argv[i];
            if (t.StartsWith('-') && t.Length > 1 && !double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                var key = t.TrimStart('-');
                string? val = null;
                int eq = key.IndexOf('=');
                if (eq > 0 && !key.Contains(' ')) { val = key[(eq + 1)..]; key = key[..eq]; }
                else if (i + 1 < argv.Length && !argv[i + 1].StartsWith('-')) val = argv[++i];
                _opts[key] = val;
            }
            else if (Command is null) Command = t;
        }
    }

    public string? Command { get; }
    public bool Flag(string k) => _opts.ContainsKey(k);
    public string? Str(string k) => _opts.TryGetValue(k, out var v) ? v : null;
    public int Int(string k, int d) => Str(k) is { } s ? int.Parse(s, CultureInfo.InvariantCulture) : d;
    public double Dbl(string k, double d) => Str(k) is { } s ? double.Parse(s, CultureInfo.InvariantCulture) : d;

    /// <summary>"5=3000,6" → (5,3000),(6,null) · "all" → every camera.</summary>
    public IEnumerable<(int Cam, int? Value)> PerCamera(string k, int count)
    {
        if (!_opts.TryGetValue(k, out var v)) yield break;
        if (string.IsNullOrWhiteSpace(v)) throw new FormatException($"--{k} needs camera numbers (e.g. 5 or 5=3000 or all)");
        foreach (var part in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i <= count; i++) yield return (i, null);
                continue;
            }
            int eq = part.IndexOf('=');
            int cam = int.Parse(eq < 0 ? part : part[..eq], CultureInfo.InvariantCulture);
            if (cam < 1 || cam > count) throw new FormatException($"--{k}: camera {cam} is outside 1..{count}");
            yield return (cam, eq < 0 ? null : int.Parse(part[(eq + 1)..], CultureInfo.InvariantCulture));
        }
    }
}
