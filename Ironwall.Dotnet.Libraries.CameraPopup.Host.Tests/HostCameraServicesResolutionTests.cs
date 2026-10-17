using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// 호스트 카메라 창구의 <see cref="CameraRequestKind.GetVideoResolution"/> — ROI 설정 창이 새 ROI 의 기준 해상도를 채울 때 쓴다.
/// 가짜 제공자(실제 ONVIF 없음).
/// </summary>
public class HostCameraServicesResolutionTests
{
    private class FakeProvider : ICameraPtzProvider
    {
        public bool Connected { get; set; } = true;
        public int Prepares { get; private set; }
        private bool _prepared;

        public Task<PtzReadiness> PrepareAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
        {
            Prepares++;
            _prepared = Connected;
            return Task.FromResult(new PtzReadiness(Connected, false, false));
        }

        public bool IsPrepared(string cameraId) => _prepared;
        public Task<bool> ContinuousMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct) => Task.FromResult(false);
        public Task StopAsync(string cameraId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PtzPresetInfo>?>(null);
        public Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> GotoHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public bool IsImagingCapable(string cameraId) => false;
        public Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct) => Task.FromResult<CameraImagingState?>(null);
        public Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct) => Task.FromResult(false);
        public Task StopFocusAsync(string cameraId, CancellationToken ct) => Task.CompletedTask;
        public void Release(string cameraId) { }
    }

    /// <summary>해상도를 아는 제공자.</summary>
    private sealed class SizedProvider : FakeProvider, ICameraPtzProvider
    {
        public CameraVideoResolution? Resolution { get; set; } = new(1280, 1024);

        public Task<CameraVideoResolution?> GetVideoResolutionAsync(string cameraId, CancellationToken ct) => Task.FromResult(Resolution);
    }

    private static readonly VideoProviderInfo Info = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 };

    private static HostCameraServices Create(ICameraPtzProvider provider)
    {
        var registry = new CameraProviderRegistry(provider, Array.Empty<ICameraVideoProvider>());
        var log = new HostLog(Path.Combine(Path.GetTempPath(), "ironwall-camhost-tests", "res-" + Guid.NewGuid().ToString("N")[..8]));
        return new HostCameraServices(registry, log, _ => { });
    }

    private static CameraRequest Request() => new() { RequestId = "r1", Kind = CameraRequestKind.GetVideoResolution, CameraId = "c1", Provider = Info };

    [Fact]
    public async Task should_answer_the_main_stream_resolution_after_preparing_the_camera()
    {
        var provider = new SizedProvider();
        var services = Create(provider);

        var response = await services.ExecuteAsync(Request());

        Assert.True(response.Success, response.ToString());
        Assert.Equal(CameraRequestKind.GetVideoResolution, response.Kind);
        Assert.Equal("r1", response.RequestId);
        Assert.Equal((1280, 1024), (response.VideoWidth, response.VideoHeight));
        Assert.Equal(1, provider.Prepares);                 // 준비 안 된 카메라는 먼저 준비한다
    }

    [Fact]
    public async Task should_fail_when_the_camera_does_not_tell_its_resolution()
    {
        var unknown = Create(new SizedProvider { Resolution = null });
        var zero = Create(new SizedProvider { Resolution = new CameraVideoResolution(0, 1080) });
        var basic = Create(new FakeProvider());              // 기본 구현(지원 안 하는 제공자 · 옛 가짜)

        foreach (var services in new[] { unknown, zero, basic })
        {
            var response = await services.ExecuteAsync(Request());
            Assert.False(response.Success);
            Assert.Equal(CameraErrorCodes.Failed, response.ErrorCode);
            Assert.Equal((0, 0), (response.VideoWidth, response.VideoHeight));
        }
    }

    [Fact]
    public async Task should_fail_when_the_camera_cannot_be_reached()
    {
        var services = Create(new SizedProvider { Connected = false });

        var response = await services.ExecuteAsync(Request());

        Assert.False(response.Success);
        Assert.Equal(CameraErrorCodes.Failed, response.ErrorCode);
    }

    [Fact]
    public void should_carry_the_resolution_through_the_pipe_envelope()
    {
        var bytes = IpcSerializer.Serialize(new CameraResponse
        {
            RequestId = "r1", Kind = CameraRequestKind.GetVideoResolution, CameraId = "c1", Success = true, VideoWidth = 1280, VideoHeight = 1024,
        }, 7);

        Assert.Equal(DecodeStatus.Ok, IpcSerializer.TryDeserialize(bytes, out var message, out _, out _));
        var back = Assert.IsType<CameraResponse>(message);
        Assert.Equal(CameraRequestKind.GetVideoResolution, back.Kind);
        Assert.Equal((1280, 1024), (back.VideoWidth, back.VideoHeight));
    }
}
