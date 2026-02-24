using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 카메라 프리셋 DTO (API 설계 문서 §5.3.8 기준)
/// Camera → Preset → ROI → Point 계층 구조의 2단계
/// </summary>
public class CameraPresetDto
{
    [JsonProperty("id", Order = 1, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Id { get; set; }

    [JsonProperty("camera_id", Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int CameraId { get; set; }

    [JsonProperty("camera_name", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? CameraName { get; set; }

    [JsonProperty("preset_index", Order = 4)]
    public int PresetIndex { get; set; }

    [JsonProperty("preset_name", Order = 5)]
    public string PresetName { get; set; } = string.Empty;

    [JsonProperty("touring_time", Order = 6)]
    public int TouringTime { get; set; } = 10;

    [JsonProperty("roi_count", Order = 7, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int RoiCount { get; set; }

    [JsonProperty("rois", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public List<RoiDto>? Rois { get; set; }

    [JsonProperty("created_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at", Order = 100, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}
