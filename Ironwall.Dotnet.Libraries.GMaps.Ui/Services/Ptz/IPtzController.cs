using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Ptz;

/****************************************************************************
   Purpose      : 팝업 PTZ 제어 단일 소유 앱서비스 (CameraPopup_PTZ_Control)
   Created By   : Claude Code
   Created On   : 2026-06-24
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>카메라 현재 PTZ 위치(프리셋 저장/복원 단위). space URI를 동봉해 저장↔이동 round-trip 보장.</summary>
public sealed record PtzPosition(double Pan, double Tilt, double Zoom, string? PanTiltSpace, string? ZoomSpace);

/// <summary>카메라 영상 옵션 상태(옵션 탭). <c>IrCutFilter</c>="ON"(주간)/"OFF"(야간)/"AUTO". <c>AutoFocus</c>=오토포커스 여부.</summary>
public sealed record CameraImagingState(string IrCutFilter, bool AutoFocus);

/// <summary>
/// 팝업 PTZ 제어의 단일 소유 앱서비스. (PRD FR-PTZCTL-01~03 / FR-WRAP-01)
///
/// 책임:
/// - cameraId → (PtzClient, profileToken) 해석(IOnvifService.InitializeFull 캐시).
/// - cameraId별 GetNode space(Rel/Abs PanTilt·Zoom XRange/YRange/URI) 캐시 — 모든 변환/클램프 진실원.
/// - ONVIF 호출 직렬화: cameraId별 SemaphoreSlim(1,1)로 직렬(FIFO). 다른 cameraId 병렬, 동일 직렬.
///   (드래그는 릴리즈당 RelativeMove 1회라 플러드 없음 → FIFO로 충분. Last-Write-Wins/CTS 취소는 후속·미구현.)
/// - IOnvifService 미노출 메서드(Relative/Absolute/GetStatus/GetNode)는 PtzClient 직접 호출 래퍼.
///
/// 스레드 안전(I-05): cameraId 맵=ConcurrentDictionary, PtzClient 동일 인스턴스 병렬 호출 금지.
/// </summary>
public interface IPtzController
{
    /// <summary>
    /// 카메라 ONVIF 연결 준비(InitializeFull 캐시) + GetNode space 캐시 적재.
    /// PTZ 실제 가능 여부(PtzClient≠null AND GetNode 성공 AND space≥1)를 반환(FR-GATE-01).
    /// </summary>
    Task<bool> EnsureReadyAsync(int cameraId, IConnectionModel conn, CancellationToken ct = default);

    /// <summary>PTZ 입력 게이팅 진실원 — EnsureReady 후의 강화된 IsPtzPossible.</summary>
    bool IsPtzCapable(int cameraId);

    /// <summary>해당 카메라가 이동 진행 중인지(AbsoluteMove 등 — 추가 입력 차단 판단용).</summary>
    bool IsBusy(int cameraId);

    /// <summary>
    /// 픽셀 드래그 델타 기반 상대 이동(RelativeMove). GetNode space로 변환·클램프(고정±1.0 금지),
    /// Y축 반전 적용. 연속 호출은 Gate로 직렬(FIFO). (FR-DRAG-03/04; LWW는 후속)
    /// </summary>
    Task<bool> RelativeMoveByPixelAsync(int cameraId, double dx, double dy,
        double imageW, double imageH, double sensitivity = 1.0, CancellationToken ct = default);

    /// <summary>
    /// 절대 이동(프리셋 좌표 pan/tilt/zoom). 카메라 절대 space로 클램프. AbsoluteMove는 드래그에 취소되지 않음. (FR-PRESET-02)
    /// </summary>
    Task<bool> AbsoluteMoveAsync(int cameraId, double pan, double tilt, double zoom, CancellationToken ct = default);

    /// <summary>현재 PTZ 위치 읽기(프리셋 저장용 GetStatus). 실패 시 null. (FR-PRESET-03)</summary>
    Task<PtzPosition?> GetStatusAsync(int cameraId, CancellationToken ct = default);

    /// <summary>이동 정지(StopPTZ panTilt+zoom).</summary>
    Task StopAsync(int cameraId, CancellationToken ct = default);

    /// <summary>영상 옵션(주야간/포커스) 사용 가능 여부 — IsImagingPossible + ImagingClient + VideoSourceToken. (FR-OPT-03)</summary>
    bool IsImagingCapable(int cameraId);

    /// <summary>현재 영상 옵션(주야간 IrCutFilter / 오토포커스) 조회 — 옵션 탭 표시. 미지원/실패 시 null. (FR-OPT-01/02)</summary>
    Task<CameraImagingState?> GetImagingAsync(int cameraId, CancellationToken ct = default);

    /// <summary>주야간(IrCutFilter: "ON"=주간/"OFF"=야간/"AUTO") 설정. read-modify-write로 타 필드 보존. (FR-OPT-01)</summary>
    Task<bool> SetIrCutFilterAsync(int cameraId, string mode, CancellationToken ct = default);

    /// <summary>오토포커스(AUTO/MANUAL) 설정. read-modify-write로 타 필드 보존. (FR-OPT-02)</summary>
    Task<bool> SetAutoFocusAsync(int cameraId, bool auto, CancellationToken ct = default);

    /// <summary>카메라 PTZ 리소스(딕셔너리 항목·space 캐시) 정리. 멱등(진행 중 태스크 안전, Semaphore 미Dispose). (FR-DISPOSE-01)</summary>
    void Release(int cameraId);
}
