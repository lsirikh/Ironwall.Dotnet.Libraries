using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Api.Messages.Common;

/// <summary>
/// API 에러 정보 DTO
/// </summary>
public class ApiError
{
    /// <summary>
    /// 에러 코드 (예: "NOT_FOUND", "BAD_REQUEST", "INTERNAL_ERROR")
    /// </summary>
    [JsonProperty("code", Order = 1)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 에러 메시지 (사용자 친화적)
    /// </summary>
    [JsonProperty("message", Order = 2)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 상세 에러 정보 (디버깅용)
    /// </summary>
    [JsonProperty("details", Order = 3)]
    public string? Details { get; set; }
}
