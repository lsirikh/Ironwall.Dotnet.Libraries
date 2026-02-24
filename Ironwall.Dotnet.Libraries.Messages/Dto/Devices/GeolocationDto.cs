using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 위치 좌표 DTO (위치 설명, 위도, 경도, 고도)
/// </summary>
public class GeolocationDto
{
    [JsonProperty("location")]
    public string? Location { get; set; }

    [JsonProperty("latitude")]
    public double Latitude { get; set; }

    [JsonProperty("longitude")]
    public double Longitude { get; set; }

    [JsonProperty("altitude")]
    public double Altitude { get; set; }
}
