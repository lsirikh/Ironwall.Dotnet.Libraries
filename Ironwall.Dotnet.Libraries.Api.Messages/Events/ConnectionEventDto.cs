using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Api.Messages.Events;

/// <summary>
/// 연결 이벤트 DTO
/// </summary>
public class ConnectionEventDto
{
    /// <summary>
    /// 데이터베이스 ID (자동 생성)
    /// </summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>
    /// 이벤트 그룹
    /// </summary>
    [JsonProperty("group_event", Order = 2)]
    public string GroupEvent { get; set; } = string.Empty;

    /// <summary>
    /// 이벤트 타입 (EnumEventType: "Connection")
    /// </summary>
    [JsonProperty("type_event", Order = 3)]
    public string TypeEvent { get; set; } = "Connection";

    /// <summary>
    /// Controller ID
    /// </summary>
    [JsonProperty("controller", Order = 4)]
    public int Controller { get; set; }

    /// <summary>
    /// Sensor ID
    /// </summary>
    [JsonProperty("sensor", Order = 5)]
    public int Sensor { get; set; }

    /// <summary>
    /// 디바이스 타입 (EnumDeviceType: "Underground", "Multi", "Fence", etc.)
    /// </summary>
    [JsonProperty("type_device", Order = 6)]
    public string TypeDevice { get; set; } = string.Empty;

    /// <summary>
    /// 시퀀스 번호
    /// </summary>
    [JsonProperty("sequence", Order = 7)]
    public int Sequence { get; set; }

    /// <summary>
    /// 이벤트 발생 일시 (ISO 8601)
    /// </summary>
    [JsonProperty("datetime", Order = 8)]
    public string Datetime { get; set; } = string.Empty;

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 9)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 10)]
    public string? UpdatedAt { get; set; }
}
