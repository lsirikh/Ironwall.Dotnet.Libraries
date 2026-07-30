using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// LAMP_CLEAR body — 경광등 이벤트 연동 동작 해제 (GIS.md v1.5.2 §2.2, REQ).
/// lamp_ids는 선택 필드: null이면 JSON에서 생략되어 "전체 해제" 의미가 된다.
/// </summary>
public class LampClearBodyDto
{
    [JsonProperty("lamp_ids", NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? LampIds { get; set; }
}
