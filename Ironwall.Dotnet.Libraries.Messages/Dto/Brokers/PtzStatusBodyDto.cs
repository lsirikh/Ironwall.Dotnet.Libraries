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
}
