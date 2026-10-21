using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                    LogFailure(response, errorResult.Error?.Code, StampRequestId(errorResult.Meta ??= new MetaDto(), response));
                    return errorResult;
                }

                var fallback = ApiResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);   // ← 422 detail 등 서버 본문 전문 보존
                fallback.StatusCode = (int)response.StatusCode;
                LogFailure(response, fallback.Error?.Code, StampRequestId(fallback.Meta ??= new MetaDto(), response));
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
                ApiListResponse<T>? result;
                try
                {
                    result = JsonConvert.DeserializeObject<ApiListResponse<T>>(content, _jsonSettings);
                }
                catch (JsonException) when (TryDeserializeListSkippingBadItems<T>(content, out var salvaged))
                {
                    // 한 항목의 형식 오류가 쪽 전체를 날리지 않게 — 문제 항목만 빼고 다시 읽었다.
                    // 봉투 자체가 깨졌으면(data 가 배열이 아님 · JSON 아님) 되살릴 수 없으니 필터가 거짓 → 원래 예외로 실패한다.
                    result = salvaged;
                }
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
                    LogFailure(response, errorResult.Error?.Code, StampRequestId(errorResult.Meta ??= new MetaDto(), response));
                    return errorResult;
                }

                var fallback = ApiListResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);
                fallback.StatusCode = (int)response.StatusCode;
                LogFailure(response, fallback.Error?.Code, StampRequestId(fallback.Meta ??= new MetaDto(), response));
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
    /// 목록 응답을 <b>항목 하나씩</b> 읽어, 형식 오류 항목은 빼고 나머지를 싣는다.
    /// 빼낸 항목은 <see cref="ApiListResponse{T}.SkippedItems"/> 에 어느 id · 어느 칸 · 사유로 남긴다.
    /// <para><b>왜</b> — 통째 역직렬화는 한 항목의 형식 오류(예 정수 칸에 <c>21.5</c>)로 쪽 전체가 0건이 된다
    /// (2026-10-12 탐지 목록 실측: 정상 2건까지 사라졌다).</para>
    /// <para>봉투가 깨졌거나 <c>data</c> 가 배열이 아니면 <c>false</c> — 호출부는 원래 오류로 실패한다.</para>
    /// </summary>
    internal static bool TryDeserializeListSkippingBadItems<T>(string content, out ApiListResponse<T>? result)
    {
        result = null;
        try
        {
            JObject root;
            using (var text = new System.IO.StringReader(content))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                // 날짜 문자열을 DateTime 으로 바꾸지 않는다 — 통째 경로(_jsonSettings)와 같은 값이 나와야 한다.
                if (JToken.ReadFrom(reader) is not JObject parsed) return false;
                root = parsed;
            }
            if (root["data"] is not JArray items) return false;

            root.Remove("data");
            var envelope = JsonConvert.DeserializeObject<ApiListResponse<T>>(root.ToString(Formatting.None), _jsonSettings);
            if (envelope == null) return false;

            var data = new List<T>(items.Count);
            var skipped = new List<ApiListItemSkip>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                try
                {
                    // ⚠ item.ToObject 를 쓰지 않는다 — JToken 경로는 정수 칸의 실수(12.5)를 예외 없이 반올림해
                    //   깨진 항목을 조용히 값만 바꿔 싣는다. 텍스트로 다시 읽어야 통째 경로와 같은 엄격함이 된다.
                    data.Add(JsonConvert.DeserializeObject<T>(item.ToString(Formatting.None), _jsonSettings)!);   // null 항목도 통째 경로처럼 그대로 싣는다
                }
                catch (JsonException ex)
                {
                    var id = item is JObject obj && obj["id"] is JValue { Value: not null } idToken ? idToken.ToString() : null;
                    var path = ex switch
                    {
                        JsonSerializationException s when !string.IsNullOrEmpty(s.Path) => s.Path,
                        JsonReaderException r when !string.IsNullOrEmpty(r.Path) => r.Path,
                        _ => null
                    };
                    var skip = new ApiListItemSkip(i, id, path is null ? $"data[{i}]" : $"data[{i}].{path}", ex.Message);
                    skipped.Add(skip);
                    System.Diagnostics.Trace.TraceWarning(skip.Describe());
                }
            }

            envelope.Data = data;
            envelope.SkippedItems = skipped.Count > 0 ? skipped : null;
            result = envelope;
            return true;
        }
        catch (JsonException)
        {
            return false;
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
                    LogFailure(response, errorResult.Error?.Code, StampRequestId(errorResult.Meta ??= new MetaDto(), response));
                    return errorResult;
                }

                var fallback = ApiListResponse<T>.CreateError(
                    GetErrorCode(response.StatusCode),
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    content);
                fallback.StatusCode = (int)response.StatusCode;
                LogFailure(response, fallback.Error?.Code, StampRequestId(fallback.Meta ??= new MetaDto(), response));
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
    /// 실패 응답 한 줄 로그를 받을 곳(<c>ApiService</c> 가 앱 로거로 연결한다). null 이면 남기지 않는다.
    /// <para>줄 끝에 <c>req=&lt;id&gt;</c> 를 붙인다 — 서버 요청 로그(<c>/api/logs</c>)와 같은 값이라 문의 때 그대로 맞춰 볼 수 있다(명세 §12.2 500 행).
    /// 화면 문구에는 넣지 않는다.</para>
    /// </summary>
    public static Action<string>? FailureLog { get; set; }

    /// <summary>
    /// 요청 id 를 정한다 — 봉투의 <c>meta.request_id</c> 우선, 없으면 응답 헤더 <c>X-Request-ID</c>(봉투 없는 응답).
    /// 헤더에서 찾았으면 <paramref name="meta"/> 에도 채워 호출부가 같은 값을 쓰게 한다.
    /// </summary>
    internal static string? StampRequestId(MetaDto meta, HttpResponseMessage response)
    {
        if (!string.IsNullOrWhiteSpace(meta.RequestId)) return meta.RequestId;
        if (response.Headers.TryGetValues("X-Request-ID", out var values))
        {
            var id = values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (id != null) meta.RequestId = id;
        }
        return meta.RequestId;
    }

    private static void LogFailure(HttpResponseMessage response, string? code, string? requestId)
    {
        var sink = FailureLog;
        if (sink is null) return;
        var request = response.RequestMessage;
        try { sink($"[API] 실패 {(int)response.StatusCode} {code} {request?.Method} {request?.RequestUri?.AbsolutePath} req={requestId ?? "-"}"); }
        catch { /* 로그 실패가 응답 변환을 깨면 안 된다 */ }
    }

    /// <summary>
    /// HTTP 상태 코드 → 에러 코드 변환 — <b>서버 닫힌 22종</b>(명세 §12.2 · v8.0.6) 안에서만 고른다.
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