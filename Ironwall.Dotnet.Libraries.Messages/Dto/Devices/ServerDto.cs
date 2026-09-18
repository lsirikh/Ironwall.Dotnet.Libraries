using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 인스턴스 DTO (§8.3)
/// </summary>
public class ServerDto : BaseDto
{
    [JsonProperty("category_id", Order = 2)]
    public int CategoryId { get; set; }

    [JsonProperty("name", Order = 3)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("status", Order = 4)]
    public string Status { get; set; } = "NORMAL";

    [JsonProperty("ip_address", Order = 5)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("port", Order = 6)]
    public int Port { get; set; }

    [JsonProperty("hostname", Order = 7)]
    public string? Hostname { get; set; }

    [JsonProperty("user_name", Order = 8)]
    public string? UserName { get; set; }

    [JsonProperty("user_password", Order = 9)]
    public string? UserPassword { get; set; }

    [JsonProperty("threshold_config", Order = 10)]
    public JObject? ThresholdConfig { get; set; }

    /// <summary>
    /// 소속 부대 id — <b>API 8.0 에서 신설</b>(<c>ServerResponse.unit_id</c>, 8.0.1 실측 · 응답 필수).
    /// 6.3·7.0 서버는 이 키를 주지 않으므로 그 판본에서는 <c>null</c> 이다.
    /// <para>⚠ <b>쓰기 게이트</b>: 값이 있으면 요청 본문에도 실린다.
    /// <c>Contract &gt;= EnumServerContract.V8_0</c> 을 확인한 뒤에만 채워라 —
    /// 6.3·7.0 의 쓰기 스키마에는 이 키가 없고 7.0 이후는 <c>additionalProperties: false</c> 라 즉시 422 다.</para>
    /// <para><see cref="NullValueHandling.Ignore"/> 를 <b>속성 단위로</b> 지정한 이유:
    /// <c>ApiService</c> 의 전역 직렬화 설정에 <c>NullValueHandling</c> 이 없어 그대로 두면
    /// <c>"unit_id": null</c> 이 모든 POST/PUT 본문에 실린다.</para>
    /// </summary>
    [JsonProperty("unit_id", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }
}
