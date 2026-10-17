using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit.Abstractions;
using PixelRect = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages.PixelRect;

namespace DummyCameras.Tests;

/// <summary>
/// Load measurement (T-09 T7 · heartbeat): the real camera host exe + the dummy farm, no GIS.
/// 10 event windows x 6 tiles = 60 streams opened in one burst, then host CPU / private memory / playing count are sampled.
/// Opt-in (takes ~2 minutes and a lot of CPU): <c>IRONWALL_CAMHOST_LOAD=1</c>.
/// <list type="bullet">
/// <item><c>IRONWALL_CAMHOST_LOAD_HEADLESS=0</c> - show the real windows (WPF render cost included). Default: headless host.</item>
/// <item><c>IRONWALL_CAMHOST_LOAD_SECONDS</c> - measurement window (default 60).</item>
/// <item><c>IRONWALL_CAMHOST_LOAD_OUT</c> - write the result JSON there.</item>
/// <item><c>IRONWALL_CAMHOST_LOAD_WINDOWS</c> - number of windows (default 10) · <c>IRONWALL_CAMHOST_LOAD_OPENS</c> - concurrent opens (default: product default).</item>
/// </list>
/// </summary>
public class EventWindowLoadTests
{
    private static readonly int Windows = int.TryParse(Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD_WINDOWS"), out var w) ? w : 10;
    private static readonly int TilesPerWindow = int.TryParse(Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD_TILES"), out var t) ? t : 6;
    private static readonly bool Big = Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD_BIG") == "1";   // one large tile per window → main stream
    private readonly ITestOutputHelper _output;

    public EventWindowLoadTests(ITestOutputHelper output) => _output = output;

    [SkippableFact]
    [Trait("Category", "Load")]
    public async Task should_play_all_tiles_without_host_restart_when_ten_windows_of_six_tiles_open_at_once()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD") == "1", "opt-in: IRONWALL_CAMHOST_LOAD=1");
        Skip.If(ToolPaths.FindOnPath("ffmpeg") is null, "ffmpeg not on PATH");
        Skip.IfNot(File.Exists(MediaMtxInstaller.ExePath), "MediaMTX not installed - run: DummyCameras fetch-mediamtx");
        var exe = CameraHostExe.Find();
        Skip.If(exe is null, $"{CameraHostExe.ExeName} not built");
        bool headless = Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD_HEADLESS") != "0";
        int seconds = int.TryParse(Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD_SECONDS"), out var s) ? s : 60;

        // Arrange — the same farm shape as the headed T-09 round: 8 cameras, 1280x720 main + 640x360 sub, 15 fps
        var options = new FarmOptions
        {
            Count = 8,
            PtzCount = 4,
            OnvifBasePort = 0,
            ControlPort = 0,
            RtspPort = 18754,
            RtpPort = 18300,
            MediaMtxApiPort = 19797,
            GotoSeconds = 1.0,
            OutDir = TestDirs.NewOutDir("load"),
        };
        await using var farm = await Farm.StartAsync(options, TextWriter.Null, CancellationToken.None);
        Assert.True(await farm.WaitStreamsReadyAsync(TimeSpan.FromSeconds(40), CancellationToken.None), "farm streams not ready - see " + farm.LogDir);

        var settings = new CameraPopupSettings().Normalize();
        var opens = new List<OpenEventWindow>();
        for (int w = 0; w < Windows; w++)
        {
            var open = new OpenEventWindow
            {
                Kind = EventWindowKind.Detection,
                EventId = $"load-{w}",
                Header = EventWindowPlanning.BuildHeader(EnumEventType.Intrusion, "구역-LOAD", $"센서 {w}", DateTime.Now),
                GridColumns = Big ? 1 : 3,
                GridRows = Big ? 1 : 2,
                Window = Big ? new PixelRect { X = 40 + w * 24, Y = 40 + w * 24, Width = 1280, Height = 784 }
                             : new PixelRect { X = 40 + w * 24, Y = 40 + w * 24, Width = 800, Height = 500 },
                TimerCloseSeconds = 0,
            };
            for (int t = 0; t < TilesPerWindow; t++)
            {
                int cam = (w + t) % options.Count + 1;
                bool ptz = cam <= options.PtzCount;
                var device = new CameraDeviceModel(cam)
                {
                    DeviceName = $"DUMMY-CAM{cam}",
                    DeviceType = EnumDeviceType.IpCamera,
                    Category = ptz ? EnumCameraType.PTZ : EnumCameraType.FIXED,
                    IpAddress = "127.0.0.1",
                    IpPort = farm.Find(cam)!.Spec.OnvifPort,
                    UserName = options.User,
                    UserPassword = options.Password,
                };
                open.Cameras.Add(EventWindowPlanning.BuildCamera(device, new MappingCameraEntry(cam, cam, t + 1, 0, true), settings, ptzAllowed: true));
            }
            opens.Add(open);
        }

        var last = new ConcurrentDictionary<string, (StreamState State, string? Detail)>(StringComparer.Ordinal);
        var everPlayed = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        var transitions = new ConcurrentQueue<string>();
        var log = new TestLog();
        var hostLogDir = TestDirs.NewOutDir("load-host");
        using var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = exe!,
            Headless = headless,
            HostLogDirectory = hostLogDir,
            MaxConcurrentStreamOpens = int.TryParse(Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD_OPENS"), out var maxOpens) ? maxOpens : new CameraPopupHostOptions().MaxConcurrentStreamOpens,
        }, log);
        host.StatusReceived += (_, e) =>
        {
            if (e.Message is not StreamStateChanged m || m.EventKey is null) return;
            last[m.StreamId] = (m.State, m.Detail);
            if (m.State == StreamState.Playing) everPlayed[m.StreamId] = true;
        };
        host.StateChanged += (_, e) => transitions.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {e.OldState}->{e.NewState} {e.Reason}");
        host.Start();
        Assert.True(await Wait.UntilAsync(() => host.State == CameraPopupHostState.Running, TimeSpan.FromSeconds(20)), $"host state={host.State}");

        // Act — open all windows in one burst (the rapid-open case), then sample
        var sw = Stopwatch.StartNew();
        foreach (var open in opens) host.OpenEventWindow(open);

        int total = Windows * TilesPerWindow;
        int Playing() => last.Values.Count(v => v.State == StreamState.Playing);
        long? allPlayingMs = null;
        var cpu = new List<double>();
        var mem = new List<double>();
        var playingTimeline = new List<string>();
        int cores = Environment.ProcessorCount;
        int? pid = null;
        TimeSpan lastCpu = default;
        long lastTick = 0;
        while (sw.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            await Task.Delay(1000);
            int playing = Playing();
            if (allPlayingMs is null && playing == total) allPlayingMs = sw.ElapsedMilliseconds;
            if ((int)sw.Elapsed.TotalSeconds % 5 == 0) playingTimeline.Add($"{sw.Elapsed.TotalSeconds:0}s={playing}");
            var current = host.HostProcessId;
            if (current is null) { pid = null; continue; }
            try
            {
                using var p = Process.GetProcessById(current.Value);
                var nowCpu = p.TotalProcessorTime;
                long nowTick = Stopwatch.GetTimestamp();
                if (pid == current && lastTick != 0)
                {
                    double elapsed = (nowTick - lastTick) / (double)Stopwatch.Frequency;
                    cpu.Add((nowCpu - lastCpu).TotalSeconds / elapsed / cores * 100.0);
                }
                mem.Add(p.PrivateMemorySize64 / (1024.0 * 1024.0));
                pid = current;
                lastCpu = nowCpu;
                lastTick = nowTick;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                pid = null;
            }
        }

        var listening = host.HostProcessId is { } hp ? ListeningSockets(hp) : new List<string> { "(no host pid)" };
        int finalPlaying = Playing();
        var notPlaying = last.Where(kv => kv.Value.State != StreamState.Playing).Select(kv => $"{kv.Key}={kv.Value.State}/{kv.Value.Detail}").OrderBy(x => x).ToList();
        var restarts = transitions.Where(t => t.Contains("->Restarting") || t.Contains("->Suspended")).ToList();
        foreach (var open in opens) host.CloseEventWindow(open.EventKey, EventWindowCloseReason.Evicted, returnHome: false);

        // Assert / report
        var result = new
        {
            headless,
            seconds,
            cores,
            total,
            finalPlaying,
            everPlayed = everPlayed.Count,
            allPlayingMs,
            cpuAvgPercentOfAllCores = cpu.Count == 0 ? 0 : Math.Round(cpu.Average(), 1),
            cpuMaxPercentOfAllCores = cpu.Count == 0 ? 0 : Math.Round(cpu.Max(), 1),
            cpuSteadyAvg = cpu.Count < 20 ? 0 : Math.Round(cpu.Skip(cpu.Count / 2).Average(), 1),
            privateMbAvg = mem.Count == 0 ? 0 : Math.Round(mem.Average()),
            privateMbMax = mem.Count == 0 ? 0 : Math.Round(mem.Max()),
            restarts,
            notPlaying,
            playingTimeline,
            listening,
            hostLogDir,
        };
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        _output.WriteLine(json);
        if (Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_LOAD_OUT") is { Length: > 0 } outFile) File.WriteAllText(outFile, json);

        Assert.Empty(restarts);
        Assert.Equal(CameraPopupHostState.Running, host.State);
        Assert.Equal(total, finalPlaying);
        Assert.Empty(listening);
    }

    /// <summary>Listening TCP endpoints and bound UDP endpoints of a process (netstat -ano, filtered by pid).</summary>
    private static List<string> ListeningSockets(int pid)
    {
        var found = new List<string>();
        try
        {
            var psi = new ProcessStartInfo("netstat", "-ano") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            string text = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10_000);
            string suffix = pid.ToString(CultureInfo.InvariantCulture);
            foreach (var raw in text.Split('\n'))
            {
                var cols = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (cols.Length < 4 || cols[^1] != suffix) continue;
                bool tcpListen = cols[0] == "TCP" && cols.Length >= 5 && cols[3] == "LISTENING";
                bool udp = cols[0] == "UDP";
                if (tcpListen || udp) found.Add(string.Join(' ', cols));
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            found.Add("netstat failed: " + ex.Message);
        }
        return found;
    }
}
