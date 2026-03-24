using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class PaletteSetBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("palette")]
    public string Palette { get; set; } = string.Empty;
}
