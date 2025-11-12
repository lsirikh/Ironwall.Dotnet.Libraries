using Ironwall.Dotnet.Libraries.Api.Messages.Common;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Events.Api.Helpers;
/****************************************************************************
   Purpose      : HTTP Response Helper
   Created By   : GHLee
   Created On   : 11/11/2025 12:00:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// HttpResponseMessage를 ApiResponse/ApiListResponse로 변환하는 Helper
/// <para>Newtonsoft.Json을 사용하여 DTO의 JsonProperty 어트리뷰트와 호환됩니다.</para>
/// </summary>
public static class ResponseHelper
{
    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat
    };

    /// <summary>
    /// HttpResponseMessage → ApiResponse<T> 변환
    /// <para>Newtonsoft.Json을 사용하여 DTO의 JsonProperty 어트리뷰트를 정확히 매핑합니다.</para>
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
                    {
                        return errorResult;
                    }
                }
                catch
                {
                    // JSON 파싱 실패 시 기본 에러 응답 생성
                }

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
    /// HttpResponseMessage → ApiListResponse<T> 변환
    /// <para>Newtonsoft.Json을 사용하여 DTO의 JsonProperty 어트리뷰트를 정확히 매핑합니다.</para>
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
                // 에러 응답 처리
                try
                {
                    var errorResult = JsonConvert.DeserializeObject<ApiListResponse<T>>(content, _jsonSettings);
                    if (errorResult != null)
                    {
                        return errorResult;
                    }
                }
                catch
                {
                    // JSON 파싱 실패 시 기본 에러 응답 생성
                }

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

    /// <summary>
    /// HTTP 상태 코드를 에러 코드로 변환
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
}
