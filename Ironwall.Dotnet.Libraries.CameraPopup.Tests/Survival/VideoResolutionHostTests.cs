using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>
/// ROI 기준 해상도 채우기 — 이 시험 프로세스("GIS") → 감시자 → 실제 호스트 exe → ONVIF 제공자 → 가짜 ONVIF 카메라.
/// GIS 는 카메라를 직접 부르지 않고 호스트에 <see cref="CameraRequestKind.GetVideoResolution"/> 를 보낸다.
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class VideoResolutionHostTests
{
    [Fact]
    public async Task should_answer_the_main_stream_resolution_through_the_host()
    {
        // Arrange — 5:4 메인(1280×1024) · 16:9 서브
        await using var camera = new FakeOnvifCamera { Profiles = new[] { ("P0", 1280, 1024), ("P1", 640, 360) } };
        var logDir = Path.Combine(HostPaths.NewLogDirectory(), "res-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(logDir);
        using var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = true,
            HostLogDirectory = logDir,
        }, new TestLog());
        var recorder = new StateRecorder(host);
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(20)));
        using var control = new CameraPopupControl(host);
        var provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = "pw" };

        // Act — 준비 없이 바로 묻는다(호스트가 먼저 준비한다)
        var response = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.GetVideoResolution, CameraId = "11", Provider = provider });

        // Assert
        Assert.True(response.Success, response.ToString());
        Assert.Equal(CameraRequestKind.GetVideoResolution, response.Kind);
        Assert.Equal((1280, 1024), (response.VideoWidth, response.VideoHeight));
        Assert.Equal(CameraPopupHostState.Running, host.State);
    }
}
