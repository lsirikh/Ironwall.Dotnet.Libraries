using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class PtzStatusBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("pan")]
    public int Pan { get; set; }

    [JsonProperty("tilt")]
    public int Tilt { get; set; }

    [JsonProperty("zoom")]
    public int Zoom { get; set; }

    /// <summary>
    /// (v4.6) 현재 위치에 해당하는 프리셋 번호. 프리셋 이동 직후 설정, 수동/연속 이동 중이면 null. GIS.md §3.6.
    /// </summary>
    [JsonProperty("current_preset", NullValueHandling = NullValueHandling.Ignore)]
    public int? CurrentPreset { get; set; }

    /// <summary>
    /// (v4.6) 현재 프리셋이 감시금지구역(CameraPreset is_restricted_zone=true)인지.
    /// true면 GIS는 해당 카메라 화면을 마스킹한다. GIS.md §3.6·§6.3. 미포함 시 false(마스킹 없음).
    /// </summary>
    [JsonProperty("is_restricted")]
    public bool IsRestricted { get; set; }
}
