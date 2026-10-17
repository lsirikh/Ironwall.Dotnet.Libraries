using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// DeviceGroup 디바이스 할당 결과 DTO
/// <para>POST /api/devices/groups/{id}/devices 응답</para>
/// </summary>
public class DeviceGroupAssignResultDto
{
    [JsonProperty("group_id")]
    public int GroupId { get; set; }

    [JsonProperty("assigned_device_ids")]
    public List<int> AssignedDeviceIds { get; set; } = new();

    [JsonProperty("skipped_device_ids")]
    public List<int> SkippedDeviceIds { get; set; } = new();

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}
