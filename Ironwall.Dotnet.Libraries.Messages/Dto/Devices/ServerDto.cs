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
}
