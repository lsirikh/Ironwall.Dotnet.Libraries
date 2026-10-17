using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 모드 지도 하단 토스트 문구 (camera-popup-modes T-07 · FR-06)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>토스트 한 줄 문구 — 시험이 같은 문구로 단언한다.</summary>
public static class CameraPopupBrokerToasts
{
    public static string Requested(string cameraName)
        => $"NVR 관제석에 {cameraName} 팝업을 요청했습니다";

    /// <summary>띄움 — 칸을 모르면(칸 자동 + 응답에 자리 없음) 모니터만 말한다.</summary>
    public static string Opened(int? monitor, int? cell)
        => (monitor, cell) switch
        {
            ({ } m, { } c) => string.Create(CultureInfo.InvariantCulture, $"모니터 {m} · 칸 {c}에 띄웠습니다"),
            ({ } m, null) => string.Create(CultureInfo.InvariantCulture, $"모니터 {m}에 띄웠습니다"),
            _ => "NVR 관제석에 띄웠습니다",
        };

    public static string Rejected(string reason)
        => string.IsNullOrWhiteSpace(reason) ? "NVR 팝업 거부 — 사유 없음" : $"NVR 팝업 거부 — {reason}";

    public static string NoResponse(int seconds)
        => string.Create(CultureInfo.InvariantCulture, $"NVR Manager 응답 없음({seconds}초) — 이 관제석에서 돌고 있는지 확인하세요");

    public const string ParseError = "NVR Manager 응답을 해석하지 못했습니다";
    public const string NoClientId = "관제석 식별자가 없어 NVR 팝업을 요청하지 못했습니다 — 설정을 확인하세요";
    public const string InvalidCamera = "카메라 ID 가 없어 NVR 팝업을 요청하지 못했습니다";
    public const string NoSubject = "NATS 부대 설정(도메인 · 부대)이 비어 NVR 팝업을 요청하지 못했습니다";
    public const string NoBroker = "브로커 요청 창구가 없어 NVR 팝업을 요청하지 못했습니다";
    public const string Failed = "NVR 팝업 요청 중 오류가 났습니다 — 로그를 확인하세요";
}
