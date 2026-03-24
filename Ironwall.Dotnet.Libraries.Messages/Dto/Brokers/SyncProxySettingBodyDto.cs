using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

public class SyncProxySettingBodyDto
{
    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("server_id")]
    public int ServerId { get; set; }
}
