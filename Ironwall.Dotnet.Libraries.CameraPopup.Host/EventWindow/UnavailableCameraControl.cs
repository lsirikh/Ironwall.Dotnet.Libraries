namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>PTZ 를 못 하는 제공자 — 모든 PTZ 호출이 즉시 false. 이유는 우클릭 항목 비활성 문구가 된다.</summary>
internal sealed class UnavailableCameraControl : ITileCameraControl
{
    private static readonly IReadOnlyList<TilePreset> NoPresets = Array.Empty<TilePreset>();

    public UnavailableCameraControl(string reason) => PtzUnavailableReason = reason;

    public string? PtzUnavailableReason { get; }

    public Task<bool> ContinuousMoveAsync(double pan, double tilt, double zoom, CancellationToken ct) => Task.FromResult(false);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<bool> DragMoveAsync(double viewX, double viewY, double viewAspect, CancellationToken ct) => Task.FromResult(false);
    public Task<IReadOnlyList<TilePreset>> GetPresetsAsync(CancellationToken ct) => Task.FromResult(NoPresets);
    public Task<bool> GotoPresetAsync(string presetToken, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> GotoHomeAsync(string? homePresetToken, CancellationToken ct) => Task.FromResult(false);

    public void Dispose()
    {
    }
}
