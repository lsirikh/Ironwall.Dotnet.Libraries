using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : 관제석 카메라 팝업 열기 요청 body — NATS CAMERA_POPUP_OPEN (REQ).
                  브로커 연동설계 v2.0.7 §11.5.9 G-52.
                  Subject: sensorway.{부대ID}.nvr_manager.popup (큐 그룹 없음 — 대상은 body target_client_id)
                  발신 from = "GIS", m_type = "REQ". RSP body = popup_id.
   Created By   : Claude (camera-popup-modes T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>CAMERA_POPUP_OPEN</c> 요청 body. 자리(<see cref="Monitor"/> · <see cref="Cell"/>)는 <b>논리 슬롯 3×3</b> 이고
/// 픽셀은 보내지 않는다 — 환산은 받는 쪽(NVR Manager) 몫이다.
/// </summary>
public class CameraPopupOpenBodyDto
{
    /// <summary>칸이 차 있으면 교체(명세 기본값).</summary>
    public const string OnOccupiedReplace = "REPLACE";

    /// <summary>칸이 차 있으면 거부.</summary>
    public const string OnOccupiedReject = "REJECT";

    /// <summary>대상 관제석 — 로그인 <c>client_id</c>(<c>X-Client-Id</c>)와 같은 값. 필수.
    /// 다른 값을 받은 NVR Manager 는 <b>조용히 무시</b>한다(회신 없음).</summary>
    [JsonProperty("target_client_id")]
    public string TargetClientId { get; set; } = string.Empty;

    /// <summary>카메라 장비 ID(REST 정본). 필수.</summary>
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    /// <summary>모니터 번호 — 1부터(<c>POPUP_LAYOUT_GET</c> 의 <c>index</c>). 선택 — 생략하면 NVR 의 기본 자리.</summary>
    [JsonProperty("monitor", NullValueHandling = NullValueHandling.Ignore)]
    public int? Monitor { get; set; }

    /// <summary>칸 1~9(왼쪽 위부터 1·2·3 / 4·5·6 / 7·8·9). 선택 — "칸 자동"이면 생략한다.</summary>
    [JsonProperty("cell", NullValueHandling = NullValueHandling.Ignore)]
    public int? Cell { get; set; }

    /// <summary><see cref="OnOccupiedReplace"/> / <see cref="OnOccupiedReject"/>. 선택(기본 REPLACE) — 우리는 늘 보낸다.</summary>
    [JsonProperty("on_occupied", NullValueHandling = NullValueHandling.Ignore)]
    public string? OnOccupied { get; set; } = OnOccupiedReplace;

    /// <summary>요청한 운영자. 선택.</summary>
    [JsonProperty("requested_by", NullValueHandling = NullValueHandling.Ignore)]
    public string? RequestedBy { get; set; }
}
