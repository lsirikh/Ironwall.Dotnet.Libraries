using Ironwall.Dotnet.Libraries.Devices.Ui.Services.Onvif;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoEncoderConfigs;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 카메라 패널 [ONVIF 검색] — 검색(WS-Discovery) 파싱 · 조회 결과 변환 · 검색 → 조회 → Draft 흐름
   Created On   : 2026-10-04
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public class WsDiscoveryParseTests
{
    private const string Relates = "urn:uuid:11111111-2222-3333-4444-555555555555";

    private static string Match(string xaddrs, string types = "dn:NetworkVideoTransmitter tds:Device", string? relatesTo = Relates,
        string address = "urn:uuid:aaaaaaaa-0000-0000-0000-000000000001", string scopes =
            "onvif://www.onvif.org/name/DUMMY-CAM1 onvif://www.onvif.org/hardware/DUMMY-PTZ onvif://www.onvif.org/type/ptz") =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:a=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\" " +
        "xmlns:d=\"http://schemas.xmlsoap.org/ws/2005/04/discovery\" xmlns:dn=\"http://www.onvif.org/ver10/network/wsdl\" xmlns:tds=\"http://www.onvif.org/ver10/device/wsdl\">" +
        "<s:Header><a:MessageID>urn:uuid:99999999-0000-0000-0000-000000000000</a:MessageID>" +
        (relatesTo is null ? "" : $"<a:RelatesTo>{relatesTo}</a:RelatesTo>") +
        "<a:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/ProbeMatches</a:Action></s:Header>" +
        "<s:Body><d:ProbeMatches><d:ProbeMatch>" +
        $"<a:EndpointReference><a:Address>{address}</a:Address></a:EndpointReference>" +
        $"<d:Types>{types}</d:Types><d:Scopes>{scopes}</d:Scopes><d:XAddrs>{xaddrs}</d:XAddrs><d:MetadataVersion>1</d:MetadataVersion>" +
        "</d:ProbeMatch></d:ProbeMatches></s:Body></s:Envelope>";

    [Fact]
    public void should_read_address_port_name_and_ptz_hint_when_probe_match_is_a_dummy_camera()
    {
        var list = WsDiscoveryClient.ParseProbeMatches(Match("http://127.0.0.1:8181/onvif/device_service"), Relates);

        var d = Assert.Single(list);
        Assert.Equal("127.0.0.1", d.Host);
        Assert.Equal(8181, d.Port);
        Assert.Equal("DUMMY-CAM1", d.Name);
        Assert.Equal("DUMMY-PTZ", d.Hardware);
        Assert.True(d.PtzHint);
    }

    [Fact]
    public void should_prefer_ipv4_xaddr_when_camera_also_announces_ipv6()
    {
        var list = WsDiscoveryClient.ParseProbeMatches(
            Match("http://[fe80::1]/onvif/device_service http://192.168.202.31/onvif/device_service"), Relates);

        Assert.Equal("192.168.202.31", Assert.Single(list).Host);
        Assert.Equal(80, list[0].Port);
    }

    [Fact]
    public void should_ignore_reply_when_relates_to_another_search()
    {
        var list = WsDiscoveryClient.ParseProbeMatches(Match("http://127.0.0.1:8181/onvif/device_service",
            relatesTo: "urn:uuid:00000000-dead-beef-0000-000000000000"), Relates);

        Assert.Empty(list);
    }

    [Fact]
    public void should_accept_reply_without_relates_to_when_device_omits_it()
    {
        Assert.Single(WsDiscoveryClient.ParseProbeMatches(Match("http://127.0.0.1:8181/onvif/device_service", relatesTo: null), Relates));
    }

    [Fact]
    public void should_skip_non_camera_when_types_lack_network_video_transmitter()
    {
        var list = WsDiscoveryClient.ParseProbeMatches(Match("http://10.0.0.5:5357/", types: "wsdp:Device pub:Computer"), Relates);

        Assert.Empty(list);
    }

    [Fact]
    public void should_return_empty_when_message_is_not_probe_matches()
    {
        Assert.Empty(WsDiscoveryClient.ParseProbeMatches(WsDiscoveryClient.BuildProbe(Relates)));
    }

    [Fact]
    public void should_ask_for_network_video_transmitter_when_building_probe()
    {
        var doc = XDocument.Parse(WsDiscoveryClient.BuildProbe(Relates));
        var probe = doc.Descendants().Single(e => e.Name.LocalName == "Probe");

        Assert.Equal(WsDiscoveryClient.NsWsd, probe.Name.NamespaceName);
        Assert.Equal("dn:NetworkVideoTransmitter", probe.Elements().Single(e => e.Name.LocalName == "Types").Value);
        Assert.Equal(Relates, doc.Descendants().Single(e => e.Name.LocalName == "MessageID").Value);
    }

    [Fact]
    public async Task should_collect_unicast_replies_and_merge_duplicates_when_probing_a_loopback_responder()
    {
        // 3702 · 멀티캐스트를 건드리지 않는다 — 임시 포트의 가짜 응답기에 유니캐스트로 묻는다.
        using var responder = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)responder.Client.LocalEndPoint!).Port;
        var answer = Task.Run(async () =>
        {
            for (int i = 0; i < 2; i++)   // 클라이언트는 Probe 를 두 번 보낸다 → 같은 장치 응답 두 번 = 한 줄로 합쳐야 한다
            {
                var rx = await responder.ReceiveAsync();
                var id = XDocument.Parse(Encoding.UTF8.GetString(rx.Buffer)).Descendants().Single(e => e.Name.LocalName == "MessageID").Value;
                await responder.SendAsync(Encoding.UTF8.GetBytes(Match("http://127.0.0.1:8181/onvif/device_service", relatesTo: id)), rx.RemoteEndPoint);
                await responder.SendAsync(Encoding.UTF8.GetBytes(Match("http://127.0.0.1:8182/onvif/device_service", relatesTo: id,
                    address: "urn:uuid:aaaaaaaa-0000-0000-0000-000000000002", scopes: "onvif://www.onvif.org/name/DUMMY-CAM2")), rx.RemoteEndPoint);
            }
        });
        var options = new WsDiscoveryProbeOptions { Multicast = false, Window = TimeSpan.FromSeconds(1.5) };
        options.UnicastTargets.Add(new IPEndPoint(IPAddress.Loopback, port));

        var found = await new WsDiscoveryClient().ProbeAsync(options);
        await answer.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { 8181, 8182 }, found.Select(d => d.Port).ToArray());
        Assert.Equal(new[] { "DUMMY-CAM1", "DUMMY-CAM2" }, found.Select(d => d.Name).ToArray());
    }

    [Fact]
    public async Task should_return_empty_without_throwing_when_nobody_answers()
    {
        var options = new WsDiscoveryProbeOptions { Multicast = false, Window = TimeSpan.FromMilliseconds(400) };
        options.UnicastTargets.Add(new IPEndPoint(IPAddress.Loopback, 9));   // discard — 응답 없음(ICMP 도 무시)

        Assert.Empty(await new WsDiscoveryClient().ProbeAsync(options));
    }
}

public class OnvifLookupMappingTests
{
    private static CameraProfileDto Profile(string uri, int w, int h) => new()
    {
        MediaUri = new MediaUriDto { Uri = uri },
        VideoEncoderConfig = new VideoEncoderConfigDto { Resolution = new VideoResolutionDto { Width = w, Height = h } },
    };

    [Fact]
    public void should_pick_largest_as_main_and_smallest_as_sub_when_two_profiles()
    {
        var (main, sub) = OnvifCameraLookup.PickStreams(new[]
        {
            Profile("rtsp://127.0.0.1:8554/cam1_sub", 640, 360),
            Profile("rtsp://127.0.0.1:8554/cam1", 1280, 720),
        });

        Assert.Equal("rtsp://127.0.0.1:8554/cam1", main);
        Assert.Equal("rtsp://127.0.0.1:8554/cam1_sub", sub);
    }

    [Fact]
    public void should_leave_sub_empty_when_only_one_stream()
    {
        var (main, sub) = OnvifCameraLookup.PickStreams(new[] { Profile("rtsp://h/main", 1920, 1080), new CameraProfileDto() });

        Assert.Equal("rtsp://h/main", main);
        Assert.Null(sub);
    }

    [Fact]
    public void should_fail_when_model_is_missing()
    {
        var r = OnvifCameraLookup.FromModel(null, 80);

        Assert.False(r.Success);
        Assert.False(string.IsNullOrEmpty(r.Error));
    }

    [Fact]
    public async Task should_fail_without_network_when_address_or_port_is_invalid()
    {
        var lookup = new OnvifCameraLookup(timeout: TimeSpan.FromSeconds(1));

        Assert.False((await lookup.LookupAsync("", 80, null, null)).Success);
        Assert.False((await lookup.LookupAsync("127.0.0.1", 0, null, null)).Success);
    }

    [Fact]
    public void should_build_onvif_draft_with_spec_urls_and_ptz_category_when_lookup_succeeded()
    {
        var lookup = new OnvifLookupResult
        {
            Success = true, Name = "DUMMY-CAM1", Manufacturer = "Ironwall DummyCam", Model = "DUMMY-PTZ", Firmware = "1.0.0",
            SerialNumber = "DUMMY-0001", HardwareId = "dummycam-1", MacAddress = "02:00:00:00:00:01", OnvifVersion = "2.60",
            DeviceServiceUrl = "http://127.0.0.1:8181/onvif/device_service", IsPtz = true,
            RtspMain = "rtsp://127.0.0.1:8554/cam1", RtspSub = "rtsp://127.0.0.1:8554/cam1_sub",
        };

        var m = OnvifCameraDraftFactory.Create(3, "127.0.0.1", 8181, "dummy", "dummy1234", lookup);

        Assert.Equal(0, m.Id);   // Draft — 서버 미반영
        Assert.Equal(3, m.DeviceNumber);
        Assert.Equal("DUMMY-CAM1", m.DeviceName);
        Assert.Equal(EnumDeviceType.IpCamera, m.DeviceType);
        Assert.Equal(("127.0.0.1", 8181), (m.IpAddress, m.IpPort));
        Assert.Equal(("dummy", "dummy1234"), (m.UserName, m.UserPassword));
        Assert.Equal(EnumCameraMode.ONVIF, m.Mode);
        Assert.Equal(EnumCameraType.PTZ, m.Category);
        Assert.Equal("Ironwall DummyCam", m.HardwareSpec!.Manufacturer);
        Assert.Equal("DUMMY-PTZ", m.HardwareSpec.Model);
        Assert.Equal("DUMMY-0001", m.HardwareSpec.DeviceId);
        Assert.Equal("http://127.0.0.1:8181/onvif/device_service", m.Urls!.OnvifDeviceService);
        Assert.Equal("rtsp://127.0.0.1:8554/cam1", m.Urls.RtspMain);
        Assert.Equal("rtsp://127.0.0.1:8554/cam1_sub", m.Urls.RtspSub);
    }

    [Fact]
    public void should_fall_back_to_maker_then_address_when_camera_has_no_name()
    {
        Assert.Equal("Dahua IPC-HFW", OnvifCameraDraftFactory.DisplayName(new OnvifLookupResult { Manufacturer = "Dahua", Model = "IPC-HFW" }, null, "h", 80));
        Assert.Equal("ONVIF h:80", OnvifCameraDraftFactory.DisplayName(new OnvifLookupResult(), " ", "h", 80));
        Assert.Equal("FrontGate", OnvifCameraDraftFactory.DisplayName(new OnvifLookupResult { Manufacturer = "Dahua" }, "FrontGate", "h", 80));
    }

    [Fact]
    public void should_store_no_credentials_when_account_is_blank()
    {
        var m = OnvifCameraDraftFactory.Create(1, "h", 80, "", "", new OnvifLookupResult { Success = true });

        Assert.Null(m.UserName);
        Assert.Null(m.UserPassword);
        Assert.Equal(EnumCameraType.FIXED, m.Category);
    }
}

public class OnvifDiscoveryViewModelTests
{
    private sealed class FakeDiscovery : IWsDiscoveryClient
    {
        public List<DiscoveredOnvifDevice> Devices { get; } = new();
        public Task<IReadOnlyList<DiscoveredOnvifDevice>> ProbeAsync(WsDiscoveryProbeOptions options, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DiscoveredOnvifDevice>>(Devices);
    }

    private sealed class FakeLookup : IOnvifCameraLookup
    {
        public List<(string Host, int Port, string? User, string? Password)> Calls { get; } = new();
        public Func<string, int, string?, OnvifLookupResult> Answer { get; set; } =
            (h, p, u) => new OnvifLookupResult { Success = true, Name = $"CAM-{p}", IsPtz = p % 2 == 1, RtspMain = $"rtsp://{h}:8554/{p}" };
        public Task<OnvifLookupResult> LookupAsync(string host, int port, string? userName, string? password, CancellationToken ct = default)
        {
            lock (Calls) Calls.Add((host, port, userName, password));
            return Task.FromResult(Answer(host, port, userName));
        }
    }

    private static DiscoveredOnvifDevice Device(int port, string? name = null) =>
        new($"urn:uuid:{port}", new[] { $"http://127.0.0.1:{port}/onvif/device_service" }, new[] { "dn:NetworkVideoTransmitter" },
            name is null ? Array.Empty<string>() : new[] { "onvif://www.onvif.org/name/" + name });

    private sealed class Harness
    {
        public FakeDiscovery Discovery { get; } = new();
        public FakeLookup Lookup { get; } = new();
        public List<CameraDeviceModel> Drafts { get; } = new();
        public HashSet<(string, int)> Saved { get; } = new();
        public bool AllowAdd { get; set; } = true;
        public OnvifDiscoveryViewModel Vm { get; }

        public Harness()
        {
            Vm = new OnvifDiscoveryViewModel(Discovery, Lookup,
                (h, p) => Saved.Contains((h, p)) || Drafts.Any(d => d.IpAddress == h && d.IpPort == p),
                build =>
                {
                    if (!AllowAdd) return false;
                    Drafts.Add(build(Drafts.Count + 1));
                    return true;
                });
        }
    }

    [Fact]
    public async Task should_list_found_cameras_and_mark_already_registered_when_searching()
    {
        var h = new Harness();
        h.Discovery.Devices.AddRange(new[] { Device(8181, "DUMMY-CAM1"), Device(8182) });
        h.Saved.Add(("127.0.0.1", 8182));

        await h.Vm.SearchAsync();

        Assert.Equal(2, h.Vm.Items.Count);
        Assert.Equal(OnvifDiscoveredState.Found, h.Vm.Items[0].State);
        Assert.Equal("DUMMY-CAM1", h.Vm.Items[0].NameDisplay);
        Assert.Equal(OnvifDiscoveredState.AlreadyRegistered, h.Vm.Items[1].State);
        Assert.False(h.Vm.Items[1].CanLookup);
        Assert.Contains("2대", h.Vm.StatusText);
    }

    [Fact]
    public async Task should_lookup_with_typed_account_then_add_draft_with_same_account()
    {
        var h = new Harness();
        h.Discovery.Devices.Add(Device(8181, "DUMMY-CAM1"));
        await h.Vm.SearchAsync();
        h.Vm.UserName = "dummy";
        h.Vm.Password = "dummy1234";
        var row = h.Vm.Items[0];

        await h.Vm.LookupAsync(row);
        h.Vm.UserName = "changed-after-lookup";
        var added = h.Vm.Add(row);

        Assert.Equal(("127.0.0.1", 8181, "dummy", "dummy1234"), Assert.Single(h.Lookup.Calls));
        Assert.True(added);
        Assert.Equal(OnvifDiscoveredState.Added, row.State);
        var d = Assert.Single(h.Drafts);
        Assert.Equal(("127.0.0.1", 8181, "dummy", "dummy1234"), (d.IpAddress, d.IpPort, d.UserName, d.UserPassword));
        Assert.Equal("CAM-8181", d.DeviceName);
        Assert.Equal(EnumCameraType.PTZ, d.Category);
        Assert.False(row.CanAdd);   // 두 번 넣지 않는다
        Assert.False(h.Vm.Add(row));
        Assert.Single(h.Drafts);
    }

    [Fact]
    public async Task should_not_add_before_lookup()
    {
        var h = new Harness();
        h.Discovery.Devices.Add(Device(8181));
        await h.Vm.SearchAsync();

        Assert.False(h.Vm.Add(h.Vm.Items[0]));
        Assert.Empty(h.Drafts);
    }

    [Fact]
    public async Task should_show_failure_and_allow_retry_when_account_is_rejected()
    {
        var h = new Harness();
        h.Discovery.Devices.Add(Device(8181));
        h.Lookup.Answer = (_, _, _) => OnvifLookupResult.Fail("장비 정보를 읽지 못했습니다 — 계정을 확인하세요.");
        await h.Vm.SearchAsync();
        var row = h.Vm.Items[0];

        await h.Vm.LookupAsync(row);

        Assert.Equal(OnvifDiscoveredState.Failed, row.State);
        Assert.Contains("계정을 확인", row.StateText);
        Assert.True(row.CanLookup);
        Assert.False(row.CanAdd);
    }

    [Fact]
    public async Task should_lookup_every_open_row_but_skip_registered_when_looking_up_all()
    {
        var h = new Harness();
        h.Discovery.Devices.AddRange(new[] { Device(8181), Device(8182), Device(8183) });
        h.Saved.Add(("127.0.0.1", 8183));
        await h.Vm.SearchAsync();

        await h.Vm.LookupAllAsync();

        Assert.Equal(new[] { 8181, 8182 }, h.Lookup.Calls.Select(c => c.Port).OrderBy(p => p).ToArray());
        Assert.All(h.Vm.Items.Take(2), r => Assert.Equal(OnvifDiscoveredState.LookedUp, r.State));
        Assert.Contains("2/2", h.Vm.StatusText);
    }

    [Fact]
    public async Task should_keep_row_open_when_panel_refuses_draft()
    {
        var h = new Harness { AllowAdd = false };
        h.Discovery.Devices.Add(Device(8181));
        await h.Vm.SearchAsync();
        var row = h.Vm.Items[0];
        await h.Vm.LookupAsync(row);

        Assert.False(h.Vm.Add(row));
        Assert.Equal(OnvifDiscoveredState.LookedUp, row.State);
        Assert.Contains("추가할 수 없습니다", h.Vm.StatusText);
    }

    [Fact]
    public async Task should_mark_registered_instead_of_adding_when_same_address_appeared_meanwhile()
    {
        var h = new Harness();
        h.Discovery.Devices.Add(Device(8181));
        await h.Vm.SearchAsync();
        var row = h.Vm.Items[0];
        await h.Vm.LookupAsync(row);
        h.Saved.Add(("127.0.0.1", 8181));   // 그사이 다른 경로로 등록됨

        Assert.False(h.Vm.Add(row));
        Assert.Equal(OnvifDiscoveredState.AlreadyRegistered, row.State);
        Assert.Empty(h.Drafts);
    }
}
