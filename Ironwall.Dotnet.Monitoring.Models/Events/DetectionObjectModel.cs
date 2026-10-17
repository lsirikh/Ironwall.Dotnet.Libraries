using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      : AI 탐지 객체 (detail.objects[] 항목) — Detection_Signal_History 확장
   Created By   : GHLee
   Created On   : 2026-07-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionObjectModel
{
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("confidence")]
    public double Confidence { get; set; }

    /// <summary>바운딩 박스 [x, y, width, height] — 픽셀(정수) 또는 정규화 비율(0~1 실수). 서버 규격 <c>List[float]</c>.</summary>
    [JsonProperty("bbox", NullValueHandling = NullValueHandling.Ignore)]
    public List<double>? Bbox { get; set; }

    [JsonProperty("thumbnail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Thumbnail { get; set; }
}
