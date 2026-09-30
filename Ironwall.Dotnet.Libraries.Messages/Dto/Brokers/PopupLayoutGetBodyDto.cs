using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : 관제석 모니터 배치 조회 요청 body — NATS POPUP_LAYOUT_GET (REQ).
                  브로커 연동설계 v2.0.7 §11.5.9 G-51. Subject: sensorway.{부대ID}.nvr_manager.popup
   Created By   : Claude (camera-popup-modes T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary><c>POPUP_LAYOUT_GET</c> 요청 body. 모니터 목록은 실시간 값이라 저장하지 않고 매번 묻는다.</summary>
public class PopupLayoutGetBodyDto
{
    /// <summary>대상 관제석 — 로그인 <c>client_id</c> 와 같은 값. 필수.</summary>
    [JsonProperty("target_client_id")]
    public string TargetClientId { get; set; } = string.Empty;
}
