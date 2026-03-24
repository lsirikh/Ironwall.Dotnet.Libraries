using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 카테고리 DTO (§8.2)
/// </summary>
public class CategoryDto : BaseDto
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type_server", Order = 3)]
    public string TypeServer { get; set; } = string.Empty;

    [JsonProperty("description", Order = 4)]
    public string? Description { get; set; }

    [JsonProperty("sort_order", Order = 5)]
    public int SortOrder { get; set; }
}
