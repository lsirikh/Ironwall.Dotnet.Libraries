using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class PtzStatusBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("pan")]
    public double Pan { get; set; }

    [JsonProperty("tilt")]
    public double Tilt { get; set; }

    [JsonProperty("zoom")]
    public double Zoom { get; set; }
}
