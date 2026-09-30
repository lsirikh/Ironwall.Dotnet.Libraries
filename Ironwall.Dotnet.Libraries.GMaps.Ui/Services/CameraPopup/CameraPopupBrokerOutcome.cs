namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 모드 더블클릭 결과 (camera-popup-modes T-07 · FR-06)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary><c>CAMERA_POPUP_OPEN</c> 한 건의 끝.</summary>
public enum CameraPopupBrokerOutcomeKind
{
    /// <summary>띄웠다(RSP success=true).</summary>
    Opened,

    /// <summary>NVR Manager 가 거부했다(RSP success=false + 한국어 사유).</summary>
    Rejected,

    /// <summary>응답 없음 — 설정 초 안에 RSP 가 오지 않았다(무구독 · NATS 끊김 포함).</summary>
    NoResponse,

    /// <summary>RSP 를 받았지만 봉투를 읽지 못했다.</summary>
    ParseError,

    /// <summary>같은 카메라 요청이 이미 가는 중 — 보내지 않고 하나로 묶었다.</summary>
    Coalesced,

    /// <summary>보내기 전 검사에서 걸렸다(관제석 식별자 · 카메라 ID · NATS 부대 설정).</summary>
    Invalid,

    /// <summary>호출 쪽이 취소했다(알리지 않는다).</summary>
    Cancelled,

    /// <summary>예상 못 한 예외 — 로그 + 안내만(FR-27).</summary>
    Failed,
}

/// <summary>결과 + 지도 하단 토스트 한 줄(알리지 않는 결과는 빈 문자열).</summary>
public sealed record CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind Kind, string Toast, string? PopupId = null)
{
    public bool IsOpened => Kind == CameraPopupBrokerOutcomeKind.Opened;
}

/// <summary>
/// 진행 중 알림. <see cref="IsPending"/> 이면 결과가 올 때까지 토스트를 걸어 두고(응답 기다림 초가 상한),
/// 아니면 몇 초 뒤 내린다.
/// </summary>
public readonly record struct CameraPopupBrokerNotice(string Text, bool IsPending);
