using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class TrackingSetBodyDto : ControlCommandBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("tracking")]
    public string Tracking { get; set; } = string.Empty;
}
