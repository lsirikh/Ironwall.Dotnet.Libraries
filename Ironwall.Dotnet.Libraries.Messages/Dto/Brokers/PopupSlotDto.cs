using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : 논리 슬롯 3×3 자리 {monitor, cell} — 브로커 연동설계 v2.0.7 §11.5.9 (G-51 default_slot · G-54)
   Created By   : Claude (camera-popup-modes T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>자리 하나. <c>monitor</c> 는 1부터, <c>cell</c> 은 1~9.</summary>
public class PopupSlotDto
{
    [JsonProperty("monitor")]
    public int Monitor { get; set; }

    [JsonProperty("cell")]
    public int Cell { get; set; }
}
