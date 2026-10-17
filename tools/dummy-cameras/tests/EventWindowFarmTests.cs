using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using PixelRect = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages.PixelRect;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit.Abstractions;

namespace DummyCameras.Tests;

/// <summary>
/// Full farm (MediaMTX + ffmpeg, ports distinct from <see cref="LiveFarmFixture"/>): cam1 PTZ · cam2 fixed.
/// </summary>
public sealed class EventWindowFarmFixture : IAsyncLifetime
{
    public const double GotoSeconds = 2.0;

    public Farm? Farm { get; private set; }
    public string? SkipReason { get; private set; }
    public bool Ready { get; private set; }
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Ptz { get; } = new();

    public async Task InitializeAsync()
    {
        if (ToolPaths.FindOnPath("ffmpeg") is null) { SkipReason = "ffmpeg not on PATH"; return; }
        if (!File.Exists(MediaMtxInstaller.ExePath)) { SkipReason = "MediaMTX not installed - run: DummyCameras fetch-mediamtx"; return; }
        if (CameraHostExe.Find() is null) { SkipReason = $"{CameraHostExe.ExeName} not built (or set IRONWALL_CAMHOST_EXE)"; return; }

        var o = new FarmOptions
        {
            Count = 2,
            PtzCount = 1,
            OnvifBasePort = 0,
            ControlPort = 0,
            RtspPort = 18654,
            RtpPort = 18200,
            MediaMtxApiPort = 19897,
            Width = 640, Height = 360, SubWidth = 320, SubHeight = 180, Fps = 10,
            GotoSeconds = GotoSeconds,
            OutDir = TestDirs.NewOutDir("evwin"),
        };
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        Farm.PtzLog.Written += e => Ptz.Enqueue(e);
        Ready = await Farm.WaitStreamsReadyAsync(TimeSpan.FromSeconds(20), CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (Farm is not null) await Farm.DisposeAsync();
    }

    /// <summary>The camera as the GIS device cache holds it (ONVIF on the dummy's port, camera account).</summary>
    public CameraDeviceModel Device(int cam, bool ptz) => new(cam)
    {
        DeviceName = $"DUMMY-CAM{cam}",
        DeviceType = EnumDeviceType.IpCamera,
        Category = ptz ? EnumCameraType.PTZ : EnumCameraType.FIXED,
        IpAddress = "127.0.0.1",
        IpPort = Farm!.Find(cam)!.Spec.OnvifPort,
        UserName = Farm.Options.User,
        UserPassword = Farm.Options.Password,
    };
}

/// <summary>Host exe lookup: env → next to the tests (shared OutDir) → repo Host bin.</summary>
internal static class CameraHostExe
{
    public const string ExeName = "Ironwall.CameraPopupHost.exe";

    public static string? Find()
    {
        var env = Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;
        var local = Path.Combine(AppContext.BaseDirectory, ExeName);
        if (File.Exists(local)) return local;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.CameraPopup.Host"))) dir = dir.Parent;
        var bin = dir is null ? null : Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.CameraPopup.Host", "bin");
        return bin is null || !Directory.Exists(bin)
            ? null
            : Directory.EnumerateFiles(bin, ExeName, SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }
}

public class EventWindowFarmTests : IClassFixture<EventWindowFarmFixture>
{
    private readonly EventWindowFarmFixture _fx;
    private readonly ITestOutputHelper _output;

    public EventWindowFarmTests(EventWindowFarmFixture fx, ITestOutputHelper output)
    {
        _fx = fx;
        _output = output;
    }

    [SkippableFact]
    public async Task should_play_both_tiles_and_goto_target_preset_when_event_window_opens_with_dummy_onvif_cameras()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        Assert.True(_fx.Ready, "streams did not become ready - see " + _fx.Farm!.LogDir);

        // Arrange — tiles built by the GIS planning code from the device cache + mapping (ONVIF provider, default settings)
        var settings = new CameraPopupSettings().Normalize();
        var ptzTile = EventWindowPlanning.BuildCamera(_fx.Device(1, ptz: true), new MappingCameraEntry(1, 1, 1, 3, true, TargetPresetIndex: 3, TargetPresetName: "P3"), settings, ptzAllowed: true);
        var fixedTile = EventWindowPlanning.BuildCamera(_fx.Device(2, ptz: false), new MappingCameraEntry(2, 2, 2, 0, true), settings, ptzAllowed: true);
        var open = new OpenEventWindow
        {
            Kind = EventWindowKind.Detection,
            EventId = "farm-" + Guid.NewGuid().ToString("N")[..8],
            Header = EventWindowPlanning.BuildHeader(EnumEventType.Intrusion, "구역-DUMMY", "DUMMY 센서", DateTime.Now),
            GridColumns = 2,
            GridRows = 1,
            Window = new PixelRect { X = 40, Y = 40, Width = 800, Height = 400 },
            TimerCloseSeconds = 0,
            Cameras = { ptzTile, fixedTile },
        };
        var sim = _fx.Farm!.Find(1)!.Ptz!;
        sim.Reset();
        var messages = new ConcurrentQueue<IIpcMessage>();
        using var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = CameraHostExe.Find()!,
            Headless = true,
            HostLogDirectory = TestDirs.NewOutDir("evwin-host"),
        }, new TestLog());
        host.StatusReceived += (_, e) => messages.Enqueue(e.Message);
        host.Start();
        Assert.True(await Wait.UntilAsync(() => host.State == CameraPopupHostState.Running, TimeSpan.FromSeconds(20)), $"host state={host.State}");

        // Act
        var sw = Stopwatch.StartNew();
        host.OpenEventWindow(open);
        bool Playing(EventWindowCamera c) => messages.OfType<StreamStateChanged>()
            .Any(m => m.EventKey == open.EventKey && m.StreamId == $"{open.EventKey}#{c.CameraId}" && m.State == StreamState.Playing);
        var bothPlaying = await Wait.UntilAsync(() => Playing(ptzTile) && Playing(fixedTile), TimeSpan.FromSeconds(40), 100);
        var playingMs = sw.ElapsedMilliseconds;
        var gotoLogged = await Wait.UntilAsync(() => _fx.Ptz.Any(e => (string?)e["cam"] == "cam1" && (string?)e["op"] == "GotoPreset"), TimeSpan.FromSeconds(10));
        var arrived = await Wait.UntilAsync(() => sim.Status(DateTime.UtcNow).Position == sim.FindPreset("3")!.Position, TimeSpan.FromSeconds(10));
        host.CloseEventWindow(open.EventKey, EventWindowCloseReason.Evicted, returnHome: false);

        // Assert — the planning output has a host/port (no "주소 없음"), both tiles play, the PTZ tile went to its target preset
        _output.WriteLine($"ptz tile provider: {ptzTile.Provider} · both playing after {playingMs} ms");
        foreach (var m in messages.OfType<StreamStateChanged>()) _output.WriteLine($"  {m.StreamId} {m.State} {m.Detail}");
        Assert.Equal(("127.0.0.1", _fx.Farm.Find(1)!.Spec.OnvifPort), (ptzTile.Provider.Host, ptzTile.Provider.Port));
        Assert.True(bothPlaying, "tiles did not both reach Playing");
        Assert.True(gotoLogged, "GotoPreset never reached the PTZ dummy");
        var go = _fx.Ptz.First(e => (string?)e["cam"] == "cam1" && (string?)e["op"] == "GotoPreset");
        Assert.Equal("ok", go["result"]);
        Assert.Equal("3", go["preset"]);
        var sent = DateTime.Parse((string)go["ts"]!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var arrivesAt = DateTime.Parse((string)go["arrivesAt"]!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.InRange((arrivesAt - sent).TotalSeconds, EventWindowFarmFixture.GotoSeconds * 0.5, EventWindowFarmFixture.GotoSeconds * 1.5);
        Assert.True(arrived, "PTZ dummy never arrived at preset 3");
        Assert.DoesNotContain(_fx.Ptz, e => (string?)e["cam"] == "cam2");   // the fixed camera got no PTZ command
        Assert.Equal(CameraPopupHostState.Running, host.State);
    }
}
