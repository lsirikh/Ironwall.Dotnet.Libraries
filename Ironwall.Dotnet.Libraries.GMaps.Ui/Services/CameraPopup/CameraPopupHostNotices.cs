using Ironwall.Dotnet.Libraries.CameraPopup;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 팝업 호스트 상태 → 지도 하단 안내(camera-popup-modes FR-25 · T-09 K3)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>지도 하단 안내의 종류.</summary>
public enum CameraPopupHostNoticeKind
{
    /// <summary>안내 없음(정상 · 시작 중 · 재시작 중).</summary>
    None = 0,
    /// <summary>계속 떠 있는 안내 + [다시 시작](일시 중지).</summary>
    Persistent = 1,
    /// <summary>한 번 뜨고 사라지는 토스트(사용할 수 없음).</summary>
    Toast = 2,
}

/// <summary>
/// 호스트 상태별 안내 문구와 종류(순수 — 시험이 같은 문구로 단언한다). 오버레이가 하나도 열려 있지 않아도
/// "영상 기능이 멈췄다"는 사실과 되살리는 방법이 지도에 보여야 한다(FR-25).
/// </summary>
public static class CameraPopupHostNotices
{
    public const string Suspended = "영상 기능 일시 중지";
    public const string RestartLabel = "다시 시작";
    public const string Unavailable = "영상 기능을 사용할 수 없습니다 — 팝업 호스트를 찾지 못했습니다";

    public static CameraPopupHostNoticeKind KindOf(CameraPopupHostState state) => state switch
    {
        CameraPopupHostState.Suspended => CameraPopupHostNoticeKind.Persistent,
        CameraPopupHostState.Unavailable => CameraPopupHostNoticeKind.Toast,
        _ => CameraPopupHostNoticeKind.None,
    };
}
