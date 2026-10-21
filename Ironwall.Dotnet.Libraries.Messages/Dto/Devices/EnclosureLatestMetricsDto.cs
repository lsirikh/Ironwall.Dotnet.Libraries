using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 전 함체 최신 계측 한 줄 — <c>GET /api/enclosure-metrics</c> 의 <c>data[]</c> 원소(명세 §5.5.13).
/// <para>모양: <c>{enclosure_id, enclosure_name, latest_metrics}</c>. 계측이 없는 함체는 <c>latest_metrics: null</c>.</para>
/// <para>최신의 기준은 관측 시각(없는 옛 행은 수신 시각) → 같으면 <c>id</c> 가 큰 행(§5.5.11 과 같다).</para>
/// </summary>
public class EnclosureLatestMetricsDto
{
    /// <summary>함체 id.</summary>
    [JsonProperty("enclosure_id", Order = 1)]
    public int EnclosureId { get; set; }

    /// <summary>함체 이름.</summary>
    [JsonProperty("enclosure_name", Order = 2)]
    public string? EnclosureName { get; set; }

    /// <summary>그 함체의 최신 계측 — 계측이 한 번도 없으면 <c>null</c>.</summary>
    [JsonProperty("latest_metrics", Order = 3)]
    public EnclosureMetricDto? LatestMetrics { get; set; }
}
