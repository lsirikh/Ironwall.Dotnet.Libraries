using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// PidsProxy 서버 운용 설정 DTO (REST API §8.8)
/// </summary>
public class ProxySettingDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("server_id")]
    public int ServerId { get; set; }

    [JsonProperty("operation_mode")]
    public string OperationMode { get; set; } = "NORMAL";

    [JsonProperty("windy_mode")]
    public string WindyMode { get; set; } = "wind0";

    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }
}
