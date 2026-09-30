using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests.Support;

/// <summary>
/// 시험용 제어 공장 — 실제 어댑터(<see cref="TileCameraControlFactory"/>)를 쓰되 만든 제어 객체를 카메라 id 로 기억한다.
/// <see cref="FailingIds"/> 의 카메라는 모든 PTZ 호출이 false 인 가짜 PTZ 를 받는다.
/// </summary>
internal sealed class RecordingControlFactory : ITileCameraControlFactory
{
    private readonly TileCameraControlFactory _inner = new();

    public Dictionary<string, ITileCameraControl> Created { get; } = new();
    public HashSet<string> FailingIds { get; } = new();

    public ITileCameraControl Create(EventWindowCamera camera)
    {
        ITileCameraControl control = FailingIds.Contains(camera.CameraId) ? new FailingControl() : _inner.Create(camera);
        Created[camera.CameraId] = control;
        return control;
    }

    public SimulatedCameraControl Simulated(string cameraId) => (SimulatedCameraControl)Created[cameraId];

    private sealed class FailingControl : ITileCameraControl
    {
        public string? PtzUnavailableReason => null;
        public Task<bool> ContinuousMoveAsync(double pan, double tilt, double zoom, CancellationToken ct) => Task.FromResult(false);
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<bool> DragMoveAsync(double viewX, double viewY, double viewAspect, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<TilePreset>> GetPresetsAsync(CancellationToken ct) => throw new InvalidOperationException("boom");
        public Task<bool> GotoPresetAsync(string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> GotoHomeAsync(string? homePresetToken, CancellationToken ct) => Task.FromResult(false);
        public void Dispose() { }
    }
}
