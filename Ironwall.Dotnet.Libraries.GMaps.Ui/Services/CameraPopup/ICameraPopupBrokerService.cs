using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 모드 — NVR Manager 관제석 팝업 낱말 (camera-popup-modes T-07 · FR-05~08)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 브로커 연동설계 v2.0.7 §11.5.9 — <c>sensorway.{부대}.nvr_manager.popup</c> 한 주제에 REQ/RSP 로 보낸다.
/// <para><b>예외를 던지지 않는다</b>(FR-27) — 결과는 <see cref="CameraPopupBrokerOutcome"/> · <see cref="NvrPopupLayoutResult"/> 로 돌려준다.
/// <b>UI 스레드를 붙잡지 않는다</b>(FR-28) — 응답 기다림은 설정 초가 상한이고 알림 콜백은 어느 스레드에서나 불릴 수 있다.</para>
/// </summary>
public interface ICameraPopupBrokerService
{
    /// <summary><c>{DomainNats}.{GroupNats}.nvr_manager.popup</c>. 부대 설정이 비면 빈 문자열.</summary>
    string BuildSubject();

    /// <summary>이 카메라 요청이 응답을 기다리는 중인가(연타 합치기, FR-06).</summary>
    bool IsPending(int cameraId);

    /// <summary>
    /// <c>CAMERA_POPUP_OPEN</c>. 같은 카메라가 응답 전이면 보내지 않고 <see cref="CameraPopupBrokerOutcomeKind.Coalesced"/>.
    /// </summary>
    /// <param name="notify">토스트 알림 — 요청(대기) 한 번 + 결과 한 번. 합치기 · 취소는 알리지 않는다.</param>
    Task<CameraPopupBrokerOutcome> RequestOpenAsync(CameraPopupOpenRequest request, Action<CameraPopupBrokerNotice>? notify = null,
                                                    CancellationToken ct = default);

    /// <summary><c>POPUP_LAYOUT_GET</c> — 관제석 모니터 목록(설정 [모니터 목록 가져오기]).</summary>
    Task<NvrPopupLayoutResult> GetLayoutAsync(string clientId, int timeoutSeconds, CancellationToken ct = default);
}
