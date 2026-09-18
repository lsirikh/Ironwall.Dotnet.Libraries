using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;

/// <summary>
/// DetectionEvent의 detail JSONB 필드 DTO
/// <para>⚠ <b>전 프로퍼티가 <c>NullValueHandling.Ignore</c> 다 — 되돌리지 말 것.</b>
/// 서버 <c>DetectionDetail.thumbnail</c> 은 비-Optional 이고 검증기는 <b><c>missing</c> 만</b> 면제한다.
/// 명시적 <c>null</c> 은 형식 위반이라 PUT/POST 가 422 <c>CONSTRAINT</c>(<c>detail.thumbnail</c>)로 끊긴다 —
/// 썸네일 없는 센서 탐지(신호만 있는 건)를 수정하면 <b>항상</b> 실패했다. 키를 빼면 통과한다.</para>
/// <para>PUT 은 detail 을 <b>전체 교체</b>하므로 "키 생략"과 "명시적 null"의 결과가 같다 — 무손실 수리다.</para>
/// <para>Backend에서 추가되는 미지 필드도 [JsonExtensionData]로 보존</para>
/// </summary>
public class DetectionDetailDto
{
    [JsonProperty("signal", NullValueHandling = NullValueHandling.Ignore)]
    public int? Signal { get; set; }

    [JsonProperty("thumbnail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Thumbnail { get; set; }

    [JsonProperty("objects", NullValueHandling = NullValueHandling.Ignore)]
    public List<DetectedObjectDto>? Objects { get; set; }

    [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
    public string? Model { get; set; }

    [JsonProperty("inference_ms", NullValueHandling = NullValueHandling.Ignore)]
    public int? InferenceMs { get; set; }

    /// <summary>
    /// AI 추론 프레임 폭(px). 설계 GIS.md v1.5 DETECT(AI) — bbox 좌표 스케일 해석 기준. optional.
    /// </summary>
    [JsonProperty("frame_width", NullValueHandling = NullValueHandling.Ignore)]
    public int? FrameWidth { get; set; }

    /// <summary>
    /// AI 추론 프레임 높이(px). 설계 GIS.md v1.5 DETECT(AI) — bbox 좌표 스케일 해석 기준. optional.
    /// </summary>
    [JsonProperty("frame_height", NullValueHandling = NullValueHandling.Ignore)]
    public int? FrameHeight { get; set; }

    /// <summary>
    /// Backend에서 추가되는 미지 필드 보존
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
