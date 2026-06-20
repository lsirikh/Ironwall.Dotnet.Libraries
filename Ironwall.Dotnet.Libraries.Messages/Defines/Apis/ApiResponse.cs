using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 단일 데이터 API 응답 래퍼
/// </summary>
/// <typeparam name="T">응답 데이터 타입</typeparam>
public class ApiResponse<T>
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
    /// 응답 데이터 (성공 시)
    /// </summary>
    [JsonProperty("data", Order = 3)]
    public T? Data { get; set; }

    /// <summary>
    /// 에러 정보 (실패 시)
    /// </summary>
    [JsonProperty("error", Order = 4)]
    public ApiError? Error { get; set; }

    /// <summary>
    /// 메타데이터 (타임스탬프, 요청 ID 등)
    /// </summary>
    [JsonProperty("meta", Order = 5)]
    public MetaDto Meta { get; set; } = new MetaDto();

    /// <summary>
    /// HTTP 상태 코드 (클라이언트 진단용 — 서버 직렬화/역직렬화 대상 아님)
    /// </summary>
    [JsonIgnore]
    public int StatusCode { get; set; }

    /// <summary>
    /// 성공 응답 생성
    /// </summary>
    public static ApiResponse<T> CreateSuccess(T data, string message = "Operation completed successfully")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Meta = new MetaDto()
        };
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    public static ApiResponse<T> CreateError(string code, string message, string? details = null)
    {
        return new ApiResponse<T>
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
