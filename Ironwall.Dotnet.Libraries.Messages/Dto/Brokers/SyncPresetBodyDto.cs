using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class SyncPresetBodyDto
{
    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("resource_id")]
    public int ResourceId { get; set; }

    [JsonProperty("camera_id")]
    public int CameraId { get; set; }
}
