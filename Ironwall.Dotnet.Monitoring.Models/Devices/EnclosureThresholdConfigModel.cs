using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class EnclosureThresholdConfigModel : IEnclosureThresholdConfigModel
{
    [JsonProperty("temp_high", Order = 1)]
    public double? TempHigh { get; set; }

    [JsonProperty("temp_low", Order = 2)]
    public double? TempLow { get; set; }

    [JsonProperty("humidity_high", Order = 3)]
    public double? HumidityHigh { get; set; }

    [JsonProperty("humidity_low", Order = 4)]
    public double? HumidityLow { get; set; }

    [JsonProperty("vibration_threshold", Order = 5)]
    public double? VibrationThreshold { get; set; }
}
