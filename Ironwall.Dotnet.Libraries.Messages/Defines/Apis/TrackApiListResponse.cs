using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 추적점 목록 응답 (cursor 기반). 표준 <see cref="ApiListResponse{T}"/>엔 cursor 슬롯이 없어 전용 정의.
/// <para>서버: <c>{ success, message, data:[...], cursor:{next_cursor,limit,has_more}, meta }</c></para>
/// </summary>
/// <typeparam name="T">목록 항목 타입 (TrackPointDto)</typeparam>
public class TrackApiListResponse<T>
{
    /// <summary>요청 성공 여부</summary>
    [JsonProperty("success", Order = 1)]
    public bool Success { get; set; }

    /// <summary>응답 메시지</summary>
    [JsonProperty("message", Order = 2)]
    public string Message { get; set; } = string.Empty;

    /// <summary>응답 데이터 배열</summary>
    [JsonProperty("data", Order = 3)]
    public List<T>? Data { get; set; }

    /// <summary>keyset cursor 메타</summary>
    [JsonProperty("cursor", Order = 4)]
    public TrackCursorDto? Cursor { get; set; }

    /// <summary>에러 정보(실패 시)</summary>
    [JsonProperty("error", Order = 5)]
    public ApiError? Error { get; set; }

    /// <summary>메타데이터</summary>
    [JsonProperty("meta", Order = 6)]
    public MetaDto Meta { get; set; } = new MetaDto();

    /// <summary>HTTP 상태 코드 (클라 진단용, 직렬화 제외)</summary>
    [JsonIgnore]
    public int StatusCode { get; set; }
}
