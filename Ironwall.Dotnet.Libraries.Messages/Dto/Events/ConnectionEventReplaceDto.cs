using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 연결 이벤트 수정(PUT) DTO — 서버 ConnectionEventReplace 계약(additionalProperties:False, type_event만 허용) 정렬.
/// <para><b>BaseDto 비상속</b>이라 id/created_at/updated_at 미전송 → PUT 422 방지.</para>
/// </summary>
public class ConnectionEventReplaceDto
{
    [JsonProperty("type_event", Order = 1)]
    public string TypeEvent { get; set; } = "Connection";
}
