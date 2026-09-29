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

    /// <summary>평면(envelope 없는) 응답 역직렬화 등에서 동일 설정 재사용 (GOP-00 FR-7, GetMe).</summary>
    public static JsonSerializerSettings JsonSettings => _jsonSettings;

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
                if (result == null)
                {
                    var parseFail = ApiResponse<T>.CreateError(
                        ApiErrorCodes.ParseError,
                        "Failed to parse API response",
                        "Response deserialization returned null");
                    parseFail.StatusCode = (int)response.StatusCode;
                    return parseFail;
                }

                // (§3.3) 성공 경로도 상태코드를 싣는다 — 201(생성됨)과 202(접수·억제)를 가를 유일한 근거다.
                result.StatusCode = (int)response.StatusCode;
                return result;
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
            // 상태코드는 알 수 있으면 보존한다 — 본문 읽기/역직렬화 예외라도 '무슨 상태였는지'는 분기 근거다.
            var crash = ApiResponse<T>.CreateError(
                ApiErrorCodes.InternalError,
                "Failed to process API response",
                ex.Message);
            crash.StatusCode = (int)response.StatusCode;
            return crash;
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
                if (result == null)
                {
                    var parseFail = ApiListResponse<T>.CreateError(
                        ApiErrorCodes.ParseError,
                        "Failed to parse API list response",
                        "Response deserialization returned null");
                    parseFail.StatusCode = (int)response.StatusCode;
                    return parseFail;
                }

                // (§3.3) 성공 경로도 상태코드를 싣는다.
                result.StatusCode = (int)response.StatusCode;
                return result;
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
            var crash = ApiListResponse<T>.CreateError(
                ApiErrorCodes.InternalError,
                "Failed to process API list response",
                ex.Message);
            crash.StatusCode = (int)response.StatusCode;
            return crash;
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
                    Pagination = new Defines.Apis.PaginationDto { Total = total },
                    StatusCode = (int)response.StatusCode   // (§3.3) 성공 경로도 상태코드를 싣는다.
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
            var crash = ApiListResponse<T>.CreateError(
                ApiErrorCodes.InternalError,
                "Failed to process API items list response",
                ex.Message);
            crash.StatusCode = (int)response.StatusCode;
            return crash;
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
    /// HTTP 상태 코드 → 에러 코드 변환 — <b>서버 닫힌 16종</b>(명세 §12.2) 안에서만 고른다.
    /// <para>
    /// ⚠ 이 폴백은 <b>봉투가 없는 응답</b>에만 쓰인다(프록시 502, 우리 쪽 타임아웃 합성 504, 비표준 본문).
    /// 서버 봉투가 오면 <c>error.code</c> 원본이 그대로 쓰인다.
    /// </para>
    /// <para>
    /// <b>종전 결함</b>(실측 2026-09-18, 배포 8.0.1 Swagger <c>ApiErrorResponse.error.code.enum</c> 대조):
    /// <list type="bullet">
    ///   <item><b>조어 3개</b> — 422→<c>UNPROCESSABLE_ENTITY</c>(서버는 <c>VALIDATION_ERROR</c>) ·
    ///         500→<c>INTERNAL_SERVER_ERROR</c>(서버는 <c>INTERNAL_ERROR</c>) · 504→<c>GATEWAY_TIMEOUT</c>(서버 어휘 없음)</item>
    ///   <item><b>누락 4개</b> — 405·410·413·502 가 전부 <c>UNKNOWN_ERROR</c> 로 떨어져
    ///         묘비 경로(410 <c>ENDPOINT_REMOVED</c>)를 "모르는 오류"로 표시했다</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>410 은 두 코드가 온다</b>(§12.2) — 봉투 없는 410 에는 <c>ENDPOINT_REMOVED</c>(제거된 경로)를 쓴다.
    /// 배포 실측에서 410 은 전부 <c>ENDPOINT_REMOVED</c> 였고, <c>GONE</c>(보고서 PDF 소실)은
    /// 서버가 <b>봉투로만</b> 보낸다. 두 코드의 구분은 <see cref="ApiStatusHelper.IsGone"/> /
    /// <see cref="ApiStatusHelper.IsEndpointRemoved"/> 로 한다.
    /// </para>
    /// <para>
    /// <b>504 만 예외로 클라 로컬 코드</b>(<see cref="ApiErrorCodes.ClientTimeout"/> = <c>"GATEWAY_TIMEOUT"</c>)를 유지한다 —
    /// 504 는 서버가 아니라 <c>ApiService.BuildExceptionResponse</c> 가 타임아웃에 붙이는 합성 상태이고,
    /// 로그인 패널이 이 코드로 "요청 시간이 초과되었습니다."를 띄운다(지우면 자격오류 문구로 오표시된다).
    /// </para>
    /// </summary>
    private static string GetErrorCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => ApiErrorCodes.BadRequest,                     // 400
            HttpStatusCode.Unauthorized => ApiErrorCodes.Unauthorized,                 // 401 (SESSION_REVOKED 는 봉투로만 온다)
            HttpStatusCode.Forbidden => ApiErrorCodes.Forbidden,                       // 403
            HttpStatusCode.NotFound => ApiErrorCodes.NotFound,                         // 404
            HttpStatusCode.MethodNotAllowed => ApiErrorCodes.MethodNotAllowed,         // 405
            HttpStatusCode.Conflict => ApiErrorCodes.Conflict,                         // 409
            HttpStatusCode.Gone => ApiErrorCodes.EndpointRemoved,                      // 410 (GONE 은 봉투로만 온다)
            HttpStatusCode.PreconditionFailed => ApiErrorCodes.PreconditionFailed,     // 412 — 서버 매핑 폴백과 같게(VERSION_CONFLICT 는 봉투로만 온다, v8.0.4)
            HttpStatusCode.RequestEntityTooLarge => ApiErrorCodes.PayloadTooLarge,     // 413
            HttpStatusCode.UnprocessableEntity => ApiErrorCodes.ValidationError,       // 422
            HttpStatusCode.PreconditionRequired => ApiErrorCodes.PreconditionRequired, // 428 — If-Match 없음(v8.0.4)
            HttpStatusCode.TooManyRequests => ApiErrorCodes.TooManyRequests,           // 429
            HttpStatusCode.InternalServerError => ApiErrorCodes.InternalError,         // 500
            HttpStatusCode.BadGateway => ApiErrorCodes.BadGateway,                     // 502
            HttpStatusCode.ServiceUnavailable => ApiErrorCodes.ServiceUnavailable,     // 503
            HttpStatusCode.GatewayTimeout => ApiErrorCodes.ClientTimeout,              // 504 — 클라 로컬(서버 어휘 아님)
            _ => ApiErrorCodes.UnknownError
        };
    }
    #endregion
}