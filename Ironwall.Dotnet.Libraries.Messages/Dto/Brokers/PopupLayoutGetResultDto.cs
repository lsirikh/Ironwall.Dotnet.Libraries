using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : POPUP_LAYOUT_GET RSP body — 브로커 연동설계 v2.0.7 §11.5.9 G-51
                  monitors[{index, is_primary, width, height}] · default_slot
   Created By   : Claude (camera-popup-modes T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary><c>POPUP_LAYOUT_GET</c> RSP body.</summary>
public class PopupLayoutGetResultDto
{
    /// <summary>관제석 모니터 목록. <c>index</c> 는 1부터.</summary>
    [JsonProperty("monitors")]
    public List<PopupLayoutMonitorDto>? Monitors { get; set; }

    /// <summary>자리를 생략한 <c>CAMERA_POPUP_OPEN</c> 이 쓸 기본 자리(선택).</summary>
    [JsonProperty("default_slot", NullValueHandling = NullValueHandling.Ignore)]
    public PopupSlotDto? DefaultSlot { get; set; }
}
