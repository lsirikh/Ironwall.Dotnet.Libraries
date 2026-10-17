using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// XY 좌표 포인트 DTO (ROI 폴리곤 꼭짓점)
/// Camera → Preset → ROI → Point 계층 구조의 4단계
/// </summary>
public class XyPointDto
{
    [JsonProperty("id", Order = 1, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Id { get; set; }

    [JsonProperty("roi_id", Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int RoiId { get; set; }

    [JsonProperty("x", Order = 3)]
    public double X { get; set; }

    [JsonProperty("y", Order = 4)]
    public double Y { get; set; }

    [JsonProperty("order", Order = 5)]
    public int PointOrder { get; set; }

    [JsonProperty("created_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at", Order = 100, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// XY 포인트 Bulk 교체용 DTO (PUT /rois/{roi_id}/points)
/// </summary>
public class XyPointBulkDto
{
    [JsonProperty("points")]
    public List<XyPointDto> Points { get; set; } = new();
}
