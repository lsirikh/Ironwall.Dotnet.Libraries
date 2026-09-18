using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 메트릭 시계열 데이터 DTO (§8.6)
/// </summary>
public class ServerMetricDto : BaseDto
{
    [JsonProperty("server_id", Order = 2)]
    public int ServerId { get; set; }

    [JsonProperty("cpu_usage", Order = 3)]
    public double? CpuUsage { get; set; }

    [JsonProperty("ram_usage", Order = 4)]
    public double? RamUsage { get; set; }

    [JsonProperty("ram_total_gb", Order = 5)]
    public double? RamTotalGb { get; set; }

    [JsonProperty("ram_used_gb", Order = 6)]
    public double? RamUsedGb { get; set; }

    [JsonProperty("disk_usage", Order = 7)]
    public double? DiskUsage { get; set; }

    [JsonProperty("disk_total_gb", Order = 8)]
    public double? DiskTotalGb { get; set; }

    [JsonProperty("disk_used_gb", Order = 9)]
    public double? DiskUsedGb { get; set; }

    [JsonProperty("network_in_mbps", Order = 10)]
    public double? NetworkInMbps { get; set; }

    [JsonProperty("network_out_mbps", Order = 11)]
    public double? NetworkOutMbps { get; set; }

    [JsonProperty("process_count", Order = 12)]
    public int? ProcessCount { get; set; }

    [JsonProperty("detail", Order = 13)]
    public JObject? Detail { get; set; }

    /// <summary>
    /// 관측 시각 — <b>6.3 계약의 이름</b>(<c>collected_at</c>). 7.0 은 같은 뜻을 <c>observed_at</c> 으로 개명했다
    /// (DB 컬럼명만 <c>collected_at</c> 유지). 판본 판별 없이 읽으려면 <see cref="ObservedAtEffective"/> 를 쓴다.
    /// <para>쓰기 주의: 7.0 의 <c>ServerMetricsCreate</c> 는 <c>additionalProperties: false</c> 이고
    /// 속성이 <c>observed_at</c> 뿐이라 <c>collected_at</c> 을 보내면 422 다. 반대로 6.3 은 <c>collected_at</c> 만 받는다.
    /// 그래서 값이 없으면 키째 생략한다(<see cref="NullValueHandling.Ignore"/>).</para>
    /// </summary>
    [JsonProperty("collected_at", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public string? CollectedAt { get; set; }

    /// <summary>
    /// 관측 시각 — <b>7.0 계약의 이름</b>. 6.3 서버는 이 키를 주지 않으므로 그쪽에서는 <c>null</c> 이다.
    /// 값이 없으면 직렬화에서 생략한다(6.3 쓰기 본문을 오염시키지 않기 위함).
    /// </summary>
    [JsonProperty("observed_at", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public string? ObservedAt { get; set; }

    /// <summary>
    /// 판본 무관 관측 시각 뷰 — <see cref="ObservedAt"/>(7.0) 우선, 없으면 <see cref="CollectedAt"/>(6.3).
    /// 직렬화 대상이 아니다.
    /// </summary>
    [JsonIgnore]
    public string? ObservedAtEffective => ObservedAt ?? CollectedAt;

    /// <summary>
    /// 임계 초과 정보. <b>판본마다 JSON 모양이 다르다</b>(실측):
    /// 6.3.2 = <c>object</c>(dict) · 7.0.1 = <c>array of ThresholdExceeded</c>
    /// (<c>{field, value, threshold, direction, severity}</c>).
    /// <para>⚠ 종전 <c>JObject?</c> 선언은 7.0 의 <b>배열</b>을 역직렬화하지 못해 예외를 던지고,
    /// 그 예외가 <c>ApiMessageHelper</c> 의 catch 에 걸려 <b>응답 전체가 <c>INTERNAL_ERROR</c> 로 바뀐다</b>
    /// — 최신 계측 조회가 통째로 실패한다(A-devices S-12). <see cref="JToken"/> 은 객체·배열·null 을 모두 수용한다.</para>
    /// <para>타입으로 읽으려면 <see cref="ThresholdExceededItems"/> 를 쓴다.</para>
    /// </summary>
    [JsonProperty("threshold_exceeded", Order = 16)]
    public JToken? ThresholdExceeded { get; set; }

    /// <summary>
    /// <see cref="ThresholdExceeded"/> 를 7.0 모양(목록)으로 읽은 뷰. 배열이 아니면(6.3 dict · null) <b>빈 목록</b>이다.
    /// 직렬화 대상이 아니다.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ServerThresholdExceededDto> ThresholdExceededItems
    {
        get
        {
            if (ThresholdExceeded is not JArray array) return Array.Empty<ServerThresholdExceededDto>();

            var list = new List<ServerThresholdExceededDto>();
            foreach (var item in array)
            {
                if (item.Type != JTokenType.Object) continue;
                try
                {
                    var dto = item.ToObject<ServerThresholdExceededDto>();
                    if (dto != null) list.Add(dto);
                }
                catch (JsonException)
                {
                    // 모양이 또 바뀌었을 뿐이다 — 원본은 ThresholdExceeded 에 그대로 남아 있다.
                }
            }
            return list;
        }
    }
}
/// <summary>
/// 서버 계측의 임계 초과 항목 (API 7.0 <c>ThresholdExceeded</c>).
/// <para>함체용 <see cref="ThresholdExceededItemDto"/> 와 <b>키가 다르다</b> — 함체는 <c>type</c>,
/// 서버는 <c>direction</c> + <c>severity</c> 2키다(7.0.1 Swagger 실측). 재사용하면 두 값이 조용히 유실된다.</para>
/// <para>6.3 서버는 이 모양을 보내지 않는다(dict 1개) — 그 판본에서는 이 목록이 비어 있다.</para>
/// </summary>
public class ServerThresholdExceededDto
{
    /// <summary>임계 대상 — <c>cpu</c> · <c>ram</c> · <c>disk</c> · <c>network</c>.</summary>
    [JsonProperty("field")]
    public string Field { get; set; } = string.Empty;

    /// <summary>관측값.</summary>
    [JsonProperty("value")]
    public double Value { get; set; }

    /// <summary>넘긴 임계치.</summary>
    [JsonProperty("threshold")]
    public double Threshold { get; set; }

    /// <summary>방향. 서버 임계는 초과만 판정하므로 사실상 <c>HIGH</c> 고정이다.</summary>
    [JsonProperty("direction")]
    public string Direction { get; set; } = string.Empty;

    /// <summary>심각도(<c>warning</c> · <c>critical</c> 계열).</summary>
    [JsonProperty("severity")]
    public string Severity { get; set; } = string.Empty;
}
