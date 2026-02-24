using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 메트릭 삭제 결과 DTO (§8.6.4, §5.5.12 공통)
/// </summary>
public class MetricDeleteResultDto
{
    [JsonProperty("server_id")]
    public int? ServerId { get; set; }

    [JsonProperty("enclosure_id")]
    public int? EnclosureId { get; set; }

    [JsonProperty("deleted_count")]
    public int DeletedCount { get; set; }
}
