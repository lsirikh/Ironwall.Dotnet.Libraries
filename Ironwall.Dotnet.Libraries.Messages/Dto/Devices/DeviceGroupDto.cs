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

    [JsonProperty("type_device_group", Order = 3)]
    public string TypeDeviceGroup { get; set; } = string.Empty;

    [JsonProperty("description", Order = 4)]
    public string? Description { get; set; }

    [JsonProperty("device_count", Order = 5)]
    public int DeviceCount { get; set; }
}
