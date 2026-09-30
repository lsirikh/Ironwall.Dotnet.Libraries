namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 요청 — [모니터 목록 가져오기] 결과 (PRD FR-08, 명세 §11.5.9 G-51 POPUP_LAYOUT_GET)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>POPUP_LAYOUT_GET</c> 의 결과. 실패는 예외가 아니라 <see cref="Success"/>=false + 한 줄 사유(<see cref="Message"/>)다 —
/// 설정 화면은 사유를 칸 아래에 그대로 보인다.
/// </summary>
public sealed record NvrPopupLayoutResult
{
    public bool Success { get; init; }

    /// <summary>모니터 목록(번호 순). 실패면 비어 있다.</summary>
    public IReadOnlyList<NvrPopupMonitor> Monitors { get; init; } = Array.Empty<NvrPopupMonitor>();

    /// <summary>NVR 의 기본 자리(선택) — 모니터.</summary>
    public int? DefaultMonitor { get; init; }

    /// <summary>NVR 의 기본 자리(선택) — 칸.</summary>
    public int? DefaultCell { get; init; }

    /// <summary>실패 사유 · 안내 한 줄.</summary>
    public string Message { get; init; } = string.Empty;

    public static NvrPopupLayoutResult Ok(IReadOnlyList<NvrPopupMonitor> monitors, int? defaultMonitor = null, int? defaultCell = null)
        => new() { Success = true, Monitors = monitors, DefaultMonitor = defaultMonitor, DefaultCell = defaultCell };

    public static NvrPopupLayoutResult Fail(string message)
        => new() { Success = false, Message = message ?? string.Empty };

    /// <summary>이 화면(호스트)이 브로커에 닿을 창구를 주지 않았다.</summary>
    public static NvrPopupLayoutResult Unavailable { get; } = Fail("이 화면에서는 모니터 목록을 가져올 수 없습니다");
}
