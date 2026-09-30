using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// 영상 · PTZ 제공자(FR-17/18, T-02) — 등록부 규칙 · RTSP 주소 계정 결합 · 외부 VMS 자리 · 가짜 SOAP 카메라 상대 ONVIF 경로
/// (Expect 헤더 없음 · GetStreamUri 해석 · 서브 스트림 선택 · 폴백 · PTZ 준비/이동/정지).
/// </summary>
public class ProviderTests
{
    private const string User = "fake-user";
    private const string Pass = "p@ss:w#rd";

    [Fact]
    public void should_pick_video_and_ptz_providers_by_kind_when_registry_default()
    {
        // Arrange
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());

        // Assert — 영상: ONVIF/RTSP/외부 VMS, 시험 무늬는 등록부 밖. PTZ: ONVIF · RTSP 주소 둘 다 ONVIF(현행 팝업과 같다)
        Assert.IsType<OnvifVideoProvider>(registry.GetVideo(VideoProviderKind.Onvif));
        Assert.IsType<RtspUrlVideoProvider>(registry.GetVideo(VideoProviderKind.Rtsp));
        Assert.IsType<ExternalVmsProvider>(registry.GetVideo(VideoProviderKind.ExternalVms));
        Assert.Null(registry.GetVideo(VideoProviderKind.TestPattern));
        Assert.IsType<OnvifPtzProvider>(registry.GetPtz(VideoProviderKind.Onvif));
        Assert.Same(registry.GetPtz(VideoProviderKind.Onvif), registry.GetPtz(VideoProviderKind.Rtsp));
        Assert.IsType<ExternalVmsProvider>(registry.GetPtz(VideoProviderKind.ExternalVms));
        Assert.Null(registry.GetPtz(VideoProviderKind.File));
    }

    [Fact]
    public async Task should_throw_not_supported_when_external_vms_is_asked()
    {
        var vms = new ExternalVmsProvider();
        await Assert.ThrowsAsync<NotSupportedException>(() => vms.ResolveStreamAsync("c1", new VideoProviderInfo(), CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => vms.PrepareAsync("c1", new VideoProviderInfo(), CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => vms.ContinuousMoveAsync("c1", 0.5, 0, 0, CancellationToken.None));
    }

    [Theory]
    [InlineData("rtsp://10.0.0.5:554/sub", "admin", "pw", "rtsp://admin:pw@10.0.0.5:554/sub")]
    [InlineData("rtsp://op:keep@10.0.0.5/sub", "admin", "pw", "rtsp://op:keep@10.0.0.5/sub")]        // 운영자가 넣은 계정 존중
    [InlineData("rtsp://10.0.0.5/sub", "ad@min", "p@ss:w#rd", "rtsp://ad%40min:p%40ss%3Aw%23rd@10.0.0.5/sub")]
    [InlineData("rtsp://10.0.0.5/a?user=x@y", "admin", "", "rtsp://admin@10.0.0.5/a?user=x@y")]   // 쿼리의 '@' 는 userinfo 아님
    [InlineData("rtsp://10.0.0.5/sub", "", "pw", "rtsp://10.0.0.5/sub")]                             // 익명 카메라
    public async Task should_inject_credentials_like_rtsp_url_credentials_when_rtsp_provider_resolves(string url, string user, string pass, string expected)
    {
        var result = await new RtspUrlVideoProvider().ResolveStreamAsync("c1",
            new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = url, Username = user, Password = pass }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Uri);
        if (!string.IsNullOrEmpty(pass)) Assert.DoesNotContain(pass, result.ToString());   // 결과 로그에 계정 없음
    }

    [Fact]
    public async Task should_fail_with_bad_request_when_rtsp_provider_has_no_uri()
    {
        var result = await new RtspUrlVideoProvider().ResolveStreamAsync("c1", new VideoProviderInfo { Kind = VideoProviderKind.Rtsp }, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CameraErrorCodes.BadRequest, result.ErrorCode);
    }

    [Fact]
    public async Task should_resolve_sub_stream_with_credentials_and_no_expect_header_when_onvif_camera_answers()
    {
        // Arrange
        await using var camera = new FakeOnvifCamera();
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var info = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = User, Password = Pass };

        // Act
        var result = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("cam-1", info, new CancellationTokenSource(10_000).Token);

        // Assert — GetStreamUri 주소(계정 없음)에 계정을 퍼센트 인코딩해 싣는다 · 낮은 해상도(P1) 프로필 · Expect 헤더 없음(T-01)
        Assert.True(result.Success, result.ToString());
        Assert.Equal($"rtsp://fake-user:p%40ss%3Aw%23rd@127.0.0.1:{camera.Port}/sub", result.Uri);
        var streamUri = Assert.Single(camera.Requests, r => r.Body.Contains("GetStreamUri"));
        Assert.Contains(">P1<", streamUri.Body);
        Assert.All(camera.Requests, r => Assert.False(r.HasExpectContinue, $"Expect: 100-continue 가 붙었다: {r.Path}"));
        Assert.DoesNotContain(Pass, result.ToString());
    }

    [Fact]
    public async Task should_fall_back_to_stored_url_when_get_stream_uri_fails()
    {
        // Arrange — 카메라가 GetStreamUri 에 엉뚱한 응답
        await using var camera = new FakeOnvifCamera { FailStreamUri = true };
        var provider = CameraProviderRegistry.CreateDefault(new TestLog()).GetVideo(VideoProviderKind.Onvif)!;
        var info = new VideoProviderInfo
        {
            Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "u", Password = "p",
            FallbackUri = "rtsp://10.9.9.9/stored",
        };

        // Act
        var result = await provider.ResolveStreamAsync("cam-2", info, new CancellationTokenSource(10_000).Token);

        // Assert — 현행 "URL 조회 폴백"(FR-06) 유지, 폴백 주소에도 계정을 싣는다
        Assert.True(result.Success, result.ToString());
        Assert.Equal("rtsp://u:p@10.9.9.9/stored", result.Uri);
        Assert.StartsWith("fallback", result.Detail);
    }

    [Fact]
    public async Task should_fail_within_timeout_when_onvif_camera_does_not_answer()
    {
        // Arrange — 응답 5초 지연(무응답 카메라), 호출 쪽 제한 1초(FR-26)
        await using var camera = new FakeOnvifCamera { Delay = TimeSpan.FromSeconds(5) };
        var provider = CameraProviderRegistry.CreateDefault(new TestLog()).GetVideo(VideoProviderKind.Onvif)!;
        var info = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "u", Password = "p" };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await provider.ResolveStreamAsync("cam-3", info, new CancellationTokenSource(1_000).Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(CameraErrorCodes.Timeout, result.ErrorCode);
        Assert.True(sw.ElapsedMilliseconds < 3_000, $"{sw.ElapsedMilliseconds} ms — 제한 시간을 넘겨 붙잡았다");
    }

    [Fact]
    public async Task should_prepare_move_and_stop_when_onvif_ptz_camera_answers()
    {
        // Arrange
        await using var camera = new FakeOnvifCamera();
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        var info = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = User, Password = Pass };
        var ct = new CancellationTokenSource(10_000).Token;

        // Act
        var ready = await ptz.PrepareAsync("ptz-1", info, ct);
        var moved = await ptz.ContinuousMoveAsync("ptz-1", 0.3, -0.2, 0, ct);
        await ptz.StopAsync("ptz-1", ct);
        var presets = await ptz.GetPresetsAsync("ptz-1", ct);

        // Assert
        Assert.True(ready.Connected);
        Assert.True(ready.PtzCapable);
        Assert.True(ptz.IsPrepared("ptz-1"));
        Assert.True(moved);
        Assert.Equal(1, camera.Count("ContinuousMove"));
        Assert.True(camera.Requests.Any(r => r.Body.Contains("Stop") && !r.Body.Contains("ContinuousMove")), "정지가 나가지 않았다");
        Assert.NotNull(presets);
        Assert.Equal(new[] { "1", "2" }, presets!.Select(p => p.Token));
        Assert.All(camera.Requests, r => Assert.False(r.HasExpectContinue));
    }

    [Fact]
    public async Task should_not_be_prepared_when_host_missing()
    {
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Rtsp)!;
        var ready = await ptz.PrepareAsync("no-host", new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "rtsp://x/y" }, CancellationToken.None);
        Assert.False(ready.Connected);
        Assert.False(ready.PtzCapable);
    }
}
