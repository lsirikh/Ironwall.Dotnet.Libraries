using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// API 메시지 변환 Helper
/// <para>HttpResponseMessage를 ApiResponse/ApiListResponse로 변환합니다.</para>
/// <para>기존 ResponseHelper의 기능을 통합하여 일관된 패턴 제공</para>
/// </summary>
public static class ApiMessageHelper
{
    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateParseHandling = DateParseHandling.None  // ISO 날짜 문자열을 DateTime으로 변환하지 않음
    };

    #region - HttpResponse → ApiResponse 변환 -
    /// <summary>
    /// HttpResponseMessage → ApiResponse&lt;T&gt; 변환 (확장 메서드)
    /// <para>기존 ResponseHelper.ToApiResponseAsync와 동일</para>
    /// </summary>
    public static async Task<ApiResponse<T>> ToApiResponseAsync<T>(this HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<ApiResponse<T>>(content, _jsonSettings);
                return result ?? ApiResponse<T>.CreateError(
                    "PARSE_ERROR",
                    "Failed to parse API response",
                    "Response deserialization returned null");
            }
            else
            {
                // (FR-8 보완) 에러 응답: 서버 표준 envelope(success/message/error)면 그대로 사용.
                //   아니면(FastAPI {"detail":[...]} 등 비표준 본문) MissingMemberHandling.Ignore 때문에 '빈 객체'가
                //   생성되어 422 detail이 통째로 폐기되던 문제 → 본문 전문을 Error.Details에 보존한다.
                ApiResponse<T>? errorResult = null;
                try { errorResult = JsonConvert.DeserializeObject<ApiResponse<T>>(content, _jsonSettings); }
                catch { }

                if (errorResult != null && (errorResult.Error != null || !string.IsNullOrEmpty(errorResult.Message)))
                {
                    errorResult.StatusCode = (int)response.StatusCode;
                    return errorResult;
                }

                var fallback = ApiResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);   // ← 422 detail 등 서버 본문 전문 보존
                fallback.StatusCode = (int)response.StatusCode;
                return fallback;
            }
        }
        catch (Exception ex)
        {
            return ApiResponse<T>.CreateError(
                "INTERNAL_ERROR",
                "Failed to process API response",
                ex.Message);
        }
    }

    /// <summary>
    /// HttpResponseMessage → ApiListResponse&lt;T&gt; 변환 (확장 메서드)
    /// <para>기존 ResponseHelper.ToApiListResponseAsync와 동일</para>
    /// </summary>
    public static async Task<ApiListResponse<T>> ToApiListResponseAsync<T>(this HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<ApiListResponse<T>>(content, _jsonSettings);
                return result ?? ApiListResponse<T>.CreateError(
                    "PARSE_ERROR",
                    "Failed to parse API list response",
                    "Response deserialization returned null");
            }
            else
            {
                // (FR-8 보완) 표준 envelope면 그대로, 아니면(FastAPI {"detail":[...]} 등) 본문 전문을 Error.Details에 보존.
                ApiListResponse<T>? errorResult = null;
                try { errorResult = JsonConvert.DeserializeObject<ApiListResponse<T>>(content, _jsonSettings); }
                catch { }

                if (errorResult != null && (errorResult.Error != null || !string.IsNullOrEmpty(errorResult.Message)))
                {
                    errorResult.StatusCode = (int)response.StatusCode;
                    return errorResult;
                }

                var fallback = ApiListResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);
                fallback.StatusCode = (int)response.StatusCode;
                return fallback;
            }
        }
        catch (Exception ex)
        {
            return ApiListResponse<T>.CreateError(
                "INTERNAL_ERROR",
                "Failed to process API list response",
                ex.Message);
        }
    }

    /// <summary>
    /// HttpResponseMessage → ApiListResponse&lt;T&gt; 변환 (items 래퍼 패턴)
    /// <para>서버가 { data: { items: [...], total: N } } 형식으로 응답하는 경우 사용</para>
    /// </summary>
    public static async Task<ApiListResponse<T>> ToApiItemsListResponseAsync<T>(this HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jObj = Newtonsoft.Json.Linq.JObject.Parse(content);
                var success = (bool?)jObj["success"] ?? false;
                var message = (string?)jObj["message"] ?? string.Empty;
                var itemsToken = jObj["data"]?["items"];
                var total = (int?)jObj["data"]?["total"] ?? 0;

                var items = itemsToken != null
                    ? itemsToken.ToObject<List<T>>(JsonSerializer.Create(_jsonSettings))
                    : new List<T>();

                return new ApiListResponse<T>
                {
                    Success = success,
                    Message = message,
                    Data = items,
                    Pagination = new Defines.Apis.PaginationDto { Total = total }
                };
            }
            else
            {
                // (FR-8 보완) 표준 envelope면 그대로, 아니면 본문 전문을 Error.Details에 보존.
                ApiListResponse<T>? errorResult = null;
                try { errorResult = JsonConvert.DeserializeObject<ApiListResponse<T>>(content, _jsonSettings); }
                catch { }

                if (errorResult != null && (errorResult.Error != null || !string.IsNullOrEmpty(errorResult.Message)))
                {
                    errorResult.StatusCode = (int)response.StatusCode;
                    return errorResult;
                }

                var fallback = ApiListResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);
                fallback.StatusCode = (int)response.StatusCode;
                return fallback;
            }
        }
        catch (Exception ex)
        {
            return ApiListResponse<T>.CreateError(
                "INTERNAL_ERROR",
                "Failed to process API items list response",
                ex.Message);
        }
    }
    #endregion

    #region - JSON 직접 변환 (선택적) -
    /// <summary>
    /// JSON 문자열 → ApiResponse&lt;T&gt; 역직렬화
    /// <para>HTTP 응답 없이 JSON 문자열만 있을 때 사용</para>
    /// </summary>
    public static ApiResponse<T>? FromJsonResponse<T>(string json)
    {
        return JsonConvert.DeserializeObject<ApiResponse<T>>(json, _jsonSettings);
    }

    /// <summary>
    /// JSON 문자열 → ApiListResponse&lt;T&gt; 역직렬화
    /// </summary>
    public static ApiListResponse<T>? FromJsonListResponse<T>(string json)
    {
        return JsonConvert.DeserializeObject<ApiListResponse<T>>(json, _jsonSettings);
    }

    /// <summary>
    /// ApiResponse&lt;T&gt; → JSON 직렬화
    /// <para>테스트나 로깅 목적으로 사용</para>
    /// </summary>
    public static string ToJson<T>(this ApiResponse<T> response)
    {
        return JsonConvert.SerializeObject(response, _jsonSettings);
    }

    /// <summary>
    /// ApiListResponse&lt;T&gt; → JSON 직렬화
    /// </summary>
    public static string ToJson<T>(this ApiListResponse<T> response)
    {
        return JsonConvert.SerializeObject(response, _jsonSettings);
    }
    #endregion

    #region - 헬퍼 메서드 -
    /// <summary>
    /// HTTP 상태 코드 → 에러 코드 변환
    /// </summary>
    private static string GetErrorCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "BAD_REQUEST",
            HttpStatusCode.Unauthorized => "UNAUTHORIZED",
            HttpStatusCode.Forbidden => "FORBIDDEN",
            HttpStatusCode.NotFound => "NOT_FOUND",
            HttpStatusCode.Conflict => "CONFLICT",
            HttpStatusCode.UnprocessableEntity => "UNPROCESSABLE_ENTITY",
            HttpStatusCode.InternalServerError => "INTERNAL_SERVER_ERROR",
            HttpStatusCode.ServiceUnavailable => "SERVICE_UNAVAILABLE",
            _ => "UNKNOWN_ERROR"
        };
    }
    #endregion
}