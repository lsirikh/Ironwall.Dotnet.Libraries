using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// 강풍 모드 변경 Body
/// </summary>
public class WindyBodyDto
{
    [JsonProperty("mode")]
    public string Mode { get; set; } = string.Empty;
}
