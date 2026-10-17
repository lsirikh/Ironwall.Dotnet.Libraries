using System.Collections.Concurrent;
using Autofac;
using DummyCameras.Infra;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
using GetDeviceInformationRequest = Ironwall.Dotnet.Libraries.OnvifSolution.DeviceIo.GetDeviceInformationRequest;
using CapabilityCategory = Ironwall.Dotnet.Libraries.OnvifSolution.DeviceIo.CapabilityCategory;
using Ironwall.Dotnet.Libraries.OnvifSolution.Factories;
using Ironwall.Dotnet.Libraries.OnvifSolution.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Modules;

namespace DummyCameras.Tests;

/// <summary>
/// Cameras that authenticate the strict ways field cameras do (2026-09-30 dummy findings):
/// cam1 PTZ demands HTTP Digest on every ONVIF call · cam2 PTZ accepts ONLY a WS-UsernameToken
/// (no PRE_AUTH GetCapabilities, no HTTP Digest fallback) · cam3 fixed, WS-UsernameToken only.
/// </summary>
public sealed class OnvifAuthFarmFixture : IAsyncLifetime
{
    public Farm Farm { get; private set; } = null!;
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Requests { get; } = new();
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Ptz { get; } = new();

    public async Task InitializeAsync()
    {
        var o = new FarmOptions
        {
            Count = 3,
            PtzCount = 2,
            OnvifBasePort = 0,
            ControlPort = 0,
            EnableRtsp = false,
            GotoSeconds = 1.0,
            OutDir = TestDirs.NewOutDir("auth"),
        };
        o.FaultSetters[1] = f => f.HttpDigest = true;
        o.FaultSetters[2] = f => f.WsSecurityOnly = true;
        o.FaultSetters[3] = f => f.WsSecurityOnly = true;
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        Farm.OnvifLog.Written += e => Requests.Enqueue(e);
        Farm.PtzLog.Written += e => Ptz.Enqueue(e);
    }

    public async Task DisposeAsync() => await Farm.DisposeAsync();

    public VideoProviderInfo Info(int cam) => new()
    {
        Kind = VideoProviderKind.Onvif,
        Host = "127.0.0.1",
        Port = Farm.Find(cam)!.Spec.OnvifPort,
        Username = Farm.Options.User,
        Password = Farm.Options.Password,
    };

    public IOnvifConnectionModel Connection(int cam) => new OnvifConnectionModel(new ConnectionModel
    {
        IpAddress = "127.0.0.1",
        PortOnvif = Farm.Find(cam)!.Spec.OnvifPort,
        Username = Farm.Options.User,
        Password = Farm.Options.Password,
    });

    public List<IReadOnlyDictionary<string, object?>> RequestsOf(string cam) => Requests.Where(e => (string?)e["cam"] == cam).ToList();

    public List<IReadOnlyDictionary<string, object?>> PtzOf(string cam) => Ptz.Where(e => (string?)e["cam"] == cam).ToList();
}

public class OnvifAuthTests : IClassFixture<OnvifAuthFarmFixture>
{
    private readonly OnvifAuthFarmFixture _fx;

    public OnvifAuthTests(OnvifAuthFarmFixture fx) => _fx = fx;

    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private static IOnvifClientFactory NewFactory()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(new OnvifServiceModule());
        return builder.Build().Resolve<IOnvifClientFactory>();
    }

    /// <summary>"ok" (GetStreamUri logs "ok uri=…").</summary>
    private static bool Ok(IReadOnlyDictionary<string, object?> e) => ((string?)e["result"] ?? "").Split(' ')[0] == "ok";

    // ── defect 1: the device client must carry WS-Security ─────────────────────────────

    [Fact]
    public async Task should_answer_device_information_and_capabilities_when_camera_accepts_only_ws_username_token()
    {
        // Arrange — cam3 refuses everything but the clock without a valid UsernameToken
        var factory = NewFactory();

        // Act
        var device = await factory.CreateDeviceAsync(_fx.Connection(3));
        var info = await device.GetDeviceInformationAsync(new GetDeviceInformationRequest());
        var caps = await device.GetCapabilitiesAsync(new[] { CapabilityCategory.All });

        // Assert
        Assert.Equal("DUMMY-FIXED", info.Model);
        Assert.NotNull(caps.Capabilities?.Media?.XAddr);
        var mine = _fx.RequestsOf("cam3");
        var devInfo = Assert.Single(mine, e => (string?)e["op"] == "GetDeviceInformation");
        Assert.True(Ok(devInfo), $"GetDeviceInformation → {devInfo["result"]}");
        Assert.Equal(true, devInfo["wsSecurity"]);
        Assert.All(mine.Where(e => (string?)e["op"] == "GetCapabilities"), e =>
        {
            Assert.True(Ok(e), $"GetCapabilities → {e["result"]}");
            Assert.Equal(true, e["wsSecurity"]);
        });
        Assert.DoesNotContain(mine, e => e["expect100"] is true);
    }

    [Fact]
    public async Task should_resolve_stream_and_drive_ptz_when_camera_accepts_only_ws_username_token()
    {
        // Arrange — production path: registry → PtzController → OnvifSolution (device core + bundle + media)
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;
        var sim = _fx.Farm.Find(2)!.Ptz!;
        sim.Reset();

        // Act
        var stream = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("ws2", _fx.Info(2), Within(10_000));
        var ready = await ptz.PrepareAsync("ws2", _fx.Info(2), Within(10_000));
        var goneTo = await ptz.GotoPresetAsync("ws2", "2", Within(5_000));

        // Assert
        Assert.True(stream.Success, stream.ToString());
        Assert.True(ready.Connected);
        Assert.True(ready.PtzCapable);
        Assert.True(goneTo);
        Assert.Contains(_fx.PtzOf("cam2"), e => (string?)e["op"] == "GotoPreset" && (string?)e["preset"] == "2" && Ok(e));
        Assert.DoesNotContain(_fx.RequestsOf("cam2"), e => ((string?)e["result"] ?? "").StartsWith("fault:NotAuthorized", StringComparison.Ordinal));
        ptz.Release("ws2");
    }

    // ── defect 2: every client answers HTTP Digest with the camera account ─────────────

    [Fact]
    public async Task should_resolve_stream_and_move_stop_and_goto_preset_when_camera_demands_http_digest()
    {
        // Arrange — cam1 challenges every ONVIF call with HTTP Digest (device · media · ptz)
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;
        var sim = _fx.Farm.Find(1)!.Ptz!;
        sim.Reset();

        // Act
        var stream = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("dg1", _fx.Info(1), Within(10_000));
        var ready = await ptz.PrepareAsync("dg1", _fx.Info(1), Within(10_000));
        var moved = await ptz.ContinuousMoveAsync("dg1", 1, 0, 0, Within(5_000));
        var sawMoving = await Wait.UntilAsync(() => sim.Status(DateTime.UtcNow).PanTiltMoving, TimeSpan.FromSeconds(2));
        await ptz.StopAsync("dg1", Within(5_000));
        var goneTo = await ptz.GotoPresetAsync("dg1", "3", Within(5_000));
        var arrived = await Wait.UntilAsync(() => !sim.Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(3));

        // Assert — our calls succeed …
        Assert.True(stream.Success, stream.ToString());
        Assert.True(ready.PtzCapable);
        Assert.True(moved);
        Assert.True(sawMoving);
        Assert.True(goneTo);
        Assert.True(arrived);
        Assert.Equal(sim.FindPreset("3")!.Position, sim.Status(DateTime.UtcNow).Position);
        var reqs = _fx.RequestsOf("cam1");
        foreach (var op in new[] { "GetProfiles", "GetStreamUri" })
            Assert.Contains(reqs, e => (string?)e["op"] == op && Ok(e) && (string?)e["httpAuth"] == "Digest");
        var ptzLog = _fx.PtzOf("cam1");
        foreach (var op in new[] { "ContinuousMove", "Stop" })
            Assert.Contains(ptzLog, e => (string?)e["op"] == op && Ok(e));
        Assert.Contains(ptzLog, e => (string?)e["op"] == "GotoPreset" && (string?)e["preset"] == "3" && Ok(e));
        // … and never with a digest for another account (Windows default credentials)
        Assert.DoesNotContain(reqs, e => e["httpAuthUserOk"] is false);
        Assert.DoesNotContain(reqs, e => e["expect100"] is true);
        ptz.Release("dg1");
    }
}
