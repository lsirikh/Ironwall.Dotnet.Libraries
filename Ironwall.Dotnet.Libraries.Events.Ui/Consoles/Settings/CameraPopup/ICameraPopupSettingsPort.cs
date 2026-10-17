using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 블록이 호스트에게서 받는 창구 (T-03)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 설정 블록 ↔ 호스트(설정 파일). 호스트(메인 앱 <c>EventSetupViewModel</c>)가 구현한다 —
/// 블록은 <c>appsettings.json</c> 을 모른다. 테스트는 가짜를 끼운다.
/// </summary>
public interface ICameraPopupSettingsPort
{
    /// <summary>지금 저장된 값(새 키 + 옛 키 이관 + 정규화).</summary>
    CameraPopupSettings LoadCameraPopup();

    /// <summary>
    /// 저장 — 새 키 덩어리와 옛 키(<c>IsCameraPopupUsed</c> · <c>CameraPopupRtspSource</c> · <c>IsAutoDiscard</c> · <c>TimeoutSeconds</c>)를 함께.
    /// 실패하면 예외를 던진다(블록이 잡아 [저장] 막대에 사유를 낸다).
    /// </summary>
    void SaveCameraPopup(CameraPopupSettings settings);

    /// <summary>관제석 식별자(읽기 전용 표시 — 값은 T-08 이 설치 때 부여).</summary>
    string CameraPopupClientId { get; }

    /// <summary>
    /// 브로커 [모니터 목록 가져오기] — NVR Manager 에 <c>POPUP_LAYOUT_GET</c>(PRD FR-08, T-07).
    /// 호스트가 브로커 서비스로 잇는다. 기본 구현은 "가져올 수 없음"(시험 · 미리보기 가짜는 그대로 컴파일된다).
    /// 예외를 던지지 않고 결과로 돌려준다.
    /// </summary>
    /// <param name="timeoutSeconds">응답 기다림(초) — 설정 초안의 값.</param>
    Task<NvrPopupLayoutResult> FetchBrokerLayoutAsync(int timeoutSeconds, CancellationToken ct = default)
        => Task.FromResult(NvrPopupLayoutResult.Unavailable);
}
