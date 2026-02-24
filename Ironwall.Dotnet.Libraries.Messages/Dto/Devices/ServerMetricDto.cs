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

    [JsonProperty("collected_at", Order = 14)]
    public string? CollectedAt { get; set; }

    [JsonProperty("threshold_exceeded", Order = 15)]
    public JObject? ThresholdExceeded { get; set; }
}
