using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// <c>POST /api/auth/sso-exchange</c> 요청 본문 — SSO PRD FR-04.
/// <para>⚠ <b>이 클래스에 속성을 더하지 말 것.</b> 서버 스키마가 <c>extra="forbid"</c> 라
/// 모르는 키가 하나라도 실리면 <b>422</b> 다(조용히 버리지 않는다). 앱 종류는 토큰의
/// <c>client_id</c> 클레임이, 설치 인스턴스는 헤더 <c>X-Client-Id</c> 가 말한다 —
/// 그래서 본문에 <c>client_id</c> 를 싣지 않는다(로그인 본문과 다르다).</para>
/// </summary>
public class SsoExchangeRequestDto
{
    /// <summary>
    /// SSO 에이전트가 앱에 내준 SSO 앱 토큰(JWT · RS256 · <c>typ=at+jwt</c>).
    /// 한 토큰은 <b>한 번만</b> 교환된다 — 갱신할 때 들고 있던 토큰을 다시 내지 말고
    /// <c>SignInAsync([])</c> 로 새 토큰을 받아 교환한다(교환 규칙 #8, <c>jti</c> 1회).
    /// </summary>
    [JsonProperty("sso_access_token")] public string SsoAccessToken { get; set; } = string.Empty;
}
