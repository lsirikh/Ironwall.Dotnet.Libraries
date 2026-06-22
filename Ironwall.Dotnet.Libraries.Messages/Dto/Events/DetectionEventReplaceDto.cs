using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 침입 탐지 이벤트 수정(PUT) DTO — 서버 DetectionEventReplace 계약(additionalProperties:False) 정렬.
/// <para>허용 필드(type_event/result/detail)만 전송. <b>BaseDto 비상속</b>이라 id/created_at/updated_at이 구조적으로 없어 PUT 422(extra fields) 방지.</para>
/// </summary>
public class DetectionEventReplaceDto
{
    [JsonProperty("type_event", Order = 1)]
    public string TypeEvent { get; set; } = "Intrusion";

    [JsonProperty("result", Order = 2)]
    public string Result { get; set; } = string.Empty;

    [JsonProperty("detail", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public DetectionDetailDto? Detail { get; set; }
}
