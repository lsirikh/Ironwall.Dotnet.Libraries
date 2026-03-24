using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// DeviceGroup 디바이스 할당 요청 DTO
/// <para>POST /api/devices/groups/{id}/devices</para>
/// </summary>
public class DeviceGroupAssignRequestDto
{
    [JsonProperty("device_ids")]
    public List<int> DeviceIds { get; set; } = new();
}
