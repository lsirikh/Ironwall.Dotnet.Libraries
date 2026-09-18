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

    [JsonProperty("bbox", NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? Bbox { get; set; }

    [JsonProperty("thumbnail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Thumbnail { get; set; }
}
