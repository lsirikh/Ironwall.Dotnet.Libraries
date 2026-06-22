using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 장애 이벤트 수정(PUT) DTO — 서버 MalfunctionEventReplace 계약(additionalProperties:False) 정렬.
/// <para>허용 필드(type_event/reason/detail)만 전송. <b>BaseDto 비상속</b>이라 id/created_at/updated_at 미전송 → PUT 422 방지. detail에 first/second start·end 보존.</para>
/// </summary>
public class MalfunctionEventReplaceDto
{
    [JsonProperty("type_event", Order = 1)]
    public string TypeEvent { get; set; } = "Fault";

    [JsonProperty("reason", Order = 2)]
    public string Reason { get; set; } = string.Empty;

    [JsonProperty("detail", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public MalfunctionDetailDto? Detail { get; set; }
}
