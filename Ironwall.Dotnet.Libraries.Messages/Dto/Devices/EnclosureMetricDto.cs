using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 함체 메트릭 시계열 데이터 DTO (§5.5.9~5.5.11)
/// <para>temperature/humidity/current/voltage는 서버가 string으로 반환</para>
/// </summary>
public class EnclosureMetricDto : BaseDto
{
    [JsonProperty("enclosure_id", Order = 2)]
    public int EnclosureId { get; set; }

    [JsonProperty("temperature", Order = 3)]
    public string? Temperature { get; set; }

    [JsonProperty("humidity", Order = 4)]
    public string? Humidity { get; set; }

    [JsonProperty("current", Order = 5)]
    public string? Current { get; set; }

    [JsonProperty("voltage", Order = 6)]
    public string? Voltage { get; set; }

    [JsonProperty("vibration", Order = 7)]
    public int? Vibration { get; set; }

    [JsonProperty("ups_battery_level", Order = 8)]
    public int? UpsBatteryLevel { get; set; }

    [JsonProperty("ups_charging", Order = 9)]
    public bool? UpsCharging { get; set; }

    [JsonProperty("detail", Order = 10)]
    public JObject? Detail { get; set; }
}
