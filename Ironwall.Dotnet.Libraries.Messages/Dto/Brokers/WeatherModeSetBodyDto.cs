using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class WeatherModeSetBodyDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("weather_mode")]
    public string WeatherMode { get; set; } = string.Empty;
}
