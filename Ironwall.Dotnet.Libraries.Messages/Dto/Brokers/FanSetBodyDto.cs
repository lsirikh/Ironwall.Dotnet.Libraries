using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class FanSetBodyDto : ControlCommandBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("fan")]
    public string Fan { get; set; } = string.Empty;
}
