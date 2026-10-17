using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>
/// <c>POST /api/auth/sso-exchange</c> 의 결과 — SSO PRD FR-04 · FR-06.
///
/// <para><b>왜 <c>ApiResponse&lt;T&gt;</c> 를 그대로 쓰지 않나</b>: 교환 실패의 분기 기준은
/// <c>error.code</c> 가 아니라 <c>error.details.retryable</c> 인데, 공용 <c>ApiError</c> 는
/// <c>code</c>·<c>message</c> 만 담아 그 값을 잃는다. 여기서 원문을 직접 읽어 보존한다.</para>
/// </summary>
public sealed class SsoExchangeResult
{
    /// <summary>교환 성공(토큰 수령).</summary>
    public bool Success { get; init; }

    /// <summary>성공 시 응답 <c>data</c>. <c>RefreshToken</c> 은 항상 빈 값이다.</summary>
    public SsoExchangeResponseDataDto? Data { get; init; }

    /// <summary>HTTP 상태. 네트워크 예외면 0.</summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// 서버 <c>error.code</c> — <c>INVALID_TOKEN</c> · <c>UNKNOWN_CLIENT</c> · <c>TOKEN_REPLAYED</c> ·
    /// <c>UNAUTHORIZED</c> · <c>FORBIDDEN</c> · <c>TOO_MANY_REQUESTS</c> · <c>VALIDATION_ERROR</c> 등.
    /// 분기에 쓰지 않는다 — 분기는 <see cref="Retryable"/> 로 한다(서버 계약).
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>사람이 읽는 사유(서버 <c>error.message</c>).</summary>
    public string? Message { get; init; }

    /// <summary>서버 <c>error.details.reason</c>(기계용 하위 사유). 진단 로그용.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// <b>재시도할 가치가 있는가.</b> <c>true</c> = 새 SSO 토큰을 받아 다시 교환 ·
    /// <c>false</c> = 로그인 화면. 서버가 값을 주지 않으면 <b><c>false</c></b>(서버 명세 · SSO · GIS 합의) —
    /// 모를 때 재시도하면 「GOP 교환 허용」 꺼진 앱이 무한 재교환에 빠진다.
    /// </summary>
    public bool Retryable { get; init; }

    /// <summary><c>429</c> 의 <c>Retry-After</c>(초). 없으면 null.</summary>
    public int? RetryAfterSeconds { get; init; }

    public static SsoExchangeResult Ok(SsoExchangeResponseDataDto data, int statusCode = 200)
        => new() { Success = true, Data = data, StatusCode = statusCode };

    /// <summary>네트워크 예외 등 서버 응답이 없는 실패 — 일시 장애로 보고 재시도 가능으로 둔다.</summary>
    public static SsoExchangeResult NetworkFailure(string message)
        => new() { Success = false, StatusCode = 0, ErrorCode = "INTERNAL_ERROR", Message = message, Retryable = true };
}

/// <summary>
/// 교환 오류 본문에서 <c>retryable</c> 을 읽는 <b>순수 함수</b> — 헤드리스 시험 대상.
///
/// <para>서버 계약(<c>app/exceptions.py</c> <c>SsoExchangeError</c> · <c>app/routers/sso.py</c>)상
/// 모양이 <b>둘</b>이다:</para>
/// <list type="bullet">
/// <item>401 교환 거절 — <c>error.details</c> 가 <b>객체</b> <c>{reason, retryable}</c></item>
/// <item>422 검증 오류 — <c>error.details</c> 가 <b>배열</b>이고 <c>retryable</c> 은 <c>details[0]</c> 안</item>
/// </list>
/// <para>둘 다 아니거나 값이 없으면 <b><c>false</c></b>.</para>
/// </summary>
public static class SsoExchangeErrorClassifier
{
    /// <summary>오류 본문(JSON 원문)을 읽어 결과를 만든다. 파싱 실패는 재시도 불가로 본다.</summary>
    public static SsoExchangeResult FromErrorBody(string? body, int statusCode, int? retryAfterSeconds = null)
    {
        JObject? root = null;
        try { root = string.IsNullOrWhiteSpace(body) ? null : JObject.Parse(body); }
        catch (Newtonsoft.Json.JsonException) { root = null; }

        var error = root?["error"] as JObject;
        var details = error?["details"];

        var (retryable, reason) = ReadDetails(details);

        // 429 는 본문에 retryable 이 없어도 서버 명세상 재시도 가능이다(Retry-After 만큼 기다린 뒤).
        if (statusCode == 429 && !HasRetryable(details))
            retryable = true;

        return new SsoExchangeResult
        {
            Success = false,
            StatusCode = statusCode,
            ErrorCode = error?["code"]?.ToString(),
            Message = MessageOf(error),
            Reason = reason,
            Retryable = retryable,
            RetryAfterSeconds = retryAfterSeconds,
        };
    }

    /// <summary><c>details</c> 의 두 모양에서 <c>(retryable, reason)</c> 을 읽는다. 없으면 <c>(false, null)</c>.</summary>
    internal static (bool Retryable, string? Reason) ReadDetails(JToken? details)
    {
        var holder = details switch
        {
            JObject o => o,
            JArray a when a.Count > 0 => a[0] as JObject,
            _ => null,
        };
        if (holder is null) return (false, null);

        var reason = holder["reason"]?.Type == JTokenType.String ? holder["reason"]!.ToString() : null;
        var token = holder["retryable"];
        var retryable = token is { Type: JTokenType.Boolean } && token.Value<bool>();
        return (retryable, reason);
    }

    private static bool HasRetryable(JToken? details) => details switch
    {
        JObject o => o["retryable"] is { Type: JTokenType.Boolean },
        JArray a when a.Count > 0 => a[0] is JObject f && f["retryable"] is { Type: JTokenType.Boolean },
        _ => false,
    };

    private static string? MessageOf(JObject? error)
    {
        var m = error?["message"];
        return m switch
        {
            null => null,
            { Type: JTokenType.String } => m.ToString(),
            _ => m.ToString(Newtonsoft.Json.Formatting.None),   // 서버가 객체로 줄 때도 잃지 않는다
        };
    }
}
