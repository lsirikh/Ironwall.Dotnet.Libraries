namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 영상 · 제어 제공자 종류 (camera-popup-modes PRD FR-17 · FR-18)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 더블클릭 팝업 · 이벤트 창 · 우클릭 PTZ 가 부를 제공자.
/// 이관: 옛 <c>CameraPopupRtspSource=Onvif</c> → <see cref="Onvif"/>, <c>Url</c> → <see cref="RtspUrl"/>.
/// </summary>
public enum VideoProviderKind
{
    /// <summary>ONVIF(기본) — 영상 주소 · PTZ 모두 카메라에 직접.</summary>
    Onvif = 0,

    /// <summary>RTSP 주소 — 장비에 저장된 주소로 영상만(PTZ 없음).</summary>
    RtspUrl = 1,

    /// <summary>외부 VMS API — 설정 칸만 자리를 잡는다(연동처가 생기면 구현). 지금은 고를 수 없다.</summary>
    ExternalVms = 2,
}
