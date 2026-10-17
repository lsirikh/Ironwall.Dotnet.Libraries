using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : CAMERA_POPUP_OPEN 성공 RSP body — 브로커 연동설계 v2.0.7 §11.5.9 G-52 (RSP popup_id)
   Created By   : Claude (camera-popup-modes T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>CAMERA_POPUP_OPEN</c> RSP body. 명세가 정한 칸은 <see cref="PopupId"/> 하나다.
/// <see cref="Monitor"/> · <see cref="Cell"/> 은 명세 밖 — 받는 쪽이 실제로 띄운 자리를 돌려주면 그것을 알리고,
/// 없으면 요청한 자리를 알린다(관용 파싱).
/// </summary>
public class CameraPopupOpenResultDto
{
    /// <summary>띄운 팝업 식별자(<c>CAMERA_POPUP_CLOSE</c> 에 쓴다). 숫자로 와도 글자로 읽는다.</summary>
    [JsonProperty("popup_id")]
    public string? PopupId { get; set; }

    /// <summary>실제로 띄운 모니터(명세 밖 · 선택).</summary>
    [JsonProperty("monitor", NullValueHandling = NullValueHandling.Ignore)]
    public int? Monitor { get; set; }

    /// <summary>실제로 띄운 칸(명세 밖 · 선택).</summary>
    [JsonProperty("cell", NullValueHandling = NullValueHandling.Ignore)]
    public int? Cell { get; set; }
}
