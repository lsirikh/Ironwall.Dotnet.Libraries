using Ironwall.Dotnet.Libraries.Api.Messages.Defines;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Api.Messages.Events;

/// <summary>
/// 침입 탐지 이벤트 DTO
/// </summary>
public class DetectionEventDto : IEventDto
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
    /// 이벤트 타입 (EnumEventType: "Intrusion")
    /// </summary>
    [JsonProperty("type_event", Order = 3)]
    public string TypeEvent { get; set; } = "Intrusion";

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
    /// 디바이스 타입 (EnumDeviceType: "Fence", "Multi", "Underground", etc.)
    /// </summary>
    [JsonProperty("type_device", Order = 6)]
    public string TypeDevice { get; set; } = string.Empty;

    /// <summary>
    /// 시퀀스 번호
    /// </summary>
    [JsonProperty("sequence", Order = 7)]
    public int Sequence { get; set; }

    /// <summary>
    /// 상태 (EnumTrueFalse: "True", "False")
    /// </summary>
    [JsonProperty("action_reported", Order = 8)]
    public string ActionReported { get; set; } = "False";

    /// <summary>
    /// 탐지 결과 (EnumDetectionType: "THERMAL_SENSOR", "PIR_SENSOR", "VIBRATION_SENSOR", etc.)
    /// </summary>
    [JsonProperty("result", Order = 9)]
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// 이벤트 발생 일시 (ISO 8601)
    /// </summary>
    [JsonProperty("datetime", Order = 10)]
    public string Datetime { get; set; } = string.Empty;

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 11)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 12)]
    public string? UpdatedAt { get; set; }
}