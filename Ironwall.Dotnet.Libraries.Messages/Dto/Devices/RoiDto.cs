using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// ROI(Region of Interest) DTO (API 설계 문서 §5.3.9 기준)
/// Camera → Preset → ROI → Point 계층 구조의 3단계
/// </summary>
public class RoiDto
{
    [JsonProperty("id", Order = 1, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Id { get; set; }

    [JsonProperty("preset_id", Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int PresetId { get; set; }

    [JsonProperty("name", Order = 3)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("resolution_width", Order = 4)]
    public double ResolutionWidth { get; set; }

    [JsonProperty("resolution_height", Order = 5)]
    public double ResolutionHeight { get; set; }

    [JsonProperty("is_enable", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEnable { get; set; }

    [JsonProperty("point_count", Order = 7, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int PointCount { get; set; }

    [JsonProperty("points", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public List<XyPointDto>? Points { get; set; }

    [JsonProperty("created_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at", Order = 100, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}
