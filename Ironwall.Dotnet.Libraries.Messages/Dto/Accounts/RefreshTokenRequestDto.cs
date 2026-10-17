using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>POST /api/auth/refresh 요청 본문 (§9.2). — GOP-00 PRD FR-1</summary>
public class RefreshTokenRequestDto
{
    [JsonProperty("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
}
