namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 팝업 3모드 (camera-popup-modes PRD FR-01)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 설정 콘솔 이벤트 절 "카메라 팝업 연동"의 팝업 방식.
/// 이관: 옛 <c>IsCameraPopupUsed=true</c> → <see cref="Self"/>, <c>false</c> → <see cref="None"/>.
/// </summary>
/// <remarks>
/// 이 폴더(<c>CameraPopup/</c>)는 WPF 를 쓰지 않는다 — 팝업 호스트 · 창 관리자(T-04/T-05)가 그대로 가져다 쓴다.
/// </remarks>
public enum CameraPopupMode
{
    /// <summary>GIS 자체 팝업 — 더블클릭은 지도 위 상자, 탐지는 이벤트 창.</summary>
    Self = 0,

    /// <summary>브로커 요청 — 더블클릭을 <c>CAMERA_POPUP_OPEN</c> 으로 NVR Manager 에 보낸다(탐지 팝업은 하지 않는다).</summary>
    Broker = 1,

    /// <summary>사용 안 함 — 더블클릭은 카메라 상세(속성)를 연다. 탐지 창 없음.</summary>
    None = 2,
}
