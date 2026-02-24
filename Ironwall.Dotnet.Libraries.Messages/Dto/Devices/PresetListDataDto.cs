using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Preset 목록 응답의 data 래퍼: { items: CameraPresetDto[], total: int }
/// ApiResponse&lt;PresetListDataDto&gt; 형태로 역직렬화
/// </summary>
public class PresetListDataDto
{
    [JsonProperty("items")]
    public List<CameraPresetDto> Items { get; set; } = new();

    [JsonProperty("total")]
    public int Total { get; set; }
}

/// <summary>
/// ROI 목록 응답의 data 래퍼: { items: RoiDto[], total: int }
/// ApiResponse&lt;RoiListDataDto&gt; 형태로 역직렬화
/// </summary>
public class RoiListDataDto
{
    [JsonProperty("items")]
    public List<RoiDto> Items { get; set; } = new();

    [JsonProperty("total")]
    public int Total { get; set; }
}

/// <summary>
/// Point 목록 응답의 data 래퍼: { items: XyPointDto[], total: int }
/// ApiResponse&lt;PointListDataDto&gt; 형태로 역직렬화
/// </summary>
public class PointListDataDto
{
    [JsonProperty("items")]
    public List<XyPointDto> Items { get; set; } = new();

    [JsonProperty("total")]
    public int Total { get; set; }
}
