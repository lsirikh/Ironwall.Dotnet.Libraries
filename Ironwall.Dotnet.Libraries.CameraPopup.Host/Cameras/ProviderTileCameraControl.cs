using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;

/// <summary>
/// 이벤트 창 타일 PTZ 의 실제 어댑터(T-02 — T-05 의 <see cref="ITileCameraControl"/> 모양 유지). 타일은 ONVIF 를 모르고
/// 호스트 제공자 창구(<see cref="HostCameraServices"/>)를 탄다 — 더블클릭 팝업과 같은 연결 캐시 · 정지 우선 게이트.
/// 모든 호출은 넘겨받은 토큰으로 시간 제한되고(FR-26) 실패는 false · 빈 목록이다(던지지 않는다).
/// </summary>
internal sealed class ProviderTileCameraControl : ITileCameraControl
{
    public const string ExternalVmsNoPtzReason = "외부 VMS 제공자는 아직 PTZ 없음";
    public const string NoHostReason = "카메라 주소가 없어 PTZ 불가";

    private static readonly IReadOnlyList<TilePreset> NoPresets = Array.Empty<TilePreset>();

    private readonly HostCameraServices _cameras;
    private readonly string _cameraId;
    private readonly VideoProviderInfo _provider;

    public ProviderTileCameraControl(HostCameraServices cameras, string cameraId, VideoProviderInfo provider)
    {
        _cameras = cameras ?? throw new ArgumentNullException(nameof(cameras));
        _cameraId = cameraId;
        _provider = provider ?? new VideoProviderInfo();
        PtzUnavailableReason = ReasonFor(_provider);
    }

    /// <summary>제공자 정보 → PTZ 못 하는 이유(순수). 할 수 있으면 null. ONVIF · RTSP 주소 제공자는 ONVIF 로 PTZ(현행 팝업과 같다).</summary>
    public static string? ReasonFor(VideoProviderInfo provider) => provider.Kind switch
    {
        VideoProviderKind.ExternalVms => ExternalVmsNoPtzReason,
        VideoProviderKind.Onvif or VideoProviderKind.Rtsp when string.IsNullOrWhiteSpace(provider.Host) => NoHostReason,
        VideoProviderKind.Onvif or VideoProviderKind.Rtsp => null,
        _ => TileCameraControlFactory.FileNoPtzReason,
    };

    public string? PtzUnavailableReason { get; }

    public async Task<bool> ContinuousMoveAsync(double pan, double tilt, double zoom, CancellationToken ct)
    {
        if (PtzUnavailableReason is not null) return false;
        try { return await _cameras.MoveAsync(_cameraId, _provider, pan, tilt, zoom, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (PtzUnavailableReason is not null) return;
        try { await _cameras.StopAsync(_cameraId, _provider, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* 정지 실패는 카메라 2초 안전 제한이 멈춘다(FR-22) */ }
    }

    public async Task<IReadOnlyList<TilePreset>> GetPresetsAsync(CancellationToken ct)
    {
        if (PtzUnavailableReason is not null) return NoPresets;
        var r = await RequestAsync(CameraRequestKind.GetPresets, null, ct).ConfigureAwait(false);
        if (r is not { Success: true, Presets: { } presets }) return NoPresets;
        return presets.Where(p => !string.IsNullOrEmpty(p.Token)).Select(p => new TilePreset(p.Token, p.Name)).ToList();
    }

    public async Task<bool> GotoPresetAsync(string presetToken, CancellationToken ct)
    {
        if (PtzUnavailableReason is not null || string.IsNullOrWhiteSpace(presetToken)) return false;
        return (await RequestAsync(CameraRequestKind.GotoPreset, presetToken, ct).ConfigureAwait(false))?.Success == true;
    }

    public async Task<bool> GotoHomeAsync(string? homePresetToken, CancellationToken ct)
    {
        if (PtzUnavailableReason is not null) return false;
        var r = string.IsNullOrWhiteSpace(homePresetToken)
            ? await RequestAsync(CameraRequestKind.GotoHome, null, ct).ConfigureAwait(false)
            : await RequestAsync(CameraRequestKind.GotoPreset, homePresetToken, ct).ConfigureAwait(false);
        return r?.Success == true;
    }

    private async Task<CameraResponse?> RequestAsync(CameraRequestKind kind, string? presetToken, CancellationToken ct)
    {
        try
        {
            var request = new CameraRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Kind = kind,
                CameraId = _cameraId,
                Provider = _provider,
                PresetToken = presetToken,
            };
            return await _cameras.ExecuteAsync(request).WaitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        // 연결 캐시는 호스트 수명 동안 공유한다(다른 창 · 더블클릭 팝업이 같은 카메라를 쓴다) — 여기서 버리지 않는다.
    }
}
