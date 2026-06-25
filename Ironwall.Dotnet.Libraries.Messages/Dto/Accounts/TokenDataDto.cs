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
}
