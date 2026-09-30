namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 시험 무늬 카메라용 가짜 PTZ(스레드 안전). 호출을 기록만 하고 성공을 돌려준다 — 헤드리스 시험 · 진단 · 렌더 확인용.
/// 프리셋 P1~P4 와 Home 이 있다고 본다.
/// </summary>
internal sealed class SimulatedCameraControl : ITileCameraControl
{
    private static readonly IReadOnlyList<TilePreset> Presets = new[]
    {
        new TilePreset("P1", "P1 정문"),
        new TilePreset("P2", "P2 외곽"),
        new TilePreset("P3", "P3 후문"),
        new TilePreset("P4", "P4 초소"),
    };

    private readonly object _gate = new();
    private readonly List<string> _calls = new();

    public string? PtzUnavailableReason => null;

    /// <summary>받은 호출 기록(예 "goto:P2", "move:0.5,0,0", "stop", "home:-").</summary>
    public IReadOnlyList<string> Calls
    {
        get { lock (_gate) { return _calls.ToArray(); } }
    }

    private void Record(string call)
    {
        lock (_gate) { _calls.Add(call); }
    }

    public Task<bool> ContinuousMoveAsync(double pan, double tilt, double zoom, CancellationToken ct)
    {
        Record(FormattableString.Invariant($"move:{pan},{tilt},{zoom}"));
        return Task.FromResult(true);
    }

    public Task StopAsync(CancellationToken ct)
    {
        Record("stop");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TilePreset>> GetPresetsAsync(CancellationToken ct) => Task.FromResult(Presets);

    public Task<bool> GotoPresetAsync(string presetToken, CancellationToken ct)
    {
        Record("goto:" + presetToken);
        return Task.FromResult(true);
    }

    public Task<bool> GotoHomeAsync(string? homePresetToken, CancellationToken ct)
    {
        Record("home:" + (homePresetToken ?? "-"));
        return Task.FromResult(true);
    }

    public void Dispose()
    {
    }
}
