namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 요청 — 칸이 차 있을 때 (PRD FR-08, 명세 §11.5.9 on_occupied)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary><c>CAMERA_POPUP_OPEN.body.on_occupied</c> — 명세 값은 대문자 <c>REPLACE</c> / <c>REJECT</c>.</summary>
public enum CameraPopupOnOccupied
{
    /// <summary>교체 — 칸에 있던 영상을 내리고 새로 띄운다.</summary>
    Replace = 0,

    /// <summary>거부 — 칸이 차 있으면 띄우지 않고 거부 응답.</summary>
    Reject = 1,
}
