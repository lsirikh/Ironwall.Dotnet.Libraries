using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// PidsProxy 운영 모드 변경 Body
/// </summary>
public class ModeChangeBodyDto
{
    [JsonProperty("mode")]
    public string Mode { get; set; } = string.Empty;
}
