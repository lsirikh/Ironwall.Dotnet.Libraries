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

    /// <summary>
    /// 소속 부대 id(서버 8.0 — 응답에 실린다). 쓰기에서는 등록 때만 호출부가 채운다(<see cref="DeviceGroupWriteDto"/>).
    /// <c>null</c> 이면 직렬화하지 않는다 — 6.3·7.0 본문 바이트가 늘지 않는다.
    /// </summary>
    [JsonProperty("unit_id", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }
}
