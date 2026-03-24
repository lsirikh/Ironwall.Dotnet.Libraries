using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class TrackingStatusBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("tracking")]
    public string Tracking { get; set; } = string.Empty;

    [JsonProperty("target")]
    public DetectedObjectDto? Target { get; set; }

    [JsonProperty("target_location")]
    public TrackingTargetLocationDto? TargetLocation { get; set; }
}
