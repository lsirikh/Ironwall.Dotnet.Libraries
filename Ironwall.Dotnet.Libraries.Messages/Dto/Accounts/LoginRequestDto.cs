using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>POST /api/auth/login 요청 본문 (§9.2). — GOP-00 PRD FR-1</summary>
public class LoginRequestDto
{
    [JsonProperty("login_id")] public string LoginId { get; set; } = string.Empty;
    [JsonProperty("password")] public string Password { get; set; } = string.Empty;
}
