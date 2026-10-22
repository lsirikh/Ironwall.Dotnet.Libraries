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

    // ── 2026-10-22 팝업 ONVIF 철저 확인: 닫을 때 복귀 · 같은 카메라 두 이벤트 창 ──

    private OpenEventWindow Window(params EventWindowCamera[] cameras)
    {
        var open = new OpenEventWindow
        {
            Kind = EventWindowKind.Detection,
            EventId = "farm-" + Guid.NewGuid().ToString("N")[..8],
            Header = EventWindowPlanning.BuildHeader(EnumEventType.Intrusion, "구역-DUMMY", "DUMMY 센서", DateTime.Now),
            GridColumns = cameras.Length,
            GridRows = 1,
            Window = new PixelRect { X = 40, Y = 40, Width = 400 * cameras.Length, Height = 400 },
            TimerCloseSeconds = 0,
        };
        foreach (var c in cameras) open.Cameras.Add(c);
        return open;
    }

    private static CameraPopupHostSupervisor StartHost(ConcurrentQueue<IIpcMessage> messages)
    {
        var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = CameraHostExe.Find()!,
            Headless = true,
            HostLogDirectory = TestDirs.NewOutDir("evwin-host"),
        }, new TestLog());
        host.StatusReceived += (_, e) => messages.Enqueue(e.Message);
        host.Start();
        return host;
    }

    private int GotoCount(string preset) => _fx.Ptz.Count(e => (string?)e["cam"] == "cam1" && (string?)e["op"] == "GotoPreset" && (string?)e["preset"] == preset);

    [SkippableTheory]
    [InlineData(1)]      // 매핑의 복귀 프리셋 번호 → GotoPreset 1
    [InlineData(null)]   // 복귀 프리셋 없음 → 카메라 Home(GotoHomePosition)
    public async Task should_return_ptz_camera_home_when_event_window_closes_with_return_home(int? homePreset)
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        Assert.True(_fx.Ready, "streams did not become ready - see " + _fx.Farm!.LogDir);
        var settings = new CameraPopupSettings().Normalize();
        var tile = EventWindowPlanning.BuildCamera(_fx.Device(1, ptz: true),
            new MappingCameraEntry(1, 1, 1, 0, true, TargetPresetIndex: 3, TargetPresetName: "P3", HomePresetIndex: homePreset), settings, ptzAllowed: true);
        var open = Window(tile);
        var sim = _fx.Farm!.Find(1)!.Ptz!;
        sim.Reset();
        int homeGotosBefore = homePreset is int hp ? GotoCount(hp.ToString(CultureInfo.InvariantCulture)) : 0;
        int homePositionsBefore = _fx.Ptz.Count(e => (string?)e["cam"] == "cam1" && (string?)e["op"] == "GotoHomePosition");
        var messages = new ConcurrentQueue<IIpcMessage>();
        using var host = StartHost(messages);
        Assert.True(await Wait.UntilAsync(() => host.State == CameraPopupHostState.Running, TimeSpan.FromSeconds(20)), $"host state={host.State}");

        host.OpenEventWindow(open);
        var atTarget = await Wait.UntilAsync(() => Wait.Near(sim.Status(DateTime.UtcNow).Position, sim.FindPreset("3")!.Position), TimeSpan.FromSeconds(30), 100);
        host.CloseEventWindow(open.EventKey, EventWindowCloseReason.Evicted, returnHome: true);
        bool homeSent = homePreset is int h
            ? await Wait.UntilAsync(() => GotoCount(h.ToString(CultureInfo.InvariantCulture)) > homeGotosBefore, TimeSpan.FromSeconds(10))
            : await Wait.UntilAsync(() => _fx.Ptz.Count(e => (string?)e["cam"] == "cam1" && (string?)e["op"] == "GotoHomePosition") > homePositionsBefore, TimeSpan.FromSeconds(10));
        var expected = homePreset is int hh ? sim.FindPreset(hh.ToString(CultureInfo.InvariantCulture))!.Position : sim.Home;
        var arrivedHome = await Wait.UntilAsync(() => Wait.Near(sim.Status(DateTime.UtcNow).Position, expected), TimeSpan.FromSeconds(10));

        Assert.True(atTarget, "PTZ dummy never reached the event preset 3");
        Assert.True(homeSent, "closing with return-home sent no home command");
        Assert.True(arrivedHome, $"camera did not return home ({sim.Status(DateTime.UtcNow).Position} vs {expected})");
        Assert.Equal(CameraPopupHostState.Running, host.State);
    }

    [SkippableFact]
    public async Task should_drive_and_keep_playing_the_other_window_when_two_event_windows_share_one_ptz_camera()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        Assert.True(_fx.Ready, "streams did not become ready - see " + _fx.Farm!.LogDir);
        var settings = new CameraPopupSettings().Normalize();
        EventWindowCamera Tile(int preset) => EventWindowPlanning.BuildCamera(_fx.Device(1, ptz: true),
            new MappingCameraEntry(1, 1, 1, 0, true, TargetPresetIndex: preset, TargetPresetName: $"P{preset}"), settings, ptzAllowed: true);
        var first = Window(Tile(3));
        var second = Window(Tile(5));
        var sim = _fx.Farm!.Find(1)!.Ptz!;
        sim.Reset();
        int gotos3 = GotoCount("3"), gotos5 = GotoCount("5");
        var messages = new ConcurrentQueue<IIpcMessage>();
        using var host = StartHost(messages);
        Assert.True(await Wait.UntilAsync(() => host.State == CameraPopupHostState.Running, TimeSpan.FromSeconds(20)), $"host state={host.State}");
        bool Playing(OpenEventWindow w) => messages.OfType<StreamStateChanged>().Any(m => m.EventKey == w.EventKey && m.State == StreamState.Playing);
        StreamState? Last(OpenEventWindow w) => messages.OfType<StreamStateChanged>().LastOrDefault(m => m.EventKey == w.EventKey)?.State;

        host.OpenEventWindow(first);
        var firstAt3 = await Wait.UntilAsync(() => Wait.Near(sim.Status(DateTime.UtcNow).Position, sim.FindPreset("3")!.Position), TimeSpan.FromSeconds(30), 100);
        host.OpenEventWindow(second);   // 같은 카메라의 다음 이벤트 — 마지막 이벤트 프리셋(5)으로 간다
        var bothPlaying = await Wait.UntilAsync(() => Playing(first) && Playing(second), TimeSpan.FromSeconds(40), 100);
        var at5 = await Wait.UntilAsync(() => Wait.Near(sim.Status(DateTime.UtcNow).Position, sim.FindPreset("5")!.Position), TimeSpan.FromSeconds(15), 100);
        host.CloseEventWindow(first.EventKey, EventWindowCloseReason.Evicted, returnHome: false);
        await Task.Delay(1500);   // 첫 창이 닫히며 같은 카메라 연결 · 재생을 건드리는지 본다

        Assert.True(firstAt3, "first window's preset 3 never reached the camera");
        Assert.True(bothPlaying, "both windows' tiles of the same camera did not play");
        Assert.True(at5, "second window's preset 5 never reached the camera");
        Assert.True(GotoCount("3") > gotos3 && GotoCount("5") > gotos5);
        Assert.Equal(StreamState.Playing, Last(second));   // 다른 창은 계속 재생
        Assert.Equal(CameraPopupHostState.Running, host.State);
        host.CloseEventWindow(second.EventKey, EventWindowCloseReason.Evicted, returnHome: false);
    }
}
