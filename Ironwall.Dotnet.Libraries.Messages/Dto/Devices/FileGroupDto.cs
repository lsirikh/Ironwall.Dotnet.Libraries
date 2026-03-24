using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 방송 파일 그룹 DTO
/// </summary>
public class FileGroupDto : BaseDto
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("files", Order = 3)]
    public List<string> Files { get; set; } = new List<string>();

    [JsonProperty("description", Order = 4)]
    public string? Description { get; set; }
}
