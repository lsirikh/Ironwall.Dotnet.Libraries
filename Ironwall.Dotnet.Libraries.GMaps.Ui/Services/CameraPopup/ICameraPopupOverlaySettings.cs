using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 더블클릭 팝업이 읽는 설정 창구(camera-popup-modes T-02)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 지도 더블클릭 팝업이 보는 설정. 옛 <c>IStreamingSetupModel</c>(LibVLC 를 쓰는 Streaming 어셈블리에 산다)을
/// GMaps.Ui 가 더는 참조하지 않도록 둔 좁은 창구 — 호스트 앱이 라이브 설정 모델을 감싸 등록한다(매번 다시 읽는다).
/// 등록이 없으면 옛 기본 동작(팝업 사용 · RTSP 주소 · 자동 닫기 설정 없음)으로 돈다.
/// </summary>
public interface ICameraPopupOverlaySettings
{
    /// <summary>옛 "카메라 팝업 연동" 게이트 — false 면 더블클릭을 무시한다(모드 전환은 뒤 태스크).</summary>
    bool IsCameraPopupUsed { get; }

    /// <summary>제공자 · 더블클릭 자동 닫기 등 새 설정(옛 키에서 이관된 값 포함).</summary>
    CameraPopupSettings Settings { get; }
}
