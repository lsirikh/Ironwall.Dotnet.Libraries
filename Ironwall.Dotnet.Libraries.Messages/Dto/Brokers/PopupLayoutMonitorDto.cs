using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : POPUP_LAYOUT_GET RSP monitors[] 원소 — 브로커 연동설계 v2.0.7 §11.5.9 G-51
   Created By   : Claude (camera-popup-modes T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>관제석 모니터 한 대. 해상도는 받는 쪽 PC 의 값(표시용)이다.</summary>
public class PopupLayoutMonitorDto
{
    /// <summary>모니터 번호 — 1부터. <c>CAMERA_POPUP_OPEN.monitor</c> 에 그대로 쓴다.</summary>
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("is_primary")]
    public bool IsPrimary { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }
}
