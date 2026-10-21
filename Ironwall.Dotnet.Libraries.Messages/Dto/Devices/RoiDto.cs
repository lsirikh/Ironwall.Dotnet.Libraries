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

    /// <summary>
    /// 제외구역 여부(<c>is_exclusion</c>) — <c>true</c> 면 이 구역을 <b>뺀다</b>(관심구역의 반대). 8.0.15 부터 목록 · 상세에 늘 실린다.
    /// <para><c>null</c> = 서버가 이 키를 주지 않았다(구판). 그때는 싣지 않는다 — 구판 쓰기 스키마는 모르는 키를 422 로 막는다.</para>
    /// </summary>
    [JsonProperty("is_exclusion", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsExclusion { get; set; }

    [JsonProperty("point_count", Order = 7, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int PointCount { get; set; }

    [JsonProperty("points", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public List<XyPointDto>? Points { get; set; }

    [JsonProperty("created_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at", Order = 100, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}
