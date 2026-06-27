using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 추적 keyset cursor 페이지네이션 메타 (서버 <c>GET /api/tracking/points</c> 전용).
/// 표준 <see cref="PaginationDto"/>(page/limit/total)엔 cursor 슬롯이 없어 별도 정의.
/// </summary>
public class TrackCursorDto
{
    /// <summary>다음 페이지 커서(opaque). null/빈 문자열이면 마지막 페이지.</summary>
    [JsonProperty("next_cursor", Order = 1)]
    public string? NextCursor { get; set; }

    /// <summary>요청 페이지 크기</summary>
    [JsonProperty("limit", Order = 2)]
    public int Limit { get; set; }

    /// <summary>다음 페이지 존재 여부</summary>
    [JsonProperty("has_more", Order = 3)]
    public bool HasMore { get; set; }
}
