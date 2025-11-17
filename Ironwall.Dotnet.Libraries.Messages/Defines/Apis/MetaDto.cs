using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 메타데이터 DTO - 모든 API 응답에 포함되는 메타 정보
/// </summary>
public class MetaDto
{
    /// <summary>
    /// 응답 생성 시각 (ISO 8601 형식)
    /// </summary>
    [JsonProperty("timestamp", Order = 1)]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>
    /// 요청 추적용 UUID
    /// </summary>
    [JsonProperty("request_id", Order = 2)]
    public string? RequestId { get; set; }
}
