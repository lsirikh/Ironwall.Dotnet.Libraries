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
        DateFormatHandling = DateFormatHandling.IsoDateFormat
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
                // 에러 응답 처리
                try
                {
                    var errorResult = JsonConvert.DeserializeObject<ApiResponse<T>>(content, _jsonSettings);
                    if (errorResult != null)
                        return errorResult;
                }
                catch { }

                return ApiResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);
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
                try
                {
                    var errorResult = JsonConvert.DeserializeObject<ApiListResponse<T>>(content, _jsonSettings);
                    if (errorResult != null)
                        return errorResult;
                }
                catch { }

                return ApiListResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);
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