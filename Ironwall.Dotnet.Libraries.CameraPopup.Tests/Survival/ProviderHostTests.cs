using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>
/// 제공자 경로 전체(T-02): 이 시험 프로세스("GIS") → 감시자 → 실제 호스트 exe → 제공자 → 가짜 ONVIF 카메라.
/// GIS 쪽은 요청 · 순간 명령만 보내고(ONVIF · LibVLC 없음), 실패는 그 요청 · 그 상자에만 보인다(FR-26).
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class ProviderHostTests
{
    private const string Secret = "Zx9-t02-secret";
    private readonly ITestOutputHelper _output;

    public ProviderHostTests(ITestOutputHelper output) => _output = output;

    private static string UniqueLogDirectory()
    {
        var dir = Path.Combine(HostPaths.NewLogDirectory(), "t02-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<(CameraPopupHostSupervisor Host, StateRecorder Recorder)> StartHostAsync(string logDir, bool headless = true)
    {
        var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = headless,
            HostLogDirectory = logDir,
        }, new TestLog());
        var recorder = new StateRecorder(host);
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(20)));
        return (host, recorder);
    }

    [Fact]
    public async Task should_prepare_list_presets_and_move_through_host_when_camera_is_onvif()
    {
        // Arrange
        await using var camera = new FakeOnvifCamera();
        var logDir = UniqueLogDirectory();
        var (host, _) = await StartHostAsync(logDir);
        using var _host = host;
        using var control = new CameraPopupControl(host);
        var provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = Secret };

        // Act
        var sw = Stopwatch.StartNew();
        var prepared = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.PreparePtz, CameraId = "7", Provider = provider });
        var prepareMs = sw.ElapsedMilliseconds;
        var presets = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.GetPresets, CameraId = "7", Provider = provider });
        Assert.True(control.Move("7", provider, 0.3, 0, 0));
        await Task.Delay(300);
        Assert.True(control.Stop("7", provider));
        var stopped = await StateRecorder.WaitUntilAsync(() => camera.Requests.Any(r => r.Body.Contains("Stop") && !r.Body.Contains("ContinuousMove")), TimeSpan.FromSeconds(5));

        // Assert
        _output.WriteLine($"PreparePtz via host {prepareMs} ms · requests={camera.Requests.Count}");
        Assert.True(prepared.Success, prepared.ToString());
        Assert.True(prepared.PtzCapable);
        Assert.True(presets.Success, presets.ToString());
        Assert.Equal(new[] { "정문", "후문" }, presets.Presets!.Select(p => p.Name));
        Assert.True(camera.Count("ContinuousMove") >= 1, "이동이 카메라에 닿지 않았다");
        Assert.True(stopped, "정지가 카메라에 닿지 않았다");
        Assert.All(camera.Requests, r => Assert.False(r.HasExpectContinue));
        // 이 프로세스(GIS 역할)에는 LibVLC 가 올라오지 않는다(§0)
        Assert.DoesNotContain(Process.GetCurrentProcess().Modules.Cast<ProcessModule>(),
            m => m.ModuleName.StartsWith("libvlc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "LibVLCSharp");
    }

    [Fact]
    public async Task should_mark_only_that_overlay_when_provider_is_unsupported_or_stream_unreachable()
    {
        // Arrange — 시험 무늬(정상) · 외부 VMS(지원 안 함) · 닿지 않는 RTSP(연결 안 됨) 셋을 한 호스트에
        var logDir = UniqueLogDirectory();
        var (host, recorder) = await StartHostAsync(logDir);
        using var _host = host;

        // Act
        using var healthy = host.OpenOverlay(new OverlayStreamRequest { StreamId = "ok", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern }, Width = 64, Height = 48 });
        using var vms = host.OpenOverlay(new OverlayStreamRequest
        {
            StreamId = "vms",
            Camera = new CameraRef { CameraId = "9" },
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.ExternalVms, Host = "10.0.0.9", Username = "u", Password = Secret },
            Width = 64,
            Height = 48,
        });
        using var dead = host.OpenOverlay(new OverlayStreamRequest
        {
            StreamId = "dead",
            Camera = new CameraRef { CameraId = "10" },
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "rtsp://127.0.0.1:1/none", Username = "fake-user", Password = Secret, OpenTimeoutMs = 4000 },
            Width = 64,
            Height = 48,
        });
        Assert.NotNull(healthy);
        Assert.NotNull(vms);
        Assert.NotNull(dead);

        // Assert — 지원 안 함은 곧바로, 닿지 않는 RTSP 는 제한 시간(+LibVLC 냉시작) 안에 실패, 시험 무늬는 계속 재생
        Assert.True(await StateRecorder.WaitUntilAsync(() => vms!.State == StreamState.Failed, TimeSpan.FromSeconds(10)), $"vms state={vms!.State}");
        Assert.Equal(CameraErrorCodes.NotSupported, vms!.StateDetail);
        Assert.True(await StateRecorder.WaitUntilAsync(() => dead!.State is StreamState.Failed or StreamState.Stalled, TimeSpan.FromSeconds(45)), $"dead state={dead!.State}");
        Assert.True(await StateRecorder.WaitUntilAsync(() => healthy!.PublishedSequence > 10, TimeSpan.FromSeconds(10)));
        Assert.Equal(StreamState.Playing, healthy!.State);
        Assert.Equal(CameraPopupHostState.Running, host.State);
        Assert.DoesNotContain(recorder.States, s => s.Args.NewState == CameraPopupHostState.Restarting);
        _output.WriteLine($"vms={vms.State}/{vms.StateDetail} dead={dead!.State}/{dead.StateDetail} healthy seq={healthy.PublishedSequence}");
    }

    [Fact]
    public async Task should_keep_credentials_out_of_host_log_when_providers_run()
    {
        // Arrange — 비밀번호가 실린 ONVIF · RTSP 요청을 한 번씩 돌린다
        await using var camera = new FakeOnvifCamera();
        var logDir = UniqueLogDirectory();
        var (host, _) = await StartHostAsync(logDir);
        using (host)
        {
            using var control = new CameraPopupControl(host);
            var onvif = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = Secret, FallbackUri = $"rtsp://fake-user:{Secret}@127.0.0.1:1/x" };
            await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.PreparePtz, CameraId = "1", Provider = onvif });
            using var overlay = host.OpenOverlay(new OverlayStreamRequest { StreamId = "cred", Camera = new CameraRef { CameraId = "1" }, Provider = onvif, Width = 32, Height = 32 });
            await StateRecorder.WaitUntilAsync(() => camera.Count("GetStreamUri") > 0, TimeSpan.FromSeconds(15));
            await Task.Delay(500);
        }

        // Assert — 호스트 로그 어디에도 비밀번호 · URL 계정이 없다
        var text = string.Join("\n", Directory.GetFiles(logDir, "*.log").Select(f =>
        {
            using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd();
        }));
        Assert.False(string.IsNullOrWhiteSpace(text), "호스트 로그가 비었다");
        Assert.Contains("resolve cam=1", text);   // 조회는 실제로 돌았다
        Assert.DoesNotContain(Secret, text);
        Assert.DoesNotContain("fake-user:", text);
    }
}
