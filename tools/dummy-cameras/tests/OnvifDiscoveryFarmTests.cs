using System.Collections.Concurrent;
using System.Net;
using DummyCameras.Onvif;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services.Onvif;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace DummyCameras.Tests;

/// <summary>
/// ONVIF-only farm (cam1 PTZ, cam2-3 fixed, cam3 auth-fail) with WS-Discovery on a free LOOPBACK UDP port (unicast only -
/// udp 3702 and multicast are not touched). Shared by the class.
/// </summary>
public sealed class DiscoveryFarmFixture : IAsyncLifetime
{
    public Farm Farm { get; private set; } = null!;
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Requests { get; } = new();

    public async Task InitializeAsync()
    {
        var o = new FarmOptions
        {
            Count = 3,
            PtzCount = 1,
            OnvifBasePort = 0,
            ControlPort = -1,
            EnableRtsp = false,
            DiscoveryPort = 0,
            DiscoveryMulticast = false,
            OutDir = TestDirs.NewOutDir("discovery"),
        };
        o.FaultSetters[3] = f => f.AuthFail = true;
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        Farm.OnvifLog.Written += e => Requests.Enqueue(e);
    }

    public async Task DisposeAsync() => await Farm.DisposeAsync();

    /// <summary>The GIS search pointed at the farm's responder (unicast, short window).</summary>
    public WsDiscoveryProbeOptions ProbeOptions()
    {
        var o = new WsDiscoveryProbeOptions { Multicast = false, Window = TimeSpan.FromSeconds(1.5) };
        o.UnicastTargets.Add(new IPEndPoint(IPAddress.Loopback, Farm.Discovery!.Port));
        return o;
    }
}

/// <summary>
/// "가짜 카메라가 ONVIF 로 검색되면 조회해서 등록할 수 있다" — the GIS camera-panel path end to end against the dummies:
/// WsDiscoveryClient (search) → OnvifCameraLookup (OnvifSolution InitializeProfile) → OnvifDiscoveryViewModel → Draft model.
/// </summary>
public class OnvifDiscoveryFarmTests : IClassFixture<DiscoveryFarmFixture>
{
    private readonly DiscoveryFarmFixture _fx;

    public OnvifDiscoveryFarmTests(DiscoveryFarmFixture fx) => _fx = fx;

    [Fact]
    public async Task should_find_every_dummy_camera_with_its_device_service_when_gis_searches()
    {
        // Act
        var found = await new WsDiscoveryClient().ProbeAsync(_fx.ProbeOptions());

        // Assert — one entry per camera, XAddr = that camera's ONVIF port, scopes carry name and type
        Assert.Equal(3, found.Count);
        foreach (var c in _fx.Farm.Cameras)
        {
            var d = Assert.Single(found, x => x.Port == c.Spec.OnvifPort);
            Assert.Equal("127.0.0.1", d.Host);
            Assert.Equal(c.Spec.DeviceServiceUrl, d.DeviceService!.ToString());
            Assert.Equal(c.Spec.Name, d.Name);
            Assert.Equal(c.Spec.IsPtz, d.PtzHint);
            Assert.Equal(WsDiscoveryResponder.EndpointAddress(c.Spec), d.EndpointAddress);
        }
        Assert.Contains(_fx.Requests, e => (string?)e["op"] == "Probe" && ((string)e["result"]!).StartsWith("ok matches=3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_read_device_info_streams_and_ptz_when_looking_up_with_camera_account()
    {
        var lookup = new OnvifCameraLookup(log: new TestLog());
        var ptz = _fx.Farm.Find(1)!.Spec;
        var fix = _fx.Farm.Find(2)!.Spec;

        var r1 = await lookup.LookupAsync("127.0.0.1", ptz.OnvifPort, _fx.Farm.Options.User, _fx.Farm.Options.Password);
        var r2 = await lookup.LookupAsync("127.0.0.1", fix.OnvifPort, _fx.Farm.Options.User, _fx.Farm.Options.Password);

        Assert.True(r1.Success, r1.Error);
        Assert.Equal("Ironwall DummyCam", r1.Manufacturer);
        Assert.Equal("DUMMY-PTZ", r1.Model);
        Assert.Equal("DUMMY-0001", r1.SerialNumber);
        Assert.Equal("DUMMY-CAM1", r1.Name);
        Assert.True(r1.IsPtz);
        Assert.Equal(ptz.MainRtspUrl, r1.RtspMain);
        Assert.Equal(ptz.SubRtspUrl, r1.RtspSub);
        Assert.Equal(ptz.DeviceServiceUrl, r1.DeviceServiceUrl);

        Assert.True(r2.Success, r2.Error);
        Assert.Equal("DUMMY-FIXED", r2.Model);
        Assert.False(r2.IsPtz);
    }

    [Fact]
    public async Task should_fail_lookup_without_throwing_when_camera_rejects_the_account()
    {
        var lookup = new OnvifCameraLookup(log: new TestLog());

        var bad = await lookup.LookupAsync("127.0.0.1", _fx.Farm.Find(3)!.Spec.OnvifPort, _fx.Farm.Options.User, _fx.Farm.Options.Password);
        var wrong = await lookup.LookupAsync("127.0.0.1", _fx.Farm.Find(2)!.Spec.OnvifPort, _fx.Farm.Options.User, "not-the-password");

        Assert.False(bad.Success);
        Assert.False(string.IsNullOrEmpty(bad.Error));
        Assert.False(wrong.Success);
    }

    [Fact]
    public async Task should_register_found_dummies_as_drafts_when_searching_looking_up_and_adding_in_the_panel_flow()
    {
        // Arrange — the camera panel's draft list (Temp-state: nothing goes to the server until [저장])
        var drafts = new List<CameraDeviceModel>();
        var vm = new OnvifDiscoveryViewModel(new WsDiscoveryClient(), new OnvifCameraLookup(log: new TestLog()),
            (h, p) => drafts.Any(d => d.IpAddress == h && d.IpPort == p),
            build => { drafts.Add(build(drafts.Count + 1)); return true; })
        {
            ProbeOptionsFactory = _fx.ProbeOptions,
            UserName = _fx.Farm.Options.User,
            Password = _fx.Farm.Options.Password,
        };

        // Act
        await vm.SearchAsync();
        await vm.LookupAllAsync();
        foreach (var row in vm.Items.ToList()) vm.Add(row);

        // Assert — cam1 · cam2 become drafts; cam3 (account rejected) stays a failed row
        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(2, drafts.Count);
        var cam3 = Assert.Single(vm.Items, r => r.Port == _fx.Farm.Find(3)!.Spec.OnvifPort);
        Assert.Equal(OnvifDiscoveredState.Failed, cam3.State);

        var p = Assert.Single(drafts, d => d.IpPort == _fx.Farm.Find(1)!.Spec.OnvifPort);
        Assert.Equal("DUMMY-CAM1", p.DeviceName);
        Assert.Equal(0, p.Id);
        Assert.Equal(EnumCameraMode.ONVIF, p.Mode);
        Assert.Equal(EnumCameraType.PTZ, p.Category);
        Assert.Equal((_fx.Farm.Options.User, _fx.Farm.Options.Password), (p.UserName, p.UserPassword));
        Assert.Equal(_fx.Farm.Find(1)!.Spec.MainRtspUrl, p.Urls!.RtspMain);

        var f = Assert.Single(drafts, d => d.IpPort == _fx.Farm.Find(2)!.Spec.OnvifPort);
        Assert.Equal(EnumCameraType.FIXED, f.Category);

        // a second search marks the drafted cameras as already listed - no duplicate rows
        await vm.SearchAsync();
        Assert.Equal(2, vm.Items.Count(r => r.State == OnvifDiscoveredState.AlreadyRegistered));
    }

    [Fact]
    public async Task should_answer_multicast_search_from_this_pc_when_farm_joins_the_group()
    {
        // The CLI mode (multicast 239.255.255.250) on an ephemeral port instead of 3702 - the real GIS multicast path.
        var o = new FarmOptions
        {
            Count = 2, PtzCount = 1, OnvifBasePort = 0, ControlPort = -1, EnableRtsp = false,
            DiscoveryPort = 0, DiscoveryMulticast = true, OutDir = TestDirs.NewOutDir("mcast"),
        };
        await using var farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        var probe = new WsDiscoveryProbeOptions { Multicast = true, MulticastPort = farm.Discovery!.Port, Window = TimeSpan.FromSeconds(1.5) };

        var found = await new WsDiscoveryClient().ProbeAsync(probe);

        Assert.True(farm.Discovery.IsMulticast);
        Assert.Equal(farm.Cameras.Select(c => c.Spec.OnvifPort).OrderBy(p => p), found.Select(d => d.Port).OrderBy(p => p));
    }

    [Fact]
    public void should_ignore_probe_for_other_device_types()
    {
        var parsed = WsDiscoveryResponder.ParseProbe(WsDiscoveryResponder.Probe("urn:uuid:1", "pub:Computer"));

        Assert.NotNull(parsed);
        Assert.False(parsed!.Value.Wanted);
        Assert.True(WsDiscoveryResponder.ParseProbe(WsDiscoveryResponder.Probe("urn:uuid:2", ""))!.Value.Wanted);
        Assert.True(WsDiscoveryResponder.ParseProbe(WsDiscoveryResponder.Probe("urn:uuid:3", "tds:Device"))!.Value.Wanted);
    }
}
