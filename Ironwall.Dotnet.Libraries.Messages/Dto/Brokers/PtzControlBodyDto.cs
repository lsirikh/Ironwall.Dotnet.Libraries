using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// PTZ 제어 Body DTO (NATS v1.2 §6)
/// 용도별 nullable 필드 조합으로 사용:
/// - 연속 이동: camera_id + pan_tilt_speed + zoom_speed + timeout_ms
/// - 프리셋: camera_id + preset + pan_tilt_speed + zoom_speed
/// - 절대 좌표: camera_id + pan + tilt + zoom + pan_tilt_speed + zoom_speed
/// - 화면 클릭: camera_id + x + y + pan_tilt_speed
/// - 정지/보조: camera_id만
/// </summary>
public class PtzControlBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("pan_tilt_speed")]
    public int? PanTiltSpeed { get; set; }

    [JsonProperty("zoom_speed")]
    public int? ZoomSpeed { get; set; }

    [JsonProperty("timeout_ms")]
    public int? TimeoutMs { get; set; }

    [JsonProperty("preset")]
    public int? Preset { get; set; }

    [JsonProperty("pan")]
    public int? Pan { get; set; }

    [JsonProperty("tilt")]
    public int? Tilt { get; set; }

    [JsonProperty("zoom")]
    public int? Zoom { get; set; }

    [JsonProperty("x")]
    public int? X { get; set; }

    [JsonProperty("y")]
    public int? Y { get; set; }
}
