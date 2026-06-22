using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>함체 임계값 — 서버 EnclosureThresholdConfig(temp_high/temp_low/humidity_high/current_high/voltage_low/vibration_high) 정렬.</summary>
public class EnclosureThresholdConfigModel : IEnclosureThresholdConfigModel
{
    [JsonProperty("temp_high", Order = 1)]
    public double? TempHigh { get; set; }

    [JsonProperty("temp_low", Order = 2)]
    public double? TempLow { get; set; }

    [JsonProperty("humidity_high", Order = 3)]
    public double? HumidityHigh { get; set; }

    [JsonProperty("current_high", Order = 4)]
    public double? CurrentHigh { get; set; }

    [JsonProperty("voltage_low", Order = 5)]
    public double? VoltageLow { get; set; }

    [JsonProperty("vibration_high", Order = 6)]
    public int? VibrationHigh { get; set; }
}
