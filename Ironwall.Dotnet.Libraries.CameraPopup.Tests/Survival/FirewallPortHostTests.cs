using System.Net;
using System.Net.Sockets;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>
/// 방화벽 창(FW) — 실제 호스트 exe 가 영상 · ONVIF 를 다루는 동안 그 PID 가 루프백 밖에 들어오는 포트(TCP 대기 · UDP 바인드)를
/// 하나도 열지 않는다. 열면 Windows 가 "이 앱의 일부 기능이 차단됨" 창을 띄우고(관리자 실행으로도 안 사라진다), 운영자가 '취소' 를
/// 누르면 영구 차단 규칙이 생긴다. 소켓은 <c>netstat -ano</c> 표본의 합집합(<see cref="ListeningSockets"/>).
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class FirewallPortHostTests
{
    private readonly ITestOutputHelper _output;

    public FirewallPortHostTests(ITestOutputHelper output) => _output = output;

    private static string UniqueLogDirectory()
    {
        var dir = Path.Combine(HostPaths.NewLogDirectory(), "fw-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<CameraPopupHostSupervisor> StartHostAsync(string logDir)
    {
        var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = true,
            HostLogDirectory = logDir,
        }, new TestLog());
        var recorder = new StateRecorder(host);
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(20)));
        return host;
    }

    private static OverlayStreamRequest Overlay(string id, VideoProviderInfo provider, int width, int height)
        => new() { StreamId = id, Camera = new CameraRef { CameraId = id }, Provider = provider, Width = width, Height = height };

    private static string ReadHostLog(string logDir)
        => string.Join("\n", Directory.GetFiles(logDir, "*.log").Select(f =>
        {
            using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new StreamReader(fs).ReadToEnd();
        }));

    private void AssertNoInboundPorts(string phase, IReadOnlySet<string> sockets)
    {
        _output.WriteLine($"[{phase}] host sockets: {(sockets.Count == 0 ? "(none)" : string.Join(", ", sockets))}");
        Assert.DoesNotContain(sockets, s => !ListeningSockets.IsLoopback(s));
    }

    [Fact]
    [Trait("Scenario", "FW-000")]
    public void should_see_loopback_udp_and_tcp_listeners_of_this_process()
    {
        // 측정기 양성 대조 — 소켓을 못 보는 측정기로 "0개" 를 단언하지 않게(루프백이라 방화벽 창은 안 뜬다)
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        try
        {
            var sockets = ListeningSockets.Of(Environment.ProcessId);
            Assert.Contains($"UDP {udp.Client.LocalEndPoint}", sockets);
            Assert.Contains($"TCP {tcp.LocalEndpoint}", sockets);
            Assert.All(sockets.Where(s => s.EndsWith($":{((IPEndPoint)tcp.LocalEndpoint).Port}", StringComparison.Ordinal)), s => Assert.True(ListeningSockets.IsLoopback(s)));
        }
        finally
        {
            tcp.Stop();
        }
    }

    [Fact]
    [Trait("Scenario", "FW-001")]
    public async Task should_open_no_inbound_port_when_host_plays_file_and_talks_onvif_and_retries_dead_rtsp()
    {
        // Arrange — 네트워크 원본 없이도 도는 경로: PNG 파일(LibVLC 초기화 · 디코딩) · ONVIF(PTZ 준비 · 프리셋 · 해상도) · 닿지 않는 RTSP(실패 후 재시도)
        await using var camera = new FakeOnvifCamera();
        using var host = await StartHostAsync(UniqueLogDirectory());
        int pid = host.HostProcessId ?? throw new InvalidOperationException("호스트 PID 없음");
        using var control = new CameraPopupControl(host);
        var onvif = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = "pw" };
        var png = Path.Combine(UniqueLogDirectory(), "fw.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64, 36, 96, 96, PixelFormats.Bgra32, null, new byte[64 * 36 * 4], 64 * 4)));
        using (var fs = File.Create(png)) encoder.Save(fs);

        // Act
        using var file = host.OpenOverlay(Overlay("file", new VideoProviderInfo { Kind = VideoProviderKind.File, Uri = png, OpenTimeoutMs = 20_000 }, 160, 90))!;
        Assert.True(await StateRecorder.WaitUntilAsync(() => file.PublishedSequence > 0, TimeSpan.FromSeconds(25)), "파일 영상이 오지 않았다");
        var afterFile = await ListeningSockets.SampleAsync(pid, TimeSpan.FromSeconds(2));

        Assert.True((await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.PreparePtz, CameraId = "fw", Provider = onvif })).Success);
        Assert.True((await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.GetPresets, CameraId = "fw", Provider = onvif })).Success);
        Assert.True((await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.GetVideoResolution, CameraId = "fw", Provider = onvif })).Success);
        var afterOnvif = await ListeningSockets.SampleAsync(pid, TimeSpan.FromSeconds(1));

        using var dead = host.OpenOverlay(Overlay("dead", new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "rtsp://127.0.0.1:1/none", OpenTimeoutMs = 3000 }, 960, 540))!;
        var duringRetry = await ListeningSockets.SampleAsync(pid, TimeSpan.FromSeconds(10));

        // Assert
        AssertNoInboundPorts("file", afterFile);
        AssertNoInboundPorts("onvif", afterOnvif);
        AssertNoInboundPorts("dead-rtsp", duringRetry);
        Assert.Equal(pid, host.HostProcessId);   // 그 사이 호스트가 재시작되지 않았다(같은 PID 를 본 것)
    }

    [Fact]
    [Trait("Scenario", "FW-002")]
    public async Task should_open_no_inbound_port_when_host_plays_rtsp_directly_and_through_onvif()
    {
        // Arrange — MediaMTX(루프백) RTSP 원본. mediamtx.exe · ffmpeg.exe 가 없는 PC 면 건너뛴다.
        using var rtsp = await LoopbackRtspServer.TryStartAsync(UniqueLogDirectory());
        if (rtsp is null)
        {
            _output.WriteLine("건너뜀: mediamtx.exe(IRONWALL_MEDIAMTX · tools\\dummy-cameras\\bin\\mediamtx) 또는 PATH 의 ffmpeg.exe 없음 · MediaMTX 시작 실패");
            return;
        }
        await using var camera = new FakeOnvifCamera { StreamUri = rtsp.Url };
        var logDir = UniqueLogDirectory();
        using var host = await StartHostAsync(logDir);
        int pid = host.HostProcessId ?? throw new InvalidOperationException("호스트 PID 없음");
        var direct = new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = rtsp.Url, OpenTimeoutMs = 15_000 };
        var onvif = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = "pw" };

        // Act — 큰 상자(하드웨어 디코딩 시도) · 작은 상자(타일) · ONVIF 가 알려 준 RTSP
        using var large = host.OpenOverlay(Overlay("large", direct, 960, 540))!;
        using var small = host.OpenOverlay(Overlay("small", direct, 320, 180))!;
        using var viaOnvif = host.OpenOverlay(Overlay("onvif", onvif, 640, 360))!;
        var opening = await ListeningSockets.SampleAsync(pid, TimeSpan.FromSeconds(30),
            () => large.PublishedSequence > 0 && small.PublishedSequence > 0 && viaOnvif.PublishedSequence > 0);
        _output.WriteLine($"frames large={large.PublishedSequence}/{large.State} small={small.PublishedSequence}/{small.State} onvif={viaOnvif.PublishedSequence}/{viaOnvif.State}");
        foreach (var line in ReadHostLog(logDir).Split('\n').Where(l => l.Contains("udp", StringComparison.OrdinalIgnoreCase) || l.Contains("libvlc", StringComparison.Ordinal)))
            _output.WriteLine(line);
        Assert.True(large.PublishedSequence > 0 && small.PublishedSequence > 0 && viaOnvif.PublishedSequence > 0, "RTSP 영상이 오지 않았다");
        var playing = await ListeningSockets.SampleAsync(pid, TimeSpan.FromSeconds(5));

        // Assert
        AssertNoInboundPorts("rtsp-opening", opening);
        AssertNoInboundPorts("rtsp-playing", playing);
        Assert.Equal(pid, host.HostProcessId);
        // TCP 재생도 열 때마다 live555 가 0.0.0.0 에 UDP 를 잠깐 묶는다(ms — netstat 표본엔 안 잡힘) → 가로채 루프백으로 옮긴 기록
        var log = ReadHostLog(logDir);
        Assert.Contains("udp bind guard applied", log);
        Assert.Contains("moved to loopback", log);
    }

    [Fact]
    [Trait("Scenario", "FW-003")]
    public async Task should_open_no_inbound_port_when_rtsp_server_refuses_tcp()
    {
        // Arrange — TCP SETUP 을 461 로 거절하는(UDP 전용) RTSP 서버
        using var rtsp = await LoopbackRtspServer.TryStartAsync(UniqueLogDirectory(), udpOnly: true);
        if (rtsp is null)
        {
            _output.WriteLine("건너뜀: mediamtx.exe 또는 ffmpeg.exe 없음 · MediaMTX 시작 실패");
            return;
        }
        var logDir = UniqueLogDirectory();
        using var host = await StartHostAsync(logDir);
        int pid = host.HostProcessId ?? throw new InvalidOperationException("호스트 PID 없음");

        // Act
        using var stream = host.OpenOverlay(Overlay("udp-only", new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = rtsp.Url, OpenTimeoutMs = 8000 }, 960, 540))!;
        var sockets = await ListeningSockets.SampleAsync(pid, TimeSpan.FromSeconds(12));
        _output.WriteLine($"udp-only frames={stream.PublishedSequence} state={stream.State}/{stream.StateDetail}");

        // Assert — UDP 로 갈아타도 루프백에만 묶인다(밖의 카메라라면 영상이 안 나온다 — 방화벽 창보다 낫다)
        AssertNoInboundPorts("rtsp-udp-only", sockets);
        Assert.InRange(sockets.Count, 0, 16);   // 막힌 bind 를 live555 가 끝없이 다시 시도해 소켓이 쌓이지 않는다(거절 방식에서 수천 개)
        Assert.Equal(pid, host.HostProcessId);
        Assert.Contains("udp bind guard applied", ReadHostLog(logDir));
    }
}
