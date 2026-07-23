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

/// <summary>카메라(ONVIF)에 저장된 프리셋 1건 — 팝업 프리셋 탭 표시/이동/삭제 단위. Name은 빈 값 가능(표시측 폴백). (FR-C1)</summary>
public sealed record PtzPresetInfo(string Token, string Name);

/// <summary>
/// 팝업 PTZ 제어의 단일 소유 앱서비스. (PRD FR-PTZCTL-01~03 / FR-WRAP-01)
///
/// 책임:
/// - cameraId → (PtzClient, profileToken) 해석(IOnvifService.InitializeFull 캐시).
/// - cameraId별 GetNode space(Rel/Abs PanTilt·Zoom XRange/YRange/URI) 캐시 — 모든 변환/클램프 진실원.
/// - ONVIF 호출 직렬화: cameraId별 SemaphoreSlim(1,1)로 직렬(FIFO). 다른 cameraId 병렬, 동일 직렬.
///   제스처(드래그/줌) Last-Write-Wins: MapViewModel이 cameraId별 CTS로 직전 제스처를 취소(큐 적체 방지),
///   취소 토큰을 Continuous/Delay에 전달한다.
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

    /// <summary>상대 줌(휠) — RelativeZoomTranslationSpace로 클램프. zoomDelta +=줌인/-=줌아웃. (FR-PTZCTL-03)</summary>
    Task<bool> RelativeZoomAsync(int cameraId, double zoomDelta, CancellationToken ct = default);

    /// <summary>연속 이동(ContinuousMove 속도) — 패드 누름/드래그 조이스틱/휠 줌. Relative 미지원 카메라 대응. Stop으로 정지. 속도 [-1,1]. (FR-PTZCTL-03)</summary>
    Task<bool> ContinuousMoveAsync(int cameraId, double panVel, double tiltVel, double zoomVel, CancellationToken ct = default);

    /// <summary>
    /// 절대 이동(프리셋 좌표 pan/tilt/zoom). 카메라 절대 space로 클램프. AbsoluteMove는 드래그에 취소되지 않음. (FR-PRESET-02)
    /// </summary>
    Task<bool> AbsoluteMoveAsync(int cameraId, double pan, double tilt, double zoom, CancellationToken ct = default);

    /// <summary>현재 PTZ 위치 읽기(프리셋 저장용 GetStatus). 실패 시 null. (FR-PRESET-03)</summary>
    Task<PtzPosition?> GetStatusAsync(int cameraId, CancellationToken ct = default);

    /// <summary>이동 정지(StopPTZ panTilt+zoom). FR-L1: 뗌 Stop은 제스처 토큰을 전달 — 재누름(새 제스처)이
    /// 취소하면 Gate 대기 중 Stop이 드롭되고 새 ContinuousMove가 자동 대체(ONVIF §5.3.2).</summary>
    Task StopAsync(int cameraId, CancellationToken ct = default);

    /*──── ONVIF 프리셋(FR-C1) — 카메라가 진실원. 전부 ctx.Gate 직렬(I-05), 워밍 ctx(PtzClient+ProfileToken) 재사용 ────*/

    /// <summary>카메라 프리셋 목록 조회(GetPresets). 실패/비PTZ=null, 프리셋 없음=빈 목록(FR-C3 구분용).</summary>
    Task<System.Collections.Generic.IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(int cameraId, CancellationToken ct = default);

    /// <summary>프리셋 토큰으로 이동(GotoPreset — 속도 미지정=카메라 기본). AbsoluteMove처럼 Busy 표시. (FR-C2)</summary>
    Task<bool> GotoPresetAsync(int cameraId, string presetToken, CancellationToken ct = default);

    /// <summary>현재 위치를 새 프리셋으로 저장(SetPreset — 토큰은 카메라 자동 할당). (FR-C2)</summary>
    Task<bool> SetPresetAsync(int cameraId, string presetName, CancellationToken ct = default);

    /// <summary>프리셋 삭제(RemovePreset). (FR-C2)</summary>
    Task<bool> RemovePresetAsync(int cameraId, string presetToken, CancellationToken ct = default);

    /// <summary>현재 위치를 Home으로 지정(SetHomePosition — ONVIF 전용 슬롯, per-preset 개념 아님). (FR-C2/OQ-6)</summary>
    Task<bool> SetHomePresetAsync(int cameraId, CancellationToken ct = default);

    /// <summary>Home 위치로 이동(GotoHomePosition — Home 미지정 카메라는 실패 반환). (FR-C2/OQ-6)</summary>
    Task<bool> GotoHomePresetAsync(int cameraId, CancellationToken ct = default);

    /// <summary>영상 옵션(주야간/포커스) 사용 가능 여부 — IsImagingPossible + ImagingClient + VideoSourceToken. (FR-OPT-03)</summary>
    bool IsImagingCapable(int cameraId);

    /// <summary>현재 영상 옵션(주야간 IrCutFilter / 오토포커스) 조회 — 옵션 탭 표시. 미지원/실패 시 null. (FR-OPT-01/02)</summary>
    Task<CameraImagingState?> GetImagingAsync(int cameraId, CancellationToken ct = default);

    /// <summary>주야간(IrCutFilter: "ON"=주간/"OFF"=야간/"AUTO") 설정. read-modify-write로 타 필드 보존. (FR-OPT-01)</summary>
    Task<bool> SetIrCutFilterAsync(int cameraId, string mode, CancellationToken ct = default);

    /// <summary>오토포커스(AUTO/MANUAL) 설정. read-modify-write로 타 필드 보존. (FR-OPT-02)</summary>
    Task<bool> SetAutoFocusAsync(int cameraId, bool auto, CancellationToken ct = default);

    /// <summary>수동 포커스 연속 이동 시작(누름) — ContinuousFocus(Stop까지 계속). direction +1=far(원경)/-1=near(근경).
    /// 속도는 카메라 GetMoveOptions(ContinuousFocus Speed) 범위로 클램프(FR-PH-10). IsImagingCapable일 때만.</summary>
    Task<bool> StartFocusAsync(int cameraId, int direction, CancellationToken ct = default);

    /// <summary>수동 포커스 정지(뗌) — ImagingClient.Stop. StopAsync(PTZ)와 별개 모터 경로(I-05 직교).</summary>
    Task StopFocusAsync(int cameraId, CancellationToken ct = default);

    /// <summary>
    /// Onvif조회 모드(CameraPopup_RtspSource_Priority FR-03): ONVIF <c>GetStreamUri</c>로 재생용 RTSP URL 조회.
    /// 워밍(EnsureReady 캐시) 재사용 — 이중 초기화 없음. 프로파일 선택은 해상도 최소→비오디오→원 순서(preferSub, OQ-01).
    /// 결과는 카메라 워밍 수명 동안 캐시(FR-07, Release 시 무효화). 실패/미지원/취소 시 null(호출측 URL조회 폴백).
    /// 반환 URL엔 자격증명이 없다 — 호출측이 조합(FR-04).
    /// </summary>
    Task<string?> ResolveStreamUriAsync(int cameraId, IConnectionModel conn, bool preferSub = true, CancellationToken ct = default);

    /// <summary>카메라 PTZ 리소스(딕셔너리 항목·space 캐시) 정리. 멱등(진행 중 태스크 안전, Semaphore 미Dispose). (FR-DISPOSE-01)</summary>
    void Release(int cameraId);
}
