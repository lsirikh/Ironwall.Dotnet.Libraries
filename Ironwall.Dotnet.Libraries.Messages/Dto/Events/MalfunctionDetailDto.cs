using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// MalfunctionEvent의 detail JSONB 필드 DTO
/// <para>센서 장애 위치 정보(first/second start/end)를 담는다</para>
/// <para>⚠ 전 프로퍼티 <c>NullValueHandling.Ignore</c> — 4필드가 서버에서 <c>Optional[int]</c> 라
/// 명시적 null 로도 통과하지만, 탐지 detail 과 <b>같은 규율</b>을 지켜 회귀를 원천 차단한다
/// (서버가 비-Optional 로 좁히는 순간 증상은 "수정이 항상 422" 다).</para>
/// </summary>
public class MalfunctionDetailDto
{
    [JsonProperty("first_start", NullValueHandling = NullValueHandling.Ignore)]
    public int? FirstStart { get; set; }

    [JsonProperty("first_end", NullValueHandling = NullValueHandling.Ignore)]
    public int? FirstEnd { get; set; }

    [JsonProperty("second_start", NullValueHandling = NullValueHandling.Ignore)]
    public int? SecondStart { get; set; }

    [JsonProperty("second_end", NullValueHandling = NullValueHandling.Ignore)]
    public int? SecondEnd { get; set; }

    /// <summary>
    /// Backend에서 추가되는 미지 필드 보존
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
