using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// DetectionEvent의 detail JSONB 필드 DTO
/// <para>Backend에서 추가되는 미지 필드도 [JsonExtensionData]로 보존</para>
/// </summary>
public class DetectionDetailDto
{
    [JsonProperty("signal")]
    public int? Signal { get; set; }

    [JsonProperty("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonProperty("objects")]
    public List<DetectedObjectDto>? Objects { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("inference_ms")]
    public int? InferenceMs { get; set; }

    /// <summary>
    /// Backend에서 추가되는 미지 필드 보존
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
