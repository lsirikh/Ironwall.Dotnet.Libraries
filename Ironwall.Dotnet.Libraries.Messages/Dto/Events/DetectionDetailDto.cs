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
    /// AI 추론 프레임 폭(px). 설계 GIS.md v1.5 DETECT(AI) — bbox 좌표 스케일 해석 기준. optional.
    /// </summary>
    [JsonProperty("frame_width")]
    public int? FrameWidth { get; set; }

    /// <summary>
    /// AI 추론 프레임 높이(px). 설계 GIS.md v1.5 DETECT(AI) — bbox 좌표 스케일 해석 기준. optional.
    /// </summary>
    [JsonProperty("frame_height")]
    public int? FrameHeight { get; set; }

    /// <summary>
    /// Backend에서 추가되는 미지 필드 보존
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
