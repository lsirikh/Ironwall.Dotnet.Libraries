using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 페이지네이션 정보 DTO
/// </summary>
public class PaginationDto
{
    /// <summary>
    /// 현재 페이지 번호 (1부터 시작)
    /// </summary>
    [JsonProperty("page", Order = 1)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// 페이지당 항목 수
    /// </summary>
    [JsonProperty("limit", Order = 2)]
    public int Limit { get; set; } = 20;

    /// <summary>
    /// 전체 항목 수
    /// </summary>
    [JsonProperty("total", Order = 3)]
    public int Total { get; set; }

    /// <summary>
    /// 전체 페이지 수
    /// </summary>
    [JsonProperty("total_pages", Order = 4)]
    public int TotalPages { get; set; }
}
