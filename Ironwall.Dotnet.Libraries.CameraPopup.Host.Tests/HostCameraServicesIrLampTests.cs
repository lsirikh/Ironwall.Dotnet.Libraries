using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// 호스트 카메라 창구의 IR 조명(<see cref="CameraRequestKind.SetIrLamp"/>) — 준비 · 영상 옵션 응답에 지원 값 · 마지막 값을 싣고,
/// 카메라가 알린 값만 제공자에 넘긴다. 실패 · 미지원은 응답으로만(호스트는 산다). 가짜 제공자(실제 ONVIF 없음).
/// </summary>
public class HostCameraServicesIrLampTests
{
    private sealed class LampProvider : ICameraPtzProvider
    {
        public IReadOnlyList<string> Modes { get; set; } = new[] { IrLampMode.On, IrLampMode.Off, IrLampMode.Auto };
        public bool SendResult { get; set; } = true;
        public bool Throws { get; set; }
        public List<string> Sent { get; } = new();
        private string? _last;
        private bool _prepared;

        public Task<PtzReadiness> PrepareAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
        {
            _prepared = true;
            return Task.FromResult(new PtzReadiness(true, true, true));
        }

        public bool IsPrepared(string cameraId) => _prepared;
        public IReadOnlyList<string> GetIrLampModes(string cameraId) => Modes;
        public string? GetLastIrLamp(string cameraId) => _last;

        public Task<bool> SetIrLampAsync(string cameraId, string mode, CancellationToken ct)
        {
            if (Throws) throw new InvalidOperationException("camera exploded");
            Sent.Add(mode);
            if (SendResult) _last = mode;
            return Task.FromResult(SendResult);
        }

        public Task<bool> ContinuousMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct) => Task.FromResult(false);
        public Task StopAsync(string cameraId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PtzPresetInfo>?>(null);
        public Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> GotoHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public bool IsImagingCapable(string cameraId) => true;
        public Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct) => Task.FromResult<CameraImagingState?>(new("AUTO", true));
        public Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct) => Task.FromResult(false);
        public Task StopFocusAsync(string cameraId, CancellationToken ct) => Task.CompletedTask;
        public void Release(string cameraId) { }
    }

    private static readonly VideoProviderInfo Info = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 };

    private static HostCameraServices Create(ICameraPtzProvider provider)
    {
        var registry = new CameraProviderRegistry(provider, Array.Empty<ICameraVideoProvider>());
        var log = new HostLog(Path.Combine(Path.GetTempPath(), "ironwall-camhost-tests", "lamp-" + Guid.NewGuid().ToString("N")[..8]));
        return new HostCameraServices(registry, log, _ => { });
    }

    private static CameraRequest Request(CameraRequestKind kind, string? lamp = null)
        => new() { RequestId = "r1", Kind = kind, CameraId = "c1", Provider = Info, IrLamp = lamp };

    [Fact]
    public async Task should_announce_supported_ir_lamp_modes_when_prepared_and_when_imaging_read()
    {
        var services = Create(new LampProvider { Modes = new[] { IrLampMode.On, IrLampMode.Off } });

        var prepared = await services.ExecuteAsync(Request(CameraRequestKind.PreparePtz));
        var imaging = await services.ExecuteAsync(Request(CameraRequestKind.GetImaging));

        Assert.Equal(new[] { "On", "Off" }, prepared.IrLampModes);
        Assert.Null(prepared.IrLamp);
        Assert.Equal(new[] { "On", "Off" }, imaging.IrLampModes);
    }

    [Fact]
    public async Task should_send_ir_lamp_and_report_the_last_value_when_camera_supports_it()
    {
        var provider = new LampProvider();
        var services = Create(provider);

        var on = await services.ExecuteAsync(Request(CameraRequestKind.SetIrLamp, IrLampMode.On));
        var imaging = await services.ExecuteAsync(Request(CameraRequestKind.GetImaging));

        Assert.True(on.Success, on.ToString());
        Assert.Equal(IrLampMode.On, on.IrLamp);
        Assert.Equal(new[] { IrLampMode.On }, provider.Sent);
        Assert.Equal(IrLampMode.On, imaging.IrLamp);   // 다른 팝업이 다시 열어도 마지막 값을 본다
    }

    [Theory]
    [InlineData("Auto")]    // 카메라가 알리지 않은 값
    [InlineData("Blink")]   // 모르는 값
    public async Task should_refuse_without_touching_the_camera_when_mode_is_not_announced(string mode)
    {
        var provider = new LampProvider { Modes = new[] { IrLampMode.On, IrLampMode.Off } };
        var services = Create(provider);

        var r = await services.ExecuteAsync(Request(CameraRequestKind.SetIrLamp, mode));

        Assert.False(r.Success);
        Assert.Equal(CameraErrorCodes.NotSupported, r.ErrorCode);
        Assert.Empty(provider.Sent);
    }

    [Fact]
    public async Task should_answer_not_supported_when_camera_has_no_ir_lamp_control()
    {
        var provider = new LampProvider { Modes = Array.Empty<string>() };
        var services = Create(provider);

        var prepared = await services.ExecuteAsync(Request(CameraRequestKind.PreparePtz));
        var r = await services.ExecuteAsync(Request(CameraRequestKind.SetIrLamp, IrLampMode.On));
        var empty = await services.ExecuteAsync(Request(CameraRequestKind.SetIrLamp));

        Assert.Null(prepared.IrLampModes);
        Assert.Equal(CameraErrorCodes.NotSupported, r.ErrorCode);
        Assert.Equal(CameraErrorCodes.BadRequest, empty.ErrorCode);
        Assert.Empty(provider.Sent);
    }

    [Fact]
    public async Task should_answer_failed_and_keep_running_when_camera_refuses_or_throws()
    {
        var refusing = Create(new LampProvider { SendResult = false });
        var throwing = Create(new LampProvider { Throws = true });

        var refused = await refusing.ExecuteAsync(Request(CameraRequestKind.SetIrLamp, IrLampMode.Off));
        var thrown = await throwing.ExecuteAsync(Request(CameraRequestKind.SetIrLamp, IrLampMode.Off));

        Assert.False(refused.Success);
        Assert.Equal(CameraErrorCodes.Failed, refused.ErrorCode);
        Assert.Null(refused.IrLamp);
        Assert.False(thrown.Success);
        Assert.Equal(CameraErrorCodes.Failed, thrown.ErrorCode);
    }

    [Fact]
    public void should_carry_ir_lamp_fields_through_the_pipe_envelope()
    {
        var request = IpcSerializer.Serialize(new CameraRequest { RequestId = "q", Kind = CameraRequestKind.SetIrLamp, CameraId = "c1", IrLamp = IrLampMode.Auto }, 1);
        var response = IpcSerializer.Serialize(new CameraResponse
        {
            RequestId = "q", Kind = CameraRequestKind.SetIrLamp, CameraId = "c1", Success = true,
            IrLampModes = new List<string> { IrLampMode.On, IrLampMode.Off, IrLampMode.Auto }, IrLamp = IrLampMode.Auto,
        }, 2);

        Assert.Equal(DecodeStatus.Ok, IpcSerializer.TryDeserialize(request, out var m1, out _, out _));
        Assert.Equal(DecodeStatus.Ok, IpcSerializer.TryDeserialize(response, out var m2, out _, out _));
        var q = Assert.IsType<CameraRequest>(m1);
        var r = Assert.IsType<CameraResponse>(m2);
        Assert.Equal((CameraRequestKind.SetIrLamp, "Auto"), (q.Kind, q.IrLamp));
        Assert.Equal(new[] { "On", "Off", "Auto" }, r.IrLampModes);
        Assert.Equal("Auto", r.IrLamp);
    }
}
