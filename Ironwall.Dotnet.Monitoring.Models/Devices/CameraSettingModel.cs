using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class CameraSettingModel : BaseModel, ICameraSettingModel
{
    [JsonProperty("camera_id", Order = 2)]
    public int CameraId { get; set; }

    [JsonProperty("weather_mode", Order = 3)]
    public string WeatherMode { get; set; } = "NORMAL";

    [JsonProperty("camera_mode", Order = 4)]
    public string CameraMode { get; set; } = "NORMAL";

    [JsonProperty("heater", Order = 5)]
    public string Heater { get; set; } = "off";

    [JsonProperty("fan", Order = 6)]
    public string Fan { get; set; } = "off";

    [JsonProperty("headlight", Order = 7)]
    public string Headlight { get; set; } = "off";

    [JsonProperty("day_night_mode", Order = 8)]
    public string DayNightMode { get; set; } = "AUTO";

    [JsonProperty("focus_mode", Order = 9)]
    public string FocusMode { get; set; } = "AUTO";

    [JsonProperty("iris_mode", Order = 10)]
    public string IrisMode { get; set; } = "AUTO";

    [JsonProperty("tracking", Order = 11)]
    public string Tracking { get; set; } = "IDLE";

    [JsonProperty("palette", Order = 12, NullValueHandling = NullValueHandling.Include)]
    public string? Palette { get; set; }
}
