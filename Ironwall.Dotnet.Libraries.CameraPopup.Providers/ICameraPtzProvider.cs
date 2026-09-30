using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers;

/****************************************************************************
   Purpose      : PTZ · 영상 옵션 제공자 추상화(PRD camera-popup-modes FR-17 · FR-22)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 연속 이동 · 정지 · 줌 · 프리셋(목록/이동/저장/삭제) · Home · 영상 옵션(주야간 · 포커스).
/// 호출 전에 <see cref="PrepareAsync"/> 로 카메라를 준비한다(한 번 준비하면 인스턴스를 붙잡아 재사용 — 워밍).
/// 준비하지 않은 카메라의 조작은 false/null 로 끝난다.
/// <para>계약: 절대 던지지 않는다(실패 = false/null). 지원하지 않는 제공자는 <see cref="NotSupportedException"/>.
/// 같은 카메라의 명령은 정지 우선 게이트로 한 줄로 나간다(FR-22) — 호출 순서가 곧 카메라 도착 순서다.</para>
/// </summary>
public interface ICameraPtzProvider
{
    /// <summary>준비(ONVIF 연결 · 좌표 공간). 이미 준비됐으면 캐시를 쓴다.</summary>
    Task<PtzReadiness> PrepareAsync(string cameraId, VideoProviderInfo info, CancellationToken ct);

    /// <summary>준비된 카메라인가(캐시 존재).</summary>
    bool IsPrepared(string cameraId);

    Task<bool> ContinuousMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct);

    Task StopAsync(string cameraId, CancellationToken ct);

    Task<IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct);

    Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct);

    Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct);

    Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct);

    Task<bool> SetHomeAsync(string cameraId, CancellationToken ct);

    Task<bool> GotoHomeAsync(string cameraId, CancellationToken ct);

    /// <summary>영상 옵션 가능(준비 뒤에만 의미).</summary>
    bool IsImagingCapable(string cameraId);

    Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct);

    Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct);

    Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct);

    /// <summary>수동 포커스 연속 이동(+1 원경 · -1 근경). 정지는 <see cref="StopFocusAsync"/>.</summary>
    Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct);

    Task StopFocusAsync(string cameraId, CancellationToken ct);

    /// <summary>캐시 버리기(멱등).</summary>
    void Release(string cameraId);
}

/// <summary>준비 결과.</summary>
public sealed record PtzReadiness(bool Connected, bool PtzCapable, bool ImagingCapable);
