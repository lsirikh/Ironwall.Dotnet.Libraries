using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// AI 탐지 객체 DTO (detail.objects[] 배열 항목)
/// </summary>
public class DetectedObjectDto
{
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("confidence")]
    public double Confidence { get; set; }

    [JsonProperty("bbox")]
    public List<int>? Bbox { get; set; }

    [JsonProperty("thumbnail")]
    public string? Thumbnail { get; set; }
}
