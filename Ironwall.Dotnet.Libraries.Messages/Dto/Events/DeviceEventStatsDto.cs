using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 함체 · 통문별 이벤트 집계 (EventByDeviceDto.<c>enclosures</c> · <c>gates</c> 하위) — 서버 <c>DeviceEventStats</c>.
/// </summary>
/// <remarks>운영 이벤트(문 개폐 · 환경 경보)가 실리는 유일한 장비별 자리다 — 이 DTO 가 없으면 그 막대가 조용히 사라진다.</remarks>
public class DeviceEventStatsDto
{
    [JsonProperty("device_id", Order = 1)]
    public int DeviceId { get; set; }

    /// <summary>서버 Optional[str] — null 이 올 수 있다.</summary>
    [JsonProperty("device_name", Order = 2)]
    public string? DeviceName { get; set; }

    [JsonProperty("device_number", Order = 3)]
    public int DeviceNumber { get; set; }

    /// <summary>비-카메라 침입 탐지(접점 등).</summary>
    [JsonProperty("sensor_detection", Order = 4)]
    public int SensorDetection { get; set; }

    [JsonProperty("alert", Order = 5)]
    public int Alert { get; set; }

    [JsonProperty("malfunction", Order = 6)]
    public int Malfunction { get; set; }

    [JsonProperty("connection", Order = 7)]
    public int Connection { get; set; }

    /// <summary>운영 이벤트(문 개폐 · 환경 경보).</summary>
    [JsonProperty("operation", Order = 8)]
    public int Operation { get; set; }
}
