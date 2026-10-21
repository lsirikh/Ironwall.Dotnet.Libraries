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

/// <summary>
/// 점 전체 교체(<c>PUT /api/rois/{roi_id}/points</c>) 응답의 data — <c>{roi_id, points, total}</c>(명세 §5.9.3).
/// <para>목록 응답(<c>items</c>)과 키가 달라 종전 <see cref="PointListDataDto"/> 로 읽으면 교체된 점이 <b>0개</b>로 보였다.
/// 같은 자리(<see cref="PointListDataDto.Items"/>)에 담아 기존 호출부가 그대로 쓴다.</para>
/// </summary>
public class PointReplaceResultDto : PointListDataDto
{
    /// <summary>점을 바꾼 ROI id.</summary>
    [JsonProperty("roi_id")]
    public int RoiId { get; set; }

    /// <summary>교체 후 점 — <see cref="PointListDataDto.Items"/> 와 같은 목록이다.</summary>
    [JsonProperty("points")]
    public List<XyPointDto> Points
    {
        get => Items;
        set => Items = value ?? new();
    }
}
