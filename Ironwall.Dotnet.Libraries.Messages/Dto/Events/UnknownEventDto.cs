using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// 조치의 <c>from_event</c> 가 판별자 · 구조 · <c>type_event</c> 어느 것으로도 종류를 알 수 없을 때의 원본(G49).
/// <para>종전엔 <c>null</c> 이라 "원본이 지워진 조치"(서버 <c>from_event: null</c>)와 "원본은 있는데 GIS 가 모르는 종류" 가 같아 보였다.
/// 공통 필드(id · type_event · category_event · 시각)만 담는다.</para>
/// <para>⚠ 소비자는 이 원본으로 <b>종류+Id 큐 · 카드 종결을 하지 않는다</b> — 종류를 모르는 Id 로 닫으면 같은 번호의 다른 종류 카드를 닫는다.</para>
/// </summary>
public class UnknownEventDto : IEventDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("type_event")]
    public string TypeEvent { get; set; } = string.Empty;

    /// <summary>서버가 보낸 판별자 원문(모르는 값 · 없으면 null).</summary>
    [JsonProperty("category_event", NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryEvent { get; set; }

    [JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}
