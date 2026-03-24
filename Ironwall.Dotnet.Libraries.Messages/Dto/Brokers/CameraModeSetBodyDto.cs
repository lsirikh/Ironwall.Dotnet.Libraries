using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class CameraModeSetBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("camera_mode")]
    public string CameraMode { get; set; } = string.Empty;
}
