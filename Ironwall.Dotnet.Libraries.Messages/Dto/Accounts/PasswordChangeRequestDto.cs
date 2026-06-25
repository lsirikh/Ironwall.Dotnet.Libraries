using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// PUT /api/users/me/password 요청 본문 (§9.3, C-4 서버 확정).
/// current_password(min1) 서버 검증, new_password(min6,max100). — GOP-00 PRD FR-17
/// </summary>
public class PasswordChangeRequestDto
{
    [JsonProperty("current_password")] public string CurrentPassword { get; set; } = string.Empty;
    [JsonProperty("new_password")]     public string NewPassword { get; set; } = string.Empty;
}
