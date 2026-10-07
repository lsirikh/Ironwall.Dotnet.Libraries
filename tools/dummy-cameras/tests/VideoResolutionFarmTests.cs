using DummyCameras.Infra;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;

namespace DummyCameras.Tests;

/// <summary>
/// ROI reference resolution: an ONVIF-only farm started like <c>run --size 1280x1024</c> (5:4 main, default 16:9 sub)
/// answers GetProfiles; our production provider path reports the MAIN stream size for PTZ and fixed cameras alike.
/// </summary>
public class VideoResolutionFarmTests
{
    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    [Fact]
    public async Task should_report_the_main_stream_size_the_farm_was_started_with()
    {
        // Arrange — cam1 PTZ · cam2 fixed, main 1280x1024 (narrower than 16:9), sub stays 640x360
        await using var farm = await Farm.StartAsync(new FarmOptions
        {
            Count = 2,
            PtzCount = 1,
            OnvifBasePort = 0,
            ControlPort = 0,
            EnableRtsp = false,
            Width = 1280,
            Height = 1024,
            OutDir = TestDirs.NewOutDir("resolution"),
        }, TextWriter.Null, CancellationToken.None);
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        VideoProviderInfo Info(int cam) => new()
        {
            Kind = VideoProviderKind.Onvif,
            Host = "127.0.0.1",
            Port = farm.Find(cam)!.Spec.OnvifPort,
            Username = farm.Options.User,
            Password = farm.Options.Password,
        };

        // Act
        var r1 = await ptz.PrepareAsync("res-c1", Info(1), Within(10_000));
        var r2 = await ptz.PrepareAsync("res-c2", Info(2), Within(10_000));
        var main1 = await ptz.GetVideoResolutionAsync("res-c1", Within(5_000));
        var main2 = await ptz.GetVideoResolutionAsync("res-c2", Within(5_000));

        // Assert
        Assert.True(r1.Connected && r2.Connected, $"{r1} {r2}");
        Assert.Equal((1280, 1024), (main1!.Width, main1.Height));
        Assert.Equal((1280, 1024), (main2!.Width, main2.Height));
    }
}
