using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers;

/****************************************************************************
   Purpose      : 외부 VMS API 제공자 자리(PRD camera-popup-modes FR-18 — 연동처가 생기면 구현)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 설정 칸만 자리를 잡은 제공자. 모든 조작이 <see cref="NotSupportedException"/> 을 던지고, 호스트는 그 스트림 · 요청을
/// "지원 안 함"(<see cref="CameraErrorCodes.NotSupported"/>)으로 알린다 — 다른 창 · GIS 에는 번지지 않는다.
/// </summary>
public sealed class ExternalVmsProvider : ICameraVideoProvider, ICameraPtzProvider
{
    private const string Message = "외부 VMS 제공자는 아직 지원하지 않는다(FR-18 — 자리만)";

    public VideoProviderKind Kind => VideoProviderKind.ExternalVms;

    public Task<StreamResolution> ResolveStreamAsync(string cameraId, VideoProviderInfo info, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<PtzReadiness> PrepareAsync(string cameraId, VideoProviderInfo info, CancellationToken ct) => throw new NotSupportedException(Message);
    public bool IsPrepared(string cameraId) => false;
    public Task<bool> ContinuousMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task StopAsync(string cameraId, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<PtzDragOutcome> DragMoveAsync(string cameraId, double viewX, double viewY, double viewAspect, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> SetHomeAsync(string cameraId, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> GotoHomeAsync(string cameraId, CancellationToken ct) => throw new NotSupportedException(Message);
    public bool IsImagingCapable(string cameraId) => false;
    public Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct) => throw new NotSupportedException(Message);
    public Task StopFocusAsync(string cameraId, CancellationToken ct) => throw new NotSupportedException(Message);
    public void Release(string cameraId) { }
}
