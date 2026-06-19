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

    /// <summary>
    /// 장비 설치 방위각 0~360° (v4.4). GIS 부채꼴(FOV) 방향 시각화용. optional.
    /// Camera/Speaker/Sensor 의미있음, Lamp/Enclosure는 null.
    /// </summary>
    [JsonProperty("heading", NullValueHandling = NullValueHandling.Ignore)]
    public double? Heading { get; set; }
}
