using System.Diagnostics;
using System.Text.Json;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.RealCamera;

/// <summary>
/// 실제 카메라(LAN) 실측(T-02) — 계정 파일이 있을 때만 돈다(<c>IRONWALL_REALCAM_CRED</c> = {"username","password"} JSON 경로).
/// 계정은 어디에도 찍지 않는다. 고정 카메라(.108) 오버레이 첫 프레임 냉/온 시간 · PTZ(.66) 아주 작은 이동과 되돌리기(프리셋 · Home 은 건드리지 않음).
/// 주소: <c>IRONWALL_REALCAM_FIXED</c>(기본 192.168.202.108) · <c>IRONWALL_REALCAM_PTZ</c>(기본 192.168.202.66).
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "RealCamera")]
public class RealCameraTests
{
    private readonly ITestOutputHelper _output;

    public RealCameraTests(ITestOutputHelper output) => _output = output;

    private static (string User, string Password)? LoadCredentials()
    {
        var path = Environment.GetEnvironmentVariable("IRONWALL_REALCAM_CRED");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return (doc.RootElement.GetProperty("username").GetString() ?? "", doc.RootElement.GetProperty("password").GetString() ?? "");
    }

    private static string Env(string name, string fallback)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

    private static async Task<(CameraPopupHostSupervisor Host, StateRecorder Recorder, long StartedMs)> StartAsync(bool headless)
    {
        var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = headless,
            HostLogDirectory = HostPaths.NewLogDirectory(),
        }, new TestLog());
        var recorder = new StateRecorder(host);
        var started = recorder.NowMs;
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(20)));
        return (host, recorder, started);
    }

    private static async Task<long> FirstFrameMsAsync(ICameraPopupHost host, string streamId, VideoProviderInfo provider)
    {
        var sw = Stopwatch.StartNew();
        using var source = host.OpenOverlay(new OverlayStreamRequest
        {
            StreamId = streamId,
            Camera = new CameraRef { CameraId = "108" },
            Provider = provider,
            Width = 576,
            Height = 386,
        });
        Assert.NotNull(source);
        var ok = await StateRecorder.WaitUntilAsync(() => source!.PublishedSequence > 0 || source.State == StreamState.Failed, TimeSpan.FromSeconds(40));
        Assert.True(ok && source!.PublishedSequence > 0, $"첫 프레임 없음 state={source!.State} detail={source.StateDetail}");
        return sw.ElapsedMilliseconds;
    }

    [Fact]
    public async Task should_play_fixed_camera_overlay_via_onvif_provider_when_camera_reachable()
    {
        var cred = LoadCredentials();
        if (cred is null) { _output.WriteLine("SKIP — IRONWALL_REALCAM_CRED 없음"); return; }
        var provider = new VideoProviderInfo
        {
            Kind = VideoProviderKind.Onvif,
            Host = Env("IRONWALL_REALCAM_FIXED", "192.168.202.108"),
            Port = 80,
            Username = cred.Value.User,
            Password = cred.Value.Password,
            PreferSubStream = true,
            OpenTimeoutMs = 20_000,
        };

        // ① 헤드리스 호스트 = 미리 데우기 없음(T-00 과 같은 냉시작) — 기준선
        var (cold, _, _) = await StartAsync(headless: true);
        long noPrewarmCold, noPrewarmWarm;
        using (cold)
        {
            noPrewarmCold = await FirstFrameMsAsync(cold, "real-a", provider);
            noPrewarmWarm = await FirstFrameMsAsync(cold, "real-b", provider);
        }

        // ② 창 모드 호스트 = 시작하자마자 LibVLC 미리 데우기(T-02). 사람이 더블클릭하는 시점을 흉내 내 3초 뒤 연다.
        var (warm, recorder, startedMs) = await StartAsync(headless: false);
        long prewarmedCold, prewarmedWarm, sinceHostStart;
        using (warm)
        {
            await Task.Delay(3000);
            sinceHostStart = recorder.NowMs - startedMs;
            prewarmedCold = await FirstFrameMsAsync(warm, "real-c", provider);
            prewarmedWarm = await FirstFrameMsAsync(warm, "real-d", provider);
        }

        _output.WriteLine($"first frame (ms) — no prewarm: cold {noPrewarmCold} · warm {noPrewarmWarm} | prewarm (opened {sinceHostStart} ms after host start): cold {prewarmedCold} · warm {prewarmedWarm}");
        Assert.True(prewarmedCold < noPrewarmCold, $"미리 데우기가 첫 프레임을 줄이지 못했다: {prewarmedCold} ≥ {noPrewarmCold}");
    }

    [Fact]
    public async Task should_move_ptz_camera_a_little_and_back_when_camera_reachable()
    {
        var cred = LoadCredentials();
        if (cred is null) { _output.WriteLine("SKIP — IRONWALL_REALCAM_CRED 없음"); return; }
        var provider = new VideoProviderInfo
        {
            Kind = VideoProviderKind.Onvif,
            Host = Env("IRONWALL_REALCAM_PTZ", "192.168.202.66"),
            Port = 80,
            Username = cred.Value.User,
            Password = cred.Value.Password,
        };
        var (host, _, _) = await StartAsync(headless: true);
        using var _host = host;
        using var control = new CameraPopupControl(host);

        var sw = Stopwatch.StartNew();
        var prepared = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.PreparePtz, CameraId = "66", Provider = provider });
        var prepareMs = sw.ElapsedMilliseconds;
        Assert.True(prepared.Success && prepared.PtzCapable, prepared.ToString());

        // 아주 작은 오른쪽 이동 400 ms → 정지 → 같은 만큼 왼쪽 → 정지(프리셋 · Home 은 건드리지 않는다)
        Assert.True(control.Move("66", provider, 0.2, 0, 0));
        await Task.Delay(400);
        Assert.True(control.Stop("66", provider));
        await Task.Delay(800);
        Assert.True(control.Move("66", provider, -0.2, 0, 0));
        await Task.Delay(400);
        Assert.True(control.Stop("66", provider));
        await Task.Delay(800);

        // 호스트가 살아 있고(명령은 보내고 잊기 — 결과는 호스트 로그) 다음 요청에도 답한다
        var again = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.PreparePtz, CameraId = "66", Provider = provider });
        _output.WriteLine($"PTZ prepare via host {prepareMs} ms (cold) · second prepare ok={again.Success}");
        Assert.Equal(CameraPopupHostState.Running, host.State);
        Assert.True(again.Success);
    }
}
