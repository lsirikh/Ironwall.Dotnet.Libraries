using Ironwall.Dotnet.Libraries.Api.Messages.Defines;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Api.Messages.Events;

/// <summary>
/// 장애 이벤트 DTO
/// </summary>
public class MalfunctionEventDto : IEventDto
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
    /// 이벤트 타입 (EnumEventType: "Fault")
    /// </summary>
    [JsonProperty("type_event", Order = 3)]
    public string TypeEvent { get; set; } = "Fault";

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
    /// 디바이스 타입 (EnumDeviceType: "Multi", "Fence", "Controller", etc.)
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
    [JsonProperty("status", Order = 8)]
    public string Status { get; set; } = "False";

    /// <summary>
    /// 조치 여부 (EnumTrueFalse: "True", "False")
    /// </summary>
    [JsonProperty("action_reported", Order = 9)]
    public string ActionReported { get; set; } = "False";

    /// <summary>
    /// 장애 원인 (EnumFaultType: "FAULT_FENCE", "FAULT_CONTROLLER", "FAULT_MULTI", etc.)
    /// </summary>
    [JsonProperty("reason", Order = 10)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 첫 번째 시작 센서
    /// </summary>
    [JsonProperty("first_start", Order = 11)]
    public int FirstStart { get; set; }

    /// <summary>
    /// 첫 번째 종료 센서
    /// </summary>
    [JsonProperty("first_end", Order = 12)]
    public int FirstEnd { get; set; }

    /// <summary>
    /// 두 번째 시작 센서
    /// </summary>
    [JsonProperty("second_start", Order = 13)]
    public int SecondStart { get; set; }

    /// <summary>
    /// 두 번째 종료 센서
    /// </summary>
    [JsonProperty("second_end", Order = 14)]
    public int SecondEnd { get; set; }

    /// <summary>
    /// 이벤트 발생 일시 (ISO 8601)
    /// </summary>
    [JsonProperty("datetime", Order = 15)]
    public string Datetime { get; set; } = string.Empty;

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 16)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 17)]
    public string? UpdatedAt { get; set; }
}
