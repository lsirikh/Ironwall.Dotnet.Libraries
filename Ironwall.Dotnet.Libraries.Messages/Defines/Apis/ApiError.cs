using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// API 에러 정보 DTO
/// <para>서버는 판본마다 <b>오류 모양을 바꾼다</b>. 그래서 <c>message</c>·<c>details</c> 둘 다
/// <see cref="JToken"/> 으로 받고, 소비처가 쓰는 문자열은 계산 프로퍼티로 내린다.
/// 사용자 문구 조립은 <see cref="Helpers.ApiErrorTextHelper"/> 한 곳에서만 한다.</para>
/// </summary>
public class ApiError
{
    /// <summary>
    /// 에러 코드 (예: "NOT_FOUND", "BAD_REQUEST", "INTERNAL_ERROR")
    /// </summary>
    [JsonProperty("code", Order = 1)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 에러 메시지 원본 토큰. 서버는 <b>문자열</b>이 표준이지만 <b>객체</b>로 보내는 판본이 있다
    /// (배포 7.0.1 보고서 다운로드 410: <c>error.message = {error_code, message, report_id}</c> — openapi example 실측).
    /// 과거처럼 <c>string</c> 으로 선언하면 객체가 올 때 역직렬화가 예외로 죽어
    /// <see cref="Helpers.ApiMessageHelper"/> 의 fallback 으로 떨어지고 서버 사유가 통째로 유실된다.
    /// 문자열 소비는 계산 프로퍼티 <see cref="Message"/> 로 한다.
    /// </summary>
    [JsonProperty("message", Order = 2)]
    public JToken? MessageToken { get; set; }

    /// <summary>
    /// 문자열 호환 뷰 — 기존 소비처(<c>res.Error?.Message</c>)용.
    /// 객체면 그 안의 <c>message</c> 문장을 꺼내고(410 판본), 문장이 없으면 압축 JSON 을 돌려준다.
    /// 값이 없으면 <see cref="string.Empty"/>(종전 기본값과 동일).
    /// </summary>
    [JsonIgnore]
    public string Message
    {
        get => TextOf(MessageToken) ?? string.Empty;
        set => MessageToken = string.IsNullOrEmpty(value) ? null : JValue.CreateString(value);
    }

    /// <summary>
    /// 상세 에러 정보. 서버는 <b>문자열</b>(FastAPI detail 등) · <b>객체</b>
    /// (v6.3 로그인 실패 <c>{failed_count, threshold, remaining, locked}</c> · 보고서 410 <c>{error_code, report_id}</c>) ·
    /// <b>배열</b>(422 검증 오류 <c>[{field, code, moved_to, message}]</c>)로 보낼 수 있어 <see cref="JToken"/> 으로 수용한다.
    /// 과거 <c>string?</c> 이던 시절 객체 details 는 역직렬화 예외→fallback 으로 서버 message 가 유실됐다(v6.3 실측).
    /// 문자열 소비는 계산 프로퍼티 <see cref="Details"/> 로 하위호환 유지.
    /// </summary>
    [JsonProperty("details", Order = 3)]
    public JToken? DetailsToken { get; set; }

    /// <summary>
    /// 문자열 호환 뷰 — 기존 소비처(로그 보간·ApiResultLite 등)용. 서버 details 가 객체면 압축 JSON 문자열로 반환한다.
    /// (역직렬화/직렬화 대상은 <see cref="DetailsToken"/>. 본 프로퍼티는 JsonIgnore.)
    /// <para>⚠ <b>운영자 화면에 이 값을 그대로 싣지 않는다</b> — 날 JSON 이 노출된다.
    /// 사용자 문구는 <see cref="Helpers.ApiErrorTextHelper"/> 를 쓴다.</para>
    /// </summary>
    [JsonIgnore]
    public string? Details
    {
        get => DetailsToken switch
        {
            null => null,
            { Type: JTokenType.String } t => t.Value<string>(),
            var t => t.ToString(Formatting.None)
        };
        set => DetailsToken = value is null ? null : JValue.CreateString(value);
    }

    /// <summary>
    /// <c>error.details[]</c> 를 타입으로 읽은 뷰(422 검증 오류). 배열이 아니면(객체·문자열·null) <b>빈 목록</b>.
    /// 단일 객체가 <c>field</c>/<c>code</c> 를 가진 검증 오류 모양이면 1건으로 승격해 담는다.
    /// <para>기계 분기(<c>code</c>·<c>moved_to</c>)와 사용자 문구(<c>message</c>) 모두 이 목록을 쓴다.</para>
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ApiFieldErrorDto> FieldErrors
    {
        get
        {
            var token = DetailsToken;
            if (token is null) return Array.Empty<ApiFieldErrorDto>();

            try
            {
                if (token.Type == JTokenType.Array)
                {
                    var list = new List<ApiFieldErrorDto>();
                    foreach (var item in (JArray)token)
                    {
                        if (item.Type != JTokenType.Object) continue;
                        var dto = item.ToObject<ApiFieldErrorDto>();
                        if (dto != null) list.Add(dto);
                    }
                    return list;
                }

                if (token.Type == JTokenType.Object)
                {
                    var obj = (JObject)token;
                    if (obj["field"] != null || obj["moved_to"] != null)
                    {
                        var dto = obj.ToObject<ApiFieldErrorDto>();
                        if (dto != null) return new[] { dto };
                    }
                }
            }
            catch (JsonException)
            {
                // 모양이 또 바뀌었을 뿐이다 — 문구 폴백(error.message)으로 넘긴다.
            }

            return Array.Empty<ApiFieldErrorDto>();
        }
    }

    /// <summary>
    /// 안정 sub-code(<c>error_code</c>) — 기계 판독용. 서버는 판본에 따라
    /// <c>error.details.error_code</c>(v8.0) 또는 <c>error.message.error_code</c>(배포 7.0.1 의 410 객체)에 싣는다.
    /// <b>양쪽을 순서대로</b> 본다(details 우선). 예: <c>PDF_FILE_MISSING</c>. 없으면 null.
    /// </summary>
    [JsonIgnore]
    public string? DetailsErrorCode
        => SubCodeOf(DetailsToken) ?? SubCodeOf(MessageToken);

    #region - 내부 헬퍼 -
    /// <summary>토큰에서 사람이 읽을 문장을 뽑는다. 객체면 내부 <c>message</c>(문자열) 우선, 없으면 압축 JSON.</summary>
    private static string? TextOf(JToken? token)
    {
        switch (token)
        {
            case null:
                return null;
            case { Type: JTokenType.String }:
                return token.Value<string>();
            case { Type: JTokenType.Object }:
                var inner = token["message"];
                if (inner is { Type: JTokenType.String })
                {
                    var text = inner.Value<string>();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                return token.ToString(Formatting.None);
            default:
                return token.ToString(Formatting.None);
        }
    }

    /// <summary>토큰이 객체일 때 <c>error_code</c> 문자열을 꺼낸다.</summary>
    private static string? SubCodeOf(JToken? token)
    {
        if (token is not { Type: JTokenType.Object }) return null;
        var code = token["error_code"];
        if (code is not { Type: JTokenType.String }) return null;
        var text = code.Value<string>();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
    #endregion
}
