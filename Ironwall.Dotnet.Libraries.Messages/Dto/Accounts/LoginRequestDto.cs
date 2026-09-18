using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// POST /api/auth/login 요청 본문 (§9.2.2). — GOP-00 PRD FR-1
/// <para>서버가 받는 키는 <c>login_id</c>·<c>password</c>·<c>client_id</c> <b>셋뿐</b>이다
/// (<c>AccountLoginRequest</c>, <c>extra="forbid"</c> — 그 밖의 키는 422 <c>UNKNOWN_FIELD</c>).</para>
/// </summary>
public class LoginRequestDto
{
    [JsonProperty("login_id")] public string LoginId { get; set; } = string.Empty;
    [JsonProperty("password")] public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 커넥션(클라이언트) 식별자 — 세션 주체 구분값(§9.2.2, v6.3 이후 전 판본 공통 스키마).
    /// <para>패턴 <c>^[A-Za-z0-9._:-]{1,64}$</c> — 위반값은 서버가 <b>무시</b>하고 로그인은 진행한다(422 아님).
    /// 저장 위치 <c>user_sessions.client_id</c> → <c>GET /api/user-sessions</c> 응답에 노출.
    /// <c>session_concurrency_policy=allow</c> + <c>session_self_replace_enabled=true</c> 일 때
    /// <b>동일 client_id</b> 재로그인만 자기 옛 세션을 교체하므로 <b>커넥션별 고유값</b>이어야 한다
    /// (공유하면 상호 축출). 값 생성은 <c>ClientIdentity.Current</c>.</para>
    /// </summary>
    [JsonProperty("client_id", NullValueHandling = NullValueHandling.Ignore)] public string? ClientId { get; set; }
}
