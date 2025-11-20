using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 연결 이벤트 DTO
/// </summary>
public class ConnectionEventDto : BaseDto
{
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
}
