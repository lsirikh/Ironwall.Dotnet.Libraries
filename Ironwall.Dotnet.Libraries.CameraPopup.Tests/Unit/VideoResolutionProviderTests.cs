using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// ROI 기준 해상도 채우기 — 카메라 메인 스트림 해상도(ONVIF 프로파일 중 최대)를 호스트 제공자가 읽는다.
/// 실제 제공자 경로(<see cref="CameraProviderRegistry"/> → PtzController → OnvifSolution)를 루프백 가짜 ONVIF 카메라에 붙인다.
/// </summary>
public class VideoResolutionProviderTests
{
    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private static VideoProviderInfo Info(FakeOnvifCamera camera)
        => new() { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = "pw" };

    [Fact]
    public void should_pick_the_largest_profile_as_the_main_resolution()
    {
        var profiles = new OnvifProfileSelector.ProfileInfo[]
        {
            new("sub", 720, 480, false),
            new("main", 1280, 1024, true),
            new("third", 352, 240, false),
        };

        Assert.Equal((1280, 1024), OnvifProfileSelector.MainResolution(profiles));
    }

    [Fact]
    public void should_have_no_main_resolution_without_profile_sizes()
    {
        Assert.Null(OnvifProfileSelector.MainResolution(new OnvifProfileSelector.ProfileInfo[] { new("a", 0, 0, false), new("b", 640, 0, false) }));
        Assert.Null(OnvifProfileSelector.MainResolution(Array.Empty<OnvifProfileSelector.ProfileInfo>()));
        Assert.Null(OnvifProfileSelector.MainResolution(null!));
    }

    [Fact]
    public async Task should_read_the_main_stream_resolution_from_the_prepared_camera_profiles()
    {
        // Arrange — 5:4 메인 · 16:9 서브(가짜 카메라 팜 --size 1280x1024 과 같은 모양)
        await using var camera = new FakeOnvifCamera { Profiles = new[] { ("P0", 1280, 1024), ("P1", 640, 360) } };
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        var ready = await ptz.PrepareAsync("res-1", Info(camera), Within(15_000));
        Assert.True(ready.Connected, ready.ToString());
        int before = camera.Count("GetProfiles");

        // Act
        var resolution = await ptz.GetVideoResolutionAsync("res-1", Within(5000));

        // Assert — 준비 때 받은 프로파일 캐시만 본다(GetProfiles 를 다시 묻지 않는다)
        Assert.NotNull(resolution);
        Assert.Equal((1280, 1024), (resolution!.Width, resolution.Height));
        Assert.Equal(before, camera.Count("GetProfiles"));
    }

    [Fact]
    public async Task should_not_know_the_resolution_of_a_camera_that_is_not_prepared()
    {
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;

        Assert.Null(await ptz.GetVideoResolutionAsync("never-prepared", Within(5000)));
    }

    [Fact]
    public async Task should_say_not_supported_for_the_external_vms_placeholder()
    {
        var vms = new ExternalVmsProvider();

        await Assert.ThrowsAsync<NotSupportedException>(() => vms.GetVideoResolutionAsync("1", CancellationToken.None));
    }
}
