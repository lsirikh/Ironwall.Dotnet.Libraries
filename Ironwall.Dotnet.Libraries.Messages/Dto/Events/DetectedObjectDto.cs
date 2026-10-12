using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// AI 탐지 객체 DTO (detail.objects[] 배열 항목)
/// <para>⚠ nullable 프로퍼티는 <c>NullValueHandling.Ignore</c> — <c>detail</c> 검증기는 중첩까지 훑고
/// <b><c>missing</c> 만</b> 면제한다(명시적 null 은 422). 상위 <c>DetectionDetailDto</c> 와 같은 규율.</para>
/// </summary>
public class DetectedObjectDto
{
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// 바운딩 박스 <c>[x, y, width, height]</c> — 서버 규격 <c>List[float]</c>: 픽셀(정수) 또는 정규화 비율(0~1 실수).
    /// <para>⚠ <c>List&lt;int&gt;</c> 로 되돌리지 말 것 — 비율 bbox 한 건이 목록 쪽 전체를 실패시킨다.
    /// 원소 쓰기는 <see cref="WholeNumberJsonConverter"/> 가 픽셀 정수를 정수 모양으로 지킨다.</para>
    /// </summary>
    [JsonProperty("bbox", NullValueHandling = NullValueHandling.Ignore, ItemConverterType = typeof(WholeNumberJsonConverter))]
    public List<double>? Bbox { get; set; }

    [JsonProperty("thumbnail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Thumbnail { get; set; }
}
