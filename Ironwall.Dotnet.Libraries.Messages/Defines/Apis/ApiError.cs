using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// API 에러 정보 DTO
/// </summary>
public class ApiError
{
    /// <summary>
    /// 에러 코드 (예: "NOT_FOUND", "BAD_REQUEST", "INTERNAL_ERROR")
    /// </summary>
    [JsonProperty("code", Order = 1)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 에러 메시지 (사용자 친화적)
    /// </summary>
    [JsonProperty("message", Order = 2)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 상세 에러 정보. 서버는 <b>문자열</b>(FastAPI detail 등) 또는 <b>객체</b>
    /// (v6.3 로그인 실패 <c>{failed_count, threshold, remaining, locked}</c>)로 보낼 수 있어 <see cref="JToken"/> 으로 수용한다.
    /// 과거 <c>string?</c> 이던 시절 객체 details 는 역직렬화 예외→fallback 으로 서버 message 가 유실됐다(v6.3 실측).
    /// 문자열 소비는 계산 프로퍼티 <see cref="Details"/> 로 하위호환 유지.
    /// </summary>
    [JsonProperty("details", Order = 3)]
    public JToken? DetailsToken { get; set; }

    /// <summary>
    /// 문자열 호환 뷰 — 기존 소비처(로그 보간·ApiResultLite 등)용. 서버 details 가 객체면 압축 JSON 문자열로 반환한다.
    /// (역직렬화/직렬화 대상은 <see cref="DetailsToken"/>. 본 프로퍼티는 JsonIgnore.)
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
}
