using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Api.Messages.Common;

/// <summary>
/// API 에러 정보 DTO
/// <para>
/// ⚠ <b>사문(dead) 타입이다.</b> 이 네임스페이스를 <c>using</c> 하는 <c>.cs</c> 는 레포에 <b>0건</b>이고,
/// <c>Ironwall.Dotnet.Libraries.Events.Ui.csproj</c> 가 <b>프로젝트 참조만</b> 남겨 두었다(코드 사용 없음).
/// 봉투 정본은 <c>Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiError</c> 이고,
/// 사용자 문구 조립은 <c>Ironwall.Dotnet.Libraries.Messages.Helpers.ApiErrorTextHelper</c> 한 곳에서만 한다.
/// <b>신규 코드는 정본을 쓰고 이 타입을 새로 참조하지 않는다.</b>
/// </para>
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
    /// 상세 에러 정보 원본 토큰.
    /// <para>서버는 <c>details</c> 를 <b>문자열</b>(FastAPI detail) · <b>객체</b>
    /// (v6.3 로그인 실패 <c>{failed_count, threshold, remaining, locked}</c>) ·
    /// <b>배열</b>(422 검증 오류 <c>[{field, code, moved_to, message}]</c>)로 보낸다.
    /// 그래서 <see cref="JToken"/> 으로 받는다 — 종전처럼 <c>string?</c> 로 선언하면
    /// 배열·객체가 올 때 <b>역직렬화 예외로 죽는다</b>(정본 <c>Messages.Defines.Apis.ApiError</c> 와 동일한 이유).</para>
    /// 문자열 소비는 계산 프로퍼티 <see cref="Details"/> 로 하위호환을 유지한다.
    /// </summary>
    [JsonProperty("details", Order = 3)]
    public JToken? DetailsToken { get; set; }

    /// <summary>
    /// 문자열 호환 뷰 — 기존 소비처(로그 보간 등)용. 객체·배열이면 압축 JSON 문자열을 돌려준다.
    /// <para>⚠ <b>운영자 화면에 이 값을 그대로 싣지 않는다</b> — 날 JSON 이 노출된다.
    /// 사용자 문구는 정본 <c>ApiErrorTextHelper</c> 를 쓴다.</para>
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
