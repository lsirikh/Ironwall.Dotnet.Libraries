using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 장비 그룹 DTO
/// </summary>
public class DeviceGroupDto : BaseDto
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 3)]
    public string? Description { get; set; }

    [JsonProperty("device_count", Order = 4)]
    public int DeviceCount { get; set; }
}
