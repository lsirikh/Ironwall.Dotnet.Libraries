using System.Collections.Concurrent;
using System.Diagnostics;
using DummyCameras.Infra;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;

namespace DummyCameras.Tests;

/// <summary>
/// ONVIF-only dummy farm on free loopback ports (cam1-2 PTZ, cam3 fixed, cam4 auth-fail, cam5 slow, cam6 no reply,
/// cam7 HTTP-Digest PTZ) shared by the class; driven by OUR production client path.
/// </summary>
public sealed class OnvifFarmFixture : IAsyncLifetime
{
    public Farm Farm { get; private set; } = null!;
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Requests { get; } = new();
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Ptz { get; } = new();

    public async Task InitializeAsync()
    {
        var o = new FarmOptions
        {
            Count = 7,
            PtzCount = 2,
            OnvifBasePort = 0,
            ControlPort = 0,
            EnableRtsp = false,
            GotoSeconds = 1.0,
            OutDir = TestDirs.NewOutDir("onvif"),
        };
        o.FaultSetters[4] = f => f.AuthFail = true;
        o.FaultSetters[5] = f => f.SlowOnvifMs = 1500;
        o.FaultSetters[6] = f => f.NoOnvifReply = true;
        o.FaultSetters[7] = f => f.HttpDigest = true;
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        Farm.OnvifLog.Written += e => Requests.Enqueue(e);
        Farm.PtzLog.Written += e => Ptz.Enqueue(e);
    }

    public async Task DisposeAsync() => await Farm.DisposeAsync();

    /// <summary>Provider info the GIS would send for camera N (ONVIF, host + port + camera account).</summary>
    public VideoProviderInfo Info(int cam) => new()
    {
        Kind = VideoProviderKind.Onvif,
        Host = "127.0.0.1",
        Port = Farm.Find(cam)!.Spec.OnvifPort,
        Username = Farm.Options.User,
        Password = Farm.Options.Password,
    };
}

public class OnvifClientTests : IClassFixture<OnvifFarmFixture>
{
    private readonly OnvifFarmFixture _fx;

    public OnvifClientTests(OnvifFarmFixture fx) => _fx = fx;

    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    [Fact]
    public async Task should_resolve_sub_stream_with_credentials_and_no_expect_header_when_dummy_answers()
    {
        // Arrange
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var cam = _fx.Farm.Find(1)!.Spec;

        // Act
        var r = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("c1", _fx.Info(1), Within(10_000));

        // Assert — lowest-resolution profile (Profile_2 = cam1_sub) with the camera account embedded
        Assert.True(r.Success, r.ToString());
        Assert.Equal($"rtsp://dummy:dummy1234@127.0.0.1:{cam.RtspPort}/cam1_sub", r.Uri);
        var mine = _fx.Requests.Where(e => (string?)e["cam"] == "cam1").ToList();
        Assert.Contains(mine, e => (string?)e["op"] == "GetStreamUri" && ((string)e["result"]!).StartsWith("ok", StringComparison.Ordinal));
        Assert.DoesNotContain(mine, e => e["expect100"] is true);   // T-01 FR-21 holds against the dummy
    }

    [Fact]
    public async Task should_report_ptz_capable_for_ptz_camera_and_not_for_fixed_camera_when_prepared()
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;

        var sw = Stopwatch.StartNew();
        var r1 = await ptz.PrepareAsync("c1", _fx.Info(1), Within(10_000));
        var firstPrepareMs = sw.ElapsedMilliseconds;
        var r3 = await ptz.PrepareAsync("c3", _fx.Info(3), Within(10_000));

        Assert.True(r1.Connected);
        Assert.True(r1.PtzCapable);
        Assert.True(r3.Connected);
        Assert.False(r3.PtzCapable);
        Assert.True(firstPrepareMs < 2000, $"first PTZ prepare took {firstPrepareMs} ms (NFR-01 target 500 ms)");
    }

    [Fact]
    public async Task should_move_then_stop_with_two_second_safety_timeout_when_continuous_move()
    {
        // Arrange
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;
        var sim = _fx.Farm.Find(2)!.Ptz!;
        sim.Reset();
        Assert.True((await ptz.PrepareAsync("c2", _fx.Info(2), Within(10_000))).PtzCapable);

        // Act — press (pan right) … release
        var moved = await ptz.ContinuousMoveAsync("c2", 1, 0, 0, Within(5_000));
        var sawMoving = await Wait.UntilAsync(() => sim.Status(DateTime.UtcNow).PanTiltMoving, TimeSpan.FromSeconds(2));
        await Task.Delay(300);
        await ptz.StopAsync("c2", Within(5_000));

        // Assert
        Assert.True(moved);
        Assert.True(sawMoving);
        var after = sim.Status(DateTime.UtcNow);
        Assert.False(after.IsMoving);
        Assert.True(after.Position.Pan > 0.05, $"pan={after.Position.Pan}");
        var log = _fx.Ptz.Where(e => (string?)e["cam"] == "cam2").ToList();
        var move = Assert.Single(log, e => (string?)e["op"] == "ContinuousMove" && (string?)e["result"] == "ok");
        Assert.Equal("PT2S", move["timeout"]);                     // FR-22 safety limit reached the camera
        Assert.Contains(log, e => (string?)e["op"] == "Stop" && (string?)e["result"] == "ok");
        ptz.Release("c2");
        await ptz.StopAsync("c2", Within(2_000));
    }

    [Fact]
    public async Task should_list_presets_and_arrive_after_travel_time_when_goto_preset()
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;
        var sim = _fx.Farm.Find(1)!.Ptz!;
        sim.Reset();
        await ptz.PrepareAsync("c1g", _fx.Info(1), Within(10_000));

        var presets = await ptz.GetPresetsAsync("c1g", Within(5_000));
        var ok = await ptz.GotoPresetAsync("c1g", "3", Within(5_000));
        var during = sim.Status(DateTime.UtcNow);
        var arrived = await Wait.UntilAsync(() => !sim.Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(3));

        Assert.NotNull(presets);
        Assert.Equal(new[] { "1", "2", "3", "4", "5" }, presets!.Select(p => p.Token));
        Assert.Equal(new[] { "P1", "P2", "P3", "P4", "P5" }, presets.Select(p => p.Name));
        Assert.True(ok);
        Assert.Equal("goto", during.Motion);
        Assert.Equal("3", during.TargetPreset);
        Assert.True(arrived);
        Assert.Equal(sim.FindPreset("3")!.Position, sim.Status(DateTime.UtcNow).Position);
        Assert.Contains(_fx.Ptz, e => (string?)e["cam"] == "cam1" && (string?)e["op"] == "GotoPreset" && (string?)e["preset"] == "3" && e["arrivesAt"] is string);
    }

    [Fact]
    public async Task should_fail_on_unknown_preset_and_travel_home_when_goto_home()
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;
        var sim = _fx.Farm.Find(1)!.Ptz!;
        await ptz.PrepareAsync("c1h", _fx.Info(1), Within(10_000));

        var bad = await ptz.GotoPresetAsync("c1h", "99", Within(5_000));
        var home = await ptz.GotoHomeAsync("c1h", Within(5_000));

        Assert.False(bad);
        Assert.True(home);
        Assert.True(await Wait.UntilAsync(() => sim.Status(DateTime.UtcNow).Position == sim.Home, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task should_not_connect_and_resolve_nothing_when_camera_rejects_credentials()
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());

        var ready = await registry.GetPtz(VideoProviderKind.Onvif)!.PrepareAsync("c4", _fx.Info(4), Within(10_000));
        var stream = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("c4", _fx.Info(4), Within(10_000));

        // GetCapabilities is PRE_AUTH (answered), every signed call is refused → no PTZ, no stream URI.
        Assert.False(ready.PtzCapable);
        Assert.False(stream.Success);
        Assert.Contains(_fx.Requests, e => (string?)e["cam"] == "cam4" && ((string)e["result"]!).StartsWith("fault:NotAuthorized", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(5, 800)]    // slow ONVIF (1.5 s per call) vs an 800 ms caller limit
    [InlineData(6, 1000)]   // ONVIF never answers
    public async Task should_return_within_caller_limit_when_onvif_is_slow_or_silent(int cam, int limitMs)
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());

        var sw = Stopwatch.StartNew();
        var ready = await registry.GetPtz(VideoProviderKind.Onvif)!.PrepareAsync($"c{cam}", _fx.Info(cam), Within(limitMs));
        var elapsed = sw.ElapsedMilliseconds;

        Assert.False(ready.PtzCapable);
        Assert.True(elapsed < limitMs + 700, $"prepare returned after {elapsed} ms (limit {limitMs} ms) - FR-26");
    }

    [Fact]
    public async Task should_answer_device_calls_via_http_digest_retry_when_camera_demands_transport_digest()
    {
        // cam7 demands HTTP Digest on every call. The device client carries HttpDigest credentials → its calls pass
        // after one 401 challenge. (Media/PTZ: see the skipped test below.)
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());

        await registry.GetPtz(VideoProviderKind.Onvif)!.PrepareAsync("c7", _fx.Info(7), Within(10_000));

        var cam7 = _fx.Requests.Where(e => (string?)e["cam"] == "cam7").ToList();
        File.WriteAllText(Path.Combine(_fx.Farm.Options.OutDir, "http-digest-outcome.txt"),
            string.Join(Environment.NewLine, cam7.Select(e => $"{e["op"]} {e["result"]} auth={e["httpAuth"]} userOk={e["httpAuthUserOk"]}")));
        Assert.Contains(cam7, e => (string?)e["op"] == "GetSystemDateAndTime" && (string?)e["result"] == "http-401-challenge");
        Assert.Contains(cam7, e => (string?)e["op"] == "GetCapabilities" && (string?)e["result"] == "ok" && (string?)e["httpAuth"] == "Digest");
    }

    [Fact(Skip = "Client gap (2026-09-30 dummy finding): OnvifClientFactory gives only the DEVICE client HttpDigest credentials; " +
                 "Media/PTZ clients answer the 401 challenge with a digest for another account, so a camera that demands HTTP Digest " +
                 "on media/ptz yields no stream URI and no PTZ. Un-skip when the factory sets ClientCredentials.HttpDigest on every client.")]
    public async Task should_resolve_stream_when_camera_demands_transport_digest()
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var stream = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("c7s", _fx.Info(7), Within(10_000));
        Assert.True(stream.Success, stream.ToString());
    }
}
