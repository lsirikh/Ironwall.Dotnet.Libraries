using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 토큰 응답 data 본문 (POST /api/auth/refresh — A-3 서버 확정: 토큰만, user/permissions/expires_in 없음).
/// access TTL 24h·refresh 7일이나 응답에 expires_in 없음 → JWT exp 클레임 디코드 필요(§2.3.1). — GOP-00 PRD FR-1
/// </summary>
public class TokenDataDto
{
    [JsonProperty("access_token")]  public string AccessToken { get; set; } = string.Empty;
    [JsonProperty("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    [JsonProperty("token_type")]    public string TokenType { get; set; } = "bearer";

    /// <summary>
    /// 세션 식별자(문자열) — 로그인·갱신 응답 공통(§9.2.2/§9.2.4, <c>LoginResponseData.session_id</c> required).
    /// <para>JWT <c>sid</c> 클레임과 같은 값이고 <b>refresh 로 회전하지 않는다</b>. 강제 로그아웃 매칭·세션 목록의
    /// '내 세션' 판정 근거 — 종전에는 sid 클레임 디코드에만 의존했다(서버가 클레임을 빼면 계정 근사 폴백으로 조용히 격하).
    /// 구서버가 키를 주지 않으면 null → 기존 sid 클레임 경로가 그대로 폴백한다.</para>
    /// </summary>
    [JsonProperty("session_id")] public string? SessionId { get; set; }
}
