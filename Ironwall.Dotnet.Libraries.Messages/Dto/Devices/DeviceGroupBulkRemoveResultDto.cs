using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// DeviceGroup 디바이스 일괄 제거 결과 DTO (v4.3)
/// <para>DELETE /api/devices/groups/{group_id}/devices (body: { device_ids:[1..100] })</para>
/// <para>응답 3분류: removed / skipped(이미 미소속) / not_found(미존재)</para>
/// </summary>
public class DeviceGroupBulkRemoveResultDto
{
    [JsonProperty("group_id")]
    public int GroupId { get; set; }

    [JsonProperty("removed_device_ids")]
    public List<int> RemovedDeviceIds { get; set; } = new();

    [JsonProperty("skipped_device_ids")]
    public List<int> SkippedDeviceIds { get; set; } = new();

    [JsonProperty("not_found_device_ids")]
    public List<int> NotFoundDeviceIds { get; set; } = new();

    [JsonProperty("message")]
    public string? Message { get; set; }
}
