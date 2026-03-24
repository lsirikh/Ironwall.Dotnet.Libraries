using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 Latest 메트릭 래퍼 DTO (§8.6.3)
/// <para>data: { server_id, server_name, latest_metrics }</para>
/// </summary>
public class ServerMetricLatestDto
{
    [JsonProperty("server_id")]
    public int ServerId { get; set; }

    [JsonProperty("server_name")]
    public string? ServerName { get; set; }

    [JsonProperty("latest_metrics")]
    public ServerMetricDto? LatestMetrics { get; set; }
}
