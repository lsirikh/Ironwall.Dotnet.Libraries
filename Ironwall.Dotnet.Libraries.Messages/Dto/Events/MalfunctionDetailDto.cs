using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// MalfunctionEvent의 detail JSONB 필드 DTO
/// <para>센서 장애 위치 정보(first/second start/end)를 담는다</para>
/// </summary>
public class MalfunctionDetailDto
{
    [JsonProperty("first_start")]
    public int? FirstStart { get; set; }

    [JsonProperty("first_end")]
    public int? FirstEnd { get; set; }

    [JsonProperty("second_start")]
    public int? SecondStart { get; set; }

    [JsonProperty("second_end")]
    public int? SecondEnd { get; set; }

    /// <summary>
    /// Backend에서 추가되는 미지 필드 보존
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
