using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 탐지 <b>로그</b> DTO — <c>GET /api/detection-logs</c> 전용.
/// <para>탐지 이벤트와 같은 모양에 <b><c>actions[]</c> 가 더 실린다</b>(서버가 조치보고를 LEFT JOIN 한 것).
/// 이 엔드포인트의 존재 이유가 그 조인이므로 <see cref="DetectionEventDto"/> 로 받으면
/// <c>actions</c> 를 조용히 버리고 조치 이력을 <c>/{id}/actions</c> N+1 로 다시 물어야 한다.</para>
/// <para>응답 전용 DTO다 — 쓰기 경로에 쓰지 않는다(상속한 <c>ShouldSerialize*</c> 보증은 그대로 유효).</para>
/// </summary>
public class DetectionLogDto : DetectionEventDto
{
    /// <summary>조치보고 목록. 서버는 없으면 <b>빈 리스트</b>를 준다(구버전 6.3.2 는 키 자체가 없어 <c>null</c>).</summary>
    [JsonProperty("actions", Order = 20, NullValueHandling = NullValueHandling.Ignore)]
    public List<ActionNestedDto>? Actions { get; set; }
}

/// <summary>
/// 탐지 로그에 중첩된 조치보고 요약(<c>ActionNested</c>). <c>from_event</c> 없이 4~5키만 온다.
/// </summary>
public class ActionNestedDto
{
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    [JsonProperty("content", Order = 2)]
    public string Content { get; set; } = string.Empty;

    [JsonProperty("user", Order = 3)]
    public string User { get; set; } = string.Empty;

    [JsonProperty("created_at", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}
