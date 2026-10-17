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

    /// <summary>설치 고도(m). optional — 서버 Geolocation.altitude(anyOf number|null). 미설정 시 직렬화 생략.</summary>
    [JsonProperty("altitude", NullValueHandling = NullValueHandling.Ignore)]
    public double? Altitude { get; set; }

    /// <summary>
    /// 장비 설치 방위각 0~360° (v4.4). GIS 부채꼴(FOV) 방향 시각화용. optional.
    /// Camera/Speaker/Sensor 의미있음, Lamp/Enclosure는 null.
    /// </summary>
    [JsonProperty("heading", NullValueHandling = NullValueHandling.Ignore)]
    public double? Heading { get; set; }
}
