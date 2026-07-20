using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 목록 데이터 API 응답 래퍼 (페이지네이션 포함)
/// </summary>
/// <typeparam name="T">목록 항목 타입</typeparam>
public class ApiListResponse<T>
{
    /// <summary>
    /// 요청 성공 여부
    /// </summary>
    [JsonProperty("success", Order = 1)]
    public bool Success { get; set; }

    /// <summary>
    /// 응답 메시지
    /// </summary>
    [JsonProperty("message", Order = 2)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 응답 데이터 배열 (성공 시)
    /// </summary>
    [JsonProperty("data", Order = 3)]
    public List<T>? Data { get; set; }

    /// <summary>
    /// 페이지네이션 정보
    /// </summary>
    [JsonProperty("pagination", Order = 4)]
    public PaginationDto? Pagination { get; set; }

    /// <summary>
    /// 서버 top-level 전체 건수 (pagination 객체 없이 total 만 주는 엔드포인트용, 예: GET /grants). null=미제공.
    /// <para>(F-1) 서버가 <c>{success,data,total}</c> 형태로 total 을 최상위에 반환하는 경우 이 필드로 수신한다.
    /// pagination 객체를 쓰는 엔드포인트(events 등)는 이 값이 null 이고 <see cref="Pagination"/> 를 사용한다.</para>
    /// </summary>
    [JsonProperty("total", Order = 7)]
    public int? Total { get; set; }

    /// <summary>
    /// 에러 정보 (실패 시)
    /// </summary>
    [JsonProperty("error", Order = 5)]
    public ApiError? Error { get; set; }

    /// <summary>
    /// 메타데이터 (타임스탬프, 요청 ID 등)
    /// </summary>
    [JsonProperty("meta", Order = 6)]
    public MetaDto Meta { get; set; } = new MetaDto();

    /// <summary>
    /// HTTP 상태 코드 (클라이언트 진단용 — 서버 직렬화/역직렬화 대상 아님)
    /// </summary>
    [JsonIgnore]
    public int StatusCode { get; set; }

    /// <summary>
    /// 성공 응답 생성
    /// </summary>
    public static ApiListResponse<T> CreateSuccess(List<T> data, PaginationDto? pagination = null, string? message = null)
    {
        var count = data?.Count ?? 0;
        var msg = message ?? $"{count} items retrieved";

        return new ApiListResponse<T>
        {
            Success = true,
            Message = msg,
            Data = data,
            Pagination = pagination,
            Meta = new MetaDto()
        };
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    public static ApiListResponse<T> CreateError(string code, string message, string? details = null)
    {
        return new ApiListResponse<T>
        {
            Success = false,
            Message = message,
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details
            },
            Meta = new MetaDto()
        };
    }
}
