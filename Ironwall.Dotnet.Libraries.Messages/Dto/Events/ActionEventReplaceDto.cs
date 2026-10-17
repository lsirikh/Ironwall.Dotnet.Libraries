using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 조치 이벤트 수정(PUT) DTO — 서버 ActionEventReplace 계약(additionalProperties:False) 정렬.
/// <para><b>from_event_id 미포함</b> → 원본 이벤트(from_event) 연결은 생성 후 불변. content/user/type_event만 수정. <b>BaseDto 비상속</b>이라 id/created_at 미전송 → PUT 422 방지.</para>
/// </summary>
public class ActionEventReplaceDto
{
    [JsonProperty("type_event", Order = 1)]
    public string TypeEvent { get; set; } = "Action";

    [JsonProperty("content", Order = 2)]
    public string Content { get; set; } = string.Empty;

    [JsonProperty("user", Order = 3)]
    public string User { get; set; } = string.Empty;
}
